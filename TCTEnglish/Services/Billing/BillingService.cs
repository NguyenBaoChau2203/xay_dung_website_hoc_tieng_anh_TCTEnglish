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

        // Striped gates (a fixed number, so nothing grows with the number of buyers).
        private const int CheckoutGateCount = 256;
        private static readonly SemaphoreSlim[] CheckoutGates = CreateCheckoutGates();

        private static SemaphoreSlim[] CreateCheckoutGates()
        {
            var gates = new SemaphoreSlim[CheckoutGateCount];
            for (var i = 0; i < gates.Length; i++)
            {
                gates[i] = new SemaphoreSlim(1, 1);
            }

            return gates;
        }

        private static int CheckoutGateIndex(int userId, int planId, string provider)
        {
            var hash = HashCode.Combine(userId, planId, provider.ToLowerInvariant());
            return (int)((uint)hash % CheckoutGateCount);
        }

        private readonly DbflashcardContext _context;
        private readonly IEnumerable<IPaymentGateway> _gateways;
        private readonly IPaymentQueryService _queryService;
        private readonly ILogger<BillingService> _logger;

        public BillingService(
            DbflashcardContext context,
            IEnumerable<IPaymentGateway> gateways,
            IPaymentQueryService queryService,
            ILogger<BillingService> logger)
        {
            _context = context;
            _gateways = gateways;
            _queryService = queryService;
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
                    "Cổng thanh toán này không được hỗ trợ.");
            }

            if (!gateway.IsEnabled)
            {
                // Learners are not told which setting is missing; operators see it on the admin health page.
                return CheckoutResult.Fail(
                    "PROVIDER_NOT_CONFIGURED",
                    "Cổng thanh toán hiện không khả dụng.");
            }

            var account = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new { u.Role })
                .FirstOrDefaultAsync(ct);
            var accountRole = Roles.Normalize(account?.Role);
            var hasAnySubscription = accountRole == Roles.Premium
                && await _context.UserSubscriptions.AsNoTracking().AnyAsync(s => s.UserId == userId, ct);
            if (PremiumAccessRules.IsPermanent(accountRole, hasAnySubscription))
            {
                return CheckoutResult.Fail(
                    "ALREADY_PERMANENT",
                    accountRole == Roles.Admin
                        ? "Tài khoản quản trị đã có sẵn quyền Premium."
                        : "Tài khoản của bạn đã có quyền Premium không thời hạn.");
            }

            // One checkout at a time per user, plan and provider: two tabs (or a double click) that arrive together
            // must not both pass the "open order" check below and create two pending orders. The gate is in
            // process; a second app instance is covered by the 15-minute expiry and the rate limit.
            var gate = CheckoutGates[CheckoutGateIndex(userId, plan.Id, gateway.ProviderName)];
            await gate.WaitAsync(ct);
            try
            {
                return await CreateOrResumeOrderAsync(userId, plan, gateway, provider, clientIp, ct);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<CheckoutResult> CreateOrResumeOrderAsync(
            int userId,
            PremiumPlan plan,
            IPaymentGateway gateway,
            string provider,
            string clientIp,
            CancellationToken ct)
        {
            // A second click, or a second tab, resumes the order that is still open instead of piling up new
            // ones (and does not count against the rate limit).
            var nowForReuse = DateTime.UtcNow;
            var openOrder = await _context.PaymentOrders
                .AsNoTracking()
                .Where(o => o.UserId == userId
                            && o.PlanId == plan.Id
                            && o.Provider == gateway.ProviderName
                            && o.Status == PaymentOrderStatuses.Pending
                            && o.ExpiresAtUtc > nowForReuse)
                .OrderByDescending(o => o.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);
            if (openOrder != null)
            {
                var resumeUrl = BuildResumeUrl(openOrder);
                if (resumeUrl != null)
                {
                    return CheckoutResult.Ok(openOrder.OrderCode, resumeUrl);
                }
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
            order.ProviderPaymentUrl = gatewayResult.ProviderPaymentUrl
                ?? StorableVnPayUrl(gateway.ProviderName, gatewayResult.RedirectUrl);
            order.ProviderDeepLink = gatewayResult.ProviderDeepLink;
            order.ProviderQrCodePayload = gatewayResult.ProviderQrCodePayload;
            order.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            // MoMo has no page of its own to send the buyer to: the order page shows the QR code and follows the order.
            var redirectUrl = gateway.ProviderName.Equals(PaymentProviders.MoMo, StringComparison.OrdinalIgnoreCase)
                ? BuildMoMoOrderUrl(orderCode)
                : gatewayResult.RedirectUrl;
            return CheckoutResult.Ok(orderCode, redirectUrl);
        }

        /// <summary>
        /// Where a buyer who already has this order open goes. MoMo: the QR page of the order. VNPay: the stored
        /// payment URL (older orders have none and are not resumed).
        /// </summary>
        private static string? BuildResumeUrl(PaymentOrder order)
        {
            if (order.Provider.Equals(PaymentProviders.MoMo, StringComparison.OrdinalIgnoreCase))
            {
                return BuildMoMoOrderUrl(order.OrderCode);
            }

            return string.IsNullOrWhiteSpace(order.ProviderPaymentUrl) ? null : order.ProviderPaymentUrl;
        }

        internal static string BuildMoMoOrderUrl(string orderCode)
            => $"/Billing/Order/{Uri.EscapeDataString(orderCode)}";

        /// <summary>The VNPay redirect URL is kept so the order can be resumed; it fits the 1000-character column.</summary>
        private static string? StorableVnPayUrl(string provider, string? redirectUrl)
        {
            return provider.Equals(PaymentProviders.VNPay, StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(redirectUrl)
                   && redirectUrl.Length <= 1000
                ? redirectUrl
                : null;
        }

        public async Task<OrderPageViewModel?> GetOrderPageAsync(
            int userId,
            string orderCode,
            bool isMockMode,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return null;
            }

            // Owner only: anyone else gets the same answer as for an order that does not exist.
            var order = await _context.PaymentOrders
                .AsNoTracking()
                .Include(o => o.Plan)
                .FirstOrDefaultAsync(o => o.UserId == userId && o.OrderCode == orderCode, ct);
            if (order == null)
            {
                return null;
            }

            DateTime? premiumEndsAtUtc = null;
            if (order.Status == PaymentOrderStatuses.Paid)
            {
                premiumEndsAtUtc = await _context.UserSubscriptions
                    .AsNoTracking()
                    .Where(s => s.ActivatedByPaymentOrderId == order.Id)
                    .Select(s => (DateTime?)s.EndsAtUtc)
                    .FirstOrDefaultAsync(ct);
            }

            return OrderPageBuilder.Build(order, premiumEndsAtUtc, isMockMode, DateTime.UtcNow);
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

        public Task<PaymentHistoryViewModel> GetPaymentHistoryAsync(
            int userId,
            CancellationToken ct = default)
            => GetPaymentHistoryAsync(userId, 1, ct);

        public async Task<PaymentHistoryViewModel> GetPaymentHistoryAsync(
            int userId,
            int page,
            CancellationToken ct = default)
        {
            var query = _context.PaymentOrders
                .AsNoTracking()
                .Where(o => o.UserId == userId);

            var total = await query.CountAsync(ct);
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PaymentHistoryViewModel.PageSize));
            var current = Math.Clamp(page, 1, pages);

            var orders = await query
                .OrderByDescending(o => o.CreatedAtUtc)
                .ThenByDescending(o => o.Id)
                .Skip((current - 1) * PaymentHistoryViewModel.PageSize)
                .Take(PaymentHistoryViewModel.PageSize)
                .Select(o => new PaymentHistoryItemViewModel
                {
                    OrderCode = o.OrderCode,
                    PlanName = o.Plan.Name,
                    Provider = o.Provider,
                    AmountVnd = o.AmountVnd,
                    Status = o.Status,
                    CreatedAtUtc = o.CreatedAtUtc,
                    PaidAtUtc = o.PaidAtUtc,
                    ExpiresAtUtc = o.ExpiresAtUtc,
                    BankCode = o.BankCode,
                    // Only open orders need the stored return to tell "đang xác nhận" from "chờ thanh toán".
                    ReturnPayloadJson = o.Status == PaymentOrderStatuses.Pending || o.Status == PaymentOrderStatuses.Expired
                        ? o.ReturnPayloadJson
                        : null
                })
                .ToListAsync(ct);

            var activatedFrom = await _context.UserSubscriptions
                .AsNoTracking()
                .Where(s => s.UserId == userId
                            && s.Status == SubscriptionStatuses.Active
                            && s.EndsAtUtc > DateTime.UtcNow
                            && s.ActivatedByPaymentOrderId != null)
                .OrderByDescending(s => s.EndsAtUtc)
                .Select(s => s.ActivatedByPaymentOrder!.OrderCode)
                .FirstOrDefaultAsync(ct);

            return new PaymentHistoryViewModel
            {
                Orders = orders,
                Page = current,
                TotalCount = total,
                ActivatedFromOrderCode = activatedFrom
            };
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

            var expired = 0;
            foreach (var order in expiredOrders)
            {
                if (!PaymentOrderStateMachine.CanExpire(order.Status))
                {
                    continue;
                }

                order.Status = PaymentOrderStatuses.Expired;
                order.FailureMessage = "Đã quá hạn thanh toán.";
                expired++;
            }

            await _context.SaveChangesAsync(ct);
            return expired;
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

            // The browser's return never confirms anything by itself. When the provider says the buyer paid,
            // ask the provider's own server once (throttled); only a verified answer with the right amount
            // confirms the order, through the single confirmation service. This also works on localhost,
            // where the provider cannot reach our IPN URL.
            var returnReportedSuccess = callbackResult.IsPaid;
            if (returnReportedSuccess)
            {
                var attempt = await _queryService.QueryAndConfirmAsync(order.Id, PaymentQueryTrigger.Return, ct);
                _logger.LogInformation(
                    "{Provider} return query for {OrderCode}: {Status}", provider, order.OrderCode, attempt.Status);
            }

            // Read the order again: the confirmation (if any) changed it in its own transaction.
            var current = await _context.PaymentOrders
                .AsNoTracking()
                .Include(o => o.Plan)
                .FirstAsync(o => o.Id == order.Id, ct);

            return new PaymentResultViewModel
            {
                OwnerUserId = current.UserId,
                OrderCode = current.OrderCode,
                PlanName = current.Plan?.Name ?? "Premium",
                AmountVnd = current.AmountVnd,
                OrderStatus = NormalizeStatus(current.Status),
                HasValidReturnSignature = true,
                CanShowOrderSummary = true,
                PaidAtUtc = current.PaidAtUtc,
                ReturnReportedSuccess = returnReportedSuccess
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

            var checkoutEvent = new PaymentEvent
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
            };
            _context.PaymentEvents.Add(checkoutEvent);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (RelationalUniqueConflict.IsUnique(ex))
            {
                // Race-condition duplicate: forget the failed row so later saves do not retry it.
                _context.Entry(checkoutEvent).State = EntityState.Detached;
            }
            catch (DbUpdateException)
            {
                _context.Entry(checkoutEvent).State = EntityState.Detached;
                throw;
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

            var returnEvent = new PaymentEvent
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
            };
            _context.PaymentEvents.Add(returnEvent);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (RelationalUniqueConflict.IsUnique(ex))
            {
                // Race-condition duplicate: forget the failed row so later saves do not retry it.
                _context.Entry(returnEvent).State = EntityState.Detached;
            }
            catch (DbUpdateException)
            {
                _context.Entry(returnEvent).State = EntityState.Detached;
                throw;
            }
        }
    }
}
