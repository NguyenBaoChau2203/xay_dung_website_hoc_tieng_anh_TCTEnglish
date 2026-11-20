using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    /// <summary>
    /// Matrix rows T13 (open redirect), T15 (mock endpoints closed) and T19 (no secret in stored payloads)
    /// for the providers that exist before payOS. See plan section 12.
    /// </summary>
    public class BillingMatrixGapTests
    {
        private static DbflashcardContext NewContext(string name)
            => new(new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(name)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);

        private static BillingController NewController(
            Mock<IBillingService> billing,
            Mock<IIpnService> ipn,
            MoMoOptions moMoOptions,
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
                Options.Create(moMoOptions),
                new StubWebHostEnvironment { EnvironmentName = environmentName },
                Mock.Of<IPaymentQueryService>(),
                Mock.Of<ISubscriptionService>(),
                NullLogger<BillingController>.Instance);

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

        // ── T15: the mock confirmation is closed unless Development + Enabled + MockModeEnabled ──────────────

        [Theory]
        [InlineData("Production", true, true)]
        [InlineData("Staging", true, true)]
        [InlineData("Development", true, false)]
        [InlineData("Development", false, true)]
        public async Task MoMoMockConfirm_IsNotFound_WhenMockModeIsNotFullyOn(
            string environmentName, bool enabled, bool mockModeEnabled)
        {
            var dbName = Guid.NewGuid().ToString();
            await using var context = NewContext(dbName);
            context.PaymentOrders.Add(MoMoOrder("ORD-MOCK", 1));
            await context.SaveChangesAsync();

            var ipn = new Mock<IIpnService>(MockBehavior.Strict);
            var controller = NewController(
                new Mock<IBillingService>(),
                ipn,
                new MoMoOptions { Enabled = enabled, MockModeEnabled = mockModeEnabled },
                environmentName,
                context);

            var result = await controller.MoMoMockConfirm("ORD-MOCK");

            Assert.IsType<NotFoundResult>(result);
            ipn.Verify(s => s.ProcessMoMoIpnAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task MoMoMockConfirm_NeverConfirmsAnotherBuyersOrder()
        {
            var dbName = Guid.NewGuid().ToString();
            await using var context = NewContext(dbName);
            context.PaymentOrders.Add(MoMoOrder("ORD-OTHER", userId: 2));
            await context.SaveChangesAsync();

            var ipn = new Mock<IIpnService>(MockBehavior.Strict);
            var controller = NewController(
                new Mock<IBillingService>(),
                ipn,
                new MoMoOptions { Enabled = true, MockModeEnabled = true },
                "Development",
                context,
                userId: 1);

            var result = await controller.MoMoMockConfirm("ORD-OTHER");

            Assert.IsType<NotFoundResult>(result);
            ipn.Verify(s => s.ProcessMoMoIpnAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── T13: the return pages only ever redirect to our own order page ────────────────────────────────────

        [Theory]
        [InlineData("returnUrl", "https://evil.example/steal")]
        [InlineData("redirect", "//evil.example")]
        [InlineData("next", "http://evil.example/x?y=1")]
        public async Task VnPayReturn_IgnoresAnyRedirectTargetInTheQuery(string name, string value)
        {
            var billing = new Mock<IBillingService>();
            billing
                .Setup(s => s.GetVnPayReturnResultAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(VerifiedReturn("ORD-1", ownerUserId: 1));
            await using var context = NewContext(Guid.NewGuid().ToString());
            var controller = NewController(billing, new Mock<IIpnService>(), new MoMoOptions(), "Production", context);
            controller.ControllerContext.HttpContext.Request.QueryString =
                new QueryString($"?vnp_TxnRef=ORD-1&vnp_ResponseCode=00&{name}={Uri.EscapeDataString(value)}");

            var result = await controller.VnPayReturn();

            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.Equal("/Billing/Order/ORD-1", redirect.Url);
        }

        [Theory]
        [InlineData("returnUrl", "https://evil.example/steal")]
        [InlineData("redirect", "//evil.example")]
        public async Task MoMoReturn_IgnoresAnyRedirectTargetInTheQuery(string name, string value)
        {
            var billing = new Mock<IBillingService>();
            billing
                .Setup(s => s.GetMoMoReturnResultAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(VerifiedReturn("ORD-1", ownerUserId: 1));
            await using var context = NewContext(Guid.NewGuid().ToString());
            var controller = NewController(billing, new Mock<IIpnService>(), new MoMoOptions(), "Production", context);
            controller.ControllerContext.HttpContext.Request.QueryString =
                new QueryString($"?orderId=ORD-1&resultCode=0&{name}={Uri.EscapeDataString(value)}");

            var result = await controller.MoMoReturn();

            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.Equal("/Billing/Order/ORD-1", redirect.Url);
        }

        [Fact]
        public async Task Return_WithAnOrderCodeThatNeedsEscaping_StaysOnTheOrderRoute()
        {
            var billing = new Mock<IBillingService>();
            billing
                .Setup(s => s.GetMoMoReturnResultAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(VerifiedReturn("ORD/../../evil?x=1", ownerUserId: 1));
            await using var context = NewContext(Guid.NewGuid().ToString());
            var controller = NewController(billing, new Mock<IIpnService>(), new MoMoOptions(), "Production", context);
            controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?orderId=ORD-1&resultCode=0");

            var redirect = Assert.IsType<RedirectResult>(await controller.MoMoReturn());

            Assert.StartsWith("/Billing/Order/", redirect.Url, StringComparison.Ordinal);
            // The code is one escaped path segment: no extra segment, no query string, no fragment.
            var segment = redirect.Url["/Billing/Order/".Length..];
            Assert.DoesNotContain('/', segment);
            Assert.DoesNotContain('?', segment);
            Assert.DoesNotContain('#', segment);
        }

        // ── T19: a stored event or order payload never holds the provider signature ───────────────────────────

        [Fact]
        public async Task MoMoIpn_StoredPayloads_DoNotContainTheSignature()
        {
            var dbName = Guid.NewGuid().ToString();
            const string signature = "SECRET-LOOKING-SIGNATURE-0123456789abcdef";
            await using (var seed = NewContext(dbName))
            {
                var user = new User { Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "hash", Role = Roles.Standard };
                var plan = new PremiumPlan
                {
                    Code = "p", Name = "Premium", Description = "d", PriceVnd = 49000m, DurationDays = 30,
                    IsActive = true, DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow
                };
                seed.Users.Add(user);
                seed.PremiumPlans.Add(plan);
                await seed.SaveChangesAsync();
                var order = MoMoOrder("ORD-SIG", user.UserId);
                order.PlanId = plan.Id;
                seed.PaymentOrders.Add(order);
                await seed.SaveChangesAsync();
            }

            await using var ctx = NewContext(dbName);
            var service = new IpnService(
                ctx,
                new IPaymentGateway[] { new AlwaysValidGateway(PaymentProviders.VNPay), new AlwaysValidGateway(PaymentProviders.MoMo) },
                new PaymentConfirmationService(ctx, new SubscriptionService(ctx), NullLogger<PaymentConfirmationService>.Instance),
                NullLogger<IpnService>.Instance);

            await service.ProcessMoMoIpnAsync(new Dictionary<string, string>
            {
                ["orderId"] = "ORD-SIG",
                ["requestId"] = "REQ-SIG",
                ["transId"] = "TXN-SIG",
                ["amount"] = "49000",
                ["resultCode"] = "0",
                ["payType"] = "qr",
                ["signature"] = signature
            });

            await using var verify = NewContext(dbName);
            var payloads = await verify.PaymentEvents.AsNoTracking().Select(e => e.PayloadJson).ToListAsync();
            var order2 = await verify.PaymentOrders.AsNoTracking().FirstAsync(o => o.OrderCode == "ORD-SIG");

            Assert.Equal(PaymentOrderStatuses.Paid, order2.Status);
            Assert.NotEmpty(payloads);
            Assert.All(payloads, p => Assert.DoesNotContain(signature, p ?? string.Empty, StringComparison.Ordinal));
            Assert.DoesNotContain(signature, order2.IpnPayloadJson ?? string.Empty, StringComparison.Ordinal);
        }

        private static PaymentOrder MoMoOrder(string orderCode, int userId) => new()
        {
            OrderCode = orderCode,
            UserId = userId,
            PlanId = 1,
            Provider = PaymentProviders.MoMo,
            AmountVnd = 49000m,
            Currency = "VND",
            Status = PaymentOrderStatuses.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
        };

        private static PaymentResultViewModel VerifiedReturn(string orderCode, int ownerUserId) => new()
        {
            HasValidReturnSignature = true,
            CanShowOrderSummary = true,
            OrderCode = orderCode,
            OwnerUserId = ownerUserId,
            OrderStatus = PaymentOrderStatuses.Pending
        };

        private sealed class AlwaysValidGateway : IPaymentGateway
        {
            public AlwaysValidGateway(string name) => ProviderName = name;

            public string ProviderName { get; }
            public bool IsEnabled => true;

            public Task<ProviderCheckoutResult> CreateCheckoutAsync(
                string orderCode, decimal amountVnd, string orderDescription, string clientIp, CancellationToken ct = default)
                => Task.FromResult(ProviderCheckoutResult.Ok("/x"));

            public Task<ProviderCallbackResult> ProcessReturnAsync(IDictionary<string, string> queryParams, CancellationToken ct = default)
                => Task.FromResult(new ProviderCallbackResult { IsVerified = true });

            public Task<ProviderIpnResult> ProcessIpnAsync(IDictionary<string, string> parameters, CancellationToken ct = default)
                => Task.FromResult(new ProviderIpnResult { SignatureValid = true, IsPaid = true });
        }
    }
}
