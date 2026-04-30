using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TCTEnglish.Models;
using TCTEnglish.ViewModels.Billing;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Billing
{
    public class BillingService : IBillingService
    {
        private const int MaxPendingOrdersPerWindow = 5;
        private static readonly TimeSpan PendingWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan OrderExpiry = TimeSpan.FromMinutes(15);

        private readonly DbflashcardContext _context;
        private readonly IEnumerable<IPaymentGateway> _gateways;
        private readonly ILogger<BillingService> _logger;

        public BillingService(
            DbflashcardContext context,
            IEnumerable<IPaymentGateway> gateways,
            ILogger<BillingService> logger)
        {
            _context = context;
            _gateways = gateways;
            _logger = logger;
        }

        public async Task<CheckoutResult> CreateCheckoutAsync(
            int userId,
            string planCode,
            string provider,
            string clientIp,
            CancellationToken ct = default)
        {
            var plan = await _context.PremiumPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code == planCode, ct);

            if (plan == null)
            {
                return CheckoutResult.Fail("PLAN_NOT_FOUND", "Gói Premium không tồn tại.");
            }

            if (!plan.IsActive)
            {
                return CheckoutResult.Fail("PLAN_INACTIVE", "Gói Premium này hiện không khả dụng.");
            }

            var gateway = _gateways.FirstOrDefault(
                g => g.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase));

            if (gateway == null)
            {
                return CheckoutResult.Fail(
                    "PROVIDER_UNSUPPORTED",
                    $"Cổng thanh toán '{provider}' không được hỗ trợ.");
            }

            if (!gateway.IsEnabled)
            {
                return CheckoutResult.Fail(
                    "PROVIDER_NOT_CONFIGURED",
                    $"Cổng thanh toán '{provider}' chưa được cấu hình. Vui lòng liên hệ quản trị viên.");
            }

            var windowStart = DateTime.UtcNow.Subtract(PendingWindow);
            var pendingCount = await _context.PaymentOrders
                .CountAsync(o => o.UserId == userId
                                 && o.Status == PaymentOrderStatuses.Pending
                                 && o.CreatedAtUtc >= windowStart, ct);

            if (pendingCount >= MaxPendingOrdersPerWindow)
            {
                return CheckoutResult.Fail(
                    "RATE_LIMITED",
                    "Bạn đã tạo quá nhiều đơn thanh toán. Vui lòng thử lại sau vài phút.");
            }

            var orderCode = GenerateOrderCode();
            var nowUtc = DateTime.UtcNow;

            var order = new PaymentOrder
            {
                OrderCode = orderCode,
                UserId = userId,
                PlanId = plan.Id,
                Provider = gateway.ProviderName,
                AmountVnd = plan.PriceVnd,
                Currency = "VND",
                Status = PaymentOrderStatuses.Pending,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                ExpiresAtUtc = nowUtc.Add(OrderExpiry)
            };

            _context.PaymentOrders.Add(order);
            await _context.SaveChangesAsync(ct);

            await TrySaveCheckoutEventAsync(order.Id, orderCode, gateway.ProviderName, ct);

            var description = $"Thanh toan goi Premium {plan.Code} don hang {orderCode}";
            var gatewayResult = await gateway.CreateCheckoutAsync(
                orderCode,
                plan.PriceVnd,
                description,
                clientIp,
                ct);

            if (!gatewayResult.Success)
            {
                order.Status = PaymentOrderStatuses.Failed;
                order.FailureMessage = gatewayResult.ErrorMessage;
                order.UpdatedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);

                _logger.LogWarning(
                    "Gateway {Provider} checkout failed for order {OrderCode}: {ErrorCode} - {ErrorMessage}",
                    provider,
                    orderCode,
                    gatewayResult.ErrorCode,
                    gatewayResult.ErrorMessage);

                return CheckoutResult.Fail(
                    gatewayResult.ErrorCode ?? "GATEWAY_ERROR",
                    gatewayResult.ErrorMessage ?? "Không thể tạo phiên thanh toán.");
            }

            order.ProviderRequestId = gatewayResult.ProviderRequestId;
            order.ProviderPaymentUrl = gatewayResult.ProviderPaymentUrl;
            order.ProviderDeepLink = gatewayResult.ProviderDeepLink;
            order.ProviderQrCodePayload = gatewayResult.ProviderQrCodePayload;
            order.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            return CheckoutResult.Ok(orderCode, gatewayResult.RedirectUrl);
        }

        public async Task<PaymentOrder?> GetOrderByCodeAsync(
            string orderCode,
            CancellationToken ct = default)
        {
            return await _context.PaymentOrders
                .AsNoTracking()
                .Include(o => o.Plan)
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode, ct);
        }

        public async Task<PaymentHistoryViewModel> GetPaymentHistoryAsync(
            int userId,
            CancellationToken ct = default)
        {
            var orders = await _context.PaymentOrders
                .AsNoTracking()
                .Include(o => o.Plan)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAtUtc)
                .Select(o => new PaymentHistoryItemViewModel
                {
                    OrderCode = o.OrderCode,
                    PlanName = o.Plan.Name,
                    Provider = o.Provider,
                    AmountVnd = o.AmountVnd,
                    Status = o.Status,
                    CreatedAtUtc = o.CreatedAtUtc,
                    PaidAtUtc = o.PaidAtUtc
                })
                .ToListAsync(ct);

            return new PaymentHistoryViewModel { Orders = orders };
        }

        public async Task<PaymentResultViewModel?> GetVnPayReturnResultAsync(
            IDictionary<string, string> queryParams,
            CancellationToken ct = default)
        {
            return await GetProviderReturnResultAsync(
                PaymentProviders.VNPay,
                "vnp_TxnRef",
                "vnp_ResponseCode",
                "vnp_SecureHash",
                queryParams,
                ct);
        }

        public async Task<PaymentResultViewModel?> GetMoMoReturnResultAsync(
            IDictionary<string, string> queryParams,
            CancellationToken ct = default)
        {
            return await GetProviderReturnResultAsync(
                PaymentProviders.MoMo,
                "orderId",
                "resultCode",
                "signature",
                queryParams,
                ct);
        }

        public async Task<int> CleanupExpiredPendingOrdersAsync(CancellationToken ct = default)
        {
            var nowUtc = DateTime.UtcNow;
            var expiredOrders = await _context.PaymentOrders
                .Where(o => o.Status == PaymentOrderStatuses.Pending && o.ExpiresAtUtc <= nowUtc)
                .ToListAsync(ct);

            if (!expiredOrders.Any())
            {
                return 0;
            }

            foreach (var order in expiredOrders)
            {
                order.Status = PaymentOrderStatuses.Expired;
                order.FailureMessage = "Đã quá hạn thanh toán.";
            }

            await _context.SaveChangesAsync(ct);
            return expiredOrders.Count;
        }

        private async Task<PaymentResultViewModel?> GetProviderReturnResultAsync(
            string provider,
            string orderCodeKey,
            string responseCodeKey,
            string signatureKey,
            IDictionary<string, string> queryParams,
            CancellationToken ct)
        {
            var gateway = _gateways.FirstOrDefault(g =>
                g.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase));

            if (gateway == null)
            {
                _logger.LogWarning(
                    "{Provider} return received but the gateway is unavailable.",
                    provider);
                return CreateInvalidReturnResult();
            }

            var callbackResult = await gateway.ProcessReturnAsync(queryParams, ct);
            if (!callbackResult.IsVerified)
            {
                _logger.LogWarning(
                    "{Provider} return rejected because signature validation failed.",
                    provider);
                return CreateInvalidReturnResult();
            }

            queryParams.TryGetValue(orderCodeKey, out var orderCode);
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return CreateInvalidReturnResult();
            }

            var order = await _context.PaymentOrders
                .Include(o => o.Plan)
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode, ct);

            if (order == null)
            {
                return null;
            }

            if (!order.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "{Provider} return rejected due to provider mismatch. OrderCode={OrderCode}, OrderProvider={OrderProvider}",
                    provider,
                    orderCode,
                    order.Provider);
                return CreateInvalidReturnResult();
            }

            queryParams.TryGetValue(signatureKey, out var signature);
            queryParams.TryGetValue(responseCodeKey, out var responseCode);
            var returnEventKey = IpnService.BuildReturnEventKey(orderCode, responseCode, signature);
            var returnPayload = SerializeSafePayload(queryParams);

            order.ReturnPayloadJson = returnPayload;
            order.ProviderRequestId ??= callbackResult.ProviderRequestId;
            order.ProviderTransactionId ??= callbackResult.ProviderTransactionId;
            order.ProviderResponseCode ??= callbackResult.ProviderResponseCode;
            order.ProviderTransactionStatus ??= callbackResult.ProviderTransactionStatus;
            order.PayType ??= callbackResult.PayType;
            order.UpdatedAtUtc = DateTime.UtcNow;

            await TrySaveReturnEventAsync(
                provider,
                order.Id,
                returnEventKey,
                callbackResult.ProviderResponseCode,
                returnPayload,
                ct);

            await _context.SaveChangesAsync(ct);

            return new PaymentResultViewModel
            {
                OwnerUserId = order.UserId,
                OrderCode = order.OrderCode,
                PlanName = order.Plan?.Name ?? "Premium",
                AmountVnd = order.AmountVnd,
                OrderStatus = NormalizeStatus(order.Status),
                HasValidReturnSignature = true,
                CanShowOrderSummary = true,
                PaidAtUtc = order.PaidAtUtc
            };
        }

        private static string GenerateOrderCode()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
            var randomHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
            return $"TCT-{timestamp}-{randomHex}";
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

        private static string NormalizeStatus(string? status)
        {
            var normalizedStatus = (status ?? string.Empty).Trim().ToLowerInvariant();
            return normalizedStatus switch
            {
                PaymentOrderStatuses.Pending => PaymentOrderStatuses.Pending,
                PaymentOrderStatuses.Paid => PaymentOrderStatuses.Paid,
                PaymentOrderStatuses.Failed => PaymentOrderStatuses.Failed,
                PaymentOrderStatuses.Cancelled => PaymentOrderStatuses.Cancelled,
                PaymentOrderStatuses.Expired => PaymentOrderStatuses.Expired,
                PaymentOrderStatuses.Refunded => PaymentOrderStatuses.Refunded,
                PaymentOrderStatuses.PartiallyRefunded => PaymentOrderStatuses.PartiallyRefunded,
                PaymentOrderStatuses.ManualReview => PaymentOrderStatuses.ManualReview,
                _ => normalizedStatus
            };
        }

        private static PaymentResultViewModel CreateInvalidReturnResult()
        {
            return new PaymentResultViewModel
            {
                HasValidReturnSignature = false,
                CanShowOrderSummary = false
            };
        }

        private async Task TrySaveCheckoutEventAsync(
            long orderId,
            string orderCode,
            string provider,
            CancellationToken ct)
        {
            var eventKey = IpnService.BuildCheckoutEventKey(orderCode);
            var exists = await _context.PaymentEvents.AsNoTracking()
                .AnyAsync(e => e.Provider == provider
                            && e.EventType == PaymentEventTypes.CheckoutCreated
                            && e.EventKey == eventKey, ct);
            if (exists)
            {
                return;
            }

            _context.PaymentEvents.Add(new PaymentEvent
            {
                Provider = provider,
                EventType = PaymentEventTypes.CheckoutCreated,
                EventKey = eventKey,
                PaymentOrderId = orderId,
                SignatureValid = true,
                PayloadJson = "{}",
                ProcessingStatus = PaymentEventProcessingStatuses.Processed,
                ProcessingMessage = "Checkout session created",
                ReceivedAtUtc = DateTime.UtcNow,
                ProcessedAtUtc = DateTime.UtcNow
            });

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // race-condition duplicate, safe to ignore
            }
        }

        private async Task TrySaveReturnEventAsync(
            string provider,
            long orderId,
            string eventKey,
            string? responseCode,
            string payloadJson,
            CancellationToken ct)
        {
            var exists = await _context.PaymentEvents.AsNoTracking()
                .AnyAsync(e => e.Provider == provider
                            && e.EventType == PaymentEventTypes.Return
                            && e.EventKey == eventKey, ct);
            if (exists)
            {
                return;
            }

            _context.PaymentEvents.Add(new PaymentEvent
            {
                Provider = provider,
                EventType = PaymentEventTypes.Return,
                EventKey = eventKey,
                PaymentOrderId = orderId,
                SignatureValid = true,
                ResultCode = responseCode,
                PayloadJson = payloadJson,
                ProcessingStatus = PaymentEventProcessingStatuses.Processed,
                ProcessingMessage = "Return URL visited - display only, no activation",
                ReceivedAtUtc = DateTime.UtcNow,
                ProcessedAtUtc = DateTime.UtcNow
            });

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // race-condition duplicate, safe to ignore
            }
        }
    }
}
