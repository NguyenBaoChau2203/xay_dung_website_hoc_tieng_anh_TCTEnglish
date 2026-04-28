using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Billing
{
    /// <summary>
    /// Reconciles stale pending VNPay orders by querying the VNPay order-status API.
    ///
    /// NOTE: VNPay query-transaction API requires additional HMAC-SHA512 signing
    /// and an HTTP POST to the VNPay server. The <see cref="IVnPayQueryClient"/> stub
    /// below is the integration point — inject a real HTTP client implementation when
    /// VNPay sandbox credentials are available.
    ///
    /// Until then, this service safely marks orders as Expired after 24 hours
    /// and flags recent (5–60 min) stale orders as ManualReview so admins can act.
    /// </summary>
    public class PaymentReconciliationService : IPaymentReconciliationService
    {
        private static readonly TimeSpan AutoExpireAfter = TimeSpan.FromHours(24);
        private static readonly TimeSpan ManualReviewThreshold = TimeSpan.FromHours(1);

        private readonly DbflashcardContext _context;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IVnPayQueryClient _vnPayQueryClient;
        private readonly ILogger<PaymentReconciliationService> _logger;

        public PaymentReconciliationService(
            DbflashcardContext context,
            ISubscriptionService subscriptionService,
            IVnPayQueryClient vnPayQueryClient,
            ILogger<PaymentReconciliationService> logger)
        {
            _context = context;
            _subscriptionService = subscriptionService;
            _vnPayQueryClient = vnPayQueryClient;
            _logger = logger;
        }

        public async Task<ReconciliationSummary> ReconcileVnPayPendingOrdersAsync(
            int minPendingMinutes = 5,
            CancellationToken ct = default)
        {
            var nowUtc = DateTime.UtcNow;
            var oldestAllowed = nowUtc.AddMinutes(-minPendingMinutes);

            // Fetch stale pending VNPay orders
            var staleOrders = await _context.PaymentOrders
                .Include(o => o.Plan)
                .Where(o => o.Provider == PaymentProviders.VNPay
                         && o.Status == PaymentOrderStatuses.Pending
                         && o.CreatedAtUtc <= oldestAllowed)
                .AsNoTracking()
                .ToListAsync(ct);

            var counters = new ReconcileCounters();

            foreach (var order in staleOrders)
            {
                try
                {
                    await ReconcileOrderAsync(order, nowUtc, counters, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Reconciliation failed for order {OrderCode}", order.OrderCode);
                }
            }

            _logger.LogInformation(
                "VNPay reconciliation complete. Checked={Count} Paid={Paid} Failed={Failed} " +
                "ManualReview={ManualReview} Expired={Expired}",
                staleOrders.Count, counters.Paid, counters.Failed, counters.ManualReview, counters.Expired);

            return new ReconciliationSummary(staleOrders.Count, counters.Paid, counters.Failed, counters.ManualReview, counters.Expired);
        }


        private sealed class ReconcileCounters
        {
            public int Paid;
            public int Failed;
            public int ManualReview;
            public int Expired;
        }

        private async Task ReconcileOrderAsync(
            PaymentOrder snapshotOrder, DateTime nowUtc,
            ReconcileCounters counters,
            CancellationToken ct)
        {
            var overdue = nowUtc - snapshotOrder.ExpiresAtUtc;

            // ── Auto-expire if past 24h ─────────────────────────────────────
            if (overdue > AutoExpireAfter)
            {
                await MarkOrderAsync(snapshotOrder.Id, PaymentOrderStatuses.Expired,
                    "Auto-expired after 24h without IPN confirmation",
                    PaymentEventTypes.Reconcile, ct);
                counters.Expired++;
                return;
            }

            // ── Try provider query ──────────────────────────────────────────
            var queryResult = await _vnPayQueryClient.QueryOrderAsync(
                snapshotOrder.OrderCode,
                snapshotOrder.CreatedAtUtc,
                snapshotOrder.ProviderTransactionId,
                ct);

            if (queryResult == null)
            {
                // Provider query not configured / unavailable — flag stale orders for manual review
                if (overdue > ManualReviewThreshold)
                {
                    await MarkOrderAsync(snapshotOrder.Id, PaymentOrderStatuses.ManualReview,
                        $"No query result after {overdue.TotalMinutes:F0} min — manual review required",
                        PaymentEventTypes.Reconcile, ct);
                    counters.ManualReview++;
                }
                // else: within normal window, leave pending
                return;
            }

            if (queryResult.IsSuccess)
            {
                await MarkPaidAsync(snapshotOrder.Id, queryResult, ct);
                counters.Paid++;
            }
            else if (queryResult.IsCancelled)
            {
                await MarkOrderAsync(snapshotOrder.Id, PaymentOrderStatuses.Cancelled,
                    $"Provider query: cancelled (code={queryResult.ResponseCode})",
                    PaymentEventTypes.Reconcile, ct);
                counters.Failed++;
            }
            else
            {
                await MarkOrderAsync(snapshotOrder.Id, PaymentOrderStatuses.Failed,
                    $"Provider query: failed (code={queryResult.ResponseCode})",
                    PaymentEventTypes.Reconcile, ct);
                counters.Failed++;
            }
        }


        private async Task MarkPaidAsync(long orderId, VnPayQueryResult queryResult, CancellationToken ct)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                var order = await _context.PaymentOrders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
                if (order == null || order.Status == PaymentOrderStatuses.Paid) return;

                var nowUtc = DateTime.UtcNow;

                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                order.Status = PaymentOrderStatuses.Paid;
                order.PaidAtUtc = nowUtc;
                order.UpdatedAtUtc = nowUtc;
                order.ProviderTransactionId = queryResult.TransactionNo;
                order.ProviderResponseCode = queryResult.ResponseCode;
                order.RawStatus = $"reconcile:code={queryResult.ResponseCode}";

                var eventKey = $"recon:{order.OrderCode}:{queryResult.ResponseCode}";
                var payloadJson = JsonSerializer.Serialize(queryResult);

                _context.PaymentEvents.Add(new PaymentEvent
                {
                    Provider = PaymentProviders.VNPay,
                    EventType = PaymentEventTypes.Reconcile,
                    EventKey = eventKey,
                    PaymentOrderId = orderId,
                    SignatureValid = true,
                    ResultCode = queryResult.ResponseCode,
                    PayloadJson = payloadJson,
                    ProcessingStatus = PaymentEventProcessingStatuses.Processed,
                    ProcessingMessage = "Paid confirmed via reconciliation query",
                    ReceivedAtUtc = nowUtc,
                    ProcessedAtUtc = nowUtc
                });

                await _context.SaveChangesAsync(ct);
                var sub = await _subscriptionService.ActivateFromPaidOrderAsync(orderId, ct);

                order.ActivatedAtUtc = sub.StartsAtUtc;
                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                _logger.LogInformation(
                    "Reconciliation: order {OrderCode} marked paid and subscription activated.",
                    order.OrderCode);
            });
        }

        private async Task MarkOrderAsync(
            long orderId, string newStatus, string reason,
            string eventType, CancellationToken ct)
        {
            _context.ChangeTracker.Clear();
            var order = await _context.PaymentOrders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order == null) return;
            if (order.Status == newStatus) return; // idempotent

            var nowUtc = DateTime.UtcNow;
            order.Status = newStatus;
            order.UpdatedAtUtc = nowUtc;
            if (order.Status is PaymentOrderStatuses.Failed or PaymentOrderStatuses.Expired)
                order.FailureMessage ??= reason;

            var eventKey = $"recon:{order.OrderCode}:{newStatus}:{nowUtc.Ticks}";
            _context.PaymentEvents.Add(new PaymentEvent
            {
                Provider = PaymentProviders.VNPay,
                EventType = eventType,
                EventKey = eventKey,
                PaymentOrderId = orderId,
                SignatureValid = true,
                PayloadJson = "{}",
                ProcessingStatus = PaymentEventProcessingStatuses.Processed,
                ProcessingMessage = reason,
                ReceivedAtUtc = nowUtc,
                ProcessedAtUtc = nowUtc
            });

            await _context.SaveChangesAsync(ct);
        }
    }

    // ─── VNPay Query Client interface & stub ─────────────────────────────────────

    /// <summary>
    /// Abstraction for the VNPay QueryDR (Query Transaction) API.
    /// Returns null if the API is not configured or unavailable (safe degradation).
    /// </summary>
    public interface IVnPayQueryClient
    {
        Task<VnPayQueryResult?> QueryOrderAsync(
            string orderCode,
            DateTime transactionCreatedAtUtc,
            string? providerTransactionNo = null,
            CancellationToken ct = default);
    }

    public class VnPayQueryResult
    {
        public bool IsSuccess { get; init; }
        public bool IsCancelled { get; init; }
        public string? ResponseCode { get; init; }
        public string? TransactionNo { get; init; }
        public string? TransactionStatus { get; init; }
        public string? BankCode { get; init; }
    }

    /// <summary>
    /// Stub implementation: always returns null (no query performed).
    /// Replace with a real HTTP implementation when VNPay query credentials are available.
    /// </summary>
    public class NoOpVnPayQueryClient : IVnPayQueryClient
    {
        public Task<VnPayQueryResult?> QueryOrderAsync(
            string orderCode,
            DateTime transactionCreatedAtUtc,
            string? providerTransactionNo = null,
            CancellationToken ct = default)
            => Task.FromResult<VnPayQueryResult?>(null);
    }
}
