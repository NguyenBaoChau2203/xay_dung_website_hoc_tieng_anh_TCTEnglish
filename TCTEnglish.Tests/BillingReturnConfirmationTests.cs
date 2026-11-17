using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.MoMo;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>
    /// The browser's return never confirms an order by itself (G16): a successful return triggers one
    /// throttled server-to-server query, and only a verified answer with the right amount confirms.
    /// </summary>
    public class BillingReturnConfirmationTests
    {
        private const decimal Amount = 399000m;

        private static DbflashcardContext CreateContext(string name)
        {
            var options = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(name)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new DbflashcardContext(options);
        }

        private static BillingService CreateService(
            DbflashcardContext ctx, string provider, bool returnSaysPaid, FakeVnPayQuery vnpay, FakeMoMoQuery momo)
        {
            var query = new PaymentQueryService(
                ctx,
                vnpay,
                momo,
                new PaymentConfirmationService(ctx, new SubscriptionService(ctx), NullLogger<PaymentConfirmationService>.Instance),
                NullLogger<PaymentQueryService>.Instance);
            return new BillingService(
                ctx,
                new IPaymentGateway[] { new ReturnGateway(provider, verified: true, paid: returnSaysPaid) },
                query,
                NullLogger<BillingService>.Instance);
        }

        private static async Task<(int userId, long orderId)> SeedAsync(
            DbflashcardContext ctx, string provider, string status = PaymentOrderStatuses.Pending)
        {
            var user = new User { Email = $"u-{Guid.NewGuid():N}@test.com", PasswordHash = "h", Role = Roles.Standard };
            var plan = new PremiumPlan
            {
                Code = $"p-{Guid.NewGuid():N}", Name = "Premium 1 năm", Description = "d", PriceVnd = Amount,
                DurationDays = 365, IsActive = true, DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow
            };
            ctx.Users.Add(user);
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();
            var order = new PaymentOrder
            {
                OrderCode = "TCT-RET-001", UserId = user.UserId, PlanId = plan.Id, Provider = provider,
                AmountVnd = Amount, Currency = "VND", Status = status,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2), UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(13)
            };
            ctx.PaymentOrders.Add(order);
            await ctx.SaveChangesAsync();
            return (user.UserId, order.Id);
        }

        private static Dictionary<string, string> VnPayReturn(string responseCode = "00")
            => new(StringComparer.OrdinalIgnoreCase)
            {
                ["vnp_TxnRef"] = "TCT-RET-001",
                ["vnp_ResponseCode"] = responseCode,
                ["vnp_TransactionStatus"] = responseCode == "00" ? "00" : "02",
                ["vnp_SecureHash"] = "ABCDEF123456"
            };

        private static Dictionary<string, string> MoMoReturn(string resultCode = "0")
            => new(StringComparer.OrdinalIgnoreCase)
            {
                ["orderId"] = "TCT-RET-001",
                ["resultCode"] = resultCode,
                ["signature"] = "abcdef123456"
            };

        private static VnPayQueryResult VnPaySuccess(long amount = 399000)
            => new() { State = PaymentQueryState.Success, IsSuccess = true, ResponseCode = "00", TransactionStatus = "00", TransactionNo = "VN-1", AmountVnd = amount };

        [Fact]
        public async Task VnPay_SuccessfulReturn_WithAVerifiedQuery_ConfirmsTheOrder_WithoutAnIpn()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (userId, orderId) = await SeedAsync(ctx, PaymentProviders.VNPay);
            var vnpay = new FakeVnPayQuery(VnPaySuccess());
            var service = CreateService(ctx, PaymentProviders.VNPay, true, vnpay, new FakeMoMoQuery());

            var model = await service.GetVnPayReturnResultAsync(VnPayReturn());

            Assert.NotNull(model);
            Assert.Equal(PaymentOrderStatuses.Paid, model!.OrderStatus);
            Assert.True(model.ReturnReportedSuccess);
            Assert.Equal(1, vnpay.Calls);
            Assert.Equal(PaymentOrderStatuses.Paid, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == orderId)).Status);
            Assert.Single(ctx.UserSubscriptions.Where(s => s.UserId == userId));
            Assert.DoesNotContain(ctx.PaymentEvents, e => e.EventType == PaymentEventTypes.Ipn);
        }

        [Fact]
        public async Task MoMo_SuccessfulReturn_WithAVerifiedQuery_ConfirmsTheOrder()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (userId, orderId) = await SeedAsync(ctx, PaymentProviders.MoMo);
            var momo = new FakeMoMoQuery(new PaymentQueryResult(PaymentQueryState.Success, 399000, "555", "0", "0", "qr"));
            var service = CreateService(ctx, PaymentProviders.MoMo, true, new FakeVnPayQuery(null), momo);

            var model = await service.GetMoMoReturnResultAsync(MoMoReturn());

            Assert.Equal(PaymentOrderStatuses.Paid, model!.OrderStatus);
            Assert.Equal(PaymentOrderStatuses.Paid, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == orderId)).Status);
            Assert.Single(ctx.UserSubscriptions.Where(s => s.UserId == userId));
        }

        [Theory]
        [InlineData("unknown")]
        [InlineData("none")]
        [InlineData("wrong-amount")]
        [InlineData("failed")]
        public async Task SuccessfulReturn_WithoutAVerifiedSuccess_NeverConfirms_AndStaysPending(string answer)
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (userId, orderId) = await SeedAsync(ctx, PaymentProviders.VNPay);
            VnPayQueryResult? result = answer switch
            {
                "unknown" => new VnPayQueryResult { State = PaymentQueryState.Unknown, ResponseCode = "91" },
                "wrong-amount" => VnPaySuccess(amount: 1000),
                "failed" => new VnPayQueryResult { State = PaymentQueryState.Failed, ResponseCode = "00", TransactionStatus = "02" },
                _ => null
            };
            var service = CreateService(ctx, PaymentProviders.VNPay, true, new FakeVnPayQuery(result), new FakeMoMoQuery());

            var model = await service.GetVnPayReturnResultAsync(VnPayReturn());

            Assert.Equal(PaymentOrderStatuses.Pending, model!.OrderStatus);
            Assert.True(model.ReturnReportedSuccess);
            Assert.Equal(PaymentOrderStatuses.Pending, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == orderId)).Status);
            Assert.Empty(ctx.UserSubscriptions.Where(s => s.UserId == userId));
        }

        [Fact]
        public async Task CancelledReturn_DoesNotQuery_AndLeavesTheOrderPending()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx, PaymentProviders.VNPay);
            var vnpay = new FakeVnPayQuery(VnPaySuccess());
            var service = CreateService(ctx, PaymentProviders.VNPay, returnSaysPaid: false, vnpay, new FakeMoMoQuery());

            var model = await service.GetVnPayReturnResultAsync(VnPayReturn("24"));

            Assert.Equal(PaymentOrderStatuses.Pending, model!.OrderStatus);
            Assert.False(model.ReturnReportedSuccess);
            Assert.Equal(0, vnpay.Calls);
            Assert.Equal(PaymentOrderStatuses.Pending, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == orderId)).Status);
        }

        [Fact]
        public async Task ReturnForAnOrderThatIsAlreadyPaid_DoesNotQueryAgain()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            await SeedAsync(ctx, PaymentProviders.VNPay, PaymentOrderStatuses.Paid);
            var vnpay = new FakeVnPayQuery(VnPaySuccess());
            var service = CreateService(ctx, PaymentProviders.VNPay, true, vnpay, new FakeMoMoQuery());

            var model = await service.GetVnPayReturnResultAsync(VnPayReturn());

            Assert.Equal(PaymentOrderStatuses.Paid, model!.OrderStatus);
            Assert.Equal(0, vnpay.Calls);
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Refunded)]
        [InlineData(PaymentOrderStatuses.ManualReview)]
        [InlineData(PaymentOrderStatuses.Failed)]
        public async Task ReturnForAClosedOrder_DoesNotQuery_AndDoesNotChangeIt(string status)
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx, PaymentProviders.VNPay, status);
            var vnpay = new FakeVnPayQuery(VnPaySuccess());
            var service = CreateService(ctx, PaymentProviders.VNPay, true, vnpay, new FakeMoMoQuery());

            var model = await service.GetVnPayReturnResultAsync(VnPayReturn());

            Assert.Equal(status, model!.OrderStatus);
            Assert.Equal(0, vnpay.Calls);
            Assert.Equal(status, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == orderId)).Status);
        }

        [Fact]
        public async Task TheSameReturnTwice_QueriesTheProviderOnlyOnce()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            await SeedAsync(ctx, PaymentProviders.VNPay);
            var vnpay = new FakeVnPayQuery(new VnPayQueryResult { State = PaymentQueryState.Unknown });
            var service = CreateService(ctx, PaymentProviders.VNPay, true, vnpay, new FakeMoMoQuery());

            await service.GetVnPayReturnResultAsync(VnPayReturn());
            await service.GetVnPayReturnResultAsync(VnPayReturn());

            Assert.Equal(1, vnpay.Calls);
        }

        [Fact]
        public async Task InvalidSignature_NeverQueries()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            await SeedAsync(ctx, PaymentProviders.VNPay);
            var vnpay = new FakeVnPayQuery(VnPaySuccess());
            var query = new PaymentQueryService(
                ctx, vnpay, new FakeMoMoQuery(),
                new PaymentConfirmationService(ctx, new SubscriptionService(ctx), NullLogger<PaymentConfirmationService>.Instance),
                NullLogger<PaymentQueryService>.Instance);
            var service = new BillingService(
                ctx,
                new IPaymentGateway[] { new ReturnGateway(PaymentProviders.VNPay, verified: false, paid: true) },
                query,
                NullLogger<BillingService>.Instance);

            var model = await service.GetVnPayReturnResultAsync(VnPayReturn());

            Assert.False(model!.HasValidReturnSignature);
            Assert.Equal(0, vnpay.Calls);
        }

        [Fact]
        public async Task ReturnPayload_IsKept_SoThePageCanSayConfirming()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx, PaymentProviders.VNPay);
            var service = CreateService(ctx, PaymentProviders.VNPay, true,
                new FakeVnPayQuery(new VnPayQueryResult { State = PaymentQueryState.Unknown }), new FakeMoMoQuery());

            await service.GetVnPayReturnResultAsync(VnPayReturn());

            var order = await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == orderId);
            Assert.True(PaymentOrderPresentation.ReturnReportedSuccess(PaymentProviders.VNPay, order.ReturnPayloadJson));
            Assert.DoesNotContain("SecureHash", order.ReturnPayloadJson, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                PaymentOrderStages.Confirming,
                PaymentOrderPresentation.ResolveStage(order.Status, order.ExpiresAtUtc, true, DateTime.UtcNow));
        }

        [Fact]
        public async Task Cleanup_OnlyExpiresPendingOrders()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, pendingId) = await SeedAsync(ctx, PaymentProviders.VNPay);
            var order = await ctx.PaymentOrders.FirstAsync(o => o.Id == pendingId);
            order.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await ctx.SaveChangesAsync();
            var service = CreateService(ctx, PaymentProviders.VNPay, true, new FakeVnPayQuery(null), new FakeMoMoQuery());

            var expired = await service.CleanupExpiredPendingOrdersAsync();

            Assert.Equal(1, expired);
            Assert.Equal(PaymentOrderStatuses.Expired, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == pendingId)).Status);
        }

        // ─── Fakes ────────────────────────────────────────────────────────────────

        private sealed class ReturnGateway : IPaymentGateway
        {
            private readonly bool _verified;
            private readonly bool _paid;

            public ReturnGateway(string provider, bool verified, bool paid)
            {
                ProviderName = provider;
                _verified = verified;
                _paid = paid;
            }

            public string ProviderName { get; }
            public bool IsEnabled => true;

            public Task<ProviderCheckoutResult> CreateCheckoutAsync(
                string orderCode, decimal amountVnd, string orderDescription, string clientIp, CancellationToken ct = default)
                => Task.FromResult(ProviderCheckoutResult.Ok("https://example.com"));

            public Task<ProviderCallbackResult> ProcessReturnAsync(IDictionary<string, string> queryParams, CancellationToken ct = default)
                => Task.FromResult(new ProviderCallbackResult { IsVerified = _verified, IsPaid = _verified && _paid });

            public Task<ProviderIpnResult> ProcessIpnAsync(IDictionary<string, string> parameters, CancellationToken ct = default)
                => Task.FromResult(new ProviderIpnResult { SignatureValid = true });
        }

        private sealed class FakeVnPayQuery : IVnPayQueryClient
        {
            private readonly VnPayQueryResult? _result;
            public FakeVnPayQuery(VnPayQueryResult? result) => _result = result;
            public int Calls { get; private set; }

            public Task<VnPayQueryResult?> QueryOrderAsync(
                string orderCode, DateTime transactionCreatedAtUtc, string? providerTransactionNo = null, CancellationToken ct = default)
            {
                Calls++;
                return Task.FromResult(_result);
            }
        }

        private sealed class FakeMoMoQuery : IMoMoQueryClient
        {
            private readonly PaymentQueryResult _result;
            public FakeMoMoQuery(PaymentQueryResult? result = null) => _result = result ?? PaymentQueryResult.Unknown();

            public Task<PaymentQueryResult> QueryOrderAsync(string orderCode, CancellationToken ct = default)
                => Task.FromResult(_result);
        }
    }
}
