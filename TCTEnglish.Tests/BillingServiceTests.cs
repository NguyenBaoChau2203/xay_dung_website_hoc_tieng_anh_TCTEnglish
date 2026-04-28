using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TCTVocabulary.Models;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;

namespace TCTEnglish.Tests
{
    public class BillingServiceTests
    {
        // ───────────────────────────────────────────────────────────────────────
        //  Helpers
        // ───────────────────────────────────────────────────────────────────────

        private DbflashcardContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new DbflashcardContext(options);
        }

        private static BillingService CreateService(
            DbflashcardContext ctx,
            IEnumerable<IPaymentGateway>? gateways = null)
        {
            gateways ??= new[] { new FakeEnabledGateway() };
            return new BillingService(
                ctx,
                gateways,
                NullLogger<BillingService>.Instance);
        }

        private async Task<(User user, PremiumPlan plan)> SeedUserAndPlan(
            DbflashcardContext ctx,
            bool planActive = true,
            string planCode = "premium_monthly")
        {
            var user = new User
            {
                Email = $"u-{Guid.NewGuid():N}@test.com",
                PasswordHash = "hash",
                Role = Roles.Standard
            };
            ctx.Users.Add(user);

            var plan = new PremiumPlan
            {
                Code = planCode,
                Name = "Premium 1 tháng",
                Description = "Test plan",
                PriceVnd = 49000,
                DurationDays = 30,
                IsActive = planActive,
                DisplayOrder = 1,
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();
            return (user, plan);
        }

        private async Task<PaymentOrder> SeedOrderAsync(
            DbflashcardContext ctx,
            User user,
            PremiumPlan plan,
            string provider,
            string status = PaymentOrderStatuses.Pending,
            string orderCode = "TCT-RETURN-001")
        {
            var order = new PaymentOrder
            {
                OrderCode = orderCode,
                UserId = user.UserId,
                PlanId = plan.Id,
                Provider = provider,
                AmountVnd = plan.PriceVnd,
                Currency = "VND",
                Status = status,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
                PaidAtUtc = status == PaymentOrderStatuses.Paid ? DateTime.UtcNow : null
            };

            ctx.PaymentOrders.Add(order);
            await ctx.SaveChangesAsync();
            return order;
        }

        // ───────────────────────────────────────────────────────────────────────
        //  Checkout — happy path
        // ───────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Checkout_ActivePlan_CreatesPendingOrder()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan) = await SeedUserAndPlan(ctx);
            var svc = CreateService(ctx);

            var result = await svc.CreateCheckoutAsync(
                user.UserId, plan.Code, "fake", "127.0.0.1");

            Assert.True(result.Success);
            Assert.NotNull(result.OrderCode);
            Assert.NotNull(result.RedirectUrl);

            var order = await ctx.PaymentOrders.FirstAsync();
            Assert.Equal(PaymentOrderStatuses.Pending, order.Status);
            Assert.Equal(plan.PriceVnd, order.AmountVnd);
            Assert.Equal(plan.Id, order.PlanId);
            Assert.Equal(user.UserId, order.UserId);
            Assert.Equal("VND", order.Currency);
        }

        [Fact]
        public async Task Checkout_OrderCode_IsUniqueAcrossCalls()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan) = await SeedUserAndPlan(ctx);
            var svc = CreateService(ctx);

            var r1 = await svc.CreateCheckoutAsync(user.UserId, plan.Code, "fake", "127.0.0.1");
            var r2 = await svc.CreateCheckoutAsync(user.UserId, plan.Code, "fake", "127.0.0.1");

            Assert.NotEqual(r1.OrderCode, r2.OrderCode);
        }

        [Fact]
        public async Task Checkout_OrderCode_IsOpaqueAndDoesNotLeakPii()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan) = await SeedUserAndPlan(ctx);
            var svc = CreateService(ctx);

            var result = await svc.CreateCheckoutAsync(
                user.UserId, plan.Code, "fake", "127.0.0.1");

            // Order code should follow TCT-{hex}-{hex} format
            Assert.StartsWith("TCT-", result.OrderCode!);
            Assert.Equal(3, result.OrderCode!.Split('-').Length);
            // Must not leak email
            Assert.DoesNotContain(user.Email, result.OrderCode!);
        }

        [Fact]
        public async Task Checkout_AmountFromDb_NotFromClient()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan) = await SeedUserAndPlan(ctx);
            var svc = CreateService(ctx);

            // Client has no way to pass amount — it's resolved from DB
            await svc.CreateCheckoutAsync(user.UserId, plan.Code, "fake", "127.0.0.1");

            var order = await ctx.PaymentOrders.FirstAsync();
            Assert.Equal(49000, order.AmountVnd);
        }

        // ───────────────────────────────────────────────────────────────────────
        //  Checkout — error paths
        // ───────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Checkout_InactivePlan_Fails()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan) = await SeedUserAndPlan(ctx, planActive: false);
            var svc = CreateService(ctx);

            var result = await svc.CreateCheckoutAsync(
                user.UserId, plan.Code, "fake", "127.0.0.1");

            Assert.False(result.Success);
            Assert.Equal("PLAN_INACTIVE", result.ErrorCode);
        }

        [Fact]
        public async Task Checkout_NonExistentPlan_Fails()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, _) = await SeedUserAndPlan(ctx);
            var svc = CreateService(ctx);

            var result = await svc.CreateCheckoutAsync(
                user.UserId, "does_not_exist", "fake", "127.0.0.1");

            Assert.False(result.Success);
            Assert.Equal("PLAN_NOT_FOUND", result.ErrorCode);
        }

        [Fact]
        public async Task Checkout_UnsupportedProvider_Fails()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan) = await SeedUserAndPlan(ctx);
            var svc = CreateService(ctx);

            var result = await svc.CreateCheckoutAsync(
                user.UserId, plan.Code, "stripe", "127.0.0.1");

            Assert.False(result.Success);
            Assert.Equal("PROVIDER_UNSUPPORTED", result.ErrorCode);
        }

        [Fact]
        public async Task Checkout_DisabledProvider_Fails()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan) = await SeedUserAndPlan(ctx);
            // Use the disabled gateway
            var svc = CreateService(ctx, new[] { new FakeDisabledGateway() });

            var result = await svc.CreateCheckoutAsync(
                user.UserId, plan.Code, "fake_disabled", "127.0.0.1");

            Assert.False(result.Success);
            Assert.Equal("PROVIDER_NOT_CONFIGURED", result.ErrorCode);
        }

        [Fact]
        public async Task Checkout_RateLimit_BlocksExcessiveOrders()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan) = await SeedUserAndPlan(ctx);
            var svc = CreateService(ctx);

            // Create 5 pending orders (the limit)
            for (int i = 0; i < 5; i++)
            {
                var r = await svc.CreateCheckoutAsync(
                    user.UserId, plan.Code, "fake", "127.0.0.1");
                Assert.True(r.Success, $"Order {i + 1} should succeed");
            }

            // 6th should be rate-limited
            var result = await svc.CreateCheckoutAsync(
                user.UserId, plan.Code, "fake", "127.0.0.1");

            Assert.False(result.Success);
            Assert.Equal("RATE_LIMITED", result.ErrorCode);
        }

        [Fact]
        public async Task Checkout_GatewayFails_MarksOrderFailed()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan) = await SeedUserAndPlan(ctx);
            var svc = CreateService(ctx, new[] { new FakeFailingGateway() });

            var result = await svc.CreateCheckoutAsync(
                user.UserId, plan.Code, "fake_fail", "127.0.0.1");

            Assert.False(result.Success);

            var order = await ctx.PaymentOrders.FirstAsync();
            Assert.Equal(PaymentOrderStatuses.Failed, order.Status);
            Assert.NotNull(order.FailureMessage);
        }

        // ───────────────────────────────────────────────────────────────────────
        //  GetPaymentHistory — anti-IDOR
        // ───────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task History_OnlyReturnsOwnOrders()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user1, plan) = await SeedUserAndPlan(ctx, planCode: "p1_" + Guid.NewGuid().ToString("N"));
            var user2 = new User { Email = "other@test.com", PasswordHash = "h", Role = Roles.Standard };
            ctx.Users.Add(user2);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);

            await svc.CreateCheckoutAsync(user1.UserId, plan.Code, "fake", "127.0.0.1");
            await svc.CreateCheckoutAsync(user1.UserId, plan.Code, "fake", "127.0.0.1");

            // user2 asks for history
            var history = await svc.GetPaymentHistoryAsync(user2.UserId);
            Assert.Empty(history.Orders);

            // user1 asks for history
            var own = await svc.GetPaymentHistoryAsync(user1.UserId);
            Assert.Equal(2, own.Orders.Count);
        }

        // ───────────────────────────────────────────────────────────────────────
        //  CleanupExpiredPendingOrders
        // ───────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task VnPayReturn_InvalidSignature_ReturnsGenericResultWithoutLoadingOrderDetails()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan) = await SeedUserAndPlan(ctx);
            var order = await SeedOrderAsync(ctx, user, plan, PaymentProviders.VNPay);
            var svc = CreateService(ctx, new[] { new FakeVnPayReturnGateway(isVerified: false) });

            var queryParams = new Dictionary<string, string>
            {
                ["vnp_TxnRef"] = order.OrderCode,
                ["vnp_ResponseCode"] = "00",
                ["vnp_TransactionStatus"] = "00",
                ["vnp_Amount"] = "4900000",
                ["vnp_SecureHash"] = "tampered"
            };

            var result = await svc.GetVnPayReturnResultAsync(queryParams);

            Assert.NotNull(result);
            Assert.False(result!.HasValidReturnSignature);
            Assert.False(result.CanShowOrderSummary);
            Assert.Null(result.OwnerUserId);
            Assert.Equal(string.Empty, result.OrderCode);
            Assert.Equal(0m, result.AmountVnd);

            var refreshedOrder = await ctx.PaymentOrders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(PaymentOrderStatuses.Pending, refreshedOrder.Status);
            Assert.Null(refreshedOrder.ReturnPayloadJson);
        }

        [Fact]
        public async Task VnPayReturn_ValidSignature_PaidOrder_ShowsPaidFromDatabaseState()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan) = await SeedUserAndPlan(ctx);
            var order = await SeedOrderAsync(
                ctx,
                user,
                plan,
                PaymentProviders.VNPay,
                PaymentOrderStatuses.Paid,
                "TCT-RETURN-PAID");

            var svc = CreateService(ctx, new[] { new FakeVnPayReturnGateway(isVerified: true) });

            var result = await svc.GetVnPayReturnResultAsync(new Dictionary<string, string>
            {
                ["vnp_TxnRef"] = order.OrderCode,
                ["vnp_ResponseCode"] = "24",
                ["vnp_TransactionStatus"] = "02",
                ["vnp_SecureHash"] = "valid"
            });

            Assert.NotNull(result);
            Assert.True(result!.HasValidReturnSignature);
            Assert.True(result.CanShowOrderSummary);
            Assert.Equal(user.UserId, result.OwnerUserId);
            Assert.Equal(PaymentOrderStatuses.Paid, result.OrderStatus);
        }

        [Fact]
        public async Task VnPayReturn_ValidSignature_PendingOrder_ShowsPendingAndDoesNotActivate()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan) = await SeedUserAndPlan(ctx);
            var order = await SeedOrderAsync(ctx, user, plan, PaymentProviders.VNPay, orderCode: "TCT-RETURN-PENDING");
            var svc = CreateService(ctx, new[] { new FakeVnPayReturnGateway(isVerified: true) });

            var result = await svc.GetVnPayReturnResultAsync(new Dictionary<string, string>
            {
                ["vnp_TxnRef"] = order.OrderCode,
                ["vnp_ResponseCode"] = "00",
                ["vnp_TransactionStatus"] = "00",
                ["vnp_SecureHash"] = "valid"
            });

            Assert.NotNull(result);
            Assert.True(result!.HasValidReturnSignature);
            Assert.True(result.CanShowOrderSummary);
            Assert.Equal(user.UserId, result.OwnerUserId);
            Assert.Equal(PaymentOrderStatuses.Pending, result.OrderStatus);
            Assert.Equal(0, await ctx.UserSubscriptions.CountAsync(s => s.UserId == user.UserId));
        }

        [Fact]
        public async Task VnPayReturn_ValidSignature_ManualReviewOrder_PreservesManualReviewStatus()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan) = await SeedUserAndPlan(ctx);
            var order = await SeedOrderAsync(
                ctx,
                user,
                plan,
                PaymentProviders.VNPay,
                PaymentOrderStatuses.ManualReview,
                "TCT-RETURN-MANUAL-REVIEW");
            var svc = CreateService(ctx, new[] { new FakeVnPayReturnGateway(isVerified: true) });

            var result = await svc.GetVnPayReturnResultAsync(new Dictionary<string, string>
            {
                ["vnp_TxnRef"] = order.OrderCode,
                ["vnp_ResponseCode"] = "00",
                ["vnp_TransactionStatus"] = "00",
                ["vnp_SecureHash"] = "valid"
            });

            Assert.NotNull(result);
            Assert.True(result!.HasValidReturnSignature);
            Assert.True(result.CanShowOrderSummary);
            Assert.Equal(PaymentOrderStatuses.ManualReview, result.OrderStatus);
        }

        [Fact]
        public async Task VnPayReturn_ValidSignature_PartiallyRefundedOrder_PreservesPartiallyRefundedStatus()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan) = await SeedUserAndPlan(ctx);
            var order = await SeedOrderAsync(
                ctx,
                user,
                plan,
                PaymentProviders.VNPay,
                PaymentOrderStatuses.PartiallyRefunded,
                "TCT-RETURN-PARTIALLY-REFUNDED");
            var svc = CreateService(ctx, new[] { new FakeVnPayReturnGateway(isVerified: true) });

            var result = await svc.GetVnPayReturnResultAsync(new Dictionary<string, string>
            {
                ["vnp_TxnRef"] = order.OrderCode,
                ["vnp_ResponseCode"] = "00",
                ["vnp_TransactionStatus"] = "00",
                ["vnp_SecureHash"] = "valid"
            });

            Assert.NotNull(result);
            Assert.True(result!.HasValidReturnSignature);
            Assert.True(result.CanShowOrderSummary);
            Assert.Equal(PaymentOrderStatuses.PartiallyRefunded, result.OrderStatus);
        }

        [Fact]
        public async Task CleanupExpiredPendingOrdersAsync_ShouldMarkOverduePendingOrdersAsExpired()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var (user, plan) = await SeedUserAndPlan(ctx);
            var svc = CreateService(ctx);

            var now = DateTime.UtcNow;

            var order1 = new PaymentOrder
            {
                OrderCode = "TCT-1",
                UserId = user.UserId,
                PlanId = plan.Id,
                Provider = "fake",
                AmountVnd = plan.PriceVnd,
                Currency = "VND",
                Status = PaymentOrderStatuses.Pending,
                CreatedAtUtc = now.AddMinutes(-30),
                ExpiresAtUtc = now.AddMinutes(-15) // expired
            };

            var order2 = new PaymentOrder
            {
                OrderCode = "TCT-2",
                UserId = user.UserId,
                PlanId = plan.Id,
                Provider = "fake",
                AmountVnd = plan.PriceVnd,
                Currency = "VND",
                Status = PaymentOrderStatuses.Pending,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(15) // not expired
            };

            var order3 = new PaymentOrder
            {
                OrderCode = "TCT-3",
                UserId = user.UserId,
                PlanId = plan.Id,
                Provider = "fake",
                AmountVnd = plan.PriceVnd,
                Currency = "VND",
                Status = PaymentOrderStatuses.Paid, // already paid
                CreatedAtUtc = now.AddMinutes(-30),
                ExpiresAtUtc = now.AddMinutes(-15)
            };

            ctx.PaymentOrders.AddRange(order1, order2, order3);
            await ctx.SaveChangesAsync();

            var count = await svc.CleanupExpiredPendingOrdersAsync();

            Assert.Equal(1, count);

            var o1 = await ctx.PaymentOrders.FirstAsync(o => o.OrderCode == "TCT-1");
            var o2 = await ctx.PaymentOrders.FirstAsync(o => o.OrderCode == "TCT-2");
            var o3 = await ctx.PaymentOrders.FirstAsync(o => o.OrderCode == "TCT-3");

            Assert.Equal(PaymentOrderStatuses.Expired, o1.Status);
            Assert.Equal("Đã quá hạn thanh toán.", o1.FailureMessage);

            Assert.Equal(PaymentOrderStatuses.Pending, o2.Status); // unchanged
            Assert.Equal(PaymentOrderStatuses.Paid, o3.Status); // unchanged
        }

        // ───────────────────────────────────────────────────────────────────────
        //  Fake gateways for testing
        // ───────────────────────────────────────────────────────────────────────

        private sealed class FakeEnabledGateway : IPaymentGateway
        {
            public string ProviderName => "fake";
            public bool IsEnabled => true;

            public Task<ProviderCheckoutResult> CreateCheckoutAsync(
                string orderCode, decimal amountVnd, string desc, string ip, CancellationToken ct)
                => Task.FromResult(ProviderCheckoutResult.Ok($"https://pay.fake/checkout?order={orderCode}"));

            public Task<ProviderCallbackResult> ProcessReturnAsync(
                IDictionary<string, string> q, CancellationToken ct)
                => Task.FromResult(new ProviderCallbackResult { IsVerified = true, IsPaid = true });

            public Task<ProviderIpnResult> ProcessIpnAsync(
                IDictionary<string, string> p, CancellationToken ct)
                => Task.FromResult(new ProviderIpnResult { SignatureValid = true, IsPaid = true });
        }

        private sealed class FakeVnPayReturnGateway : IPaymentGateway
        {
            private readonly bool _isVerified;

            public FakeVnPayReturnGateway(bool isVerified)
            {
                _isVerified = isVerified;
            }

            public string ProviderName => PaymentProviders.VNPay;
            public bool IsEnabled => true;

            public Task<ProviderCheckoutResult> CreateCheckoutAsync(
                string orderCode, decimal amountVnd, string desc, string ip, CancellationToken ct)
                => Task.FromResult(ProviderCheckoutResult.Ok($"https://pay.fake/checkout?order={orderCode}"));

            public Task<ProviderCallbackResult> ProcessReturnAsync(
                IDictionary<string, string> q, CancellationToken ct)
                => Task.FromResult(new ProviderCallbackResult
                {
                    IsVerified = _isVerified,
                    IsPaid = _isVerified
                });

            public Task<ProviderIpnResult> ProcessIpnAsync(
                IDictionary<string, string> p, CancellationToken ct)
                => Task.FromResult(new ProviderIpnResult
                {
                    SignatureValid = _isVerified,
                    IsPaid = _isVerified
                });
        }

        private sealed class FakeDisabledGateway : IPaymentGateway
        {
            public string ProviderName => "fake_disabled";
            public bool IsEnabled => false;

            public Task<ProviderCheckoutResult> CreateCheckoutAsync(
                string orderCode, decimal amountVnd, string desc, string ip, CancellationToken ct)
                => Task.FromResult(ProviderCheckoutResult.Fail("DISABLED", "Not configured"));

            public Task<ProviderCallbackResult> ProcessReturnAsync(
                IDictionary<string, string> q, CancellationToken ct)
                => Task.FromResult(new ProviderCallbackResult());

            public Task<ProviderIpnResult> ProcessIpnAsync(
                IDictionary<string, string> p, CancellationToken ct)
                => Task.FromResult(new ProviderIpnResult());
        }

        private sealed class FakeFailingGateway : IPaymentGateway
        {
            public string ProviderName => "fake_fail";
            public bool IsEnabled => true;

            public Task<ProviderCheckoutResult> CreateCheckoutAsync(
                string orderCode, decimal amountVnd, string desc, string ip, CancellationToken ct)
                => Task.FromResult(ProviderCheckoutResult.Fail("GATEWAY_DOWN", "Simulated failure"));

            public Task<ProviderCallbackResult> ProcessReturnAsync(
                IDictionary<string, string> q, CancellationToken ct)
                => Task.FromResult(new ProviderCallbackResult());

            public Task<ProviderIpnResult> ProcessIpnAsync(
                IDictionary<string, string> p, CancellationToken ct)
                => Task.FromResult(new ProviderIpnResult());
        }
    }
}
