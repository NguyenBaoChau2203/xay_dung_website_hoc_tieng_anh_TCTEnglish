using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing.PayOS;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Billing
{
    public class IpnService : IIpnService
    {
        private readonly DbflashcardContext _context;
        private readonly IPaymentGateway _vnpayGateway;
        private readonly IPaymentGateway? _momoGateway;
        private readonly IPaymentGateway? _payOsGateway;
        private readonly IPaymentConfirmationService _confirmationService;
        private readonly ILogger<IpnService> _logger;

        public IpnService(
            DbflashcardContext context,
            IEnumerable<IPaymentGateway> gateways,
            IPaymentConfirmationService confirmationService,
            ILogger<IpnService> logger)
        {
            _context = context;
            _vnpayGateway = gateways.First(g =>
                g.ProviderName.Equals(PaymentProviders.VNPay, StringComparison.OrdinalIgnoreCase));
            _momoGateway = gateways.FirstOrDefault(g =>
                g.ProviderName.Equals(PaymentProviders.MoMo, StringComparison.OrdinalIgnoreCase));
            _payOsGateway = gateways.FirstOrDefault(g =>
                g.ProviderName.Equals(PaymentProviders.PayOS, StringComparison.OrdinalIgnoreCase));
            _confirmationService = confirmationService;
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
                if (!PaymentOrderStateMachine.CanMarkFailedOrCancelled(order.Status, PaymentTransitionSource.ProviderCallback))
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
                        message: $"Failure callback ignored: order is '{order.Status}'",
                        ct: ct);
                    return IpnProcessingResult.Ok();
                }

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
                var confirmation = await _confirmationService.ConfirmPaidAsync(
                    new PaymentConfirmation(
                        OrderId: order.Id,
                        Provider: PaymentProviders.VNPay,
                        Source: PaymentTransitionSource.ProviderCallback,
                        VerifiedAmountVnd: vnpAmount / 100,
                        EventType: PaymentEventTypes.Ipn,
                        EventKey: eventKey,
                        PayloadJson: payloadJson,
                        ProviderTransactionId: txnNo,
                        ResponseCode: responseCode,
                        TransactionStatus: txnStatus,
                        BankCode: bankCode,
                        BankTranNo: bankTranNo,
                        CardType: cardType),
                    ct);

                return confirmation.Outcome switch
                {
                    PaymentConfirmationOutcome.Activated => IpnProcessingResult.Ok(),
                    PaymentConfirmationOutcome.SentToManualReview => IpnProcessingResult.Ok(),
                    PaymentConfirmationOutcome.AlreadyPaid => IpnProcessingResult.AlreadyConfirmed(),
                    PaymentConfirmationOutcome.RejectedState => IpnProcessingResult.AlreadyConfirmed(),
                    PaymentConfirmationOutcome.RejectedAmount => IpnProcessingResult.AmountMismatch(),
                    _ => IpnProcessingResult.OrderNotFound()
                };
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
                if (!PaymentOrderStateMachine.CanMarkFailedOrCancelled(order.Status, PaymentTransitionSource.ProviderCallback))
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
                        message: $"Failure callback ignored: order is '{order.Status}'",
                        ct: ct);
                    return;
                }

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

            await _confirmationService.ConfirmPaidAsync(
                new PaymentConfirmation(
                    OrderId: order.Id,
                    Provider: PaymentProviders.MoMo,
                    Source: PaymentTransitionSource.ProviderCallback,
                    VerifiedAmountVnd: momoAmount,
                    EventType: PaymentEventTypes.Ipn,
                    EventKey: eventKey,
                    PayloadJson: payloadJson,
                    ProviderTransactionId: transId,
                    ProviderRequestId: requestId,
                    ResponseCode: resultCode,
                    TransactionStatus: resultCode,
                    PayType: payType),
                ct);
        }

        public async Task ProcessPayOSWebhookAsync(
            IDictionary<string, string> parameters,
            CancellationToken ct = default)
        {
            if (_payOsGateway == null)
            {
                _logger.LogWarning("payOS webhook received but the gateway is unavailable.");
                return;
            }

            parameters.TryGetValue("data.orderCode", out var orderCodeText);
            parameters.TryGetValue("data.reference", out var reference);
            parameters.TryGetValue("data.paymentLinkId", out var paymentLinkId);
            parameters.TryGetValue("data.amount", out var amountText);
            parameters.TryGetValue("data.code", out var dataCode);

            var eventKey = BuildPayOSIpnEventKey(orderCodeText, string.IsNullOrWhiteSpace(reference) ? paymentLinkId : reference, dataCode);
            var payloadJson = SerializePayOSPayload(parameters);
            var ipnResult = await _payOsGateway.ProcessIpnAsync(parameters, ct);

            if (!ipnResult.SignatureValid)
            {
                await TrySaveEventAsync(
                    provider: PaymentProviders.PayOS,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: BuildInvalidSignatureAuditKey(eventKey),
                    orderId: null,
                    signatureValid: false,
                    resultCode: dataCode,
                    payloadJson: payloadJson,
                    processingStatus: PaymentEventProcessingStatuses.Failed,
                    message: "Invalid HMAC-SHA256 signature",
                    ct: ct);
                return;
            }

            var isDuplicate = await _context.PaymentEvents
                .AsNoTracking()
                .AnyAsync(e => e.Provider == PaymentProviders.PayOS
                            && e.EventType == PaymentEventTypes.Ipn
                            && e.EventKey == eventKey, ct);
            if (isDuplicate)
            {
                return;
            }

            async Task Record(long? orderId, string status, string message)
                => await TrySaveEventAsync(
                    provider: PaymentProviders.PayOS,
                    eventType: PaymentEventTypes.Ipn,
                    eventKey: eventKey,
                    orderId: orderId,
                    signatureValid: true,
                    resultCode: dataCode,
                    payloadJson: payloadJson,
                    processingStatus: status,
                    message: message,
                    ct: ct);

            if (!PayOSOrderCode.TryParse(orderCodeText, out _))
            {
                await Record(null, PaymentEventProcessingStatuses.Failed, "Missing or invalid orderCode");
                return;
            }

            var order = await _context.PaymentOrders
                .Include(o => o.Plan)
                .Where(o => o.Provider == PaymentProviders.PayOS && o.ProviderRequestId == orderCodeText)
                .OrderBy(o => o.Id)
                .FirstOrDefaultAsync(ct);
            if (order == null)
            {
                // Also what payOS's own "confirm webhook" test call lands on (a sample order code).
                await Record(null, PaymentEventProcessingStatuses.Failed, $"Order not found: payOS code {orderCodeText}");
                return;
            }

            if (!long.TryParse(amountText, out var payOsAmount))
            {
                await Record(order.Id, PaymentEventProcessingStatuses.Failed, $"Unparseable amount: {amountText}");
                return;
            }

            var expectedAmount = Convert.ToInt64(decimal.Truncate(order.AmountVnd));
            if (payOsAmount != expectedAmount)
            {
                await Record(order.Id, PaymentEventProcessingStatuses.Failed, $"Amount mismatch: expected {expectedAmount}, got {payOsAmount}");
                return;
            }

            if (order.Status == PaymentOrderStatuses.Paid)
            {
                await Record(order.Id, PaymentEventProcessingStatuses.Ignored, "Order already paid");
                return;
            }

            if (!ipnResult.IsPaid)
            {
                // payOS sends webhooks for received transfers; a signed one that is not a success changes nothing.
                await Record(order.Id, PaymentEventProcessingStatuses.Ignored, $"Webhook is not a success: code={dataCode}");
                return;
            }

            await _confirmationService.ConfirmPaidAsync(
                new PaymentConfirmation(
                    OrderId: order.Id,
                    Provider: PaymentProviders.PayOS,
                    Source: PaymentTransitionSource.ProviderCallback,
                    VerifiedAmountVnd: payOsAmount,
                    EventType: PaymentEventTypes.Ipn,
                    EventKey: eventKey,
                    PayloadJson: payloadJson,
                    ProviderTransactionId: ipnResult.ProviderTransactionId,
                    ProviderRequestId: orderCodeText,
                    ResponseCode: dataCode,
                    TransactionStatus: PayOSStatuses.Paid,
                    PayType: "bank_transfer"),
                ct);
        }

        internal static string BuildPayOSIpnEventKey(string? orderCode, string? referenceOrLink, string? code)
            => $"payos:{orderCode ?? "?"}:{(string.IsNullOrWhiteSpace(referenceOrLink) ? "?" : referenceOrLink)}:{code ?? "?"}";

        /// <summary>
        /// What is stored of a payOS webhook: no signature, bank account numbers cut to their last four digits.
        /// </summary>
        internal static string SerializePayOSPayload(IDictionary<string, string> parameters)
        {
            var safe = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in parameters)
            {
                if (pair.Key.Equals("signature", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                safe[pair.Key] = pair.Key.EndsWith("AccountNumber", StringComparison.OrdinalIgnoreCase)
                                 || pair.Key.EndsWith(".accountNumber", StringComparison.OrdinalIgnoreCase)
                    ? MaskAccountNumber(pair.Value)
                    : pair.Value;
            }

            return JsonSerializer.Serialize(safe);
        }

        internal static string MaskAccountNumber(string? value)
        {
            var digits = (value ?? string.Empty).Trim();
            if (digits.Length == 0)
            {
                return string.Empty;
            }

            return digits.Length <= 4 ? new string('*', digits.Length) : "****" + digits[^4..];
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
                when (RelationalUniqueConflict.IsUnique(ex))
            {
                // MariaDB "Duplicate entry" (1062), SQLite and the MySQL 1022 variant all land here.
                _context.Entry(evt).State = EntityState.Detached;
            }
        }
    }
}
