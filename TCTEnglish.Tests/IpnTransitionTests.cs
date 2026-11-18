using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>
    /// Both IPN handlers drive the order through the state machine (plan 4.1/4.3):
    /// a failure callback cannot overwrite manual_review/refunded/expired, a success callback cannot
    /// revive a refunded order, a late success goes to manual review for MoMo too, and a duplicate
    /// event insert that the database reports as a MariaDB or SQLite unique error is not a crash.
    /// </summary>
    public class IpnTransitionTests
    {
        private const decimal Amount = 399000m;

        private static DbflashcardContext CreateContext(string dbName, params IInterceptor[] interceptors)
        {
            var builder = new DbContextOptionsBuilder<DbflashcardContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            if (interceptors.Length > 0)
            {
                builder.AddInterceptors(interceptors);
            }

            return new DbflashcardContext(builder.Options);
        }

        private static IpnService CreateService(DbflashcardContext ctx)
        {
            var subscriptions = new SubscriptionService(ctx);
            return new IpnService(
                ctx,
                new IPaymentGateway[] { new SignedGateway(PaymentProviders.VNPay), new SignedGateway(PaymentProviders.MoMo) },
                new PaymentConfirmationService(ctx, subscriptions, NullLogger<PaymentConfirmationService>.Instance),
                NullLogger<IpnService>.Instance);
        }

        private static async Task<(int userId, long orderId)> SeedAsync(
            string dbName, string status, string provider, TimeSpan? expiresIn = null)
        {
            await using var ctx = CreateContext(dbName);
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
                OrderCode = "TCT-IPN-001", UserId = user.UserId, PlanId = plan.Id, Provider = provider,
                AmountVnd = Amount, Currency = "VND", Status = status,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
                ExpiresAtUtc = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromMinutes(10))
            };
            ctx.PaymentOrders.Add(order);
            await ctx.SaveChangesAsync();
            return (user.UserId, order.Id);
        }

        private static Dictionary<string, string> VnPayParams(bool success, string txn = "VN-1", string? responseCode = null)
            => new(StringComparer.OrdinalIgnoreCase)
            {
                ["vnp_TxnRef"] = "TCT-IPN-001",
                ["vnp_Amount"] = ((long)(Amount * 100)).ToString(),
                ["vnp_CurrCode"] = "VND",
                ["vnp_ResponseCode"] = responseCode ?? (success ? "00" : "10"),
                ["vnp_TransactionStatus"] = success ? "00" : "02",
                ["vnp_TransactionNo"] = txn,
                ["vnp_SecureHash"] = "VALID"
            };

        private static Dictionary<string, string> MoMoParams(bool success, string trans = "MM-1", string? resultCode = null)
            => new(StringComparer.OrdinalIgnoreCase)
            {
                ["orderId"] = "TCT-IPN-001",
                ["requestId"] = "REQ-1",
                ["transId"] = trans,
                ["amount"] = ((long)Amount).ToString(),
                ["resultCode"] = resultCode ?? (success ? "0" : "1005"),
                ["payType"] = "qr",
                ["signature"] = "VALID"
            };

        private static async Task RunAsync(DbflashcardContext ctx, string provider, bool success)
        {
            var svc = CreateService(ctx);
            if (provider == PaymentProviders.VNPay)
            {
                var result = await svc.ProcessVnPayIpnAsync(VnPayParams(success));
                Assert.NotEqual("99", result.RspCode);
            }
            else
            {
                await svc.ProcessMoMoIpnAsync(MoMoParams(success));
            }
        }

        public static IEnumerable<object[]> Providers => new[]
        {
            new object[] { PaymentProviders.VNPay },
            new object[] { PaymentProviders.MoMo }
        };

        // ─── Failure callbacks ────────────────────────────────────────────────────

        [Theory]
        [MemberData(nameof(Providers))]
        public async Task FailureCallback_OnPending_MarksFailed(string provider)
        {
            var db = Guid.NewGuid().ToString();
            var (_, orderId) = await SeedAsync(db, PaymentOrderStatuses.Pending, provider);

            await using (var ctx = CreateContext(db)) await RunAsync(ctx, provider, success: false);

            await using var verify = CreateContext(db);
            Assert.Equal(PaymentOrderStatuses.Failed, (await verify.PaymentOrders.FirstAsync(o => o.Id == orderId)).Status);
        }

        [Theory]
        [InlineData(PaymentProviders.VNPay, PaymentOrderStatuses.ManualReview)]
        [InlineData(PaymentProviders.VNPay, PaymentOrderStatuses.Refunded)]
        [InlineData(PaymentProviders.VNPay, PaymentOrderStatuses.PartiallyRefunded)]
        [InlineData(PaymentProviders.VNPay, PaymentOrderStatuses.Expired)]
        [InlineData(PaymentProviders.VNPay, PaymentOrderStatuses.Failed)]
        [InlineData(PaymentProviders.VNPay, PaymentOrderStatuses.Cancelled)]
        [InlineData(PaymentProviders.MoMo, PaymentOrderStatuses.ManualReview)]
        [InlineData(PaymentProviders.MoMo, PaymentOrderStatuses.Refunded)]
        [InlineData(PaymentProviders.MoMo, PaymentOrderStatuses.PartiallyRefunded)]
        [InlineData(PaymentProviders.MoMo, PaymentOrderStatuses.Expired)]
        [InlineData(PaymentProviders.MoMo, PaymentOrderStatuses.Failed)]
        [InlineData(PaymentProviders.MoMo, PaymentOrderStatuses.Cancelled)]
        public async Task FailureCallback_CannotChangeAnOrderOutsidePending(string provider, string status)
        {
            var db = Guid.NewGuid().ToString();
            var (_, orderId) = await SeedAsync(db, status, provider, TimeSpan.FromMinutes(-30));

            await using (var ctx = CreateContext(db)) await RunAsync(ctx, provider, success: false);

            await using var verify = CreateContext(db);
            Assert.Equal(status, (await verify.PaymentOrders.FirstAsync(o => o.Id == orderId)).Status);
            var evt = await verify.PaymentEvents.FirstAsync(e => e.EventType == PaymentEventTypes.Ipn);
            Assert.Equal(PaymentEventProcessingStatuses.Ignored, evt.ProcessingStatus);
        }

        [Fact]
        public async Task VnPayFailureCallback_AcknowledgesWithCode00_WhenIgnored()
        {
            var db = Guid.NewGuid().ToString();
            await SeedAsync(db, PaymentOrderStatuses.ManualReview, PaymentProviders.VNPay);
            await using var ctx = CreateContext(db);

            var result = await CreateService(ctx).ProcessVnPayIpnAsync(VnPayParams(success: false));

            Assert.Equal("00", result.RspCode);
        }

        // ─── Success callbacks ────────────────────────────────────────────────────

        [Theory]
        [InlineData(PaymentProviders.VNPay, PaymentOrderStatuses.Refunded)]
        [InlineData(PaymentProviders.VNPay, PaymentOrderStatuses.PartiallyRefunded)]
        [InlineData(PaymentProviders.VNPay, PaymentOrderStatuses.ManualReview)]
        [InlineData(PaymentProviders.MoMo, PaymentOrderStatuses.Refunded)]
        [InlineData(PaymentProviders.MoMo, PaymentOrderStatuses.PartiallyRefunded)]
        [InlineData(PaymentProviders.MoMo, PaymentOrderStatuses.ManualReview)]
        public async Task SuccessCallback_CannotReviveRefundedOrManualReviewOrders(string provider, string status)
        {
            var db = Guid.NewGuid().ToString();
            var (userId, orderId) = await SeedAsync(db, status, provider);

            await using (var ctx = CreateContext(db)) await RunAsync(ctx, provider, success: true);

            await using var verify = CreateContext(db);
            Assert.Equal(status, (await verify.PaymentOrders.FirstAsync(o => o.Id == orderId)).Status);
            Assert.Empty(verify.UserSubscriptions.Where(s => s.UserId == userId));
        }

        [Theory]
        [InlineData(PaymentProviders.VNPay)]
        [InlineData(PaymentProviders.MoMo)]
        public async Task SuccessCallback_OnAnOrderThatExpiredHoursAgo_StillActivates(string provider)
        {
            var db = Guid.NewGuid().ToString();
            var (userId, orderId) = await SeedAsync(db, PaymentOrderStatuses.Expired, provider, TimeSpan.FromHours(-3));

            await using (var ctx = CreateContext(db)) await RunAsync(ctx, provider, success: true);

            await using var verify = CreateContext(db);
            Assert.Equal(PaymentOrderStatuses.Paid, (await verify.PaymentOrders.FirstAsync(o => o.Id == orderId)).Status);
            Assert.Single(verify.UserSubscriptions.Where(s => s.UserId == userId));
        }

        [Theory]
        [InlineData(PaymentProviders.VNPay)]
        [InlineData(PaymentProviders.MoMo)]
        public async Task SuccessCallback_MoreThanADayAfterExpiry_GoesToManualReview_ForBothProviders(string provider)
        {
            var db = Guid.NewGuid().ToString();
            var (userId, orderId) = await SeedAsync(db, PaymentOrderStatuses.Expired, provider, TimeSpan.FromHours(-26));

            await using (var ctx = CreateContext(db)) await RunAsync(ctx, provider, success: true);

            await using var verify = CreateContext(db);
            Assert.Equal(PaymentOrderStatuses.ManualReview, (await verify.PaymentOrders.FirstAsync(o => o.Id == orderId)).Status);
            Assert.Empty(verify.UserSubscriptions.Where(s => s.UserId == userId));
        }

        [Theory]
        [InlineData(PaymentProviders.VNPay, PaymentOrderStatuses.Failed)]
        [InlineData(PaymentProviders.VNPay, PaymentOrderStatuses.Cancelled)]
        [InlineData(PaymentProviders.MoMo, PaymentOrderStatuses.Failed)]
        [InlineData(PaymentProviders.MoMo, PaymentOrderStatuses.Cancelled)]
        public async Task SuccessCallback_AfterAnEarlierFailure_WinsAndActivates(string provider, string status)
        {
            var db = Guid.NewGuid().ToString();
            var (userId, orderId) = await SeedAsync(db, status, provider);

            await using (var ctx = CreateContext(db)) await RunAsync(ctx, provider, success: true);

            await using var verify = CreateContext(db);
            Assert.Equal(PaymentOrderStatuses.Paid, (await verify.PaymentOrders.FirstAsync(o => o.Id == orderId)).Status);
            Assert.Single(verify.UserSubscriptions.Where(s => s.UserId == userId));
        }

        [Fact]
        public async Task VnPaySuccessCallback_OnRefundedOrder_AnswersAlreadyConfirmed()
        {
            var db = Guid.NewGuid().ToString();
            await SeedAsync(db, PaymentOrderStatuses.Refunded, PaymentProviders.VNPay);
            await using var ctx = CreateContext(db);

            var result = await CreateService(ctx).ProcessVnPayIpnAsync(VnPayParams(success: true));

            Assert.Equal("02", result.RspCode);
        }

        [Fact]
        public async Task VnPaySuccessCallback_AfterwardsAnIdenticalRepeat_IsAcknowledgedAsDuplicate()
        {
            var db = Guid.NewGuid().ToString();
            var (userId, _) = await SeedAsync(db, PaymentOrderStatuses.Pending, PaymentProviders.VNPay);
            await using var ctx = CreateContext(db);
            var svc = CreateService(ctx);

            var first = await svc.ProcessVnPayIpnAsync(VnPayParams(success: true));
            var second = await svc.ProcessVnPayIpnAsync(VnPayParams(success: true));

            Assert.Equal("00", first.RspCode);
            Assert.Equal("02", second.RspCode);
            Assert.Single(ctx.UserSubscriptions.Where(s => s.UserId == userId));
        }

        // ─── Unique errors reported by the real providers ─────────────────────────

        /// <summary>Shape of MySqlConnector's exception: the type name and a <c>Number</c> property.</summary>
        private sealed class MySqlException : Exception
        {
            public MySqlException() : base("Duplicate entry 'ipn:TCT-IPN-001' for key 'IX_PaymentEvents_Provider_EventType_EventKey'") { }
            public int Number => 1062;
        }

        private sealed class FailFirstIpnEventInsert : SaveChangesInterceptor
        {
            private readonly Func<Exception> _inner;
            private bool _thrown;

            public FailFirstIpnEventInsert(Func<Exception> inner) => _inner = inner;

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
            {
                var adding = eventData.Context!.ChangeTracker.Entries<PaymentEvent>()
                    .Any(e => e.State == EntityState.Added && e.Entity.EventType == PaymentEventTypes.Ipn);
                if (adding && !_thrown)
                {
                    _thrown = true;
                    throw new DbUpdateException("duplicate", _inner());
                }

                return base.SavingChangesAsync(eventData, result, ct);
            }
        }

        public static IEnumerable<object[]> UniqueErrors => new[]
        {
            new object[] { "mariadb", (Func<Exception>)(() => new MySqlException()) },
            new object[] { "sqlite", (Func<Exception>)(() => new SqliteException("SQLite Error 19: 'UNIQUE constraint failed: PaymentEvents.EventKey'.", 19)) }
        };

        [Theory]
        [MemberData(nameof(UniqueErrors))]
        public async Task VnPayFailureCallback_WhenTheEventInsertHitsAUniqueError_IsStillAcknowledged(string kind, Func<Exception> error)
        {
            _ = kind;
            var db = Guid.NewGuid().ToString();
            var (_, orderId) = await SeedAsync(db, PaymentOrderStatuses.Pending, PaymentProviders.VNPay);
            await using var ctx = CreateContext(db, new FailFirstIpnEventInsert(error));

            var result = await CreateService(ctx).ProcessVnPayIpnAsync(VnPayParams(success: false));

            Assert.Equal("00", result.RspCode);
            await using var verify = CreateContext(db);
            Assert.Equal(PaymentOrderStatuses.Failed, (await verify.PaymentOrders.FirstAsync(o => o.Id == orderId)).Status);
        }

        [Theory]
        [MemberData(nameof(UniqueErrors))]
        public async Task MoMoFailureCallback_WhenTheEventInsertHitsAUniqueError_DoesNotThrow(string kind, Func<Exception> error)
        {
            _ = kind;
            var db = Guid.NewGuid().ToString();
            var (_, orderId) = await SeedAsync(db, PaymentOrderStatuses.Pending, PaymentProviders.MoMo);
            await using var ctx = CreateContext(db, new FailFirstIpnEventInsert(error));

            await CreateService(ctx).ProcessMoMoIpnAsync(MoMoParams(success: false));

            await using var verify = CreateContext(db);
            Assert.Equal(PaymentOrderStatuses.Failed, (await verify.PaymentOrders.FirstAsync(o => o.Id == orderId)).Status);
        }

        // ─── Fake gateway ─────────────────────────────────────────────────────────

        private sealed class SignedGateway : IPaymentGateway
        {
            public SignedGateway(string provider) => ProviderName = provider;
            public string ProviderName { get; }
            public bool IsEnabled => true;

            public Task<ProviderCheckoutResult> CreateCheckoutAsync(
                string orderCode, decimal amountVnd, string orderDescription, string clientIp, CancellationToken ct = default)
                => Task.FromResult(ProviderCheckoutResult.Ok("https://example.com/pay"));

            public Task<ProviderCallbackResult> ProcessReturnAsync(IDictionary<string, string> queryParams, CancellationToken ct = default)
                => Task.FromResult(new ProviderCallbackResult { IsVerified = true });

            public Task<ProviderIpnResult> ProcessIpnAsync(IDictionary<string, string> parameters, CancellationToken ct = default)
                => Task.FromResult(new ProviderIpnResult { SignatureValid = true, IsPaid = true });
        }
    }
}
