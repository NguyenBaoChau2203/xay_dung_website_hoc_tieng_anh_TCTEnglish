using System;
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
    /// Reconciliation (plan 4.5): both providers, pending and recently expired orders, the three-way answer,
    /// and no more "failed" for a transaction that is merely in progress or not found yet (F3).
    /// </summary>
    public class PaymentReconciliationServiceTests
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

        private static PaymentReconciliationService CreateService(
            DbflashcardContext ctx, FakeVnPayQuery vnpay, FakeMoMoQuery momo)
        {
            var query = new PaymentQueryService(
                ctx,
                vnpay,
                momo,
                new PaymentConfirmationService(ctx, new SubscriptionService(ctx), NullLogger<PaymentConfirmationService>.Instance),
                NullLogger<PaymentQueryService>.Instance);
            return new PaymentReconciliationService(ctx, query, NullLogger<PaymentReconciliationService>.Instance);
        }

        private static async Task<(int userId, long orderId)> SeedAsync(
            DbflashcardContext ctx,
            string provider = PaymentProviders.VNPay,
            string status = PaymentOrderStatuses.Pending,
            TimeSpan? expiresIn = null,
            int createdMinutesAgo = 30,
            string? returnPayload = null,
            string orderCode = "TCT-REC-001")
        {
            var user = new User { Email = $"u-{Guid.NewGuid():N}@test.com", PasswordHash = "h", Role = Roles.Standard };
            var plan = new PremiumPlan
            {
                Code = $"p-{Guid.NewGuid():N}", Name = "Premium", Description = "d", PriceVnd = Amount,
                DurationDays = 365, IsActive = true, DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow
            };
            ctx.Users.Add(user);
            ctx.PremiumPlans.Add(plan);
            await ctx.SaveChangesAsync();
            var order = new PaymentOrder
            {
                OrderCode = orderCode, UserId = user.UserId, PlanId = plan.Id, Provider = provider,
                AmountVnd = Amount, Currency = "VND", Status = status,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-createdMinutesAgo),
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-createdMinutesAgo),
                ExpiresAtUtc = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromMinutes(-15)),
                ReturnPayloadJson = returnPayload
            };
            ctx.PaymentOrders.Add(order);
            await ctx.SaveChangesAsync();
            return (user.UserId, order.Id);
        }

        private static VnPayQueryResult VnPaySuccess(long amount = 399000)
            => new() { State = PaymentQueryState.Success, IsSuccess = true, ResponseCode = "00", TransactionStatus = "00", TransactionNo = "VN-1", AmountVnd = amount };

        private static string Status(DbflashcardContext ctx, long id)
            => ctx.PaymentOrders.AsNoTracking().First(o => o.Id == id).Status;

        // ─── Confirmation ─────────────────────────────────────────────────────────

        [Fact]
        public async Task PendingVnPayOrder_WithVerifiedSuccess_IsPaidOnce()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (userId, orderId) = await SeedAsync(ctx);
            var service = CreateService(ctx, new FakeVnPayQuery(VnPaySuccess()), new FakeMoMoQuery());

            var summary = await service.ReconcileOpenOrdersAsync();

            Assert.Equal(1, summary.Checked);
            Assert.Equal(1, summary.Paid);
            Assert.Equal(PaymentOrderStatuses.Paid, Status(ctx, orderId));
            Assert.Single(ctx.UserSubscriptions.Where(s => s.UserId == userId));
        }

        [Fact]
        public async Task RecentlyExpiredVnPayOrder_WithVerifiedSuccess_IsPaid_NotLeftExpiredForever()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx, status: PaymentOrderStatuses.Expired, expiresIn: TimeSpan.FromHours(-3));
            var service = CreateService(ctx, new FakeVnPayQuery(VnPaySuccess()), new FakeMoMoQuery());

            var summary = await service.ReconcileOpenOrdersAsync();

            Assert.Equal(1, summary.Paid);
            Assert.Equal(PaymentOrderStatuses.Paid, Status(ctx, orderId));
        }

        [Fact]
        public async Task MoMoOrders_AreReconciledToo()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx, PaymentProviders.MoMo);
            var momo = new FakeMoMoQuery(new PaymentQueryResult(PaymentQueryState.Success, 399000, "555", "0", "0", "qr"));
            var service = CreateService(ctx, new FakeVnPayQuery(null), momo);

            var summary = await service.ReconcileOpenOrdersAsync();

            Assert.Equal(1, summary.Paid);
            Assert.Equal(PaymentOrderStatuses.Paid, Status(ctx, orderId));
        }

        [Fact]
        public async Task TheOldVnPayEntryPoint_StillWorks_AndCoversBothProviders()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, momoId) = await SeedAsync(ctx, PaymentProviders.MoMo, orderCode: "TCT-REC-M");
            var (_, vnpayId) = await SeedAsync(ctx, PaymentProviders.VNPay, orderCode: "TCT-REC-V");
            var momo = new FakeMoMoQuery(new PaymentQueryResult(PaymentQueryState.Success, 399000, "555", "0", "0", "qr"));
            var service = CreateService(ctx, new FakeVnPayQuery(VnPaySuccess()), momo);

            var summary = await service.ReconcileVnPayPendingOrdersAsync();

            Assert.Equal(2, summary.Paid);
            Assert.Equal(PaymentOrderStatuses.Paid, Status(ctx, momoId));
            Assert.Equal(PaymentOrderStatuses.Paid, Status(ctx, vnpayId));
        }

        [Fact]
        public async Task VerifiedSuccess_WithTheWrongAmount_DoesNotConfirm()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (userId, orderId) = await SeedAsync(ctx);
            var service = CreateService(ctx, new FakeVnPayQuery(VnPaySuccess(amount: 49000)), new FakeMoMoQuery());

            var summary = await service.ReconcileOpenOrdersAsync();

            Assert.Equal(0, summary.Paid);
            Assert.Equal(PaymentOrderStatuses.Pending, Status(ctx, orderId));
            Assert.Empty(ctx.UserSubscriptions.Where(s => s.UserId == userId));
        }

        // ─── Unknown never fails an order (F3) ────────────────────────────────────

        [Theory]
        [InlineData("91")] // not found yet
        [InlineData("99")] // the query itself failed
        public async Task UnknownAnswer_LeavesThePendingOrderAlone_EvenWhenOlderThanFiveMinutes(string code)
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx, createdMinutesAgo: 20);
            var vnpay = new FakeVnPayQuery(new VnPayQueryResult { State = PaymentQueryState.Unknown, ResponseCode = code });
            var service = CreateService(ctx, vnpay, new FakeMoMoQuery());

            var summary = await service.ReconcileOpenOrdersAsync();

            Assert.Equal(0, summary.Failed);
            Assert.Equal(PaymentOrderStatuses.Pending, Status(ctx, orderId));
        }

        [Fact]
        public async Task NoAnswerFromTheProvider_LeavesTheOrderAlone()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx);
            var service = CreateService(ctx, new FakeVnPayQuery(null), new FakeMoMoQuery());

            var summary = await service.ReconcileOpenOrdersAsync();

            Assert.Equal(0, summary.Failed + summary.ManualReview + summary.Expired + summary.Paid);
            Assert.Equal(PaymentOrderStatuses.Pending, Status(ctx, orderId));
        }

        [Fact]
        public async Task OverAnHourPastExpiry_WithoutASuccessfulReturn_StaysAsItIs_NotManualReview()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx, status: PaymentOrderStatuses.Expired, expiresIn: TimeSpan.FromHours(-2));
            var service = CreateService(ctx, new FakeVnPayQuery(new VnPayQueryResult { State = PaymentQueryState.Unknown, ResponseCode = "91" }), new FakeMoMoQuery());

            var summary = await service.ReconcileOpenOrdersAsync();

            Assert.Equal(0, summary.ManualReview);
            Assert.Equal(PaymentOrderStatuses.Expired, Status(ctx, orderId));
        }

        [Fact]
        public async Task OverAnHourPastExpiry_WithASuccessfulReturnAndNoConfirmation_GoesToManualReview()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(
                ctx, status: PaymentOrderStatuses.Expired, expiresIn: TimeSpan.FromHours(-2),
                returnPayload: "{\"vnp_ResponseCode\":\"00\",\"vnp_TransactionStatus\":\"00\"}");
            var service = CreateService(ctx, new FakeVnPayQuery(new VnPayQueryResult { State = PaymentQueryState.Unknown, ResponseCode = "91" }), new FakeMoMoQuery());

            var summary = await service.ReconcileOpenOrdersAsync();

            Assert.Equal(1, summary.ManualReview);
            Assert.Equal(PaymentOrderStatuses.ManualReview, Status(ctx, orderId));
        }

        [Fact]
        public async Task WithinAnHourOfExpiry_ASuccessfulReturnWithoutConfirmation_WaitsInsteadOfManualReview()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(
                ctx, expiresIn: TimeSpan.FromMinutes(-20),
                returnPayload: "{\"vnp_ResponseCode\":\"00\"}");
            var service = CreateService(ctx, new FakeVnPayQuery(new VnPayQueryResult { State = PaymentQueryState.Unknown }), new FakeMoMoQuery());

            await service.ReconcileOpenOrdersAsync();

            Assert.Equal(PaymentOrderStatuses.Pending, Status(ctx, orderId));
        }

        // ─── Failure answers ──────────────────────────────────────────────────────

        [Fact]
        public async Task VnPayFailedAnswer_CancelsAPendingOrder_ButNeverTouchesAnExpiredOne()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, pendingId) = await SeedAsync(ctx, orderCode: "TCT-REC-P");
            var (_, expiredId) = await SeedAsync(ctx, status: PaymentOrderStatuses.Expired, expiresIn: TimeSpan.FromHours(-2), orderCode: "TCT-REC-E");
            var failed = new VnPayQueryResult { State = PaymentQueryState.Failed, ResponseCode = "00", TransactionStatus = "02" };
            var service = CreateService(ctx, new FakeVnPayQuery(failed), new FakeMoMoQuery());

            var summary = await service.ReconcileOpenOrdersAsync();

            Assert.Equal(1, summary.Failed);
            Assert.Equal(PaymentOrderStatuses.Cancelled, Status(ctx, pendingId));
            Assert.Equal(PaymentOrderStatuses.Expired, Status(ctx, expiredId));
        }

        [Theory]
        [InlineData("1006", PaymentOrderStatuses.Cancelled)]
        [InlineData("1001", PaymentOrderStatuses.Failed)]
        public async Task MoMoFailedAnswer_MapsToCancelledOrFailed(string code, string expected)
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx, PaymentProviders.MoMo);
            var momo = new FakeMoMoQuery(new PaymentQueryResult(PaymentQueryState.Failed, null, null, code, code));
            var service = CreateService(ctx, new FakeVnPayQuery(null), momo);

            await service.ReconcileOpenOrdersAsync();

            Assert.Equal(expected, Status(ctx, orderId));
        }

        // ─── Selection and auto-expire ────────────────────────────────────────────

        [Fact]
        public async Task PendingOrderMoreThanADayPastExpiry_IsExpired_WithoutAskingTheProvider()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx, expiresIn: TimeSpan.FromHours(-26));
            var vnpay = new FakeVnPayQuery(VnPaySuccess());
            var service = CreateService(ctx, vnpay, new FakeMoMoQuery());

            var summary = await service.ReconcileOpenOrdersAsync();

            Assert.Equal(1, summary.Expired);
            Assert.Equal(0, vnpay.Calls);
            Assert.Equal(PaymentOrderStatuses.Expired, Status(ctx, orderId));
        }

        [Theory]
        [InlineData(PaymentOrderStatuses.Paid)]
        [InlineData(PaymentOrderStatuses.Refunded)]
        [InlineData(PaymentOrderStatuses.PartiallyRefunded)]
        [InlineData(PaymentOrderStatuses.ManualReview)]
        [InlineData(PaymentOrderStatuses.Failed)]
        [InlineData(PaymentOrderStatuses.Cancelled)]
        public async Task OrdersThatAreNotOpen_AreNotSelected(string status)
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx, status: status);
            var vnpay = new FakeVnPayQuery(VnPaySuccess());
            var service = CreateService(ctx, vnpay, new FakeMoMoQuery());

            var summary = await service.ReconcileOpenOrdersAsync();

            Assert.Equal(0, summary.Checked);
            Assert.Equal(0, vnpay.Calls);
            Assert.Equal(status, Status(ctx, orderId));
        }

        [Fact]
        public async Task ExpiredMoreThanADayAgo_IsNotSelected()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            await SeedAsync(ctx, status: PaymentOrderStatuses.Expired, expiresIn: TimeSpan.FromHours(-30));
            var service = CreateService(ctx, new FakeVnPayQuery(VnPaySuccess()), new FakeMoMoQuery());

            var summary = await service.ReconcileOpenOrdersAsync();

            Assert.Equal(0, summary.Checked);
        }

        [Fact]
        public async Task OrdersYoungerThanTheMinimumAge_AreNotSelected()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            await SeedAsync(ctx, createdMinutesAgo: 1, expiresIn: TimeSpan.FromMinutes(14));
            var service = CreateService(ctx, new FakeVnPayQuery(VnPaySuccess()), new FakeMoMoQuery());

            var summary = await service.ReconcileOpenOrdersAsync(minPendingMinutes: 5);

            Assert.Equal(0, summary.Checked);
        }

        [Fact]
        public async Task OneBrokenOrder_DoesNotStopTheRest()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, firstId) = await SeedAsync(ctx, orderCode: "TCT-REC-1", createdMinutesAgo: 40);
            var (_, secondId) = await SeedAsync(ctx, orderCode: "TCT-REC-2", createdMinutesAgo: 30);
            var vnpay = new FakeVnPayQuery(VnPaySuccess()) { ThrowOnFirstCall = true };
            var service = CreateService(ctx, vnpay, new FakeMoMoQuery());

            var summary = await service.ReconcileOpenOrdersAsync();

            Assert.Equal(2, summary.Checked);
            Assert.Equal(1, summary.Paid);
            Assert.Equal(PaymentOrderStatuses.Pending, Status(ctx, firstId));
            Assert.Equal(PaymentOrderStatuses.Paid, Status(ctx, secondId));
        }

        // ─── Fakes ────────────────────────────────────────────────────────────────

        private sealed class FakeVnPayQuery : IVnPayQueryClient
        {
            private readonly VnPayQueryResult? _result;
            public FakeVnPayQuery(VnPayQueryResult? result) => _result = result;
            public int Calls { get; private set; }
            public bool ThrowOnFirstCall { get; init; }

            public Task<VnPayQueryResult?> QueryOrderAsync(
                string orderCode, DateTime transactionCreatedAtUtc, string? providerTransactionNo = null, CancellationToken ct = default)
            {
                Calls++;
                if (ThrowOnFirstCall && Calls == 1) throw new InvalidOperationException("boom");
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
