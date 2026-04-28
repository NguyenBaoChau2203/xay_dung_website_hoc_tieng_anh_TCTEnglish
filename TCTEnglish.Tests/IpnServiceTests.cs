using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TCTVocabulary.Models;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;

namespace TCTEnglish.Tests
{
    public class IpnServiceTests
    {
        // ─── Helpers ──────────────────────────────────────────────────────────

        private DbflashcardContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new DbflashcardContext(options);
        }

        private IpnService CreateIpnService(
            DbflashcardContext ctx,
            IPaymentGateway? gateway = null)
        {
            gateway ??= new FakeVnPayGateway(signatureValid: true, isPaid: true);
            var subService = new SubscriptionService(ctx);
            return new IpnService(
                ctx,
                new[] { gateway },
                subService,
                NullLogger<IpnService>.Instance);
        }

        private async Task<(User user, PremiumPlan plan, PaymentOrder order)> SeedPendingOrder(
            DbflashcardContext ctx,
            decimal amountVnd = 49000m,
            string orderCode = "TCT-TEST-001",
            string provider = PaymentProviders.VNPay)
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
                Code = "premium_monthly",
                Name = "Premium 1 tháng",
                Description = "Test plan",
                PriceVnd = amountVnd,
                DurationDays = 30,
                IsActive = true,
                DisplayOrder = 1,
                CreatedAtUtc = DateTime.UtcNow
            };
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();

            var order = new PaymentOrder
            {
                OrderCode = orderCode,
                UserId = user.UserId,
                PlanId = plan.Id,
                Provider = provider,
                AmountVnd = amountVnd,
                Currency = "VND",
                Status = PaymentOrderStatuses.Pending,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15)
            };
            ctx.PaymentOrders.Add(order);
            await ctx.SaveChangesAsync();

            return (user, plan, order);
        }

        private static Dictionary<string, string> BuildSuccessParams(
            string orderCode = "TCT-TEST-001",
            decimal amountVnd = 49000m,
            string txnNo = "14000001",
            string responseCode = "00",
            string txnStatus = "00",
            string currencyCode = "VND")
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["vnp_TxnRef"] = orderCode,
                ["vnp_Amount"] = ((long)(amountVnd * 100)).ToString(),
                ["vnp_CurrCode"] = currencyCode,
                ["vnp_ResponseCode"] = responseCode,
                ["vnp_TransactionStatus"] = txnStatus,
                ["vnp_TransactionNo"] = txnNo,
                ["vnp_PayDate"] = "20260425120000",
                ["vnp_SecureHash"] = "VALID_HASH"
            };
        }

        // ─── Test 1: Invalid signature => no update ───────────────────────────

        [Fact]
        public async Task Ipn_InvalidChecksum_ReturnsCode97_NoOrderUpdate()
        {
            var db = Guid.NewGuid().ToString();
            var ctx = CreateContext(db);
            var (_, _, order) = await SeedPendingOrder(ctx);

            // Gateway that always reports invalid signature
            var gateway = new FakeVnPayGateway(signatureValid: false, isPaid: false);
            var svc = CreateIpnService(ctx, gateway);

            var parms = BuildSuccessParams();
            var result = await svc.ProcessVnPayIpnAsync(parms);

            Assert.Equal("97", result.RspCode);
            Assert.Equal("Invalid signature", result.Message);

            // Order must remain Pending
            var ctx2 = CreateContext(db);
            var refreshedOrder = await ctx2.PaymentOrders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(PaymentOrderStatuses.Pending, refreshedOrder.Status);

            // PaymentEvent logged with SignatureValid=false
            var evt = await ctx2.PaymentEvents.FirstOrDefaultAsync();
            Assert.NotNull(evt);
            Assert.False(evt!.SignatureValid);
        }

        [Fact]
        public async Task Ipn_InvalidChecksum_DoesNotBlockLaterValidRetry()
        {
            var db = Guid.NewGuid().ToString();
            var ctx = CreateContext(db);
            var (user, _, order) = await SeedPendingOrder(ctx);

            var invalidGateway = new FakeVnPayGateway(signatureValid: false, isPaid: false);
            var invalidSvc = CreateIpnService(ctx, invalidGateway);
            var parms = BuildSuccessParams();

            var invalidResult = await invalidSvc.ProcessVnPayIpnAsync(parms);
            Assert.Equal("97", invalidResult.RspCode);

            var ctxRetry = CreateContext(db);
            var validGateway = new FakeVnPayGateway(signatureValid: true, isPaid: true);
            var validSvc = CreateIpnService(ctxRetry, validGateway);

            var validResult = await validSvc.ProcessVnPayIpnAsync(parms);
            Assert.Equal("00", validResult.RspCode);

            var ctxVerify = CreateContext(db);
            var refreshedOrder = await ctxVerify.PaymentOrders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(PaymentOrderStatuses.Paid, refreshedOrder.Status);

            var subscription = await ctxVerify.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == user.UserId);
            Assert.NotNull(subscription);
            Assert.Equal(3, await ctxVerify.PaymentEvents.CountAsync());
        }

        // ─── Test 2: Order not found => no update ────────────────────────────

        [Fact]
        public async Task Ipn_OrderNotFound_ReturnsCode01_NoSubscription()
        {
            var db = Guid.NewGuid().ToString();
            var ctx = CreateContext(db);
            // Don't seed any order
            var svc = CreateIpnService(ctx);

            var parms = BuildSuccessParams(orderCode: "NONEXISTENT-ORDER");
            var result = await svc.ProcessVnPayIpnAsync(parms);

            Assert.Equal("01", result.RspCode);
            Assert.Equal("Order not found", result.Message);

            // No subscriptions created
            var ctx2 = CreateContext(db);
            Assert.Equal(0, await ctx2.UserSubscriptions.CountAsync());
        }

        // ─── Test 3: Amount mismatch => no activate ──────────────────────────

        [Fact]
        public async Task Ipn_AmountMismatch_ReturnsCode04_NoActivation()
        {
            var db = Guid.NewGuid().ToString();
            var ctx = CreateContext(db);
            var (_, _, order) = await SeedPendingOrder(ctx, amountVnd: 49000m);
            var svc = CreateIpnService(ctx);

            // Send wrong amount (99000 instead of 49000)
            var parms = BuildSuccessParams(amountVnd: 99000m);
            var result = await svc.ProcessVnPayIpnAsync(parms);

            Assert.Equal("04", result.RspCode);
            Assert.Equal("invalid amount", result.Message);

            // Order still Pending
            var ctx2 = CreateContext(db);
            var refreshedOrder = await ctx2.PaymentOrders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(PaymentOrderStatuses.Pending, refreshedOrder.Status);

            // No subscription created
            Assert.Equal(0, await ctx2.UserSubscriptions.CountAsync());
        }

        // ─── Test 4: Successful IPN => order Paid + subscription Active ──────

        [Fact]
        public async Task Ipn_CurrencyMismatch_ReturnsCode04_LogsFailedEventAndDoesNotActivate()
        {
            var db = Guid.NewGuid().ToString();
            var ctx = CreateContext(db);
            var (user, _, order) = await SeedPendingOrder(ctx);
            var svc = CreateIpnService(ctx);

            var parms = BuildSuccessParams(currencyCode: "USD");
            var result = await svc.ProcessVnPayIpnAsync(parms);

            Assert.Equal("04", result.RspCode);
            Assert.Equal("Invalid Currency", result.Message);

            var ctx2 = CreateContext(db);
            var refreshedOrder = await ctx2.PaymentOrders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(PaymentOrderStatuses.Pending, refreshedOrder.Status);
            Assert.Equal(0, await ctx2.UserSubscriptions.CountAsync(s => s.UserId == user.UserId));

            var evt = await ctx2.PaymentEvents.FirstAsync(e => e.PaymentOrderId == order.Id);
            Assert.Equal(PaymentEventProcessingStatuses.Failed, evt.ProcessingStatus);
            Assert.Contains("Currency mismatch", evt.ProcessingMessage);
        }

        [Fact]
        public async Task Ipn_ProviderMismatch_ReturnsCode01_LogsFailedEventAndDoesNotActivate()
        {
            var db = Guid.NewGuid().ToString();
            var ctx = CreateContext(db);
            var (user, _, order) = await SeedPendingOrder(
                ctx,
                orderCode: "TCT-TEST-PROVIDER",
                provider: PaymentProviders.MoMo);
            var svc = CreateIpnService(ctx);

            var parms = BuildSuccessParams(orderCode: order.OrderCode);
            var result = await svc.ProcessVnPayIpnAsync(parms);

            Assert.Equal("01", result.RspCode);
            Assert.Equal("Order not found", result.Message);

            var ctx2 = CreateContext(db);
            var refreshedOrder = await ctx2.PaymentOrders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(PaymentOrderStatuses.Pending, refreshedOrder.Status);
            Assert.Equal(0, await ctx2.UserSubscriptions.CountAsync(s => s.UserId == user.UserId));

            var evt = await ctx2.PaymentEvents.FirstAsync(e => e.PaymentOrderId == order.Id);
            Assert.Equal(PaymentEventProcessingStatuses.Failed, evt.ProcessingStatus);
            Assert.Contains("Provider mismatch", evt.ProcessingMessage);
        }

        [Fact]
        public async Task Ipn_Success_OrderPaid_SubscriptionActive()
        {
            var db = Guid.NewGuid().ToString();
            var ctx = CreateContext(db);
            var (user, plan, order) = await SeedPendingOrder(ctx);
            var svc = CreateIpnService(ctx);

            var parms = BuildSuccessParams();
            var result = await svc.ProcessVnPayIpnAsync(parms);

            Assert.Equal("00", result.RspCode);
            Assert.Equal("Confirm Success", result.Message);

            // Order must be Paid
            var ctx2 = CreateContext(db);
            var refreshedOrder = await ctx2.PaymentOrders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(PaymentOrderStatuses.Paid, refreshedOrder.Status);
            Assert.NotNull(refreshedOrder.PaidAtUtc);
            Assert.Equal("14000001", refreshedOrder.ProviderTransactionId);
            Assert.Equal("00", refreshedOrder.ProviderResponseCode);
            Assert.NotNull(refreshedOrder.IpnPayloadJson);

            // Subscription must be created and Active
            var sub = await ctx2.UserSubscriptions.FirstOrDefaultAsync(
                s => s.UserId == user.UserId);
            Assert.NotNull(sub);
            Assert.Equal(SubscriptionStatuses.Active, sub!.Status);
            Assert.Equal(order.Id, sub.ActivatedByPaymentOrderId);
            Assert.Equal(plan.Id, sub.PlanId);

            // User role upgraded to Premium
            var refreshedUser = await ctx2.Users.FirstAsync(u => u.UserId == user.UserId);
            Assert.Equal(Roles.Premium, refreshedUser.Role);

            // PaymentEvent logged
            var evt = await ctx2.PaymentEvents.FirstOrDefaultAsync(
                e => e.PaymentOrderId == order.Id);
            Assert.NotNull(evt);
            Assert.True(evt!.SignatureValid);
            Assert.Equal(PaymentEventProcessingStatuses.Processed, evt.ProcessingStatus);
        }

        // ─── Test 5: Duplicate IPN => no double extension ────────────────────

        [Fact]
        public async Task Ipn_Duplicate_NoDoubleExtension()
        {
            var db = Guid.NewGuid().ToString();
            var ctx = CreateContext(db);
            var (user, _, order) = await SeedPendingOrder(ctx);
            var svc = CreateIpnService(ctx);

            // First IPN
            var parms = BuildSuccessParams();
            var result1 = await svc.ProcessVnPayIpnAsync(parms);
            Assert.Equal("00", result1.RspCode);

            // Second identical IPN
            var result2 = await svc.ProcessVnPayIpnAsync(parms);
            Assert.Equal("02", result2.RspCode);
            Assert.Equal("Order already confirmed", result2.Message);

            // Only one subscription must exist
            var ctx2 = CreateContext(db);
            var subCount = await ctx2.UserSubscriptions
                .CountAsync(s => s.UserId == user.UserId);
            Assert.Equal(1, subCount);
        }

        // ─── Test 6: Failed response => order Failed, no subscription ────────

        [Fact]
        public async Task Ipn_FailedResponse_OrderFailed_NoSubscription()
        {
            var db = Guid.NewGuid().ToString();
            var ctx = CreateContext(db);
            var (user, _, order) = await SeedPendingOrder(ctx);

            // Gateway says signature valid but IPN result is failed
            var gateway = new FakeVnPayGateway(signatureValid: true, isPaid: false);
            var svc = CreateIpnService(ctx, gateway);

            var parms = BuildSuccessParams(responseCode: "07", txnStatus: "02");
            var result = await svc.ProcessVnPayIpnAsync(parms);

            Assert.Equal("00", result.RspCode); // VNPay expects "00" to confirm receipt

            // Order must be Failed
            var ctx2 = CreateContext(db);
            var refreshedOrder = await ctx2.PaymentOrders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(PaymentOrderStatuses.Failed, refreshedOrder.Status);

            // No subscription
            Assert.Equal(0, await ctx2.UserSubscriptions.CountAsync(s => s.UserId == user.UserId));
        }

        // ─── Test 7: Cancelled payment (code 24) => order Cancelled ──────────

        [Fact]
        public async Task Ipn_Cancelled_OrderCancelled_NoSubscription()
        {
            var db = Guid.NewGuid().ToString();
            var ctx = CreateContext(db);
            var (user, _, order) = await SeedPendingOrder(ctx);

            var gateway = new FakeVnPayGateway(signatureValid: true, isPaid: false);
            var svc = CreateIpnService(ctx, gateway);

            var parms = BuildSuccessParams(responseCode: "24", txnStatus: "02");
            var result = await svc.ProcessVnPayIpnAsync(parms);

            Assert.Equal("00", result.RspCode);

            var ctx2 = CreateContext(db);
            var refreshedOrder = await ctx2.PaymentOrders.FirstAsync(o => o.Id == order.Id);
            Assert.Equal(PaymentOrderStatuses.Cancelled, refreshedOrder.Status);

            Assert.Equal(0, await ctx2.UserSubscriptions.CountAsync(s => s.UserId == user.UserId));
        }

        // ─── Test 8: Already paid order receives IPN => returns 02 ───────────

        [Fact]
        public async Task Ipn_AlreadyPaidOrder_DifferentEventKey_ReturnsCode02()
        {
            var db = Guid.NewGuid().ToString();
            var ctx = CreateContext(db);
            var (user, _, order) = await SeedPendingOrder(ctx);
            var svc = CreateIpnService(ctx);

            // First successful IPN
            var parms1 = BuildSuccessParams(txnNo: "14000001");
            await svc.ProcessVnPayIpnAsync(parms1);

            // Second IPN with different event key but same order
            var parms2 = BuildSuccessParams(txnNo: "14000002");
            var result2 = await svc.ProcessVnPayIpnAsync(parms2);

            Assert.Equal("02", result2.RspCode);
            Assert.Equal("Order already confirmed", result2.Message);

            // Still only one subscription
            var ctx2 = CreateContext(db);
            var subCount = await ctx2.UserSubscriptions
                .CountAsync(s => s.UserId == user.UserId);
            Assert.Equal(1, subCount);
        }

        // ─── Test 9: IPN does not leak secrets in PayloadJson ────────────────

        [Fact]
        public async Task Ipn_PayloadJson_DoesNotContainSecureHash()
        {
            var db = Guid.NewGuid().ToString();
            var ctx = CreateContext(db);
            await SeedPendingOrder(ctx);
            var svc = CreateIpnService(ctx);

            var parms = BuildSuccessParams();
            parms["vnp_SecureHash"] = "SUPER_SECRET_HASH_VALUE";
            await svc.ProcessVnPayIpnAsync(parms);

            var ctx2 = CreateContext(db);
            var evt = await ctx2.PaymentEvents.FirstAsync();
            Assert.DoesNotContain("SUPER_SECRET_HASH_VALUE", evt.PayloadJson);
            Assert.DoesNotContain("vnp_SecureHash", evt.PayloadJson);
        }

        // ─── Fake Gateway ────────────────────────────────────────────────────

        private sealed class FakeVnPayGateway : IPaymentGateway
        {
            private readonly bool _signatureValid;
            private readonly bool _isPaid;

            public FakeVnPayGateway(bool signatureValid, bool isPaid)
            {
                _signatureValid = signatureValid;
                _isPaid = isPaid;
            }

            public string ProviderName => PaymentProviders.VNPay;
            public bool IsEnabled => true;

            public Task<ProviderCheckoutResult> CreateCheckoutAsync(
                string orderCode, decimal amountVnd, string desc, string ip, CancellationToken ct)
                => Task.FromResult(ProviderCheckoutResult.Ok("https://example.com/pay"));

            public Task<ProviderCallbackResult> ProcessReturnAsync(
                IDictionary<string, string> q, CancellationToken ct)
                => Task.FromResult(new ProviderCallbackResult { IsVerified = true, IsPaid = true });

            public Task<ProviderIpnResult> ProcessIpnAsync(
                IDictionary<string, string> p, CancellationToken ct)
            {
                p.TryGetValue("vnp_TxnRef", out var orderCode);
                p.TryGetValue("vnp_TransactionNo", out var txnNo);
                p.TryGetValue("vnp_ResponseCode", out var responseCode);
                p.TryGetValue("vnp_TransactionStatus", out var txnStatus);

                return Task.FromResult(new ProviderIpnResult
                {
                    SignatureValid = _signatureValid,
                    IsPaid = _isPaid && _signatureValid && responseCode == "00",
                    OrderCode = orderCode,
                    ProviderTransactionId = txnNo,
                    ProviderResponseCode = responseCode,
                    ProviderTransactionStatus = txnStatus
                });
            }
        }
    }
}

