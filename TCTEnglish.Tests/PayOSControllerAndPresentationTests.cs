using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TCTEnglish.Controllers;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.MoMo;
using TCTEnglish.Services.Billing.PayOS;
using TCTEnglish.Services.Billing.VnPay;
using TCTEnglish.Tests.TestHelpers;
using TCTEnglish.ViewModels.Billing;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    public class PayOSControllerAndPresentationTests
    {
        private static DbflashcardContext NewContext(string name)
            => new(new DbContextOptionsBuilder<DbflashcardContext>().UseInMemoryDatabase(name).Options);

        private static BillingController NewController(
            Mock<IBillingService> billing,
            Mock<IIpnService> ipn,
            PayOSOptions payOs,
            string environmentName,
            DbflashcardContext context,
            int? userId = 1)
        {
            var controller = new BillingController(
                context,
                billing.Object,
                Mock.Of<IPremiumAccessService>(),
                ipn.Object,
                Mock.Of<IPaymentProviderHealthService>(),
                Options.Create(new MoMoOptions()),
                new StubWebHostEnvironment { EnvironmentName = environmentName },
                Mock.Of<IPaymentQueryService>(),
                Mock.Of<ISubscriptionService>(),
                NullLogger<BillingController>.Instance,
                Options.Create(payOs));

            var identity = userId.HasValue
                ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "TestAuthType")
                : new ClaimsIdentity();
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
            controller.TempData = new TempDataDictionary(controller.HttpContext, Mock.Of<ITempDataProvider>());
            return controller;
        }

        private static void SetBody(BillingController controller, string json)
            => controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        private const string Webhook =
            "{\"code\":\"00\",\"desc\":\"success\",\"success\":true,\"data\":{\"orderCode\":123,\"amount\":3000,\"reference\":\"TF1\",\"counterAccountName\":null,\"code\":\"00\"},\"signature\":\"abc\"}";

        // ── webhook ──────────────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Webhook_PassesTheFlattenedBodyToTheIpnService_AndAnswers200()
        {
            await using var context = NewContext(Guid.NewGuid().ToString());
            var ipn = new Mock<IIpnService>();
            IDictionary<string, string>? seen = null;
            ipn.Setup(s => s.ProcessPayOSWebhookAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback<IDictionary<string, string>, CancellationToken>((p, _) => seen = p)
                .Returns(Task.CompletedTask);
            var controller = NewController(new Mock<IBillingService>(), ipn, new PayOSOptions(), "Production", context, userId: null);
            SetBody(controller, Webhook);

            var result = await controller.PayOSWebhook();

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("{ success = True }", ok.Value!.ToString());
            Assert.NotNull(seen);
            Assert.Equal("00", seen!["code"]);
            Assert.Equal("true", seen["success"]);
            Assert.Equal("abc", seen["signature"]);
            Assert.Equal("123", seen["data.orderCode"]);
            Assert.Equal("3000", seen["data.amount"]);
            Assert.Equal(string.Empty, seen["data.counterAccountName"]);
            Assert.Equal("00", seen["data.code"]);
            Assert.False(seen.ContainsKey("data"));
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("[]")]
        [InlineData("{\"code\":\"00\"}")]
        [InlineData("{\"data\":\"text\"}")]
        [InlineData("")]
        public async Task Webhook_ABodyThatIsNotOurs_Answers200_AndIsNotProcessed(string json)
        {
            await using var context = NewContext(Guid.NewGuid().ToString());
            var ipn = new Mock<IIpnService>(MockBehavior.Strict);
            var controller = NewController(new Mock<IBillingService>(), ipn, new PayOSOptions(), "Production", context, userId: null);
            SetBody(controller, json);

            Assert.IsType<OkObjectResult>(await controller.PayOSWebhook());
            ipn.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Webhook_ATooLargeBody_Answers200_AndIsNotProcessed()
        {
            await using var context = NewContext(Guid.NewGuid().ToString());
            var ipn = new Mock<IIpnService>(MockBehavior.Strict);
            var controller = NewController(new Mock<IBillingService>(), ipn, new PayOSOptions(), "Production", context, userId: null);
            SetBody(controller, "{\"code\":\"00\",\"data\":{\"x\":\"" + new string('a', 70 * 1024) + "\"}}");

            Assert.IsType<OkObjectResult>(await controller.PayOSWebhook());
            ipn.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Webhook_WhenProcessingThrows_StillAnswers200()
        {
            await using var context = NewContext(Guid.NewGuid().ToString());
            var ipn = new Mock<IIpnService>();
            ipn.Setup(s => s.ProcessPayOSWebhookAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("database is down"));
            var controller = NewController(new Mock<IBillingService>(), ipn, new PayOSOptions(), "Production", context, userId: null);
            SetBody(controller, Webhook);

            Assert.IsType<OkObjectResult>(await controller.PayOSWebhook());
        }

        // ── return / cancel ──────────────────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("")]
        [InlineData("?orderCode=abc")]
        [InlineData("?orderCode=-5")]
        public async Task Return_WithoutAValidNumericCode_ShowsTheInvalidPage_AndAsksNothing(string query)
        {
            await using var context = NewContext(Guid.NewGuid().ToString());
            var billing = new Mock<IBillingService>(MockBehavior.Strict);
            var controller = NewController(billing, new Mock<IIpnService>(), new PayOSOptions(), "Production", context);
            controller.ControllerContext.HttpContext.Request.QueryString = new QueryString(query);

            Assert.Equal("InvalidReturn", Assert.IsType<ViewResult>(await controller.PayOSReturn()).ViewName);
            Assert.Equal("InvalidReturn", Assert.IsType<ViewResult>(await controller.PayOSCancel()).ViewName);
        }

        [Theory]
        [InlineData("returnUrl", "https://evil.example/x")]
        [InlineData("redirect", "//evil.example")]
        public async Task Return_OnlyEverRedirectsToTheOrderPage(string name, string value)
        {
            var billing = new Mock<IBillingService>();
            billing.Setup(s => s.GetPayOSReturnResultAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResultViewModel
                {
                    HasValidReturnSignature = true, CanShowOrderSummary = true, OrderCode = "TCT-1", OwnerUserId = 1
                });
            await using var context = NewContext(Guid.NewGuid().ToString());
            var controller = NewController(billing, new Mock<IIpnService>(), new PayOSOptions(), "Production", context);
            controller.ControllerContext.HttpContext.Request.QueryString =
                new QueryString($"?orderCode=123&status=PAID&{name}={Uri.EscapeDataString(value)}");

            Assert.Equal("/Billing/Order/TCT-1", Assert.IsType<RedirectResult>(await controller.PayOSReturn()).Url);
        }

        [Fact]
        public async Task Return_ForAnotherBuyersOrder_IsNotFound_AndAnAnonymousVisitorGoesToLogin()
        {
            var billing = new Mock<IBillingService>();
            billing.Setup(s => s.GetPayOSReturnResultAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentResultViewModel
                {
                    HasValidReturnSignature = true, CanShowOrderSummary = true, OrderCode = "TCT-1", OwnerUserId = 2
                });
            await using var context = NewContext(Guid.NewGuid().ToString());

            var other = NewController(billing, new Mock<IIpnService>(), new PayOSOptions(), "Production", context, userId: 1);
            other.ControllerContext.HttpContext.Request.QueryString = new QueryString("?orderCode=123");
            Assert.IsType<NotFoundResult>(await other.PayOSReturn());

            var anonymous = NewController(billing, new Mock<IIpnService>(), new PayOSOptions(), "Production", context, userId: null);
            anonymous.ControllerContext.HttpContext.Request.QueryString = new QueryString("?orderCode=123");
            var login = Assert.IsType<RedirectToActionResult>(await anonymous.PayOSReturn());
            Assert.Equal("Login", login.ActionName);
        }

        [Fact]
        public async Task Return_AnUnknownOrder_IsNotFound()
        {
            var billing = new Mock<IBillingService>();
            billing.Setup(s => s.GetPayOSReturnResultAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PaymentResultViewModel?)null);
            await using var context = NewContext(Guid.NewGuid().ToString());
            var controller = NewController(billing, new Mock<IIpnService>(), new PayOSOptions(), "Production", context);
            controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?orderCode=123");

            Assert.IsType<NotFoundResult>(await controller.PayOSReturn());
        }

        // ── mock endpoints (T15) ─────────────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("Production", true, true)]
        [InlineData("Staging", true, true)]
        [InlineData("Development", true, false)]
        [InlineData("Development", false, true)]
        public async Task Mock_EndpointsAreNotFound_UnlessDevelopmentEnabledAndMockMode(string environment, bool enabled, bool mock)
        {
            var dbName = Guid.NewGuid().ToString();
            await using var context = NewContext(dbName);
            context.PaymentOrders.Add(PayOsOrder(userId: 1));
            await context.SaveChangesAsync();
            var ipn = new Mock<IIpnService>(MockBehavior.Strict);
            var controller = NewController(new Mock<IBillingService>(), ipn,
                new PayOSOptions { Enabled = enabled, MockMode = mock }, environment, context);

            Assert.IsType<NotFoundResult>(await controller.PayOSMock("TCT-MOCK"));
            Assert.IsType<NotFoundResult>(await controller.PayOSMockConfirm("TCT-MOCK"));
            ipn.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Mock_NeverTouchesAnotherBuyersOrder_OrAnOrderOfAnotherProvider()
        {
            await using var context = NewContext(Guid.NewGuid().ToString());
            context.PaymentOrders.Add(PayOsOrder(userId: 2));
            var momo = PayOsOrder(userId: 1);
            momo.OrderCode = "TCT-MOMO";
            momo.Provider = PaymentProviders.MoMo;
            context.PaymentOrders.Add(momo);
            await context.SaveChangesAsync();
            var ipn = new Mock<IIpnService>(MockBehavior.Strict);
            var controller = NewController(new Mock<IBillingService>(), ipn,
                new PayOSOptions { Enabled = true, MockMode = true }, "Development", context, userId: 1);

            Assert.IsType<NotFoundResult>(await controller.PayOSMock("TCT-MOCK"));
            Assert.IsType<NotFoundResult>(await controller.PayOSMockConfirm("TCT-MOCK"));
            Assert.IsType<NotFoundResult>(await controller.PayOSMockConfirm("TCT-MOMO"));
            ipn.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Mock_Confirm_SendsAWebhookThatTheGatewayAcceptsAsSigned()
        {
            await using var context = NewContext(Guid.NewGuid().ToString());
            context.PaymentOrders.Add(PayOsOrder(userId: 1));
            await context.SaveChangesAsync();

            IDictionary<string, string>? sent = null;
            var ipn = new Mock<IIpnService>();
            ipn.Setup(s => s.ProcessPayOSWebhookAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Callback<IDictionary<string, string>, CancellationToken>((p, _) => sent = p)
                .Returns(Task.CompletedTask);
            var options = new PayOSOptions { Enabled = true, MockMode = true };
            var controller = NewController(new Mock<IBillingService>(), ipn, options, "Development", context);

            var result = await controller.PayOSMockConfirm("TCT-MOCK");

            Assert.Equal("/Billing/Order/TCT-MOCK", Assert.IsType<RedirectResult>(result).Url);
            Assert.NotNull(sent);
            var gateway = new PayOSGateway(
                Mock.Of<IPayOSClient>(), Options.Create(options),
                new StubWebHostEnvironment { EnvironmentName = "Development" }, NullLogger<PayOSGateway>.Instance);
            var parsed = await gateway.ProcessIpnAsync(sent!);
            Assert.True(parsed.SignatureValid);
            Assert.True(parsed.IsPaid);
            Assert.Equal("261120130509123", parsed.ProviderRequestId);
            Assert.Equal("49000", sent!["data.amount"]);
        }

        [Fact]
        public async Task Mock_Confirm_OfAnExpiredOrder_SendsNothing()
        {
            await using var context = NewContext(Guid.NewGuid().ToString());
            var order = PayOsOrder(userId: 1);
            order.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            context.PaymentOrders.Add(order);
            await context.SaveChangesAsync();
            var ipn = new Mock<IIpnService>(MockBehavior.Strict);
            var controller = NewController(new Mock<IBillingService>(), ipn,
                new PayOSOptions { Enabled = true, MockMode = true }, "Development", context);

            Assert.IsType<RedirectResult>(await controller.PayOSMockConfirm("TCT-MOCK"));
            ipn.VerifyNoOtherCalls();
        }

        // ── health, labels, presentation, pricing ────────────────────────────────────────────────────────────

        [Fact]
        public void Health_ReportsPayOS_FirstAndWithoutValues()
        {
            var service = NewHealth(new PayOSOptions { Enabled = true, ApiKey = "very-secret-api-key" }, "Production");

            var rows = service.GetProviderHealthStatus();
            var row = rows.Single(r => r.ProviderCode == PaymentProviders.PayOS);

            Assert.Equal(PaymentProviders.PayOS, rows[0].ProviderCode);
            Assert.True(row.Enabled);
            Assert.False(row.Configured);
            Assert.Contains("ClientId", row.MissingFields);
            Assert.DoesNotContain(row.MissingFields, f => f.Contains("very-secret-api-key", StringComparison.Ordinal));
        }

        [Fact]
        public void Health_PayOSIsConfigured_WithKeysAndAPublicUrl_OrInTheDevelopmentMock()
        {
            var configured = new PayOSOptions
            {
                Enabled = true, ClientId = "c", ApiKey = "k", ChecksumKey = "s", PublicBaseUrl = "https://tct.example.com"
            };

            Assert.True(NewHealth(configured, "Production").GetProviderHealthStatus().Single(r => r.ProviderCode == "payos").Configured);
            var mock = new PayOSOptions { Enabled = true, MockMode = true };
            Assert.True(NewHealth(mock, "Development").GetProviderHealthStatus().Single(r => r.ProviderCode == "payos").Configured);
            Assert.False(NewHealth(mock, "Production").GetProviderHealthStatus().Single(r => r.ProviderCode == "payos").Configured);
        }

        [Fact]
        public void Health_WithoutPayOSOptions_KeepsTheThreeOldRows()
        {
            var service = new PaymentProviderHealthService(
                Options.Create(new VnPayOptions()), Options.Create(new MoMoOptions()),
                new StubWebHostEnvironment { EnvironmentName = "Production" }, new ConfigurationBuilder().Build());

            Assert.DoesNotContain(service.GetProviderHealthStatus(), r => r.ProviderCode == PaymentProviders.PayOS);
            Assert.Equal(3, service.GetProviderHealthStatus().Count);
        }

        [Fact]
        public void Labels_NameThePayOSProvider()
        {
            Assert.Equal("payOS", PaymentOrderLabels.ProviderName("payos"));
            Assert.Equal("payOS", PaymentOrderLabels.ProviderName(" PayOS "));
            Assert.Equal("payOS · MBBank", PaymentOrderLabels.Method("payos", "MBBank"));
        }

        [Theory]
        [InlineData("{\"status\":\"PAID\",\"code\":\"00\",\"cancel\":\"false\"}", true)]
        [InlineData("{\"status\":\"PAID\",\"code\":\"00\",\"cancel\":\"true\"}", false)]
        [InlineData("{\"status\":\"CANCELLED\",\"code\":\"00\",\"cancel\":\"true\"}", false)]
        [InlineData("{\"status\":\"PENDING\",\"code\":\"00\"}", false)]
        [InlineData("{\"status\":\"PAID\",\"code\":\"01\"}", false)]
        [InlineData("{\"resultCode\":\"0\"}", false)]
        public void ReturnReportedSuccess_ReadsThePayOSQuery_AsAHintOnly(string json, bool expected)
        {
            Assert.Equal(expected, PaymentOrderPresentation.ReturnReportedSuccess(PaymentProviders.PayOS, json));
        }

        [Fact]
        public void Pricing_ListsPayOSFirst_WhenItIsReady_AndHidesNothingElse()
        {
            var plans = new[] { new PricingPlanViewModel { Code = "p", Name = "P", PriceVnd = 49000, DurationDays = 30 } };
            var readiness = new Dictionary<string, bool> { ["payos"] = true, ["vnpay"] = true, ["momo"] = false };

            var model = PricingPageBuilder.Build(plans, new PremiumAccessSnapshot(), readiness, false, null, DateTime.UtcNow);

            Assert.Equal(new[] { "payos", "vnpay", "momo" }, model.Providers.Select(p => p.Code).ToArray());
            Assert.Equal("payos", model.Providers.Single(p => p.IsPreselected).Code);
            Assert.False(model.Providers.Single(p => p.Code == "momo").Available);
        }

        [Fact]
        public void Pricing_WhenPayOSIsNotReady_TheFirstReadyProviderIsPreselected()
        {
            var plans = new[] { new PricingPlanViewModel { Code = "p", Name = "P", PriceVnd = 49000, DurationDays = 30 } };
            var readiness = new Dictionary<string, bool> { ["payos"] = false, ["vnpay"] = true };

            var model = PricingPageBuilder.Build(plans, new PremiumAccessSnapshot(), readiness, false, null, DateTime.UtcNow);

            Assert.False(model.Providers.Single(p => p.Code == "payos").Available);
            Assert.Equal("vnpay", model.Providers.Single(p => p.IsPreselected).Code);
        }

        [Fact]
        public void Pricing_PayOSMockIsMarkedAsATest_ToLearnersOfTheLocalBuildOnly()
        {
            var plans = new[] { new PricingPlanViewModel { Code = "p", Name = "P", PriceVnd = 49000, DurationDays = 30 } };
            var readiness = new Dictionary<string, bool> { ["payos"] = true };

            var withMock = PricingPageBuilder.Build(plans, new PremiumAccessSnapshot(), readiness, false, null, DateTime.UtcNow, payOsMockMode: true);
            var without = PricingPageBuilder.Build(plans, new PremiumAccessSnapshot(), readiness, false, null, DateTime.UtcNow);

            Assert.True(withMock.Providers.Single(p => p.Code == "payos").IsTest);
            Assert.False(without.Providers.Single(p => p.Code == "payos").IsTest);
        }

        private static PaymentProviderHealthService NewHealth(PayOSOptions options, string environment)
            => new(
                Options.Create(new VnPayOptions()),
                Options.Create(new MoMoOptions()),
                new StubWebHostEnvironment { EnvironmentName = environment },
                new ConfigurationBuilder().Build(),
                Options.Create(options));

        private static PaymentOrder PayOsOrder(int userId) => new()
        {
            OrderCode = "TCT-MOCK",
            UserId = userId,
            PlanId = 1,
            Provider = PaymentProviders.PayOS,
            ProviderRequestId = "261120130509123",
            AmountVnd = 49000m,
            Currency = "VND",
            Status = PaymentOrderStatuses.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
        };
    }
}
