using System;
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
    /// The single path that marks a payment order paid and activates Premium.
    /// Order status changes are checked against <see cref="PaymentOrderStateMachine"/>.
    /// </summary>
    public sealed class PaymentConfirmationService : IPaymentConfirmationService
    {
        private static readonly TimeSpan LateConfirmationGracePeriod = TimeSpan.FromHours(24);

        private readonly DbflashcardContext _context;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<PaymentConfirmationService> _logger;

        public PaymentConfirmationService(
            DbflashcardContext context,
            ISubscriptionService subscriptionService,
            ILogger<PaymentConfirmationService> logger)
        {
            _context = context;
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        public async Task<PaymentConfirmationResult> ConfirmPaidAsync(
            PaymentConfirmation c,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(c);

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();

                await using var transaction = await _context.Database.BeginTransactionAsync(ct);

                var order = await _context.PaymentOrders
                    .Include(o => o.Plan)
                    .FirstOrDefaultAsync(o => o.Id == c.OrderId, ct);

                if (order == null)
                {
                    return new PaymentConfirmationResult(PaymentConfirmationOutcome.OrderNotFound, null);
                }

                var nowUtc = DateTime.UtcNow;

                // 2. Already paid: idempotent, make sure the subscription exists.
                if (order.Status == PaymentOrderStatuses.Paid)
                {
                    var existing = await _subscriptionService.ActivateFromPaidOrderAsync(order.Id, ct);
                    if (order.ActivatedAtUtc == null)
                    {
                        order.ActivatedAtUtc = existing.StartsAtUtc;
                        await _context.SaveChangesAsync(ct);
                    }

                    await TryAddEventAsync(
                        c.Provider, c.EventType, c.EventKey, order.Id, c.ResponseCode, c.PayloadJson,
                        PaymentEventProcessingStatuses.Ignored, "Order already paid", ct);

                    await transaction.CommitAsync(ct);
                    return new PaymentConfirmationResult(PaymentConfirmationOutcome.AlreadyPaid, existing.EndsAtUtc);
                }

                // 3. State machine.
                if (!PaymentOrderStateMachine.CanMarkPaid(order.Status, c.Source))
                {
                    await TryAddEventAsync(
                        c.Provider, c.EventType, c.EventKey, order.Id, c.ResponseCode, c.PayloadJson,
                        PaymentEventProcessingStatuses.Ignored,
                        $"Ignored: order is '{order.Status}', {c.Source} may not mark it paid", ct);

                    await transaction.CommitAsync(ct);
                    return new PaymentConfirmationResult(PaymentConfirmationOutcome.RejectedState, null);
                }

                // 4. Amount must be proven by the provider, except for an explicit admin decision.
                if (c.Source != PaymentTransitionSource.Admin)
                {
                    var expectedAmount = Convert.ToInt64(decimal.Truncate(order.AmountVnd));
                    if (c.VerifiedAmountVnd != expectedAmount)
                    {
                        await TryAddEventAsync(
                            c.Provider, c.EventType, c.EventKey, order.Id, c.ResponseCode, c.PayloadJson,
                            PaymentEventProcessingStatuses.Failed,
                            $"Amount mismatch: expected {expectedAmount}, got {(c.VerifiedAmountVnd?.ToString() ?? "none")}", ct);

                        await transaction.CommitAsync(ct);
                        return new PaymentConfirmationResult(PaymentConfirmationOutcome.RejectedAmount, null);
                    }
                }

                // 5. Late rule (every provider): a success long after expiry needs a human.
                var isExpired = order.Status == PaymentOrderStatuses.Expired
                                || (order.Status == PaymentOrderStatuses.Pending && order.ExpiresAtUtc < nowUtc);
                if (isExpired
                    && c.Source != PaymentTransitionSource.Admin
                    && nowUtc - order.ExpiresAtUtc > LateConfirmationGracePeriod
                    && PaymentOrderStateMachine.CanSendToManualReview(order.Status, c.Source))
                {
                    var overdue = nowUtc - order.ExpiresAtUtc;
                    order.Status = PaymentOrderStatuses.ManualReview;
                    order.RawStatus = $"late_confirmation={overdue.TotalHours:F1}h";
                    ApplyProviderFields(order, c);
                    order.UpdatedAtUtc = nowUtc;

                    await TryAddEventAsync(
                        c.Provider, PaymentEventTypes.ManualReview, c.EventKey, order.Id, c.ResponseCode, c.PayloadJson,
                        PaymentEventProcessingStatuses.Processed,
                        $"Late confirmation: overdue {overdue.TotalHours:F1}h > {LateConfirmationGracePeriod.TotalHours}h", ct);

                    await _context.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return new PaymentConfirmationResult(PaymentConfirmationOutcome.SentToManualReview, null);
                }

                // 6. Mark paid.
                order.Status = PaymentOrderStatuses.Paid;
                order.RawStatus = BuildRawStatus(c);
                order.PaidAtUtc = nowUtc;
                order.UpdatedAtUtc = nowUtc;
                order.FailureMessage = null;
                ApplyProviderFields(order, c);
                if (c.Source == PaymentTransitionSource.ProviderCallback)
                {
                    order.IpnPayloadJson = c.PayloadJson;
                }

                if (c.Source == PaymentTransitionSource.Admin)
                {
                    order.ConfirmedAtUtc = nowUtc;
                    order.ConfirmedByUserId = c.AdminUserId;
                }

                await TryAddEventAsync(
                    c.Provider, c.EventType, c.EventKey, order.Id, c.ResponseCode, c.PayloadJson,
                    PaymentEventProcessingStatuses.Processed,
                    c.Source == PaymentTransitionSource.Admin
                        ? $"Payment confirmed by admin: {c.AdminReason}"
                        : $"Payment confirmed via {DescribeSource(c.Source)} - activating subscription",
                    ct);

                await _context.SaveChangesAsync(ct);

                // Concurrency guard without a schema change: (Provider, EventType, EventKey) is
                // unique, so only one of two racing confirmations can insert grant:{OrderCode}.
                var grantKey = $"grant:{order.OrderCode}";
                var grantEvent = NewEvent(
                    order.Provider, PaymentEventTypes.GrantPremium, grantKey, order.Id, c.ResponseCode,
                    c.PayloadJson, PaymentEventProcessingStatuses.Processed, "Activating subscription");
                var grantInserted = await TryInsertAsync(grantEvent, ct);
                if (!grantInserted)
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogInformation(
                        "Payment confirmation for {OrderCode} lost the race to another confirmation.",
                        order.OrderCode);
                    return new PaymentConfirmationResult(PaymentConfirmationOutcome.AlreadyPaid, null);
                }

                var sub = await _subscriptionService.ActivateFromPaidOrderAsync(order.Id, ct);
                order.ActivatedAtUtc = sub.StartsAtUtc;
                grantEvent.ProcessingMessage = $"Subscription activated. SubId={sub.Id}, Ends={sub.EndsAtUtc:O}";
                await _context.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);
                return new PaymentConfirmationResult(PaymentConfirmationOutcome.Activated, sub.EndsAtUtc);
            });

            // Buyer notification: INotificationService has no per-user create method, so none is
            // sent (recorded in the billing plan, section 12). Adding one is out of scope.
        }

        private static string DescribeSource(PaymentTransitionSource source) => source switch
        {
            PaymentTransitionSource.ProviderCallback => "IPN",
            PaymentTransitionSource.ProviderQuery => "provider query",
            PaymentTransitionSource.Admin => "admin",
            _ => source.ToString()
        };

        private static string BuildRawStatus(PaymentConfirmation c)
        {
            if (c.Source == PaymentTransitionSource.Admin)
            {
                return "manual_confirm";
            }

            if (c.Source == PaymentTransitionSource.ProviderQuery)
            {
                return $"query:{c.Provider}:code={c.ResponseCode}";
            }

            return c.Provider.Equals(PaymentProviders.VNPay, StringComparison.OrdinalIgnoreCase)
                ? $"vnp_ResponseCode={c.ResponseCode};vnp_TransactionStatus={c.TransactionStatus}"
                : $"momo_resultCode={c.ResponseCode}";
        }

        private static void ApplyProviderFields(PaymentOrder order, PaymentConfirmation c)
        {
            order.ProviderTransactionId = c.ProviderTransactionId ?? order.ProviderTransactionId;
            order.ProviderRequestId = c.ProviderRequestId ?? order.ProviderRequestId;
            order.ProviderResponseCode = c.ResponseCode ?? order.ProviderResponseCode;
            order.ProviderTransactionStatus = c.TransactionStatus ?? order.ProviderTransactionStatus;
            order.PayType = c.PayType ?? order.PayType;
            order.BankCode = c.BankCode ?? order.BankCode;
            order.BankTransactionNo = c.BankTranNo ?? order.BankTransactionNo;
            order.CardType = c.CardType ?? order.CardType;
        }

        private static PaymentEvent NewEvent(
            string provider, string eventType, string eventKey, long orderId, string? resultCode,
            string payloadJson, string processingStatus, string message)
        {
            var now = DateTime.UtcNow;
            return new PaymentEvent
            {
                Provider = provider,
                EventType = eventType,
                EventKey = eventKey,
                PaymentOrderId = orderId,
                SignatureValid = true,
                ResultCode = resultCode,
                PayloadJson = payloadJson,
                ProcessingStatus = processingStatus,
                ProcessingMessage = message,
                ReceivedAtUtc = now,
                ProcessedAtUtc = now
            };
        }

        private async Task TryAddEventAsync(
            string provider, string eventType, string eventKey, long orderId, string? resultCode,
            string payloadJson, string processingStatus, string message, CancellationToken ct)
        {
            var exists = await _context.PaymentEvents
                .AsNoTracking()
                .AnyAsync(e => e.Provider == provider && e.EventType == eventType && e.EventKey == eventKey, ct);
            if (exists)
            {
                return;
            }

            await TryInsertAsync(
                NewEvent(provider, eventType, eventKey, orderId, resultCode, payloadJson, processingStatus, message),
                ct);
        }

        /// <summary>Inserts the event; returns false (and detaches it) on a unique conflict.</summary>
        private async Task<bool> TryInsertAsync(PaymentEvent evt, CancellationToken ct)
        {
            _context.PaymentEvents.Add(evt);
            try
            {
                await _context.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex) when (RelationalUniqueConflict.IsUnique(ex))
            {
                _context.Entry(evt).State = EntityState.Detached;
                return false;
            }
        }
    }
}
