using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
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
    public class BillingControllerTests
    {
        private readonly Mock<IBillingService> _mockBillingService = new();
        private readonly Mock<IPremiumAccessService> _mockPremiumAccessService = new();
        private readonly Mock<IIpnService> _mockIpnService = new();
        private readonly Mock<IPaymentProviderHealthService> _mockHealthService = new();

        private BillingController CreateController(string dbName, int? userId = 1)
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            // Default: return empty readiness (all providers unconfigured) so tests don't need real config.
            _mockHealthService
                .Setup(s => s.GetProviderHealthStatus())
                .Returns(new List<ProviderHealthViewModel>());

            var context = new DbflashcardContext(options);
            var controller = new BillingController(
                context,
                _mockBillingService.Object,
                _mockPremiumAccessService.Object,
                _mockIpnService.Object,
                _mockHealthService.Object,
                Options.Create(new MoMoOptions()),
                new StubWebHostEnvironment { EnvironmentName = "Development" },
                NullLogger<BillingController>.Instance);

            var identity = userId.HasValue
                ? new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                        new Claim(ClaimTypes.Email, "test@example.com")
                    },
                    "TestAuthType")
                : new ClaimsIdentity();

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            controller.TempData = new TempDataDictionary(
                controller.HttpContext,
                Mock.Of<ITempDataProvider>());

            return controller;
        }

        [Fact]
        public async Task Pricing_ReturnsViewWithPlans()
        {
            var dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            await using (var context = new DbflashcardContext(options))
            {
                context.PremiumPlans.Add(new PremiumPlan
                {
                    Id = 1,
                    Code = "p1",
                    Name = "Plan 1",
                    Description = "Desc 1",
                    IsActive = true,
                    DisplayOrder = 1
                });
                context.PremiumPlans.Add(new PremiumPlan
                {
                    Id = 2,
                    Code = "p2",
                    Name = "Plan 2",
                    Description = "Desc 2",
                    IsActive = false,
                    DisplayOrder = 2
                });
                await context.SaveChangesAsync();
            }

            _mockPremiumAccessService
                .Setup(s => s.GetAccessSnapshotAsync(1))
                .ReturnsAsync(new PremiumAccessSnapshot { IsPremium = false, IsAdmin = false });

            var controller = CreateController(dbName);
            var result = await controller.Pricing();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<PricingViewModel>(viewResult.Model);
            Assert.Single(model.Plans);
            Assert.Equal("Plan 1", model.Plans[0].Name);
        }

        [Fact]
        public async Task Checkout_Success_RedirectsToGateway()
        {
            var model = new CheckoutRequestViewModel { PlanCode = "monthly", Provider = "vnpay" };
            _mockBillingService
                .Setup(s => s.CreateCheckoutAsync(1, "monthly", "vnpay", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CheckoutResult.Ok("ORD-123", "https://gateway.example/pay"));

            var controller = CreateController(Guid.NewGuid().ToString());
            var result = await controller.Checkout(model);

            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Equal("https://gateway.example/pay", redirectResult.Url);
        }

        [Fact]
        public async Task Checkout_Failure_RedirectsToPricingWithError()
        {
            var model = new CheckoutRequestViewModel { PlanCode = "monthly", Provider = "vnpay" };
            _mockBillingService
                .Setup(s => s.CreateCheckoutAsync(1, "monthly", "vnpay", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CheckoutResult.Fail("LIMIT_EXCEEDED", "Too many orders."));

            var controller = CreateController(Guid.NewGuid().ToString());
            var result = await controller.Checkout(model);

            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Pricing", redirectResult.ActionName);
            Assert.Equal("Too many orders.", controller.TempData["CheckoutError"]);
        }

        [Fact]
        public async Task History_ReturnsViewWithUserOrders()
        {
            _mockBillingService
                .Setup(s => s.GetPaymentHistoryAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentHistoryViewModel
                {
                    Orders =
                    {
                        new PaymentHistoryItemViewModel
                        {
                            OrderCode = "ORD-1",
                            PlanName = "Plan 1",
                            Status = PaymentOrderStatuses.Paid
                        }
                    }
                });

            var controller = CreateController(Guid.NewGuid().ToString());
            var result = await controller.History();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<PaymentHistoryViewModel>(viewResult.Model);
            Assert.Single(model.Orders);
        }

        [Fact]
        public async Task VnPayReturn_MissingTxnRef_RendersInvalidResult()
        {
            var controller = CreateController(Guid.NewGuid().ToString());
            controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?vnp_ResponseCode=00");

            var result = await controller.VnPayReturn();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<PaymentResultViewModel>(viewResult.Model);
            Assert.False(model.HasValidReturnSignature);
            Assert.False(model.CanShowOrderSummary);
        }

        [Fact]
        public async Task VnPayReturn_OrderFound_RendersPaymentResult()
        {
            var controller = CreateController(Guid.NewGuid().ToString());
            controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?vnp_TxnRef=ORD-123&vnp_ResponseCode=00");

            _mockBillingService
                .Setup(s => s.GetVnPayReturnResultAsync(
                    It.Is<IDictionary<string, string>>(q => q["vnp_TxnRef"] == "ORD-123"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResultViewModel
                {
                    OwnerUserId = 1,
                    OrderCode = "ORD-123",
                    PlanName = "Premium",
                    AmountVnd = 49000m,
                    OrderStatus = PaymentOrderStatuses.Pending,
                    HasValidReturnSignature = true,
                    CanShowOrderSummary = true
                });

            var result = await controller.VnPayReturn();

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("PaymentResult", viewResult.ViewName);
            var model = Assert.IsType<PaymentResultViewModel>(viewResult.Model);
            Assert.Equal("ORD-123", model.OrderCode);
        }

        [Fact]
        public async Task VnPayReturn_OrderNotFound_ReturnsNotFound()
        {
            var controller = CreateController(Guid.NewGuid().ToString());
            controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?vnp_TxnRef=ORD-404&vnp_ResponseCode=00");

            _mockBillingService
                .Setup(s => s.GetVnPayReturnResultAsync(
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((PaymentResultViewModel?)null);

            var result = await controller.VnPayReturn();

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task VnPayReturn_ValidSignature_AnonymousUser_RedirectsToLogin()
        {
            var controller = CreateController(Guid.NewGuid().ToString(), userId: null);
            controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?vnp_TxnRef=ORD-123&vnp_ResponseCode=00");

            _mockBillingService
                .Setup(s => s.GetVnPayReturnResultAsync(
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResultViewModel
                {
                    OwnerUserId = 1,
                    OrderCode = "ORD-123",
                    HasValidReturnSignature = true,
                    CanShowOrderSummary = true
                });

            var result = await controller.VnPayReturn();

            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Login", redirectResult.ActionName);
            Assert.Equal("Account", redirectResult.ControllerName);
        }

        [Fact]
        public async Task VnPayReturn_ValidSignature_WrongOwner_ReturnsNotFound()
        {
            var controller = CreateController(Guid.NewGuid().ToString(), userId: 2);
            controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?vnp_TxnRef=ORD-123&vnp_ResponseCode=00");

            _mockBillingService
                .Setup(s => s.GetVnPayReturnResultAsync(
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResultViewModel
                {
                    OwnerUserId = 1,
                    OrderCode = "ORD-123",
                    HasValidReturnSignature = true,
                    CanShowOrderSummary = true
                });

            var result = await controller.VnPayReturn();

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task VnPayReturn_InvalidSignature_RendersGenericResultWithoutOrderDetails()
        {
            var controller = CreateController(Guid.NewGuid().ToString(), userId: null);
            controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?vnp_TxnRef=ORD-123&vnp_ResponseCode=00");

            _mockBillingService
                .Setup(s => s.GetVnPayReturnResultAsync(
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResultViewModel
                {
                    HasValidReturnSignature = false,
                    CanShowOrderSummary = false
                });

            var result = await controller.VnPayReturn();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<PaymentResultViewModel>(viewResult.Model);
            Assert.False(model.HasValidReturnSignature);
            Assert.False(model.CanShowOrderSummary);
        }
    }
}
