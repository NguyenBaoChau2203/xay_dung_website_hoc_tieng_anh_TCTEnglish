using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    public class PaymentConfirmationServiceTests
    {
        private static DbflashcardContext InMemory(string name)
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(name)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new DbflashcardContext(options);
        }

        private static PaymentConfirmationService CreateService(DbflashcardContext ctx)
            => new(ctx, new SubscriptionService(ctx), NullLogger<PaymentConfirmationService>.Instance);

        private static async Task<(User user, PaymentOrder order)> SeedOrderAsync(
            DbflashcardContext ctx,
            string status = PaymentOrderStatuses.Pending,
            string provider = PaymentProviders.VNPay,
            decimal amount = 399000m,
            int planDays = 365,
            TimeSpan? expiresIn = null,
            string orderCode = "TCT-CONF-001")
        {
            var user = new User
            {
                Email = $"u-{Guid.NewGuid():N}@test.com",
                PasswordHash = "hash",
                Role = Roles.Standard
            };
            var plan = new PremiumPlan
            {
                Code = $"plan-{Guid.NewGuid():N}",
                Name = "Premium",
                Description = "Plan",
                PriceVnd = amount,
                DurationDays = planDays,
                IsActive = true,
                DisplayOrder = 1,
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.Users.Add(user);
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();

            var order = new PaymentOrder
            {
                OrderCode = orderCode,
                UserId = user.UserId,
                PlanId = plan.Id,
                Provider = provider,
                AmountVnd = amount,
                Currency = "VND",
                Status = status,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                ExpiresAtUtc = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromMinutes(15))
            };
            ctx.PaymentOrders.Add(order);
            await ctx.SaveChangesAsync();
            return (user, order);
        }

        private static PaymentConfirmation Confirmation(
            PaymentOrder order,
            PaymentTransitionSource source = PaymentTransitionSource.ProviderCallback,
            long? amount = null,
            string eventKey = "evt-1",
            string eventType = PaymentEventTypes.Ipn)
            => new(
                order.Id,
                order.Provider,
                source,
                source == PaymentTransitionSource.Admin ? null : amount ?? (long)order.AmountVnd,
                eventType,
                eventKey,
                "{}",
                ProviderTransactionId: "TX-1",
                ResponseCode: order.Provider == PaymentProviders.MoMo ? "0" : "00",
                TransactionStatus: "00",
                AdminReason: source == PaymentTransitionSource.Admin ? "checked with provider" : null,
                AdminUserId: source == PaymentTransitionSource.Admin ? 99 : null);

        [Theory]
        [InlineData(PaymentProviders.VNPay)]
        [InlineData(PaymentProviders.MoMo)]
        public async Task Pending_WithMatchingAmount_ActivatesOnce(string provider)
        {
            var ctx = InMemory(Guid.NewGuid().ToString());
            var (user, order) = await SeedOrderAsync(ctx, provider: provider);

            var result = await CreateService(ctx).ConfirmPaidAsync(Confirmation(order));

            Assert.Equal(PaymentConfirmationOutcome.Activated, result.Outcome);
            Assert.NotNull(result.PremiumEndsAtUtc);
            var saved = await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
            Assert.Equal(PaymentOrderStatuses.Paid, saved.Status);
            Assert.NotNull(saved.PaidAtUtc);
            Assert.NotNull(saved.ActivatedAtUtc);
            Assert.Equal("TX-1", saved.ProviderTransactionId);
            Assert.Single(ctx.UserSubscriptions.Where(s => s.UserId == user.UserId));
            Assert.Equal(Roles.Premium, (await ctx.Users.AsNoTracking().FirstAsync(u => u.UserId == user.UserId)).Role);
            Assert.Contains(ctx.PaymentEvents, e => e.EventType == PaymentEventTypes.Ipn && e.EventKey == "evt-1");
            Assert.Contains(ctx.PaymentEvents, e => e.EventType == PaymentEventTypes.GrantPremium && e.EventKey == $"grant:{order.OrderCode}");
        }

        [Fact]
        public async Task RepeatedCall_ReturnsAlreadyPaid_AndDoesNotStackDuration()
        {
            var ctx = InMemory(Guid.NewGuid().ToString());
            var (user, order) = await SeedOrderAsync(ctx);
            var svc = CreateService(ctx);

            var first = await svc.ConfirmPaidAsync(Confirmation(order));
            var second = await svc.ConfirmPaidAsync(Confirmation(order, eventKey: "evt-2"));

            Assert.Equal(PaymentConfirmationOutcome.Activated, first.Outcome);
            Assert.Equal(PaymentConfirmationOutcome.AlreadyPaid, second.Outcome);
            Assert.Equal(first.PremiumEndsAtUtc, second.PremiumEndsAtUtc);
            Assert.Single(ctx.UserSubscriptions.Where(s => s.UserId == user.UserId));
        }

        [Fact]
        public async Task UnknownOrder_ReturnsOrderNotFound()
        {
            var ctx = InMemory(Guid.NewGuid().ToString());
            var (_, order) = await SeedOrderAsync(ctx);
            var c = Confirmation(order) with { OrderId = 987654 };

            var result = await CreateService(ctx).ConfirmPaidAsync(c);

            Assert.Equal(PaymentConfirmationOutcome.OrderNotFound, result.Outcome);
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Refunded)]
        [InlineData(PaymentOrderStatuses.PartiallyRefunded)]
        [InlineData(PaymentOrderStatuses.ManualReview)]
        public async Task ProviderSuccess_OnRefundedOrManualReviewOrder_IsRejectedAndRecordedAsIgnored(string status)
        {
            var ctx = InMemory(Guid.NewGuid().ToString());
            var (user, order) = await SeedOrderAsync(ctx, status: status);

            var result = await CreateService(ctx).ConfirmPaidAsync(Confirmation(order));

            Assert.Equal(PaymentConfirmationOutcome.RejectedState, result.Outcome);
            Assert.Equal(status, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id)).Status);
            Assert.Empty(ctx.UserSubscriptions.Where(s => s.UserId == user.UserId));
            var evt = Assert.Single(ctx.PaymentEvents);
            Assert.Equal(PaymentEventProcessingStatuses.Ignored, evt.ProcessingStatus);
        }

        [Theory]
        [InlineData(399000L - 1)]
        [InlineData(399000L + 1)]
        public async Task WrongAmount_IsRejected_AndOrderIsUnchanged(long amount)
        {
            var ctx = InMemory(Guid.NewGuid().ToString());
            var (user, order) = await SeedOrderAsync(ctx);

            var result = await CreateService(ctx).ConfirmPaidAsync(Confirmation(order, amount: amount));

            Assert.Equal(PaymentConfirmationOutcome.RejectedAmount, result.Outcome);
            Assert.Equal(PaymentOrderStatuses.Pending, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id)).Status);
            Assert.Empty(ctx.UserSubscriptions.Where(s => s.UserId == user.UserId));
            Assert.Contains(ctx.PaymentEvents, e => e.ProcessingStatus == PaymentEventProcessingStatuses.Failed
                                                     && e.ProcessingMessage!.Contains("Amount mismatch"));
        }

        [Fact]
        public async Task MissingAmount_FromAProvider_IsRejected()
        {
            var ctx = InMemory(Guid.NewGuid().ToString());
            var (_, order) = await SeedOrderAsync(ctx);
            var c = Confirmation(order) with { VerifiedAmountVnd = null };

            var result = await CreateService(ctx).ConfirmPaidAsync(c);

            Assert.Equal(PaymentConfirmationOutcome.RejectedAmount, result.Outcome);
        }

        [Theory]
        [InlineData(PaymentProviders.VNPay)]
        [InlineData(PaymentProviders.MoMo)]
        public async Task LateSuccess_AfterTwentyFourHours_GoesToManualReview_ForBothProviders(string provider)
        {
            var ctx = InMemory(Guid.NewGuid().ToString());
            var (user, order) = await SeedOrderAsync(
                ctx, status: PaymentOrderStatuses.Expired, provider: provider, expiresIn: TimeSpan.FromHours(-25));

            var result = await CreateService(ctx).ConfirmPaidAsync(Confirmation(order));

            Assert.Equal(PaymentConfirmationOutcome.SentToManualReview, result.Outcome);
            Assert.Equal(PaymentOrderStatuses.ManualReview, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id)).Status);
            Assert.Empty(ctx.UserSubscriptions.Where(s => s.UserId == user.UserId));
            Assert.Contains(ctx.PaymentEvents, e => e.EventType == PaymentEventTypes.ManualReview);
        }

        [Theory]
        [InlineData(PaymentProviders.VNPay)]
        [InlineData(PaymentProviders.MoMo)]
        public async Task SuccessWithinTheGraceWindow_AfterExpiry_StillActivates(string provider)
        {
            var ctx = InMemory(Guid.NewGuid().ToString());
            var (_, order) = await SeedOrderAsync(
                ctx, status: PaymentOrderStatuses.Expired, provider: provider, expiresIn: TimeSpan.FromHours(-2));

            var result = await CreateService(ctx).ConfirmPaidAsync(Confirmation(order, PaymentTransitionSource.ProviderQuery));

            Assert.Equal(PaymentConfirmationOutcome.Activated, result.Outcome);
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Failed)]
        [InlineData(PaymentOrderStatuses.Cancelled)]
        public async Task VerifiedSuccess_WinsOverAnEarlierFailure(string status)
        {
            var ctx = InMemory(Guid.NewGuid().ToString());
            var (_, order) = await SeedOrderAsync(ctx, status: status);
            order.FailureMessage = "earlier failure";
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).ConfirmPaidAsync(Confirmation(order, PaymentTransitionSource.ProviderQuery));

            Assert.Equal(PaymentConfirmationOutcome.Activated, result.Outcome);
            var saved = await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
            Assert.Equal(PaymentOrderStatuses.Paid, saved.Status);
            Assert.Null(saved.FailureMessage);
        }

        [Fact]
        public async Task Admin_CanConfirmManualReview_WithoutAnAmount_AndRecordsWhoDidIt()
        {
            var ctx = InMemory(Guid.NewGuid().ToString());
            var (_, order) = await SeedOrderAsync(ctx, status: PaymentOrderStatuses.ManualReview, expiresIn: TimeSpan.FromHours(-30));

            var result = await CreateService(ctx).ConfirmPaidAsync(
                Confirmation(order, PaymentTransitionSource.Admin, eventType: PaymentEventTypes.ManualConfirm));

            Assert.Equal(PaymentConfirmationOutcome.Activated, result.Outcome);
            var saved = await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
            Assert.Equal(PaymentOrderStatuses.Paid, saved.Status);
            Assert.NotNull(saved.ActivatedAtUtc);
            Assert.NotNull(saved.ConfirmedAtUtc);
            Assert.Equal(99, saved.ConfirmedByUserId);
        }

        [Fact]
        public async Task Admin_CannotConfirmAPendingOrder()
        {
            var ctx = InMemory(Guid.NewGuid().ToString());
            var (_, order) = await SeedOrderAsync(ctx);

            var result = await CreateService(ctx).ConfirmPaidAsync(
                Confirmation(order, PaymentTransitionSource.Admin, eventType: PaymentEventTypes.ManualConfirm));

            Assert.Equal(PaymentConfirmationOutcome.RejectedState, result.Outcome);
        }

        [Fact]
        public async Task Activation_ExtendsAnExistingSubscription()
        {
            var ctx = InMemory(Guid.NewGuid().ToString());
            var (user, first) = await SeedOrderAsync(ctx, planDays: 30, amount: 49000m, orderCode: "TCT-CONF-A");
            var svc = CreateService(ctx);
            var one = await svc.ConfirmPaidAsync(Confirmation(first));

            var secondPlan = await ctx.PremiumPlans.FirstAsync();
            var second = new PaymentOrder
            {
                OrderCode = "TCT-CONF-B",
                UserId = user.UserId,
                PlanId = secondPlan.Id,
                Provider = PaymentProviders.VNPay,
                AmountVnd = 49000m,
                Currency = "VND",
                Status = PaymentOrderStatuses.Pending,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
            };
            ctx.PaymentOrders.Add(second);
            await ctx.SaveChangesAsync();

            var two = await svc.ConfirmPaidAsync(Confirmation(second, eventKey: "evt-b"));

            Assert.Equal(PaymentConfirmationOutcome.Activated, two.Outcome);
            Assert.Equal(one.PremiumEndsAtUtc!.Value.AddDays(30), two.PremiumEndsAtUtc!.Value);
        }

        [Fact]
        public async Task ProviderCallback_StoresIpnPayload_ButQueryDoesNot()
        {
            var ctx = InMemory(Guid.NewGuid().ToString());
            var (_, callbackOrder) = await SeedOrderAsync(ctx, orderCode: "TCT-CONF-CB");
            var (_, queryOrder) = await SeedOrderAsync(ctx, orderCode: "TCT-CONF-Q");
            var svc = CreateService(ctx);

            await svc.ConfirmPaidAsync(Confirmation(callbackOrder) with { PayloadJson = "{\"a\":1}" });
            await svc.ConfirmPaidAsync(Confirmation(queryOrder, PaymentTransitionSource.ProviderQuery, eventType: PaymentEventTypes.Reconcile, eventKey: "retq:x")
                with { PayloadJson = "{\"b\":2}" });

            Assert.Equal("{\"a\":1}", (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == callbackOrder.Id)).IpnPayloadJson);
            Assert.Null((await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == queryOrder.Id)).IpnPayloadJson);
        }

        // ─── Unique conflicts need a relational provider: InMemory does not enforce the index ───

        private static (DbflashcardContext ctx, SqliteConnection keeper) Sqlite()
        {
            var keeper = new SqliteConnection("Data Source=:memory:");
            keeper.Open();
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseSqlite(keeper)
                .ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>()
                .Options;
            var ctx = new DbflashcardContext(options);
            ctx.Database.EnsureCreated();
            return (ctx, keeper);
        }

        [Fact]
        public async Task LosingTheGrantRace_ReturnsAlreadyPaid_AndCreatesNoSecondSubscription()
        {
            var (ctx, keeper) = Sqlite();
            using var _ = keeper;
            await using var __ = ctx;
            var (user, order) = await SeedOrderAsync(ctx);

            // Another confirmation already inserted the unique grant event for this order.
            ctx.PaymentEvents.Add(new PaymentEvent
            {
                Provider = order.Provider,
                EventType = PaymentEventTypes.GrantPremium,
                EventKey = $"grant:{order.OrderCode}",
                PaymentOrderId = order.Id,
                SignatureValid = true,
                PayloadJson = "{}",
                ProcessingStatus = PaymentEventProcessingStatuses.Processed,
                ReceivedAtUtc = DateTime.UtcNow,
                ProcessedAtUtc = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).ConfirmPaidAsync(Confirmation(order));

            Assert.Equal(PaymentConfirmationOutcome.AlreadyPaid, result.Outcome);
            Assert.Empty(ctx.UserSubscriptions.Where(s => s.UserId == user.UserId));
            // The rolled-back transaction left the order pending.
            Assert.Equal(PaymentOrderStatuses.Pending,
                await ctx.PaymentOrders.AsNoTracking().Where(o => o.Id == order.Id).Select(o => o.Status).FirstAsync());
        }

        [Fact]
        public async Task DuplicatePrimaryEvent_IsIgnored_AndConfirmationStillCompletes()
        {
            var (ctx, keeper) = Sqlite();
            using var _ = keeper;
            await using var __ = ctx;
            var (user, order) = await SeedOrderAsync(ctx);

            ctx.PaymentEvents.Add(new PaymentEvent
            {
                Provider = order.Provider,
                EventType = PaymentEventTypes.Ipn,
                EventKey = "evt-1",
                PaymentOrderId = order.Id,
                SignatureValid = true,
                PayloadJson = "{}",
                ProcessingStatus = PaymentEventProcessingStatuses.Received,
                ReceivedAtUtc = DateTime.UtcNow,
                ProcessedAtUtc = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).ConfirmPaidAsync(Confirmation(order));

            Assert.Equal(PaymentConfirmationOutcome.Activated, result.Outcome);
            Assert.Single(ctx.UserSubscriptions.Where(s => s.UserId == user.UserId));
        }
    }
}
