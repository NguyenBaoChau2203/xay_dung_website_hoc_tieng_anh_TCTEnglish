using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.MoMo;
using TCTEnglish.Services.Billing.PayOS;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>
    /// The payOS rows of the billing matrix (plan 12.3): webhook (T1, T3, T4, T5, T6, T19), return (T2, T7, T13),
    /// query/reconciliation (T9), cleanup cancel (plan D8), all through the real services with a fake payOS API.
    /// </summary>
    public class PayOSBillingFlowTests
    {
        private const string ChecksumKey = "flow-test-checksum-key";
        private const string PayOsCode = "261120130509123";
        private const string OrderCode = "TCT-20261120130509000-AABBCCDD";

        // ── webhook: T1 T3 T4 T5 T6 T19 ──────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Webhook_ASignedPayment_ActivatesPremiumOnce_AndStoresNoSignatureOrFullAccountNumber()
        {
            var w = await World.CreateAsync();
            var body = w.SignedWebhook(PayOsCode, 49000, reference: "FT-100");

            await w.Ipn().ProcessPayOSWebhookAsync(body);

            var order = await w.OrderAsync();
            Assert.Equal(PaymentOrderStatuses.Paid, order.Status);
            Assert.Equal("FT-100", order.ProviderTransactionId);
            Assert.Equal(PayOsCode, order.ProviderRequestId);
            Assert.Equal("bank_transfer", order.PayType);
            Assert.StartsWith("payos_code=00", order.RawStatus, StringComparison.Ordinal);
            Assert.Equal(1, await w.CountSubscriptionsAsync());

            var stored = string.Join("\n", await w.PayloadsAsync()) + (order.IpnPayloadJson ?? string.Empty);
            Assert.DoesNotContain(body["signature"], stored, StringComparison.Ordinal);
            Assert.DoesNotContain("0123456789", stored, StringComparison.Ordinal);
            Assert.Contains("****6789", stored, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Webhook_TheSameWebhookThreeTimes_AndInParallel_ActivatesOnce()
        {
            var w = await World.CreateAsync();
            var body = w.SignedWebhook(PayOsCode, 49000);

            await w.Ipn().ProcessPayOSWebhookAsync(body);
            await w.Ipn().ProcessPayOSWebhookAsync(new Dictionary<string, string>(body));
            await w.Ipn().ProcessPayOSWebhookAsync(new Dictionary<string, string>(body));
            await Task.WhenAll(
                w.Ipn().ProcessPayOSWebhookAsync(new Dictionary<string, string>(body)),
                w.Ipn().ProcessPayOSWebhookAsync(new Dictionary<string, string>(body)));

            Assert.Equal(PaymentOrderStatuses.Paid, (await w.OrderAsync()).Status);
            Assert.Equal(1, await w.CountSubscriptionsAsync());
        }

        [Fact]
        public async Task Webhook_AnInvalidSignature_ChangesNothing_AndIsRecordedAsFailed()
        {
            var w = await World.CreateAsync();
            var body = w.SignedWebhook(PayOsCode, 49000);
            body["data.amount"] = "49001"; // tampered after signing

            await w.Ipn().ProcessPayOSWebhookAsync(body);

            Assert.Equal(PaymentOrderStatuses.Pending, (await w.OrderAsync()).Status);
            Assert.Equal(0, await w.CountSubscriptionsAsync());
            await using var ctx = w.Context();
            var evt = await ctx.PaymentEvents.AsNoTracking().SingleAsync(e => e.Provider == PaymentProviders.PayOS);
            Assert.False(evt.SignatureValid);
            Assert.Equal(PaymentEventProcessingStatuses.Failed, evt.ProcessingStatus);
        }

        [Theory]
        [InlineData(48999)]
        [InlineData(49001)]
        [InlineData(1)]
        [InlineData(490000)]
        public async Task Webhook_AnAmountThatDiffersFromTheOrder_DoesNotActivate(long amount)
        {
            var w = await World.CreateAsync();

            await w.Ipn().ProcessPayOSWebhookAsync(w.SignedWebhook(PayOsCode, amount));

            Assert.Equal(PaymentOrderStatuses.Pending, (await w.OrderAsync()).Status);
            Assert.Equal(0, await w.CountSubscriptionsAsync());
        }

        [Fact]
        public async Task Webhook_AnUnknownOrderCode_IsRecordedAndIgnored_WithoutAnException()
        {
            var w = await World.CreateAsync();

            // payOS's own "confirm webhook" test call uses a sample code like this one.
            await w.Ipn().ProcessPayOSWebhookAsync(w.SignedWebhook("123", 3000));

            Assert.Equal(PaymentOrderStatuses.Pending, (await w.OrderAsync()).Status);
            await using var ctx = w.Context();
            var evt = await ctx.PaymentEvents.AsNoTracking().SingleAsync(e => e.Provider == PaymentProviders.PayOS);
            Assert.Contains("Order not found", evt.ProcessingMessage, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Webhook_ForAnOrderOfAnotherProvider_IsNotConfirmed()
        {
            var w = await World.CreateAsync(provider: PaymentProviders.MoMo);

            await w.Ipn().ProcessPayOSWebhookAsync(w.SignedWebhook(PayOsCode, 49000));

            Assert.Equal(PaymentOrderStatuses.Pending, (await w.OrderAsync()).Status);
        }

        [Fact]
        public async Task Webhook_AValidlySignedNonSuccess_LeavesThePendingOrderAlone()
        {
            var w = await World.CreateAsync();

            await w.Ipn().ProcessPayOSWebhookAsync(w.SignedWebhook(PayOsCode, 49000, code: "01"));

            Assert.Equal(PaymentOrderStatuses.Pending, (await w.OrderAsync()).Status);
            Assert.Equal(0, await w.CountSubscriptionsAsync());
        }

        [Fact]
        public async Task Webhook_AfterTheOrderWasPaid_IsIgnored_AndDoesNotExtendPremium()
        {
            var w = await World.CreateAsync();
            await w.Ipn().ProcessPayOSWebhookAsync(w.SignedWebhook(PayOsCode, 49000, reference: "FT-1"));
            await w.Ipn().ProcessPayOSWebhookAsync(w.SignedWebhook(PayOsCode, 49000, reference: "FT-2"));

            Assert.Equal(1, await w.CountSubscriptionsAsync());
        }

        [Fact]
        public async Task Webhook_APaymentAfterTheOrderExpired_WithinAndBeyondTheLateWindow()
        {
            var within = await World.CreateAsync(expiresAgo: TimeSpan.FromHours(2));
            var beyond = await World.CreateAsync(expiresAgo: TimeSpan.FromHours(30));

            await within.Ipn().ProcessPayOSWebhookAsync(within.SignedWebhook(PayOsCode, 49000));
            await beyond.Ipn().ProcessPayOSWebhookAsync(beyond.SignedWebhook(PayOsCode, 49000));

            Assert.Equal(PaymentOrderStatuses.Paid, (await within.OrderAsync(PaymentOrderStatuses.Expired)).Status);
            Assert.Equal(PaymentOrderStatuses.ManualReview, (await beyond.OrderAsync(PaymentOrderStatuses.Expired)).Status);
            Assert.Equal(0, await beyond.CountSubscriptionsAsync());
        }

        // ── return: T2 T7 T13 ────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Return_PayOSSaysPaid_ConfirmsTheOrderOnce_AndALaterWebhookIsAlreadyConfirmed()
        {
            var w = await World.CreateAsync();
            w.Api.Info = new PayOSPaymentInfo(long.Parse(PayOsCode), 49000, 49000, PayOSStatuses.Paid, "link-1", "FT-RET");

            var result = await w.Billing().GetPayOSReturnResultAsync(Query(("orderCode", PayOsCode), ("status", "PAID"), ("code", "00"), ("ref", OrderCode)));

            Assert.True(result!.HasValidReturnSignature);
            Assert.Equal(PaymentOrderStatuses.Paid, result.OrderStatus);
            Assert.Equal(OrderCode, result.OrderCode);
            Assert.Equal(1, await w.CountSubscriptionsAsync());

            await w.Ipn().ProcessPayOSWebhookAsync(w.SignedWebhook(PayOsCode, 49000, reference: "FT-RET"));
            Assert.Equal(1, await w.CountSubscriptionsAsync());
        }

        [Fact]
        public async Task Return_TheQuerySaysPaid_ButPayOSSaysPending_ConfirmsNothing()
        {
            var w = await World.CreateAsync();
            w.Api.Info = new PayOSPaymentInfo(long.Parse(PayOsCode), 49000, 0, PayOSStatuses.Pending, "link-1", null);

            var result = await w.Billing().GetPayOSReturnResultAsync(Query(("orderCode", PayOsCode), ("status", "PAID"), ("code", "00"), ("cancel", "false")));

            Assert.True(result!.HasValidReturnSignature);
            Assert.Equal(PaymentOrderStatuses.Pending, result.OrderStatus);
            Assert.Equal(0, await w.CountSubscriptionsAsync());
        }

        [Fact]
        public async Task Return_TheCancelPage_ConfirmedByPayOS_CancelsTheOrder_ButNotAPaidOne()
        {
            var w = await World.CreateAsync();
            w.Api.Info = new PayOSPaymentInfo(long.Parse(PayOsCode), 49000, 0, PayOSStatuses.Cancelled, "link-1", null);

            var cancelled = await w.Billing().GetPayOSReturnResultAsync(Query(("orderCode", PayOsCode), ("cancel", "true"), ("status", "CANCELLED")));

            Assert.Equal(PaymentOrderStatuses.Cancelled, cancelled!.OrderStatus);
            Assert.Equal(0, await w.CountSubscriptionsAsync());

            var paid = await World.CreateAsync();
            await paid.Ipn().ProcessPayOSWebhookAsync(paid.SignedWebhook(PayOsCode, 49000));
            paid.Api.Info = new PayOSPaymentInfo(long.Parse(PayOsCode), 49000, 0, PayOSStatuses.Cancelled, "link-1", null);
            var again = await paid.Billing().GetPayOSReturnResultAsync(Query(("orderCode", PayOsCode), ("cancel", "true")));

            Assert.Equal(PaymentOrderStatuses.Paid, again!.OrderStatus);
        }

        [Fact]
        public async Task Return_ACancelQueryAlone_WithoutPayOSAgreeing_CancelsNothing()
        {
            var w = await World.CreateAsync();
            w.Api.Info = new PayOSPaymentInfo(long.Parse(PayOsCode), 49000, 0, PayOSStatuses.Pending, "link-1", null);

            var result = await w.Billing().GetPayOSReturnResultAsync(Query(("orderCode", PayOsCode), ("cancel", "true"), ("status", "CANCELLED")));

            Assert.Equal(PaymentOrderStatuses.Pending, result!.OrderStatus);
        }

        [Fact]
        public async Task Return_WhenPayOSCannotBeAsked_IsInvalid_AndChangesNothing()
        {
            var w = await World.CreateAsync();
            w.Api.Info = null;

            var result = await w.Billing().GetPayOSReturnResultAsync(Query(("orderCode", PayOsCode), ("status", "PAID")));

            Assert.False(result!.HasValidReturnSignature);
            Assert.Equal(PaymentOrderStatuses.Pending, (await w.OrderAsync()).Status);
        }

        [Fact]
        public async Task Return_AReferenceThatNamesAnotherOrder_IsInvalid()
        {
            var w = await World.CreateAsync();
            w.Api.Info = new PayOSPaymentInfo(long.Parse(PayOsCode), 49000, 49000, PayOSStatuses.Paid, "link-1", "FT");

            var result = await w.Billing().GetPayOSReturnResultAsync(Query(("orderCode", PayOsCode), ("ref", "TCT-SOMEONE-ELSES-ORDER")));

            Assert.False(result!.HasValidReturnSignature);
            Assert.Equal(0, await w.CountSubscriptionsAsync());
        }

        [Fact]
        public async Task Return_AnUnknownPayOSCode_IsNotFound()
        {
            var w = await World.CreateAsync();
            w.Api.Info = new PayOSPaymentInfo(999, 49000, 49000, PayOSStatuses.Paid, "link-1", "FT");

            Assert.Null(await w.Billing().GetPayOSReturnResultAsync(Query(("orderCode", "999"))));
        }

        [Fact]
        public async Task Return_TheSameReturnTwice_StoresOneReturnEvent()
        {
            var w = await World.CreateAsync();
            w.Api.Info = new PayOSPaymentInfo(long.Parse(PayOsCode), 49000, 0, PayOSStatuses.Pending, "link-1", null);
            var query = Query(("orderCode", PayOsCode), ("id", "link-1"), ("status", "PENDING"));

            await w.Billing().GetPayOSReturnResultAsync(new Dictionary<string, string>(query));
            await w.Billing().GetPayOSReturnResultAsync(new Dictionary<string, string>(query));

            await using var ctx = w.Context();
            Assert.Equal(1, await ctx.PaymentEvents.CountAsync(e => e.EventType == PaymentEventTypes.Return));
        }

        // ── query / reconciliation: T9 ───────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(PayOSStatuses.Paid, 49000, 49000, PaymentOrderStatuses.Paid)]
        [InlineData(PayOSStatuses.Paid, 49000, 20000, PaymentOrderStatuses.Pending)]
        [InlineData(PayOSStatuses.Pending, 49000, 0, PaymentOrderStatuses.Pending)]
        [InlineData(PayOSStatuses.Processing, 49000, 0, PaymentOrderStatuses.Pending)]
        [InlineData(PayOSStatuses.Expired, 49000, 0, PaymentOrderStatuses.Pending)]
        [InlineData(PayOSStatuses.Cancelled, 49000, 0, PaymentOrderStatuses.Cancelled)]
        [InlineData("SOMETHING_NEW", 49000, 0, PaymentOrderStatuses.Pending)]
        public async Task Reconciliation_MapsThePayOSStatus_Idempotently(string status, long amount, long amountPaid, string expected)
        {
            var w = await World.CreateAsync(createdAgo: TimeSpan.FromMinutes(30));
            w.Api.Info = new PayOSPaymentInfo(long.Parse(PayOsCode), amount, amountPaid, status, "link-1", "FT-REC");

            var first = await w.Reconciliation().ReconcileOpenOrdersAsync(minPendingMinutes: 5);
            await w.Reconciliation().ReconcileOpenOrdersAsync(minPendingMinutes: 5);

            Assert.Equal(1, first.Checked);
            Assert.Equal(expected, (await w.OrderAsync()).Status);
            Assert.Equal(expected == PaymentOrderStatuses.Paid ? 1 : 0, await w.CountSubscriptionsAsync());
        }

        [Fact]
        public async Task Reconciliation_APayOSClientThatThrows_IsLogged_AndDoesNotStopTheRest()
        {
            var w = await World.CreateAsync(createdAgo: TimeSpan.FromMinutes(30));
            w.Api.Throw = true;

            var summary = await w.Reconciliation().ReconcileOpenOrdersAsync(minPendingMinutes: 5);

            Assert.Equal(1, summary.Checked);
            Assert.Equal(PaymentOrderStatuses.Pending, (await w.OrderAsync()).Status);
        }

        // ── cleanup: plan D8 ─────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Cleanup_AnExpiredPayOSOrder_ClosesTheLinkAtPayOS_AndAFailureIsHarmless()
        {
            var w = await World.CreateAsync(expiresAgo: TimeSpan.FromMinutes(1));

            Assert.Equal(1, await w.Billing().CleanupExpiredPendingOrdersAsync());
            Assert.Equal(new[] { long.Parse(PayOsCode) }, w.Api.Cancelled);
            Assert.Equal(PaymentOrderStatuses.Expired, (await w.OrderAsync(PaymentOrderStatuses.Expired)).Status);

            var broken = await World.CreateAsync(expiresAgo: TimeSpan.FromMinutes(1));
            broken.Api.ThrowOnCancel = true;
            Assert.Equal(1, await broken.Billing().CleanupExpiredPendingOrdersAsync());
        }

        [Fact]
        public async Task Cleanup_AnOpenPayOSOrder_IsNotCancelled()
        {
            var w = await World.CreateAsync();

            Assert.Equal(0, await w.Billing().CleanupExpiredPendingOrdersAsync());
            Assert.Empty(w.Api.Cancelled);
        }

        // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

        private static Dictionary<string, string> Query(params (string Key, string Value)[] pairs)
            => pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

        private sealed class FakeApi : IPayOSClient
        {
            public PayOSPaymentInfo? Info { get; set; }
            public bool Throw { get; set; }
            public bool ThrowOnCancel { get; set; }
            public List<long> Cancelled { get; } = new();

            public Task<PayOSCreateLinkResult> CreatePaymentLinkAsync(PayOSCreateLinkRequest request, CancellationToken ct = default)
                => Task.FromResult(new PayOSCreateLinkResult(false, ErrorCode: "UNUSED"));

            public Task<PayOSPaymentInfo?> GetPaymentAsync(long orderCode, CancellationToken ct = default)
            {
                if (Throw) throw new InvalidOperationException("payOS is down");
                return Task.FromResult(Info);
            }

            public Task<bool> CancelAsync(long orderCode, string reason, CancellationToken ct = default)
            {
                if (ThrowOnCancel) throw new InvalidOperationException("cancel failed");
                Cancelled.Add(orderCode);
                return Task.FromResult(true);
            }
        }

        private sealed class World
        {
            private readonly string _db;

            private World(string db) => _db = db;

            public FakeApi Api { get; } = new();

            public static async Task<World> CreateAsync(
                string provider = PaymentProviders.PayOS,
                TimeSpan? createdAgo = null,
                TimeSpan? expiresAgo = null)
            {
                var world = new World(Guid.NewGuid().ToString());
                await using var ctx = world.Context();
                var user = new User { Email = $"{Guid.NewGuid():N}@test.com", PasswordHash = "hash", Role = Roles.Standard };
                var plan = new PremiumPlan
                {
                    Code = "p", Name = "Premium 1 tháng", Description = "d", PriceVnd = 49000m, DurationDays = 30,
                    IsActive = true, DisplayOrder = 1, CreatedAtUtc = DateTime.UtcNow
                };
                ctx.Users.Add(user);
                ctx.PremiumPlans.Add(plan);
                await ctx.SaveChangesAsync();

                var now = DateTime.UtcNow;
                ctx.PaymentOrders.Add(new PaymentOrder
                {
                    OrderCode = OrderCode,
                    UserId = user.UserId,
                    PlanId = plan.Id,
                    Provider = provider,
                    ProviderRequestId = PayOsCode,
                    AmountVnd = 49000m,
                    Currency = "VND",
                    Status = PaymentOrderStatuses.Pending,
                    CreatedAtUtc = now - (createdAgo ?? TimeSpan.Zero),
                    UpdatedAtUtc = now,
                    ExpiresAtUtc = expiresAgo.HasValue ? now - expiresAgo.Value : now.AddMinutes(15)
                });
                await ctx.SaveChangesAsync();
                return world;
            }

            public DbflashcardContext Context()
                => new(new DbContextOptionsBuilder<DbflashcardContext>()
                    .UseInMemoryDatabase(_db)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                    .Options);

            private static PayOSOptions Options() => new()
            {
                Enabled = true,
                ClientId = "cid",
                ApiKey = "key",
                ChecksumKey = ChecksumKey,
                PublicBaseUrl = "https://tct.example.com"
            };

            private PayOSGateway Gateway()
                => new(Api, Microsoft.Extensions.Options.Options.Create(Options()),
                    new StubWebHostEnvironment { EnvironmentName = "Production" }, NullLogger<PayOSGateway>.Instance);

            private PaymentConfirmationService Confirmation(DbflashcardContext ctx)
                => new(ctx, new SubscriptionService(ctx), NullLogger<PaymentConfirmationService>.Instance);

            private PaymentQueryService Query(DbflashcardContext ctx)
                => new(ctx, new NoOpVnPayQueryClient(), Mock.Of<IMoMoQueryClient>(), Confirmation(ctx),
                    NullLogger<PaymentQueryService>.Instance, Api);

            public IpnService Ipn()
            {
                var ctx = Context();
                return new IpnService(
                    ctx,
                    new IPaymentGateway[] { new NoGateway(PaymentProviders.VNPay), new NoGateway(PaymentProviders.MoMo), Gateway() },
                    Confirmation(ctx),
                    NullLogger<IpnService>.Instance);
            }

            public BillingService Billing()
            {
                var ctx = Context();
                return new BillingService(ctx, new IPaymentGateway[] { Gateway() }, Query(ctx), NullLogger<BillingService>.Instance, Api);
            }

            public PaymentReconciliationService Reconciliation()
            {
                var ctx = Context();
                return new PaymentReconciliationService(ctx, Query(ctx), NullLogger<PaymentReconciliationService>.Instance);
            }

            public Dictionary<string, string> SignedWebhook(string payOsCode, long amount, string reference = "FT-1", string code = "00")
            {
                var data = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["orderCode"] = payOsCode,
                    ["amount"] = amount.ToString(),
                    ["description"] = "TCT509123",
                    ["accountNumber"] = "0123456789",
                    ["reference"] = reference,
                    ["transactionDateTime"] = "2026-11-20 13:06:00",
                    ["currency"] = "VND",
                    ["paymentLinkId"] = "link-1",
                    ["code"] = code,
                    ["desc"] = "Thành công",
                    ["counterAccountBankId"] = "",
                    ["counterAccountBankName"] = "",
                    ["counterAccountName"] = "",
                    ["counterAccountNumber"] = "",
                    ["virtualAccountName"] = "",
                    ["virtualAccountNumber"] = ""
                };
                var body = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["code"] = code,
                    ["desc"] = "success",
                    ["success"] = code == "00" ? "true" : "false",
                    ["signature"] = PayOSSignature.ForData(data, ChecksumKey)
                };
                foreach (var pair in data)
                {
                    body["data." + pair.Key] = pair.Value ?? string.Empty;
                }

                return body;
            }

            public async Task<PaymentOrder> OrderAsync(string? _ = null)
            {
                await using var ctx = Context();
                return await ctx.PaymentOrders.AsNoTracking().SingleAsync(o => o.OrderCode == OrderCode);
            }

            public async Task<int> CountSubscriptionsAsync()
            {
                await using var ctx = Context();
                return await ctx.UserSubscriptions.AsNoTracking().CountAsync();
            }

            public async Task<List<string>> PayloadsAsync()
            {
                await using var ctx = Context();
                return await ctx.PaymentEvents.AsNoTracking().Select(e => e.PayloadJson ?? string.Empty).ToListAsync();
            }
        }

        private sealed class NoGateway : IPaymentGateway
        {
            public NoGateway(string name) => ProviderName = name;

            public string ProviderName { get; }
            public bool IsEnabled => false;

            public Task<ProviderCheckoutResult> CreateCheckoutAsync(string orderCode, decimal amountVnd, string orderDescription, string clientIp, CancellationToken ct = default)
                => Task.FromResult(ProviderCheckoutResult.Fail("NO", "no"));

            public Task<ProviderCallbackResult> ProcessReturnAsync(IDictionary<string, string> queryParams, CancellationToken ct = default)
                => Task.FromResult(new ProviderCallbackResult());

            public Task<ProviderIpnResult> ProcessIpnAsync(IDictionary<string, string> parameters, CancellationToken ct = default)
                => Task.FromResult(new ProviderIpnResult());
        }
    }
}
