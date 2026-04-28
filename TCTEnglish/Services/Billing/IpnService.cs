using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Billing
{
    public class IpnService : IIpnService
    {
        private static readonly TimeSpan LateIpnGracePeriod = TimeSpan.FromHours(24);

        private readonly DbflashcardContext _context;
        private readonly IPaymentGateway _vnpayGateway;
        private readonly IPaymentGateway? _momoGateway;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<IpnService> _logger;

        public IpnService(
            DbflashcardContext context,
            IEnumerable<IPaymentGateway> gateways,
            ISubscriptionService subscriptionService,
            ILogger<IpnService> logger)
        {
            _context = context;
            _vnpayGateway = gateways.First(g =>
                g.ProviderName.Equals(PaymentProviders.VNPay, StringComparison.OrdinalIgnoreCase));
            _momoGateway = gateways.FirstOrDefault(g =>
                g.ProviderName.Equals(PaymentProviders.MoMo, StringComparison.OrdinalIgnoreCase));
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        public async Task<IpnProcessingResult> ProcessVnPayIpnAsync(
            IDictionary<string, string> queryParams,
            CancellationToken ct = default)
        {
            queryParams.TryGetValue("vnp_TransactionNo", out var txnNo);
            queryParams.TryGetValue("vnp_TxnRef", out var orderCode);
            queryParams.TryGetValue("vnp_ResponseCode", out var responseCode);
            queryParams.TryGetValue("vnp_TransactionStatus", out var txnStatus);
            queryParams.TryGetValue("vnp_Amount", out var amountStr);
            queryParams.TryGetValue("vnp_CurrCode", out var currencyCode);
            queryParams.TryGetValue("vnp_BankCode", out var bankCode);
            queryParams.TryGetValue("vnp_BankTranNo", out var bankTranNo);
            queryParams.TryGetValue("vnp_CardType", out var cardType);

            var eventKey = BuildIpnEventKey(txnNo, orderCode, responseCode);
            var payloadJson = SerializeSafePayload(queryParams);

            var ipnResult = await _vnpayGateway.ProcessIpnAsync(queryParams, ct);
            if (!ipnResult.SignatureValid)
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.VNPay,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: BuildInvalidSignatureAuditKey(eventKey),
                    orderId: null,
                    signatureValid: false,
                    resultCode: responseCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Failed,
                    message: "Invalid HMAC-SHA512 signature",
                    ct: ct);

                return IpnProcessingResult.InvalidSignature();
            }

            var isDuplicate = await _context.PaymentEvents
                .AsNoTracking()
                .AnyAsync(e => e.Provider == PaymentProviders.VNPay
                            && e.EventType == PaymentEventTypes.Ipn
                            && e.EventKey == eventKey, ct);
            if (isDuplicate)
            {
                return IpnProcessingResult.AlreadyConfirmed();
            }

            if (string.IsNullOrWhiteSpace(orderCode))
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.VNPay,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: null,
                    signatureValid: true,
                    resultCode: responseCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Failed,
                    message: "Missing vnp_TxnRef",
                    ct: ct);
                return IpnProcessingResult.OrderNotFound();
            }

            var order = await _context.PaymentOrders
                .Include(o => o.Plan)
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode, ct);
            if (order == null)
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.VNPay,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: null,
                    signatureValid: true,
                    resultCode: responseCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Failed,
                    message: $"Order not found: {orderCode}",
                    ct: ct);
                return IpnProcessingResult.OrderNotFound();
            }

            if (!order.Provider.Equals(PaymentProviders.VNPay, StringComparison.OrdinalIgnoreCase))
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.VNPay,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: order.Id,
                    signatureValid: true,
                    resultCode: responseCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Failed,
                    message: $"Provider mismatch: expected {PaymentProviders.VNPay}, got {order.Provider}",
                    ct: ct);
                return IpnProcessingResult.InvalidOrder();
            }

            if (order.Status == PaymentOrderStatuses.Paid)
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.VNPay,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: order.Id,
                    signatureValid: true,
                    resultCode: responseCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Ignored,
                    message: "Order already paid",
                    ct: ct);
                return IpnProcessingResult.AlreadyConfirmed();
            }

            if (!string.IsNullOrWhiteSpace(currencyCode)
                && !currencyCode.Equals("VND", StringComparison.OrdinalIgnoreCase))
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.VNPay,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: order.Id,
                    signatureValid: true,
                    resultCode: responseCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Failed,
                    message: $"Currency mismatch: {currencyCode}",
                    ct: ct);
                return IpnProcessingResult.CurrencyMismatch();
            }

            if (!long.TryParse(amountStr, out var vnpAmount))
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.VNPay,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: order.Id,
                    signatureValid: true,
                    resultCode: responseCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Failed,
                    message: $"Unparseable vnp_Amount: {amountStr}",
                    ct: ct);
                return IpnProcessingResult.AmountMismatch();
            }

            var expectedAmount = (long)(order.AmountVnd * 100);
            if (vnpAmount != expectedAmount)
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.VNPay,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: order.Id,
                    signatureValid: true,
                    resultCode: responseCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Failed,
                    message: $"Amount mismatch: expected {expectedAmount}, got {vnpAmount}",
                    ct: ct);
                return IpnProcessingResult.AmountMismatch();
            }

            var isSuccess = responseCode == "00" && txnStatus == "00";
            if (!isSuccess)
            {
                var newStatus = responseCode == "24"
                    ? PaymentOrderStatuses.Cancelled
                    : PaymentOrderStatuses.Failed;

                order.Status = newStatus;
                order.RawStatus = $"vnp_ResponseCode={responseCode};vnp_TransactionStatus={txnStatus}";
                order.ProviderTransactionId = txnNo;
                order.ProviderResponseCode = responseCode;
                order.ProviderTransactionStatus = txnStatus;
                order.BankCode = bankCode;
                order.BankTransactionNo = bankTranNo;
                order.CardType = cardType;
                order.IpnPayloadJson = payloadJson;
                order.FailureMessage = $"VNPay response: {responseCode}, status: {txnStatus}";
                order.UpdatedAtUtc = DateTime.UtcNow;

                await TrySaveEventAsync(
                    provider: PaymentProviders.VNPay,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: order.Id,
                    signatureValid: true,
                    resultCode: responseCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Processed,
                    message: $"Payment {newStatus}: responseCode={responseCode}",
                    ct: ct);

                await _context.SaveChangesAsync(ct);
                return IpnProcessingResult.Ok();
            }

            try
            {
                await ProcessSuccessfulPaymentAsync(
                    provider: PaymentProviders.VNPay,
                    orderId: order.Id,
                    eventKey: eventKey,
                    payloadJson: payloadJson,
                    providerTransactionId: txnNo,
                    providerRequestId: null,
                    providerResponseCode: responseCode,
                    providerTransactionStatus: txnStatus,
                    payType: null,
                    bankCode: bankCode,
                    bankTranNo: bankTranNo,
                    cardType: cardType,
                    ct: ct);

                return IpnProcessingResult.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "VNPay IPN: failed to process successful payment. OrderCode={OrderCode}",
                    orderCode);
                return IpnProcessingResult.UnknownError();
            }
        }

        public async Task ProcessMoMoIpnAsync(
            IDictionary<string, string> parameters,
            CancellationToken ct = default)
        {
            if (_momoGateway == null)
            {
                _logger.LogWarning("MoMo IPN received but gateway is unavailable.");
                return;
            }

            parameters.TryGetValue("orderId", out var orderCode);
            parameters.TryGetValue("requestId", out var requestId);
            parameters.TryGetValue("transId", out var transId);
            parameters.TryGetValue("resultCode", out var resultCode);
            parameters.TryGetValue("amount", out var amountStr);
            parameters.TryGetValue("payType", out var payType);

            var eventKey = BuildMoMoIpnEventKey(orderCode, requestId, transId, resultCode);
            var payloadJson = SerializeSafePayload(parameters);
            var ipnResult = await _momoGateway.ProcessIpnAsync(parameters, ct);

            if (!ipnResult.SignatureValid)
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.MoMo,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: BuildInvalidSignatureAuditKey(eventKey),
                    orderId: null,
                    signatureValid: false,
                    resultCode: resultCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Failed,
                    message: "Invalid HMAC-SHA256 signature",
                    ct: ct);
                return;
            }

            var isDuplicate = await _context.PaymentEvents
                .AsNoTracking()
                .AnyAsync(e => e.Provider == PaymentProviders.MoMo
                            && e.EventType == PaymentEventTypes.Ipn
                            && e.EventKey == eventKey, ct);
            if (isDuplicate)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(orderCode))
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.MoMo,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: null,
                    signatureValid: true,
                    resultCode: resultCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Failed,
                    message: "Missing orderId",
                    ct: ct);
                return;
            }

            var order = await _context.PaymentOrders
                .Include(o => o.Plan)
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode, ct);
            if (order == null)
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.MoMo,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: null,
                    signatureValid: true,
                    resultCode: resultCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Failed,
                    message: $"Order not found: {orderCode}",
                    ct: ct);
                return;
            }

            if (!order.Provider.Equals(PaymentProviders.MoMo, StringComparison.OrdinalIgnoreCase))
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.MoMo,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: order.Id,
                    signatureValid: true,
                    resultCode: resultCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Failed,
                    message: $"Provider mismatch: expected {PaymentProviders.MoMo}, got {order.Provider}",
                    ct: ct);
                return;
            }

            if (!long.TryParse(amountStr, out var momoAmount))
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.MoMo,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: order.Id,
                    signatureValid: true,
                    resultCode: resultCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Failed,
                    message: $"Unparseable amount: {amountStr}",
                    ct: ct);
                return;
            }

            var expectedAmount = Convert.ToInt64(decimal.Truncate(order.AmountVnd));
            if (momoAmount != expectedAmount)
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.MoMo,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: order.Id,
                    signatureValid: true,
                    resultCode: resultCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Failed,
                    message: $"Amount mismatch: expected {expectedAmount}, got {momoAmount}",
                    ct: ct);
                return;
            }

            if (order.Status == PaymentOrderStatuses.Paid)
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.MoMo,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: order.Id,
                    signatureValid: true,
                    resultCode: resultCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Ignored,
                    message: "Order already paid",
                    ct: ct);
                return;
            }

            var isSuccess = string.Equals(resultCode, "0", StringComparison.Ordinal);
            if (!isSuccess)
            {
                order.Status = string.Equals(resultCode, "1006", StringComparison.Ordinal)
                    ? PaymentOrderStatuses.Cancelled
                    : PaymentOrderStatuses.Failed;
                order.RawStatus = $"momo_resultCode={resultCode}";
                order.ProviderRequestId = requestId;
                order.ProviderTransactionId = transId;
                order.ProviderResponseCode = resultCode;
                order.ProviderTransactionStatus = resultCode;
                order.PayType = payType;
                order.IpnPayloadJson = payloadJson;
                order.FailureMessage = ipnResult.ProviderMessage ?? $"MoMo resultCode={resultCode}";
                order.UpdatedAtUtc = DateTime.UtcNow;

                await TrySaveEventAsync(
                    provider: PaymentProviders.MoMo,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: order.Id,
                    signatureValid: true,
                    resultCode: resultCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Processed,
                    message: $"Payment {order.Status}: resultCode={resultCode}",
                    ct: ct);

                await _context.SaveChangesAsync(ct);
                return;
            }

            await ProcessSuccessfulPaymentAsync(
                provider: PaymentProviders.MoMo,
                orderId: order.Id,
                eventKey: eventKey,
                payloadJson: payloadJson,
                providerTransactionId: transId,
                providerRequestId: requestId,
                providerResponseCode: resultCode,
                providerTransactionStatus: resultCode,
                payType: payType,
                bankCode: null,
                bankTranNo: null,
                cardType: null,
                ct: ct);
        }

        internal static string BuildIpnEventKey(
            string? txnNo,
            string? orderCode,
            string? responseCode)
        {
            var rc = responseCode ?? "?";
            if (!string.IsNullOrEmpty(txnNo))
                return $"{txnNo}:{orderCode ?? "?"}:{rc}";

            return $"{orderCode ?? "?"}::{rc}";
        }

        internal static string BuildReturnEventKey(
            string? orderCode,
            string? responseCode,
            string? secureHash)
        {
            var rc = responseCode ?? "?";
            var hash7 = string.IsNullOrEmpty(secureHash)
                ? Guid.NewGuid().ToString("N")[..7]
                : secureHash[..Math.Min(7, secureHash.Length)];

            return $"ret:{orderCode ?? "?"}:{rc}:{hash7}";
        }

        internal static string BuildCheckoutEventKey(string orderCode)
            => $"co:{orderCode}";

        internal static string BuildMoMoIpnEventKey(
            string? orderCode,
            string? requestId,
            string? transId,
            string? resultCode)
        {
            var o = orderCode ?? "?";
            var r = requestId ?? "?";
            var t = transId ?? "?";
            var c = resultCode ?? "?";
            return $"momo:{o}:{r}:{t}:{c}";
        }

        private static string BuildInvalidSignatureAuditKey(string prefix)
        {
            var safe = prefix.Length <= 20 ? prefix : prefix[..20];
            return $"badsig:{safe}:{Guid.NewGuid():N}";
        }

        private async Task ProcessSuccessfulPaymentAsync(
            string provider,
            long orderId,
            string eventKey,
            string payloadJson,
            string? providerTransactionId,
            string? providerRequestId,
            string? providerResponseCode,
            string? providerTransactionStatus,
            string? payType,
            string? bankCode,
            string? bankTranNo,
            string? cardType,
            CancellationToken ct)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();

                await using var transaction = await _context.Database.BeginTransactionAsync(ct);

                var order = await _context.PaymentOrders
                    .Include(o => o.Plan)
                    .FirstOrDefaultAsync(o => o.Id == orderId, ct);

                if (order == null)
                {
                    throw new InvalidOperationException($"Payment order {orderId} no longer exists.");
                }

                if (order.Status == PaymentOrderStatuses.Paid)
                {
                    await transaction.CommitAsync(ct);
                    return;
                }

                var nowUtc = DateTime.UtcNow;
                var isExpired = order.Status == PaymentOrderStatuses.Expired
                             || (order.ExpiresAtUtc < nowUtc && order.Status == PaymentOrderStatuses.Pending);

                if (provider.Equals(PaymentProviders.VNPay, StringComparison.OrdinalIgnoreCase) && isExpired)
                {
                    var overdue = nowUtc - order.ExpiresAtUtc;
                    if (overdue > LateIpnGracePeriod)
                    {
                        order.Status = PaymentOrderStatuses.ManualReview;
                        order.RawStatus = $"late_ipn={overdue.TotalHours:F1}h";
                        order.ProviderTransactionId = providerTransactionId;
                        order.ProviderRequestId = providerRequestId;
                        order.ProviderResponseCode = providerResponseCode;
                        order.ProviderTransactionStatus = providerTransactionStatus;
                        order.PayType = payType;
                        order.IpnPayloadJson = payloadJson;
                        order.UpdatedAtUtc = nowUtc;

                        await TrySaveEventAsync(
                            provider: provider,
                            eventType: PaymentEventTypes.ManualReview,
                            eventKey: eventKey,
                            orderId: order.Id,
                            signatureValid: true,
                            resultCode: providerResponseCode,
                            payloadJson: payloadJson,
                            processingStatus: PaymentEventProcessingStatuses.Processed,
                            message: $"Late IPN: overdue {overdue.TotalHours:F1}h > {LateIpnGracePeriod.TotalHours}h",
                            ct: ct);

                        await _context.SaveChangesAsync(ct);
                        await transaction.CommitAsync(ct);
                        return;
                    }
                }

                order.Status = PaymentOrderStatuses.Paid;
                order.RawStatus = provider.Equals(PaymentProviders.VNPay, StringComparison.OrdinalIgnoreCase)
                    ? $"vnp_ResponseCode={providerResponseCode};vnp_TransactionStatus={providerTransactionStatus}"
                    : $"momo_resultCode={providerResponseCode}";
                order.PaidAtUtc = nowUtc;
                order.UpdatedAtUtc = nowUtc;
                order.ProviderTransactionId = providerTransactionId;
                order.ProviderRequestId = providerRequestId;
                order.ProviderResponseCode = providerResponseCode;
                order.ProviderTransactionStatus = providerTransactionStatus;
                order.PayType = payType;
                order.BankCode = bankCode;
                order.BankTransactionNo = bankTranNo;
                order.CardType = cardType;
                order.IpnPayloadJson = payloadJson;

                await TrySaveEventAsync(
                    provider: provider,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: order.Id,
                    signatureValid: true,
                    resultCode: providerResponseCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Processed,
                    message: "Payment confirmed via IPN - activating subscription",
                    ct: ct);

                await _context.SaveChangesAsync(ct);

                var sub = await _subscriptionService.ActivateFromPaidOrderAsync(order.Id, ct);
                order.ActivatedAtUtc = sub.StartsAtUtc;
                await _context.SaveChangesAsync(ct);

                await TrySaveEventAsync(
                    provider: provider,
                    eventType: PaymentEventTypes.GrantPremium,
                    eventKey: $"grant:{order.OrderCode}",
                    orderId: order.Id,
                    signatureValid: true,
                    resultCode: providerResponseCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Processed,
                    message: $"Subscription activated. SubId={sub.Id}, Ends={sub.EndsAtUtc:O}",
                    ct: ct);

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            });
        }

        private static string SerializeSafePayload(IDictionary<string, string> queryParams)
        {
            var safePayload = queryParams
                .Where(kv => !kv.Key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase)
                          && !kv.Key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase)
                          && !kv.Key.Equals("signature", StringComparison.OrdinalIgnoreCase)
                          && !kv.Key.Equals("sign", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            return JsonSerializer.Serialize(safePayload);
        }

        private async Task TrySaveEventAsync(
            string provider,
            string eventType,
            string eventKey,
            long? orderId,
            bool signatureValid,
            string? resultCode,
            string payloadJson,
            string processingStatus,
            string? message,
            CancellationToken ct)
        {
            var exists = await _context.PaymentEvents
                .AsNoTracking()
                .AnyAsync(e => e.Provider == provider
                            && e.EventType == eventType
                            && e.EventKey == eventKey, ct);

            if (exists)
            {
                return;
            }

            var evt = new PaymentEvent
            {
                Provider = provider,
                EventType = eventType,
                EventKey = eventKey,
                PaymentOrderId = orderId,
                SignatureValid = signatureValid,
                ResultCode = resultCode,
                PayloadJson = payloadJson,
                ProcessingStatus = processingStatus,
                ProcessingMessage = message,
                ReceivedAtUtc = DateTime.UtcNow,
                ProcessedAtUtc = DateTime.UtcNow
            };

            _context.PaymentEvents.Add(evt);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
                when (IsUniqueConstraintViolation(ex))
            {
                _context.Entry(evt).State = EntityState.Detached;
            }
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? string.Empty;
            return inner.Contains("2601", StringComparison.Ordinal)
                || inner.Contains("2627", StringComparison.Ordinal)
                || inner.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                || inner.Contains("unique index", StringComparison.OrdinalIgnoreCase);
        }
    }
}
