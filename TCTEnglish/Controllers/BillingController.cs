using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.MoMo;
using TCTEnglish.ViewModels.Billing;
using TCTVocabulary.Controllers;
using TCTVocabulary.Models;

namespace TCTEnglish.Controllers
{
    public class BillingController : BaseController
    {
        private readonly DbflashcardContext _context;
        private readonly IBillingService _billingService;
        private readonly IPremiumAccessService _premiumAccessService;
        private readonly IIpnService _ipnService;
        private readonly IPaymentProviderHealthService _healthService;
        private readonly MoMoOptions _moMoOptions;
        private readonly IWebHostEnvironment _environment;
        private readonly IPaymentQueryService _queryService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<BillingController> _logger;

        public BillingController(
            DbflashcardContext context,
            IBillingService billingService,
            IPremiumAccessService premiumAccessService,
            IIpnService ipnService,
            IPaymentProviderHealthService healthService,
            IOptions<MoMoOptions> moMoOptions,
            IWebHostEnvironment environment,
            IPaymentQueryService queryService,
            ISubscriptionService subscriptionService,
            ILogger<BillingController> logger)
        {
            _context = context;
            _billingService = billingService;
            _premiumAccessService = premiumAccessService;
            _ipnService = ipnService;
            _healthService = healthService;
            _moMoOptions = moMoOptions.Value.Normalize();
            _environment = environment;
            _queryService = queryService;
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("/Premium")]
        public async Task<IActionResult> Pricing()
        {
            var userId = GetCurrentUserId();
            await _subscriptionService.ExpireForUserAsync(userId);
            var plans = await _context.PremiumPlans
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.DisplayOrder)
                .Select(p => new PricingPlanViewModel
                {
                    Code = p.Code,
                    Name = p.Name,
                    Description = p.Description,
                    PriceVnd = p.PriceVnd,
                    DurationDays = p.DurationDays
                })
                .ToListAsync();

            var accessSnapshot = await _premiumAccessService.GetAccessSnapshotAsync(userId);
            var providerHealth = _healthService.GetProviderHealthStatus();
            var providerReadiness = providerHealth.ToDictionary(
                p => p.ProviderCode,
                p => p.Enabled && p.Configured);

            var model = PricingPageBuilder.Build(
                plans,
                accessSnapshot,
                providerReadiness,
                IsMoMoMockModeEnabled(),
                TempData["CheckoutError"] as string,
                DateTime.UtcNow);

            return View(model);
        }

        [Authorize]
        [HttpPost("/Billing/Checkout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["CheckoutError"] = "Thông tin đăng ký không hợp lệ.";
                return RedirectToAction(nameof(Pricing));
            }

            var userId = GetCurrentUserId();
            var clientIp = GetClientIp(HttpContext);

            var result = await _billingService.CreateCheckoutAsync(
                userId,
                model.PlanCode,
                model.Provider,
                clientIp);

            if (!result.Success)
            {
                TempData["CheckoutError"] = result.ErrorMessage;
                return RedirectToAction(nameof(Pricing));
            }

            if (string.IsNullOrWhiteSpace(result.RedirectUrl))
            {
                _logger.LogError(
                    "Checkout succeeded but RedirectUrl is null for order {OrderCode}",
                    result.OrderCode);

                TempData["CheckoutError"] = "Cổng thanh toán hiện không khả dụng.";
                return RedirectToAction(nameof(Pricing));
            }

            return Redirect(result.RedirectUrl);
        }

        [Authorize]
        [HttpGet("/Billing/History")]
        public async Task<IActionResult> History()
        {
            var userId = GetCurrentUserId();
            await _subscriptionService.ExpireForUserAsync(userId);
            var model = await _billingService.GetPaymentHistoryAsync(userId);
            return View(model);
        }

        [Authorize]
        [HttpGet("/Billing/MoMoQr")]
        public async Task<IActionResult> MoMoQr(string orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return NotFound();
            }

            var userId = GetCurrentUserId();
            var order = await _context.PaymentOrders
                .AsNoTracking()
                .Include(o => o.Plan)
                .Where(o => o.UserId == userId && o.OrderCode == orderCode)
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound();
            }

            if (!order.Provider.Equals(PaymentProviders.MoMo, StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            var model = new MoMoQrViewModel
            {
                OrderCode = order.OrderCode,
                PlanName = order.Plan?.Name ?? "Premium",
                AmountVnd = order.AmountVnd,
                ExpiresAtUtc = order.ExpiresAtUtc,
                ProviderPaymentUrl = order.ProviderPaymentUrl,
                ProviderDeepLink = order.ProviderDeepLink,
                ProviderQrCodePayload = order.ProviderQrCodePayload,
                IsExpired = order.ExpiresAtUtc <= DateTime.UtcNow,
                IsPaid = string.Equals(order.Status, PaymentOrderStatuses.Paid, StringComparison.OrdinalIgnoreCase),
                IsMockMode = IsMoMoMockModeEnabled()
            };

            return View(model);
        }

        [Authorize]
        [HttpPost("/Billing/MoMoMockConfirm")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoMoMockConfirm(string orderCode)
        {
            if (!IsMoMoMockModeEnabled())
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return NotFound();
            }

            var userId = GetCurrentUserId();
            var order = await _context.PaymentOrders
                .AsNoTracking()
                .Where(o => o.UserId == userId && o.OrderCode == orderCode)
                .FirstOrDefaultAsync();

            if (order == null || !order.Provider.Equals(PaymentProviders.MoMo, StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            if (order.ExpiresAtUtc <= DateTime.UtcNow)
            {
                TempData["CheckoutError"] = "Đơn hàng đã hết hạn, vui lòng tạo đơn mới.";
                return RedirectToAction(nameof(MoMoQr), new { orderCode });
            }

            var requestId = string.IsNullOrWhiteSpace(order.ProviderRequestId)
                ? $"MOMO-MOCK-{order.OrderCode}"
                : order.ProviderRequestId;

            var transId = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var partnerCode = string.IsNullOrWhiteSpace(_moMoOptions.PartnerCode)
                ? "MOMO"
                : _moMoOptions.PartnerCode;

            var ipn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["partnerCode"] = partnerCode,
                ["orderId"] = order.OrderCode,
                ["requestId"] = requestId,
                ["transId"] = transId,
                ["resultCode"] = "0",
                ["amount"] = Convert.ToInt64(decimal.Truncate(order.AmountVnd), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                ["payType"] = "qr",
                ["orderType"] = "momo_wallet",
                ["orderInfo"] = $"Premium payment {order.OrderCode}",
                ["message"] = "Successful.",
                ["responseTime"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
                ["extraData"] = string.Empty
            };

            var secret = string.IsNullOrWhiteSpace(_moMoOptions.SecretKey)
                ? _moMoOptions.MockSecretKey
                : _moMoOptions.SecretKey;
            var accessKey = string.IsNullOrWhiteSpace(_moMoOptions.AccessKey)
                ? MoMoSignatureHelper.MockAccessKey
                : _moMoOptions.AccessKey;
            var raw = MoMoSignatureHelper.BuildReturnOrIpnRawSignature(ipn, accessKey);
            ipn["signature"] = MoMoSignatureHelper.ComputeHmacSha256(secret, raw);

            await _ipnService.ProcessMoMoIpnAsync(ipn);

            return RedirectToAction(nameof(MoMoQr), new { orderCode });
        }

        [Authorize]
        [HttpGet("/Billing/OrderStatus")]
        public async Task<IActionResult> OrderStatus(string orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return NotFound();
            }

            var status = await BuildOrderStatusAsync(GetCurrentUserId(), orderCode, checkedWith: null);
            return status == null ? NotFound() : Json(status);
        }

        /// <summary>
        /// Asks the provider's own server about the buyer's order, at most once every 20 s (see
        /// <see cref="IPaymentQueryService"/>). Used by the "Kiểm tra lại ngay" button and by the order page's
        /// polling while it is waiting for confirmation. Owner only; anyone else gets 404.
        /// </summary>
        [Authorize]
        [HttpPost("/Billing/Order/{orderCode}/Refresh")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshOrder(string orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return NotFound();
            }

            var userId = GetCurrentUserId();
            var orderId = await _context.PaymentOrders
                .AsNoTracking()
                .Where(o => o.UserId == userId && o.OrderCode == orderCode)
                .Select(o => (long?)o.Id)
                .FirstOrDefaultAsync();

            if (orderId == null)
            {
                return NotFound();
            }

            var attempt = await _queryService.QueryAndConfirmAsync(orderId.Value, PaymentQueryTrigger.Refresh);

            if (!WantsJson())
            {
                return Redirect($"/Billing/Order/{Uri.EscapeDataString(orderCode)}");
            }

            var status = await BuildOrderStatusAsync(userId, orderCode, attempt.Status.ToString());
            return status == null ? NotFound() : Json(status);
        }

        private bool WantsJson()
        {
            return Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The fields the order page polls: the original three plus <c>stage</c> and <c>premiumEndsAt</c>.</summary>
        private async Task<object?> BuildOrderStatusAsync(int userId, string orderCode, string? checkedWith)
        {
            var order = await _context.PaymentOrders
                .AsNoTracking()
                .Where(o => o.UserId == userId && o.OrderCode == orderCode)
                .Select(o => new
                {
                    o.Id,
                    o.Status,
                    o.Provider,
                    o.ExpiresAtUtc,
                    o.ReturnPayloadJson
                })
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return null;
            }

            var nowUtc = DateTime.UtcNow;
            var paid = order.Status == PaymentOrderStatuses.Paid;
            var stage = PaymentOrderPresentation.ResolveStage(
                order.Status,
                order.ExpiresAtUtc,
                PaymentOrderPresentation.ReturnReportedSuccess(order.Provider, order.ReturnPayloadJson),
                nowUtc);

            string? premiumEndsAt = null;
            if (paid)
            {
                var endsAtUtc = await _context.UserSubscriptions
                    .AsNoTracking()
                    .Where(s => s.ActivatedByPaymentOrderId == order.Id)
                    .Select(s => (DateTime?)s.EndsAtUtc)
                    .FirstOrDefaultAsync();
                premiumEndsAt = endsAtUtc.HasValue
                    ? DateTime.SpecifyKind(endsAtUtc.Value, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture)
                    : null;
            }

            return new
            {
                status = order.Status,
                paid,
                expired = order.ExpiresAtUtc <= nowUtc,
                stage,
                premiumEndsAt,
                @checked = checkedWith
            };
        }

        [AllowAnonymous]
        [HttpGet("/api/billing/vnpay/ipn")]
        public async Task<IActionResult> VnPayIpn()
        {
            try
            {
                var queryParams = ExtractVnPayQuery(HttpContext.Request.Query);
                var result = await _ipnService.ProcessVnPayIpnAsync(queryParams);
                return VnPayIpnJson(result.RspCode, result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error while processing VNPAY IPN.");
                return VnPayIpnJson("99", "Unknown error");
            }
        }

        [AllowAnonymous]
        [HttpPost("/api/billing/momo/ipn")]
        public async Task<IActionResult> MoMoIpn()
        {
            var parameters = await TryReadMoMoIpnBodyAsync(Request.Body, HttpContext.RequestAborted);
            if (parameters != null)
            {
                await _ipnService.ProcessMoMoIpnAsync(parameters);
            }

            // Always 204: a body we cannot use will not become usable by MoMo retrying it.
            return NoContent();
        }

        [AllowAnonymous]
        [HttpGet("/Billing/VnPayReturn")]
        public async Task<IActionResult> VnPayReturn()
        {
            var queryParams = ExtractVnPayQuery(HttpContext.Request.Query);

            if (!queryParams.TryGetValue("vnp_TxnRef", out var orderCode)
                || string.IsNullOrWhiteSpace(orderCode))
            {
                return View("PaymentResult", new PaymentResultViewModel
                {
                    HasValidReturnSignature = false,
                    CanShowOrderSummary = false
                });
            }

            var model = await _billingService.GetVnPayReturnResultAsync(queryParams);
            return await RenderPaymentResultForOwnerAsync(model, orderCode);
        }

        [AllowAnonymous]
        [HttpGet("/Billing/MoMoReturn")]
        public async Task<IActionResult> MoMoReturn()
        {
            var queryParams = HttpContext.Request.Query
                .ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);

            if (!queryParams.TryGetValue("orderId", out var orderCode)
                || string.IsNullOrWhiteSpace(orderCode))
            {
                return RedirectToAction("Index", "Home");
            }

            var model = await _billingService.GetMoMoReturnResultAsync(queryParams);
            return await RenderPaymentResultForOwnerAsync(model, orderCode);
        }

        private async Task<IActionResult> RenderPaymentResultForOwnerAsync(
            PaymentResultViewModel? model,
            string orderCode)
        {
            if (model == null)
            {
                _logger.LogWarning("Payment return: order not found. OrderCode={OrderCode}", orderCode);
                return NotFound();
            }

            if (model.HasValidReturnSignature)
            {
                if (!TryGetCurrentUserId(out var currentUserId))
                {
                    return RedirectToAction("Login", "Account", new { returnUrl = "/Billing/History" });
                }

                if (model.OwnerUserId != currentUserId)
                {
                    _logger.LogWarning(
                        "Payment return rejected due to ownership mismatch. OrderCode={OrderCode}, CurrentUserId={CurrentUserId}, OwnerUserId={OwnerUserId}",
                        orderCode,
                        currentUserId,
                        model.OwnerUserId);
                    return NotFound();
                }
            }

            return View("PaymentResult", model);
        }

        private bool IsMoMoMockModeEnabled()
            => _environment.IsDevelopment() && _moMoOptions.Enabled && _moMoOptions.MockModeEnabled;

        private static IActionResult VnPayIpnJson(string rspCode, string message)
            => new JsonResult(new VnPayIpnResponse(rspCode, message));

        private static Dictionary<string, string> ExtractVnPayQuery(IQueryCollection query)
        {
            return query
                .Where(kv => kv.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);
        }

        private static string GetClientIp(HttpContext httpContext)
        {
            return BillingClientIp.Resolve(
                httpContext.Connection.RemoteIpAddress,
                httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault());
        }

        private const int MaxMoMoIpnBodyBytes = 64 * 1024;

        /// <summary>
        /// Reads at most 64 KB and returns the JSON object as strings, or null (after one warning
        /// line without the payload) when the body is too large, not JSON, or not an object.
        /// </summary>
        private async Task<Dictionary<string, string>?> TryReadMoMoIpnBodyAsync(Stream body, CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await body.ReadAsync(chunk.AsMemory(), ct)) > 0)
            {
                if (buffer.Length + read > MaxMoMoIpnBodyBytes)
                {
                    _logger.LogWarning("MoMo IPN ignored: body is larger than {LimitBytes} bytes.", MaxMoMoIpnBodyBytes);
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(buffer.ToArray());
            }
            catch (JsonException)
            {
                _logger.LogWarning("MoMo IPN ignored: body is not valid JSON ({Length} bytes).", buffer.Length);
                return null;
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogWarning("MoMo IPN ignored: body is not a JSON object.");
                    return null;
                }

                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    result[property.Name] = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                        JsonValueKind.Number => property.Value.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Null => string.Empty,
                        _ => property.Value.GetRawText()
                    };
                }

                return result;
            }
        }

        private sealed record VnPayIpnResponse(
            [property: JsonPropertyName("RspCode")] string RspCode,
            [property: JsonPropertyName("Message")] string Message);
    }
}
