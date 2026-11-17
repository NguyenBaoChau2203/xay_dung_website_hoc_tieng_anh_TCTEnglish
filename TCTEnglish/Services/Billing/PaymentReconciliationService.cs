using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Billing
{
    /// <summary>
    /// Reconciles open payment orders (<c>pending</c>, or <c>expired</c> within the last 24 hours) of both
    /// providers by asking the provider's own server, through <see cref="IPaymentQueryService"/>.
    ///
    /// Only a verified success with the right amount confirms an order (through the single confirmation
    /// service). A provider answer that does not settle the order never changes it: the old behaviour of
    /// marking an in-progress or "not found yet" transaction as failed is gone.
    /// </summary>
    public class PaymentReconciliationService : IPaymentReconciliationService
    {
        private static readonly TimeSpan AutoExpireAfter = TimeSpan.FromHours(24);
        private static readonly TimeSpan ManualReviewThreshold = TimeSpan.FromHours(1);

        private readonly DbflashcardContext _context;
        private readonly IPaymentQueryService _queryService;
        private readonly ILogger<PaymentReconciliationService> _logger;

        public PaymentReconciliationService(
            DbflashcardContext context,
            IPaymentQueryService queryService,
            ILogger<PaymentReconciliationService> logger)
        {
            _context = context;
            _queryService = queryService;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task<ReconciliationSummary> ReconcileVnPayPendingOrdersAsync(
            int minPendingMinutes = 5,
            CancellationToken ct = default)
            => ReconcileOpenOrdersAsync(minPendingMinutes, ct);

        /// <inheritdoc />
        public async Task<ReconciliationSummary> ReconcileOpenOrdersAsync(
            int minPendingMinutes = 5,
            CancellationToken ct = default)
        {
            var nowUtc = DateTime.UtcNow;
            var oldestAllowed = nowUtc.AddMinutes(-minPendingMinutes);
            var recentlyExpired = nowUtc - AutoExpireAfter;

            var openOrders = await _context.PaymentOrders
                .AsNoTracking()
                .Where(o => (o.Provider == PaymentProviders.VNPay || o.Provider == PaymentProviders.MoMo)
                         && o.CreatedAtUtc <= oldestAllowed
                         && (o.Status == PaymentOrderStatuses.Pending
                             || (o.Status == PaymentOrderStatuses.Expired && o.ExpiresAtUtc >= recentlyExpired)))
                .OrderBy(o => o.CreatedAtUtc)
                .Select(o => new OpenOrder(o.Id, o.OrderCode, o.Provider, o.Status, o.ExpiresAtUtc, o.ReturnPayloadJson))
                .ToListAsync(ct);

            var counters = new ReconcileCounters();
            foreach (var order in openOrders)
            {
                try
                {
                    await ReconcileOrderAsync(order, nowUtc, counters, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reconciliation failed for order {OrderCode}", order.OrderCode);
                }
            }

            _logger.LogInformation(
                "Payment reconciliation complete. Checked={Count} Paid={Paid} Failed={Failed} " +
                "ManualReview={ManualReview} Expired={Expired}",
                openOrders.Count, counters.Paid, counters.Failed, counters.ManualReview, counters.Expired);

            return new ReconciliationSummary(openOrders.Count, counters.Paid, counters.Failed, counters.ManualReview, counters.Expired);
        }

        private sealed record OpenOrder(
            long Id, string OrderCode, string Provider, string Status, DateTime ExpiresAtUtc, string? ReturnPayloadJson);

        private sealed class ReconcileCounters
        {
            public int Paid;
            public int Failed;
            public int ManualReview;
            public int Expired;
        }

        private async Task ReconcileOrderAsync(
            OpenOrder order, DateTime nowUtc, ReconcileCounters counters, CancellationToken ct)
        {
            var overdue = nowUtc - order.ExpiresAtUtc;

            // Auto-expire a pending order that is more than 24 h past its expiry (the cleanup worker normally
            // does it after 15 min; this covers a worker that was off). Nothing to ask the provider any more.
            if (overdue > AutoExpireAfter)
            {
                if (await ChangeStatusAsync(order.Id, PaymentOrderStatuses.Expired, null,
                        "Auto-expired after 24h without confirmation", ct))
                {
                    counters.Expired++;
                }

                return;
            }

            var attempt = await _queryService.QueryAndConfirmAsync(order.Id, PaymentQueryTrigger.Reconciliation, ct);
            switch (attempt.Status)
            {
                case PaymentQueryStatus.Confirmed:
                    if (attempt.Confirmation?.Outcome == PaymentConfirmationOutcome.SentToManualReview)
                    {
                        counters.ManualReview++;
                    }
                    else
                    {
                        counters.Paid++;
                    }

                    break;

                case PaymentQueryStatus.Failed:
                    var failedStatus = order.Provider.Equals(PaymentProviders.MoMo, StringComparison.OrdinalIgnoreCase)
                        ? (attempt.Result?.ResponseCode == "1006" ? PaymentOrderStatuses.Cancelled : PaymentOrderStatuses.Failed)
                        : PaymentOrderStatuses.Cancelled; // VNPay status 02 is what a cancelled payment reports
                    if (await ChangeStatusAsync(order.Id, failedStatus, PaymentTransitionSource.ProviderQuery,
                            $"Provider query: {failedStatus} (code={attempt.Result?.ResponseCode})", ct))
                    {
                        counters.Failed++;
                    }

                    break;

                case PaymentQueryStatus.Unknown:
                case PaymentQueryStatus.ProviderUnavailable:
                    // The provider has not settled the order. Leave it as it is, except: a buyer who came back with a
                    // success page more than an hour ago and still has nothing needs a human to look at it.
                    if (overdue > ManualReviewThreshold
                        && PaymentOrderPresentation.ReturnReportedSuccess(order.Provider, order.ReturnPayloadJson)
                        && await ChangeStatusAsync(order.Id, PaymentOrderStatuses.ManualReview, PaymentTransitionSource.ProviderQuery,
                            $"Return reported success but no provider confirmation after {overdue.TotalMinutes:F0} min", ct))
                    {
                        counters.ManualReview++;
                    }

                    break;

                default:
                    // Throttled, NotEligible, AmountMismatch: nothing to do now.
                    break;
            }
        }

        /// <summary>
        /// Moves an order to a non-paid status if the state machine allows it. <paramref name="source"/> is null
        /// for the 24 h auto-expire (a cleanup).
        /// </summary>
        private async Task<bool> ChangeStatusAsync(
            long orderId, string newStatus, PaymentTransitionSource? source, string reason, CancellationToken ct)
        {
            _context.ChangeTracker.Clear();
            var order = await _context.PaymentOrders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order == null || order.Status == newStatus)
            {
                return false;
            }

            var allowed = newStatus switch
            {
                PaymentOrderStatuses.Expired => PaymentOrderStateMachine.CanExpire(order.Status),
                PaymentOrderStatuses.ManualReview => PaymentOrderStateMachine.CanSendToManualReview(order.Status, source ?? PaymentTransitionSource.Cleanup),
                _ => PaymentOrderStateMachine.CanMarkFailedOrCancelled(order.Status, source ?? PaymentTransitionSource.Cleanup)
            };
            if (!allowed)
            {
                return false;
            }

            var nowUtc = DateTime.UtcNow;
            order.Status = newStatus;
            order.UpdatedAtUtc = nowUtc;
            if (newStatus is PaymentOrderStatuses.Failed or PaymentOrderStatuses.Expired)
            {
                order.FailureMessage ??= reason;
            }

            _context.PaymentEvents.Add(new PaymentEvent
            {
                Provider = order.Provider,
                EventType = newStatus == PaymentOrderStatuses.ManualReview ? PaymentEventTypes.ManualReview : PaymentEventTypes.Reconcile,
                EventKey = $"recon:{order.OrderCode}:{newStatus}:{nowUtc.Ticks}",
                PaymentOrderId = orderId,
                SignatureValid = true,
                PayloadJson = "{}",
                ProcessingStatus = PaymentEventProcessingStatuses.Processed,
                ProcessingMessage = reason,
                ReceivedAtUtc = nowUtc,
                ProcessedAtUtc = nowUtc
            });

            await _context.SaveChangesAsync(ct);
            return true;
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
        /// <summary>Three-way answer: only <see cref="PaymentQueryState.Success"/> may confirm an order.</summary>
        public PaymentQueryState State { get; init; } = PaymentQueryState.Unknown;
        public bool IsSuccess { get; init; }
        public bool IsCancelled { get; init; }
        public string? ResponseCode { get; init; }
        public string? TransactionNo { get; init; }
        public string? TransactionStatus { get; init; }
        public string? BankCode { get; init; }

        /// <summary>Amount in VND (<c>vnp_Amount</c> / 100); the response signature covers it.</summary>
        public long? AmountVnd { get; init; }
        public string? PayDate { get; init; }
    }

    /// <summary>
    /// Stub implementation: always returns null (no query performed).
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
