using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing.MoMo;
using TCTEnglish.Services.Billing.PayOS;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Billing
{
    public enum PaymentQueryTrigger
    {
        /// <summary>The buyer's browser came back from the provider with a success code.</summary>
        Return,

        /// <summary>The buyer (or the order page) asked for a re-check.</summary>
        Refresh,

        /// <summary>The background reconciliation pass.</summary>
        Reconciliation
    }

    public enum PaymentQueryStatus
    {
        /// <summary>Unknown order, other provider, or the order is not open (anything but pending / recently expired).</summary>
        NotEligible,

        /// <summary>Asked too recently or too often; no provider call was made.</summary>
        Throttled,

        /// <summary>The provider could not be reached or is not configured; nothing was learned.</summary>
        ProviderUnavailable,

        /// <summary>The provider's answer does not settle the order (still processing, not found yet…).</summary>
        Unknown,

        /// <summary>The provider reports a final failure. The order was not changed; the caller decides.</summary>
        Failed,

        /// <summary>The provider reports success for a different amount. The order was not changed.</summary>
        AmountMismatch,

        /// <summary>The provider confirmed the payment and the confirmation service handled it.</summary>
        Confirmed
    }

    public sealed record PaymentQueryAttempt(
        PaymentQueryStatus Status,
        PaymentQueryResult? Result = null,
        PaymentConfirmationResult? Confirmation = null);

    /// <summary>
    /// Asks the provider's server about one order, at most once every 20 s and 12 times in all,
    /// and turns a verified success with the right amount into a confirmation. The browser's return
    /// alone never confirms anything: only this verified answer (or an IPN) does.
    /// </summary>
    public interface IPaymentQueryService
    {
        Task<PaymentQueryAttempt> QueryAndConfirmAsync(
            long orderId,
            PaymentQueryTrigger trigger,
            CancellationToken ct = default);
    }

    public sealed class PaymentQueryService : IPaymentQueryService
    {
        internal static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(20);
        internal const int MaxQueriesPerOrder = 12;
        internal static readonly TimeSpan MaxOrderAge = TimeSpan.FromHours(24);

        private readonly DbflashcardContext _context;
        private readonly IVnPayQueryClient _vnPayQueryClient;
        private readonly IMoMoQueryClient _moMoQueryClient;
        private readonly IPaymentConfirmationService _confirmationService;
        private readonly ILogger<PaymentQueryService> _logger;
        private readonly IPayOSClient? _payOsClient;

        public PaymentQueryService(
            DbflashcardContext context,
            IVnPayQueryClient vnPayQueryClient,
            IMoMoQueryClient moMoQueryClient,
            IPaymentConfirmationService confirmationService,
            ILogger<PaymentQueryService> logger,
            IPayOSClient? payOsClient = null)
        {
            _context = context;
            _vnPayQueryClient = vnPayQueryClient;
            _moMoQueryClient = moMoQueryClient;
            _confirmationService = confirmationService;
            _logger = logger;
            _payOsClient = payOsClient;
        }

        public async Task<PaymentQueryAttempt> QueryAndConfirmAsync(
            long orderId,
            PaymentQueryTrigger trigger,
            CancellationToken ct = default)
        {
            var order = await _context.PaymentOrders
                .AsNoTracking()
                .Where(o => o.Id == orderId)
                .Select(o => new
                {
                    o.Id, o.OrderCode, o.Provider, o.Status, o.CreatedAtUtc, o.ExpiresAtUtc, o.ProviderTransactionId,
                    o.ProviderRequestId
                })
                .FirstOrDefaultAsync(ct);

            var nowUtc = DateTime.UtcNow;
            if (order == null
                || !(order.Status == PaymentOrderStatuses.Pending || order.Status == PaymentOrderStatuses.Expired)
                || nowUtc - order.ExpiresAtUtc > MaxOrderAge)
            {
                return new PaymentQueryAttempt(PaymentQueryStatus.NotEligible);
            }

            var isVnPay = order.Provider.Equals(PaymentProviders.VNPay, StringComparison.OrdinalIgnoreCase);
            var isMoMo = order.Provider.Equals(PaymentProviders.MoMo, StringComparison.OrdinalIgnoreCase);
            var isPayOs = order.Provider.Equals(PaymentProviders.PayOS, StringComparison.OrdinalIgnoreCase);
            if (!isVnPay && !isMoMo && !isPayOs)
            {
                return new PaymentQueryAttempt(PaymentQueryStatus.NotEligible);
            }

            if (await IsThrottledAsync(order.Id, nowUtc, ct))
            {
                return new PaymentQueryAttempt(PaymentQueryStatus.Throttled);
            }

            PaymentQueryResult? answer;
            try
            {
                answer = isVnPay
                    ? await QueryVnPayAsync(order.OrderCode, order.CreatedAtUtc, order.ProviderTransactionId, ct)
                    : isPayOs
                        ? await QueryPayOSAsync(order.ProviderRequestId, ct)
                        : await _moMoQueryClient.QueryOrderAsync(order.OrderCode, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider query failed. OrderCode={OrderCode}", order.OrderCode);
                answer = null;
            }

            var payload = SafePayload(trigger, answer);
            await SaveQueryEventAsync(order.Provider, order.Id, order.OrderCode, answer, trigger, payload, ct);

            if (answer == null)
            {
                return new PaymentQueryAttempt(PaymentQueryStatus.ProviderUnavailable);
            }

            switch (answer.State)
            {
                case PaymentQueryState.Failed:
                    return new PaymentQueryAttempt(PaymentQueryStatus.Failed, answer);

                case PaymentQueryState.Success:
                    var confirmation = await _confirmationService.ConfirmPaidAsync(
                        new PaymentConfirmation(
                            OrderId: order.Id,
                            Provider: order.Provider,
                            Source: PaymentTransitionSource.ProviderQuery,
                            VerifiedAmountVnd: answer.AmountVnd,
                            EventType: PaymentEventTypes.Reconcile,
                            EventKey: $"{KeyPrefix(trigger)}:{order.OrderCode}",
                            PayloadJson: payload,
                            ProviderTransactionId: answer.TransactionId,
                            ResponseCode: answer.ResponseCode,
                            TransactionStatus: answer.TransactionStatus,
                            PayType: answer.PayType,
                            BankCode: answer.BankCode),
                        ct);

                    var status = confirmation.Outcome switch
                    {
                        PaymentConfirmationOutcome.Activated => PaymentQueryStatus.Confirmed,
                        PaymentConfirmationOutcome.AlreadyPaid => PaymentQueryStatus.Confirmed,
                        PaymentConfirmationOutcome.SentToManualReview => PaymentQueryStatus.Confirmed,
                        PaymentConfirmationOutcome.RejectedAmount => PaymentQueryStatus.AmountMismatch,
                        _ => PaymentQueryStatus.NotEligible
                    };
                    return new PaymentQueryAttempt(status, answer, confirmation);

                default:
                    return new PaymentQueryAttempt(PaymentQueryStatus.Unknown, answer);
            }
        }

        private async Task<PaymentQueryResult?> QueryVnPayAsync(
            string orderCode, DateTime createdAtUtc, string? transactionNo, CancellationToken ct)
        {
            var result = await _vnPayQueryClient.QueryOrderAsync(orderCode, createdAtUtc, transactionNo, ct);
            if (result == null)
            {
                return null;
            }

            return new PaymentQueryResult(
                result.State,
                AmountVnd: result.AmountVnd,
                TransactionId: result.TransactionNo,
                ResponseCode: result.ResponseCode,
                TransactionStatus: result.TransactionStatus,
                BankCode: result.BankCode);
        }

        /// <summary>
        /// payOS: PAID is a success carrying the amount actually paid (the confirmation compares it with the order);
        /// CANCELLED is a final failure; PENDING, PROCESSING, EXPIRED or anything new does not settle the order.
        /// </summary>
        private async Task<PaymentQueryResult?> QueryPayOSAsync(string? providerRequestId, CancellationToken ct)
        {
            if (_payOsClient == null || !PayOSOrderCode.TryParse(providerRequestId, out var payOsCode))
            {
                return PaymentQueryResult.Unknown("payOS is not available for this order");
            }

            var info = await _payOsClient.GetPaymentAsync(payOsCode, ct);
            if (info == null)
            {
                return null;
            }

            return info.Status switch
            {
                PayOSStatuses.Paid => new PaymentQueryResult(
                    PaymentQueryState.Success,
                    AmountVnd: info.AmountPaid,
                    TransactionId: info.Reference,
                    ResponseCode: "00",
                    TransactionStatus: PayOSStatuses.Paid,
                    PayType: "bank_transfer"),
                PayOSStatuses.Cancelled => new PaymentQueryResult(
                    PaymentQueryState.Failed,
                    ResponseCode: PayOSStatuses.Cancelled,
                    TransactionStatus: PayOSStatuses.Cancelled),
                _ => new PaymentQueryResult(
                    PaymentQueryState.Unknown,
                    ResponseCode: info.Status,
                    TransactionStatus: info.Status)
            };
        }

        private async Task<bool> IsThrottledAsync(long orderId, DateTime nowUtc, CancellationToken ct)
        {
            var query = _context.PaymentEvents
                .AsNoTracking()
                .Where(e => e.PaymentOrderId == orderId && e.EventType == PaymentEventTypes.Query);

            var count = await query.CountAsync(ct);
            if (count >= MaxQueriesPerOrder)
            {
                return true;
            }

            if (count == 0)
            {
                return false;
            }

            var newest = await query.MaxAsync(e => e.ReceivedAtUtc, ct);
            return nowUtc - newest < MinInterval;
        }

        /// <summary>No signature, hash or key: only what the provider answered about the order.</summary>
        private static string SafePayload(PaymentQueryTrigger trigger, PaymentQueryResult? answer)
        {
            return JsonSerializer.Serialize(new
            {
                trigger = trigger.ToString(),
                state = answer?.State.ToString() ?? "NoAnswer",
                amountVnd = answer?.AmountVnd,
                responseCode = answer?.ResponseCode,
                transactionStatus = answer?.TransactionStatus,
                transactionId = answer?.TransactionId
            });
        }

        private static string KeyPrefix(PaymentQueryTrigger trigger) => trigger switch
        {
            PaymentQueryTrigger.Return => "retq",
            PaymentQueryTrigger.Refresh => "refq",
            _ => "recon"
        };

        private async Task SaveQueryEventAsync(
            string provider, long orderId, string orderCode, PaymentQueryResult? answer,
            PaymentQueryTrigger trigger, string payload, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var evt = new PaymentEvent
            {
                Provider = provider,
                EventType = PaymentEventTypes.Query,
                EventKey = $"query:{orderCode}:{now.Ticks}",
                PaymentOrderId = orderId,
                SignatureValid = answer != null,
                ResultCode = answer?.ResponseCode,
                PayloadJson = payload,
                ProcessingStatus = PaymentEventProcessingStatuses.Processed,
                ProcessingMessage = $"{trigger}: {answer?.State.ToString() ?? "no answer"}",
                ReceivedAtUtc = now,
                ProcessedAtUtc = now
            };

            _context.PaymentEvents.Add(evt);
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (RelationalUniqueConflict.IsUnique(ex))
            {
                _context.Entry(evt).State = EntityState.Detached;
            }
        }
    }
}
