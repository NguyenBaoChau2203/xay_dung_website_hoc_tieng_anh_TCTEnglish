using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TCTEnglish.Controllers;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.MoMo;
using TCTEnglish.Tests.TestHelpers;
using TCTEnglish.ViewModels.Billing;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>Checkout-related controller behaviour (plan 4.7): plain-Vietnamese messages and lazy expiry.</summary>
    public class BillingControllerMessageTests
    {
        private static (BillingController controller, DbflashcardContext ctx, Mock<IBillingService> billing, Mock<ISubscriptionService> subscriptions) Create(
            bool mock = false, string environment = "Development")
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var ctx = new DbflashcardContext(options);
            var billing = new Mock<IBillingService>();
            var subscriptions = new Mock<ISubscriptionService>();
            var health = new Mock<IPaymentProviderHealthService>();
            health.Setup(h => h.GetProviderHealthStatus()).Returns(new System.Collections.Generic.List<ProviderHealthViewModel>());
            var premium = new Mock<IPremiumAccessService>();
            premium.Setup(p => p.GetAccessSnapshotAsync(It.IsAny<int>())).ReturnsAsync(new PremiumAccessSnapshot { IsAuthenticated = true });
            billing.Setup(b => b.GetPaymentHistoryAsync(It.IsAny<int>(), It.IsAny<int>(), default)).ReturnsAsync(new PaymentHistoryViewModel());

            var controller = new BillingController(
                ctx,
                billing.Object,
                premium.Object,
                Mock.Of<IIpnService>(),
                health.Object,
                Options.Create(new MoMoOptions { Enabled = mock, MockModeEnabled = mock }),
                new StubWebHostEnvironment { EnvironmentName = environment },
                Mock.Of<IPaymentQueryService>(),
                subscriptions.Object,
                NullLogger<BillingController>.Instance);

            var http = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "7") }, "Test"))
            };
            controller.ControllerContext = new ControllerContext { HttpContext = http };
            controller.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());
            return (controller, ctx, billing, subscriptions);
        }

        [Fact]
        public async Task Checkout_WithAnInvalidForm_ShowsTheDiacriticMessage()
        {
            var (controller, _, billing, _) = Create();
            controller.ModelState.AddModelError("PlanCode", "required");

            var result = await controller.Checkout(new CheckoutRequestViewModel());

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Thông tin đăng ký không hợp lệ.", controller.TempData["CheckoutError"]);
            billing.Verify(b => b.CreateCheckoutAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        }

        [Fact]
        public async Task Checkout_WhenTheGatewayGivesNoRedirectUrl_ShowsTheDiacriticMessage()
        {
            var (controller, _, billing, _) = Create();
            billing.Setup(b => b.CreateCheckoutAsync(7, "plan", "momo", It.IsAny<string>(), default))
                .ReturnsAsync(CheckoutResult.Ok("TCT-1", null));

            await controller.Checkout(new CheckoutRequestViewModel { PlanCode = "plan", Provider = "momo" });

            Assert.Equal("Cổng thanh toán hiện không khả dụng.", controller.TempData["CheckoutError"]);
        }

        [Fact]
        public async Task Checkout_PassesTheServiceErrorThrough_AndSendsAGoodAddress()
        {
            var (controller, _, billing, _) = Create();
            string? seenIp = null;
            billing.Setup(b => b.CreateCheckoutAsync(7, "plan", "momo", It.IsAny<string>(), default))
                .Callback<int, string, string, string, System.Threading.CancellationToken>((_, _, _, ip, _) => seenIp = ip)
                .ReturnsAsync(CheckoutResult.Fail("ALREADY_PERMANENT", "Tài khoản quản trị đã có sẵn quyền Premium."));
            controller.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.IPv6Loopback;

            await controller.Checkout(new CheckoutRequestViewModel { PlanCode = "plan", Provider = "momo" });

            Assert.Equal("Tài khoản quản trị đã có sẵn quyền Premium.", controller.TempData["CheckoutError"]);
            Assert.Equal("127.0.0.1", seenIp);
        }

        [Fact]
        public async Task MockConfirm_OnAnExpiredOrder_ShowsTheDiacriticMessage()
        {
            var (controller, ctx, _, _) = Create(mock: true);
            var plan = new PremiumPlan { Code = "p", Name = "P", Description = "d", PriceVnd = 49000m, DurationDays = 30, IsActive = true, DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow };
            ctx.Users.Add(new User { UserId = 7, Email = "a@test.com", PasswordHash = "h", Role = Roles.Standard });
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();
            ctx.PaymentOrders.Add(new PaymentOrder
            {
                OrderCode = "ORD-X", UserId = 7, PlanId = plan.Id, Provider = PaymentProviders.MoMo, AmountVnd = 49000m,
                Currency = "VND", Status = PaymentOrderStatuses.Pending, CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
                UpdatedAtUtc = DateTime.UtcNow.AddHours(-1), ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
            await ctx.SaveChangesAsync();

            await controller.MoMoMockConfirm("ORD-X");

            Assert.Equal("Đơn hàng đã hết hạn, vui lòng tạo đơn mới.", controller.TempData["CheckoutError"]);
        }

        [Theory]
        [InlineData("Production")]
        [InlineData("Staging")]
        public async Task MockConfirm_OutsideDevelopment_Is404_EvenWhenTheMockFlagIsOn(string environment)
        {
            var (controller, ctx, _, _) = Create(mock: true, environment: environment);
            ctx.Users.Add(new User { UserId = 7, Email = "a@test.com", PasswordHash = "h", Role = Roles.Standard });
            await ctx.SaveChangesAsync();

            var result = await controller.MoMoMockConfirm("ORD-X");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Order_ForAMissingOrSomeoneElsesOrder_Is404_AndExpiresTheUsersLapsedSubscriptionFirst()
        {
            var (controller, _, billing, subscriptions) = Create();
            billing.Setup(b => b.GetOrderPageAsync(7, "ORD-9", false, default)).ReturnsAsync((OrderPageViewModel?)null);

            var result = await controller.Order("ORD-9");

            Assert.IsType<NotFoundResult>(result);
            subscriptions.Verify(s => s.ExpireForUserAsync(7, default), Times.Once);
        }

        [Fact]
        public async Task PricingAndHistory_ExpireTheUsersLapsedSubscription_BeforeBuildingTheModel()
        {
            var (controller, _, _, subscriptions) = Create();

            await controller.Pricing();
            await controller.History();

            subscriptions.Verify(s => s.ExpireForUserAsync(7, default), Times.Exactly(2));
        }
    }
}
