using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Notifications;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Plan 4.6: confirming a payment tells the buyer once, after the payment is committed, and a notification
/// problem can never change the payment result.
/// </summary>
public sealed class PaymentActivationNotificationTests
{
    private static DbflashcardContext InMemory()
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DbflashcardContext(options);
    }

    private static NotificationService RealNotifications(DbflashcardContext ctx)
        => new(ctx, new LearningBusinessClock(TimeProvider.System), NullLogger<NotificationService>.Instance);

    private static PaymentConfirmationService CreateService(DbflashcardContext ctx, INotificationService? notifications)
        => new(ctx, new SubscriptionService(ctx), NullLogger<PaymentConfirmationService>.Instance, notifications);

    private static async Task<PaymentOrder> SeedOrderAsync(
        DbflashcardContext ctx, string planName = "Premium năm", decimal amount = 399000m,
        string orderCode = "TCT-NOTIFY-001")
    {
        var user = new User { Email = $"u-{Guid.NewGuid():N}@test.com", PasswordHash = "hash", Role = Roles.Standard };
        var plan = new PremiumPlan
        {
            Code = $"plan-{Guid.NewGuid():N}", Name = planName, Description = "Plan", PriceVnd = amount,
            DurationDays = 365, IsActive = true, DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow
        };
        ctx.Users.Add(user);
        ctx.PremiumPlans.Add(plan);
        await ctx.SaveChangesAsync();

        var order = new PaymentOrder
        {
            OrderCode = orderCode, UserId = user.UserId, PlanId = plan.Id, Provider = PaymentProviders.VNPay,
            AmountVnd = amount, Currency = "VND", Status = PaymentOrderStatuses.Pending,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1), ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
        };
        ctx.PaymentOrders.Add(order);
        await ctx.SaveChangesAsync();
        return order;
    }

    private static PaymentConfirmation Confirmation(
        PaymentOrder order, string eventKey = "evt-1", long? amount = null,
        PaymentTransitionSource source = PaymentTransitionSource.ProviderCallback)
        => new(
            order.Id, order.Provider, source,
            source == PaymentTransitionSource.Admin ? null : amount ?? (long)order.AmountVnd,
            PaymentEventTypes.Ipn, eventKey, "{}",
            ProviderTransactionId: "TX-1", ResponseCode: "00", TransactionStatus: "00",
            AdminReason: source == PaymentTransitionSource.Admin ? "checked" : null,
            AdminUserId: source == PaymentTransitionSource.Admin ? 99 : null);

    [Fact]
    public async Task Activation_CreatesExactlyOneNotification_ForTheBuyer()
    {
        var ctx = InMemory();
        var order = await SeedOrderAsync(ctx);

        var result = await CreateService(ctx, RealNotifications(ctx)).ConfirmPaidAsync(Confirmation(order));

        Assert.Equal(PaymentConfirmationOutcome.Activated, result.Outcome);
        var row = await ctx.Notifications.AsNoTracking().SingleAsync();
        var endsLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(result.PremiumEndsAtUtc!.Value, DateTimeKind.Utc), NotificationDisplay.BusinessZone);
        Assert.Equal(order.UserId, row.UserId);
        Assert.Equal(NotificationType.PremiumActivated, row.Type);
        Assert.Equal("Premium đã được kích hoạt", row.Title);
        Assert.Equal($"Gói Premium năm có hiệu lực đến {endsLocal:dd/MM/yyyy}.", row.Message);
        Assert.Equal("/Billing/Order/TCT-NOTIFY-001", row.RelatedUrl);
        Assert.Equal("fa-solid fa-crown", row.IconClass);
        Assert.Equal("premium:TCT-NOTIFY-001", row.EventKey);
        Assert.False(row.IsRead);
    }

    [Fact]
    public async Task ARepeatedConfirmation_CreatesNoSecondNotification()
    {
        var ctx = InMemory();
        var order = await SeedOrderAsync(ctx);
        var service = CreateService(ctx, RealNotifications(ctx));

        var first = await service.ConfirmPaidAsync(Confirmation(order, "evt-1"));
        var second = await service.ConfirmPaidAsync(Confirmation(order, "evt-2"));
        var third = await service.ConfirmPaidAsync(Confirmation(order, "evt-1")); // the same provider event again

        Assert.Equal(PaymentConfirmationOutcome.Activated, first.Outcome);
        Assert.Equal(PaymentConfirmationOutcome.AlreadyPaid, second.Outcome);
        Assert.Equal(PaymentConfirmationOutcome.AlreadyPaid, third.Outcome);
        Assert.Equal(1, await ctx.Notifications.CountAsync());
    }

    [Fact]
    public async Task ARejectedConfirmation_NotifiesNobody()
    {
        var ctx = InMemory();
        var order = await SeedOrderAsync(ctx);

        var result = await CreateService(ctx, RealNotifications(ctx))
            .ConfirmPaidAsync(Confirmation(order, amount: 1)); // wrong amount

        Assert.Equal(PaymentConfirmationOutcome.RejectedAmount, result.Outcome);
        Assert.Equal(0, await ctx.Notifications.CountAsync());
    }

    [Fact]
    public async Task AnAdminConfirmation_AlsoTellsTheBuyer()
    {
        var ctx = InMemory();
        var order = await SeedOrderAsync(ctx);
        // A manual review is the state an admin normally confirms from.
        order.Status = PaymentOrderStatuses.ManualReview;
        await ctx.SaveChangesAsync();

        var result = await CreateService(ctx, RealNotifications(ctx))
            .ConfirmPaidAsync(Confirmation(order, source: PaymentTransitionSource.Admin));

        Assert.Equal(PaymentConfirmationOutcome.Activated, result.Outcome);
        Assert.Equal(order.UserId, (await ctx.Notifications.AsNoTracking().SingleAsync()).UserId);
    }

    [Fact]
    public async Task ANotificationThatThrows_NeverChangesThePayment()
    {
        var ctx = InMemory();
        var order = await SeedOrderAsync(ctx);
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateForUserAsync(
                It.IsAny<int>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("notification store is down"));

        var result = await CreateService(ctx, notifications.Object).ConfirmPaidAsync(Confirmation(order));

        Assert.Equal(PaymentConfirmationOutcome.Activated, result.Outcome);
        Assert.NotNull(result.PremiumEndsAtUtc);
        var saved = await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
        Assert.Equal(PaymentOrderStatuses.Paid, saved.Status);
        Assert.NotNull(saved.ActivatedAtUtc);
        Assert.Single(ctx.UserSubscriptions.Where(s => s.UserId == order.UserId));
        notifications.Verify(n => n.CreateForUserAsync(
            order.UserId, NotificationType.PremiumActivated, It.IsAny<string>(), It.IsAny<string>(),
            "/Billing/Order/TCT-NOTIFY-001", "fa-solid fa-crown", "premium:TCT-NOTIFY-001"), Times.Once);
    }

    [Fact]
    public async Task ANotificationThatReportsAFailure_NeverChangesThePayment()
    {
        var ctx = InMemory();
        var order = await SeedOrderAsync(ctx);
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateForUserAsync(
                It.IsAny<int>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Invalid("Không tạo được thông báo."));

        var result = await CreateService(ctx, notifications.Object).ConfirmPaidAsync(Confirmation(order));

        Assert.Equal(PaymentConfirmationOutcome.Activated, result.Outcome);
        Assert.Equal(PaymentOrderStatuses.Paid, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id)).Status);
    }

    [Fact]
    public async Task WithoutANotificationService_ThePaymentStillActivates()
    {
        var ctx = InMemory();
        var order = await SeedOrderAsync(ctx);

        var result = await CreateService(ctx, notifications: null).ConfirmPaidAsync(Confirmation(order));

        Assert.Equal(PaymentConfirmationOutcome.Activated, result.Outcome);
        Assert.Equal(0, await ctx.Notifications.CountAsync());
    }
}
