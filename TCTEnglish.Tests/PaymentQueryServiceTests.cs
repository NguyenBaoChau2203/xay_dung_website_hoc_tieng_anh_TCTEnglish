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
    /// The query service asks the provider's own server (never the browser), is throttled without a schema
    /// change, and confirms only a verified success with the right amount (plan 4.4/4.5, G16).
    /// </summary>
    public class PaymentQueryServiceTests
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

        private static PaymentQueryService CreateService(
            DbflashcardContext ctx, FakeVnPayQuery vnpay, FakeMoMoQuery momo)
            => new(
                ctx,
                vnpay,
                momo,
                new PaymentConfirmationService(ctx, new SubscriptionService(ctx), NullLogger<PaymentConfirmationService>.Instance),
                NullLogger<PaymentQueryService>.Instance);

        private static async Task<(int userId, long orderId)> SeedAsync(
            DbflashcardContext ctx, string status = PaymentOrderStatuses.Pending,
            string provider = PaymentProviders.VNPay, TimeSpan? expiresIn = null)
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
                OrderCode = "TCT-Q-001", UserId = user.UserId, PlanId = plan.Id, Provider = provider,
                AmountVnd = Amount, Currency = "VND", Status = status,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-20),
                ExpiresAtUtc = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromMinutes(-5))
            };
            ctx.PaymentOrders.Add(order);
            await ctx.SaveChangesAsync();
            return (user.UserId, order.Id);
        }

        private static VnPayQueryResult VnPaySuccess(long? amount = 399000)
            => new() { State = PaymentQueryState.Success, IsSuccess = true, ResponseCode = "00", TransactionStatus = "00", TransactionNo = "VN-9", AmountVnd = amount };

        // ─── Confirmation ─────────────────────────────────────────────────────────

        [Theory]
        [InlineData(PaymentQueryTrigger.Return, "retq")]
        [InlineData(PaymentQueryTrigger.Refresh, "refq")]
        [InlineData(PaymentQueryTrigger.Reconciliation, "recon")]
        public async Task VerifiedSuccess_WithMatchingAmount_ConfirmsTheOrder_ForEveryTrigger(PaymentQueryTrigger trigger, string prefix)
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (userId, orderId) = await SeedAsync(ctx);
            var service = CreateService(ctx, new FakeVnPayQuery(VnPaySuccess()), new FakeMoMoQuery());

            var attempt = await service.QueryAndConfirmAsync(orderId, trigger);

            Assert.Equal(PaymentQueryStatus.Confirmed, attempt.Status);
            Assert.Equal(PaymentConfirmationOutcome.Activated, attempt.Confirmation!.Outcome);
            var order = await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == orderId);
            Assert.Equal(PaymentOrderStatuses.Paid, order.Status);
            Assert.Equal("VN-9", order.ProviderTransactionId);
            Assert.Single(ctx.UserSubscriptions.Where(s => s.UserId == userId));
            Assert.Contains(ctx.PaymentEvents, e => e.EventType == PaymentEventTypes.Reconcile && e.EventKey == $"{prefix}:TCT-Q-001");
            Assert.Single(ctx.PaymentEvents, e => e.EventType == PaymentEventTypes.Query);
        }

        [Fact]
        public async Task MoMoVerifiedSuccess_ConfirmsThroughTheSamePath()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx, provider: PaymentProviders.MoMo);
            var momo = new FakeMoMoQuery(new PaymentQueryResult(PaymentQueryState.Success, 399000, "555", "0", "0", "qr"));
            var service = CreateService(ctx, new FakeVnPayQuery(null), momo);

            var attempt = await service.QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Refresh);

            Assert.Equal(PaymentQueryStatus.Confirmed, attempt.Status);
            Assert.Equal(1, momo.Calls);
            Assert.Equal(PaymentOrderStatuses.Paid, (await ctx.PaymentOrders.AsNoTracking().FirstAsync()).Status);
        }

        [Theory]
        [InlineData(398999L)]
        [InlineData(399001L)]
        [InlineData(null)]
        public async Task VerifiedSuccess_WithAWrongOrMissingAmount_DoesNotConfirm(long? amount)
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (userId, orderId) = await SeedAsync(ctx);
            var service = CreateService(ctx, new FakeVnPayQuery(VnPaySuccess(amount)), new FakeMoMoQuery());

            var attempt = await service.QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Return);

            Assert.Equal(PaymentQueryStatus.AmountMismatch, attempt.Status);
            Assert.Equal(PaymentOrderStatuses.Pending, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == orderId)).Status);
            Assert.Empty(ctx.UserSubscriptions.Where(s => s.UserId == userId));
        }

        [Fact]
        public async Task UnknownAnswer_NeverChangesTheOrder_NotEvenAfterFiveMinutes()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (userId, orderId) = await SeedAsync(ctx);
            var unknown = new VnPayQueryResult { State = PaymentQueryState.Unknown, ResponseCode = "91" };
            var service = CreateService(ctx, new FakeVnPayQuery(unknown), new FakeMoMoQuery());

            var attempt = await service.QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Reconciliation);

            Assert.Equal(PaymentQueryStatus.Unknown, attempt.Status);
            Assert.Equal(PaymentOrderStatuses.Pending, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == orderId)).Status);
            Assert.Empty(ctx.UserSubscriptions.Where(s => s.UserId == userId));
        }

        [Fact]
        public async Task FailedAnswer_IsReportedButNotAppliedByTheService()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx);
            var failed = new VnPayQueryResult { State = PaymentQueryState.Failed, ResponseCode = "00", TransactionStatus = "02" };
            var service = CreateService(ctx, new FakeVnPayQuery(failed), new FakeMoMoQuery());

            var attempt = await service.QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Return);

            Assert.Equal(PaymentQueryStatus.Failed, attempt.Status);
            Assert.Equal(PaymentOrderStatuses.Pending, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == orderId)).Status);
        }

        [Fact]
        public async Task NoAnswer_IsProviderUnavailable_AndStillCountsAsAnAttempt()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx);
            var service = CreateService(ctx, new FakeVnPayQuery(null), new FakeMoMoQuery());

            var attempt = await service.QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Refresh);

            Assert.Equal(PaymentQueryStatus.ProviderUnavailable, attempt.Status);
            Assert.Equal(PaymentOrderStatuses.Pending, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == orderId)).Status);
            Assert.Single(ctx.PaymentEvents, e => e.EventType == PaymentEventTypes.Query);
        }

        [Fact]
        public async Task AClientThatThrows_IsProviderUnavailable_NotACrash()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx);
            var service = CreateService(ctx, new FakeVnPayQuery(null) { Throw = true }, new FakeMoMoQuery());

            var attempt = await service.QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Return);

            Assert.Equal(PaymentQueryStatus.ProviderUnavailable, attempt.Status);
        }

        // ─── Eligibility ──────────────────────────────────────────────────────────

        [Theory]
        [InlineData(PaymentOrderStatuses.Paid)]
        [InlineData(PaymentOrderStatuses.Refunded)]
        [InlineData(PaymentOrderStatuses.PartiallyRefunded)]
        [InlineData(PaymentOrderStatuses.ManualReview)]
        [InlineData(PaymentOrderStatuses.Failed)]
        [InlineData(PaymentOrderStatuses.Cancelled)]
        public async Task OrdersOutsidePendingOrExpired_AreNotQueried(string status)
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx, status);
            var vnpay = new FakeVnPayQuery(VnPaySuccess());
            var service = CreateService(ctx, vnpay, new FakeMoMoQuery());

            var attempt = await service.QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Return);

            Assert.Equal(PaymentQueryStatus.NotEligible, attempt.Status);
            Assert.Equal(0, vnpay.Calls);
            Assert.Equal(status, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == orderId)).Status);
        }

        [Fact]
        public async Task AnOrderThatExpiredMoreThanADayAgo_IsNotQueried()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx, PaymentOrderStatuses.Expired, expiresIn: TimeSpan.FromHours(-25));
            var vnpay = new FakeVnPayQuery(VnPaySuccess());

            var attempt = await CreateService(ctx, vnpay, new FakeMoMoQuery()).QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Refresh);

            Assert.Equal(PaymentQueryStatus.NotEligible, attempt.Status);
            Assert.Equal(0, vnpay.Calls);
        }

        [Fact]
        public async Task AnOrderThatExpiredHoursAgo_IsQueriedAndCanStillBeConfirmed()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx, PaymentOrderStatuses.Expired, expiresIn: TimeSpan.FromHours(-3));

            var attempt = await CreateService(ctx, new FakeVnPayQuery(VnPaySuccess()), new FakeMoMoQuery())
                .QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Return);

            Assert.Equal(PaymentQueryStatus.Confirmed, attempt.Status);
            Assert.Equal(PaymentOrderStatuses.Paid, (await ctx.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == orderId)).Status);
        }

        [Fact]
        public async Task UnknownOrderId_IsNotEligible()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());

            var attempt = await CreateService(ctx, new FakeVnPayQuery(VnPaySuccess()), new FakeMoMoQuery())
                .QueryAndConfirmAsync(424242, PaymentQueryTrigger.Return);

            Assert.Equal(PaymentQueryStatus.NotEligible, attempt.Status);
        }

        // ─── Throttle ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task SecondQueryWithinTwentySeconds_IsThrottled_WithoutCallingTheProvider()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx);
            var vnpay = new FakeVnPayQuery(new VnPayQueryResult { State = PaymentQueryState.Unknown, ResponseCode = "91" });
            var service = CreateService(ctx, vnpay, new FakeMoMoQuery());

            var first = await service.QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Return);
            var second = await service.QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Refresh);

            Assert.Equal(PaymentQueryStatus.Unknown, first.Status);
            Assert.Equal(PaymentQueryStatus.Throttled, second.Status);
            Assert.Equal(1, vnpay.Calls);
        }

        [Fact]
        public async Task AQueryOlderThanTwentySeconds_AllowsTheNext()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx);
            SeedQueryEvents(ctx, orderId, 1, ageSeconds: 25);
            var vnpay = new FakeVnPayQuery(new VnPayQueryResult { State = PaymentQueryState.Unknown });

            var attempt = await CreateService(ctx, vnpay, new FakeMoMoQuery()).QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Refresh);

            Assert.Equal(PaymentQueryStatus.Unknown, attempt.Status);
            Assert.Equal(1, vnpay.Calls);
        }

        [Fact]
        public async Task TwelveQueriesInAll_StopFurtherQueries_EvenWhenOld()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx);
            SeedQueryEvents(ctx, orderId, 12, ageSeconds: 600);
            var vnpay = new FakeVnPayQuery(VnPaySuccess());

            var attempt = await CreateService(ctx, vnpay, new FakeMoMoQuery()).QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Refresh);

            Assert.Equal(PaymentQueryStatus.Throttled, attempt.Status);
            Assert.Equal(0, vnpay.Calls);
        }

        [Fact]
        public async Task ElevenQueriesInAll_StillAllowOneMore()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx);
            SeedQueryEvents(ctx, orderId, 11, ageSeconds: 600);
            var vnpay = new FakeVnPayQuery(new VnPayQueryResult { State = PaymentQueryState.Unknown });

            await CreateService(ctx, vnpay, new FakeMoMoQuery()).QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Refresh);

            Assert.Equal(1, vnpay.Calls);
        }

        [Fact]
        public async Task ThrottleIsPerOrder()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx);
            SeedQueryEvents(ctx, orderId + 1000, 12, ageSeconds: 1);
            var vnpay = new FakeVnPayQuery(new VnPayQueryResult { State = PaymentQueryState.Unknown });

            await CreateService(ctx, vnpay, new FakeMoMoQuery()).QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Refresh);

            Assert.Equal(1, vnpay.Calls);
        }

        [Fact]
        public async Task TheQueryEvent_CarriesNoSignatureOrKey()
        {
            await using var ctx = CreateContext(Guid.NewGuid().ToString());
            var (_, orderId) = await SeedAsync(ctx);

            await CreateService(ctx, new FakeVnPayQuery(VnPaySuccess()), new FakeMoMoQuery())
                .QueryAndConfirmAsync(orderId, PaymentQueryTrigger.Return);

            var evt = Assert.Single(ctx.PaymentEvents, e => e.EventType == PaymentEventTypes.Query);
            Assert.StartsWith("query:TCT-Q-001:", evt.EventKey);
            Assert.DoesNotContain("hash", evt.PayloadJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("signature", evt.PayloadJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", evt.PayloadJson, StringComparison.OrdinalIgnoreCase);
        }

        private static void SeedQueryEvents(DbflashcardContext ctx, long orderId, int count, int ageSeconds)
        {
            for (var i = 0; i < count; i++)
            {
                var at = DateTime.UtcNow.AddSeconds(-ageSeconds - i);
                ctx.PaymentEvents.Add(new PaymentEvent
                {
                    Provider = PaymentProviders.VNPay,
                    EventType = PaymentEventTypes.Query,
                    EventKey = $"query:seed:{orderId}:{i}",
                    PaymentOrderId = orderId,
                    SignatureValid = true,
                    PayloadJson = "{}",
                    ProcessingStatus = PaymentEventProcessingStatuses.Processed,
                    ReceivedAtUtc = at,
                    ProcessedAtUtc = at
                });
            }

            ctx.SaveChanges();
        }

        // ─── Fakes ────────────────────────────────────────────────────────────────

        private sealed class FakeVnPayQuery : IVnPayQueryClient
        {
            private readonly VnPayQueryResult? _result;
            public FakeVnPayQuery(VnPayQueryResult? result) => _result = result;
            public int Calls { get; private set; }
            public bool Throw { get; init; }

            public Task<VnPayQueryResult?> QueryOrderAsync(
                string orderCode, DateTime transactionCreatedAtUtc, string? providerTransactionNo = null, CancellationToken ct = default)
            {
                Calls++;
                if (Throw) throw new InvalidOperationException("boom");
                return Task.FromResult(_result);
            }
        }

        private sealed class FakeMoMoQuery : IMoMoQueryClient
        {
            private readonly PaymentQueryResult _result;
            public FakeMoMoQuery(PaymentQueryResult? result = null) => _result = result ?? PaymentQueryResult.Unknown();
            public int Calls { get; private set; }

            public Task<PaymentQueryResult> QueryOrderAsync(string orderCode, CancellationToken ct = default)
            {
                Calls++;
                return Task.FromResult(_result);
            }
        }
    }
}
