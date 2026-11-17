using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TCTEnglish.Controllers;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.MoMo;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>The order status JSON and the owner-only refresh endpoint (plan 4.5).</summary>
    public class BillingOrderEndpointsTests
    {
        private const int OwnerId = 1;
        private const int OtherId = 2;

        private static async Task<(BillingController controller, Mock<IPaymentQueryService> query, DbflashcardContext ctx)> CreateAsync(
            int? signedInUser,
            string status = PaymentOrderStatuses.Pending,
            string provider = PaymentProviders.MoMo,
            string? returnPayload = null,
            bool withSubscription = false,
            string accept = "application/json")
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var ctx = new DbflashcardContext(options);
            var owner = new User { UserId = OwnerId, Email = "owner@test.com", PasswordHash = "h", Role = Roles.Standard };
            var other = new User { UserId = OtherId, Email = "other@test.com", PasswordHash = "h", Role = Roles.Standard };
            var plan = new PremiumPlan
            {
                Code = "p", Name = "Premium", Description = "d", PriceVnd = 399000m, DurationDays = 365,
                IsActive = true, DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow
            };
            ctx.Users.AddRange(owner, other);
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();
            var order = new PaymentOrder
            {
                OrderCode = "ORD-1", UserId = OwnerId, PlanId = plan.Id, Provider = provider, AmountVnd = 399000m,
                Currency = "VND", Status = status, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10), ReturnPayloadJson = returnPayload
            };
            ctx.PaymentOrders.Add(order);
            await ctx.SaveChangesAsync();
            if (withSubscription)
            {
                ctx.UserSubscriptions.Add(new UserSubscription
                {
                    UserId = OwnerId, PlanId = plan.Id, Status = SubscriptionStatuses.Active,
                    StartsAtUtc = new DateTime(2026, 11, 17, 7, 0, 0, DateTimeKind.Utc),
                    EndsAtUtc = new DateTime(2027, 11, 17, 7, 0, 0, DateTimeKind.Utc),
                    ActivatedByPaymentOrderId = order.Id, CreatedAtUtc = DateTime.UtcNow
                });
                await ctx.SaveChangesAsync();
            }

            var query = new Mock<IPaymentQueryService>();
            var controller = new BillingController(
                ctx,
                Mock.Of<IBillingService>(),
                Mock.Of<IPremiumAccessService>(),
                Mock.Of<IIpnService>(),
                Mock.Of<IPaymentProviderHealthService>(),
                Options.Create(new MoMoOptions()),
                new StubWebHostEnvironment { EnvironmentName = "Development" },
                query.Object,
                Mock.Of<ISubscriptionService>(),
                NullLogger<BillingController>.Instance);

            var httpContext = new DefaultHttpContext();
            if (signedInUser.HasValue)
            {
                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, signedInUser.Value.ToString()) }, "Test"));
            }

            if (accept != null)
            {
                httpContext.Request.Headers.Accept = accept;
            }

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return (controller, query, ctx);
        }

        private static JsonElement ToJson(IActionResult result)
        {
            var json = Assert.IsType<JsonResult>(result);
            return JsonSerializer.SerializeToElement(json.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        // ─── OrderStatus ──────────────────────────────────────────────────────────

        [Fact]
        public async Task OrderStatus_KeepsItsOriginalFields_AndAddsStage()
        {
            var (controller, _, _) = await CreateAsync(OwnerId);

            var json = ToJson(await controller.OrderStatus("ORD-1"));

            Assert.Equal("pending", json.GetProperty("status").GetString());
            Assert.False(json.GetProperty("paid").GetBoolean());
            Assert.False(json.GetProperty("expired").GetBoolean());
            Assert.Equal("waiting", json.GetProperty("stage").GetString());
            Assert.Equal(JsonValueKind.Null, json.GetProperty("premiumEndsAt").ValueKind);
        }

        [Fact]
        public async Task OrderStatus_PendingWithASuccessfulReturn_IsConfirming()
        {
            var (controller, _, _) = await CreateAsync(
                OwnerId, provider: PaymentProviders.VNPay, returnPayload: "{\"vnp_ResponseCode\":\"00\",\"vnp_TransactionStatus\":\"00\"}");

            var json = ToJson(await controller.OrderStatus("ORD-1"));

            Assert.Equal("confirming", json.GetProperty("stage").GetString());
        }

        [Fact]
        public async Task OrderStatus_Paid_ReturnsTheEndDateInIsoUtc()
        {
            var (controller, _, _) = await CreateAsync(OwnerId, PaymentOrderStatuses.Paid, withSubscription: true);

            var json = ToJson(await controller.OrderStatus("ORD-1"));

            Assert.True(json.GetProperty("paid").GetBoolean());
            Assert.Equal("paid", json.GetProperty("stage").GetString());
            var endsAt = DateTimeOffset.Parse(json.GetProperty("premiumEndsAt").GetString()!);
            Assert.Equal(new DateTimeOffset(2027, 11, 17, 7, 0, 0, TimeSpan.Zero), endsAt);
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Failed)]
        [InlineData(PaymentOrderStatuses.Cancelled)]
        [InlineData(PaymentOrderStatuses.Expired)]
        [InlineData(PaymentOrderStatuses.Refunded)]
        public async Task OrderStatus_ClosedOrders_AreNotCompleted(string status)
        {
            var (controller, _, _) = await CreateAsync(OwnerId, status);

            Assert.Equal("not_completed", ToJson(await controller.OrderStatus("ORD-1")).GetProperty("stage").GetString());
        }

        [Fact]
        public async Task OrderStatus_ForSomeoneElsesOrder_Is404()
        {
            var (controller, _, _) = await CreateAsync(OtherId);

            Assert.IsType<NotFoundResult>(await controller.OrderStatus("ORD-1"));
        }

        [Fact]
        public async Task OrderStatus_WithoutACode_Is404()
        {
            var (controller, _, _) = await CreateAsync(OwnerId);

            Assert.IsType<NotFoundResult>(await controller.OrderStatus(" "));
        }

        // ─── Refresh ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task Refresh_AsksTheQueryServiceOnce_AndReturnsTheStatusWithTheOutcome()
        {
            var (controller, query, _) = await CreateAsync(OwnerId);
            query.Setup(q => q.QueryAndConfirmAsync(It.IsAny<long>(), PaymentQueryTrigger.Refresh, default))
                .ReturnsAsync(new PaymentQueryAttempt(PaymentQueryStatus.Throttled));

            var json = ToJson(await controller.RefreshOrder("ORD-1"));

            query.Verify(q => q.QueryAndConfirmAsync(It.IsAny<long>(), PaymentQueryTrigger.Refresh, default), Times.Once);
            Assert.Equal("Throttled", json.GetProperty("checked").GetString());
            Assert.Equal("waiting", json.GetProperty("stage").GetString());
        }

        [Fact]
        public async Task Refresh_ReflectsAConfirmationThatHappenedDuringTheQuery()
        {
            var (controller, query, ctx) = await CreateAsync(OwnerId);
            query.Setup(q => q.QueryAndConfirmAsync(It.IsAny<long>(), PaymentQueryTrigger.Refresh, default))
                .Returns(async () =>
                {
                    var order = await ctx.PaymentOrders.FirstAsync();
                    order.Status = PaymentOrderStatuses.Paid;
                    await ctx.SaveChangesAsync();
                    return new PaymentQueryAttempt(PaymentQueryStatus.Confirmed);
                });

            var json = ToJson(await controller.RefreshOrder("ORD-1"));

            Assert.True(json.GetProperty("paid").GetBoolean());
            Assert.Equal("paid", json.GetProperty("stage").GetString());
        }

        [Fact]
        public async Task Refresh_WithoutAJsonRequest_RedirectsBackToTheOrderPage()
        {
            var (controller, query, _) = await CreateAsync(OwnerId, accept: "text/html");
            query.Setup(q => q.QueryAndConfirmAsync(It.IsAny<long>(), It.IsAny<PaymentQueryTrigger>(), default))
                .ReturnsAsync(new PaymentQueryAttempt(PaymentQueryStatus.Unknown));

            var result = await controller.RefreshOrder("ORD-1");

            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.Equal("/Billing/Order/ORD-1", redirect.Url);
        }

        [Fact]
        public async Task Refresh_ForSomeoneElsesOrder_Is404_AndNeverQueries()
        {
            var (controller, query, _) = await CreateAsync(OtherId);

            Assert.IsType<NotFoundResult>(await controller.RefreshOrder("ORD-1"));
            query.Verify(q => q.QueryAndConfirmAsync(It.IsAny<long>(), It.IsAny<PaymentQueryTrigger>(), default), Times.Never);
        }

        [Fact]
        public async Task Refresh_ForAnUnknownCode_Is404()
        {
            var (controller, _, _) = await CreateAsync(OwnerId);

            Assert.IsType<NotFoundResult>(await controller.RefreshOrder("NOPE"));
        }

        [Fact]
        public void Refresh_IsPostOnly_Authorized_AndAntiforgeryProtected()
        {
            var method = typeof(BillingController).GetMethod(nameof(BillingController.RefreshOrder))!;

            Assert.NotNull(method.GetCustomAttribute<AuthorizeAttribute>());
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            var post = Assert.Single(method.GetCustomAttributes<HttpPostAttribute>());
            Assert.Equal("/Billing/Order/{orderCode}/Refresh", post.Template);
        }

        [Fact]
        public void OnlyTheProviderCallbacks_AreAnonymousOnTheBillingController()
        {
            var anonymous = typeof(BillingController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null)
                .Select(m => m.Name)
                .OrderBy(n => n)
                .ToArray();

            Assert.Equal(new[] { "MoMoIpn", "MoMoReturn", "VnPayIpn", "VnPayReturn" }, anonymous);
        }
    }
}
