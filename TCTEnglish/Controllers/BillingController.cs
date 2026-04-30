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
        private readonly ILogger<BillingController> _logger;

        public BillingController(
            DbflashcardContext context,
            IBillingService billingService,
            IPremiumAccessService premiumAccessService,
            IIpnService ipnService,
            IPaymentProviderHealthService healthService,
            IOptions<MoMoOptions> moMoOptions,
            IWebHostEnvironment environment,
            ILogger<BillingController> logger)
        {
            _context = context;
            _billingService = billingService;
            _premiumAccessService = premiumAccessService;
            _ipnService = ipnService;
            _healthService = healthService;
            _moMoOptions = moMoOptions.Value.Normalize();
            _environment = environment;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("/Premium")]
        public async Task<IActionResult> Pricing()
        {
            var userId = GetCurrentUserId();
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

            var model = new PricingViewModel
            {
                Plans = plans,
                CurrentAccess = accessSnapshot,
                ErrorMessage = TempData["CheckoutError"] as string,
                ProviderReadiness = providerReadiness
            };

            return View(model);
        }

        [Authorize]
        [HttpPost("/Billing/Checkout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutRequestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["CheckoutError"] = "Thong tin dang ky khong hop le.";
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

                TempData["CheckoutError"] = "Cong thanh toan hien khong kha dung.";
                return RedirectToAction(nameof(Pricing));
            }

            return Redirect(result.RedirectUrl);
        }

        [Authorize]
        [HttpGet("/Billing/History")]
        public async Task<IActionResult> History()
        {
            var userId = GetCurrentUserId();
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
                TempData["CheckoutError"] = "Don hang da het han, vui long tao don moi.";
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

            var userId = GetCurrentUserId();
            var order = await _context.PaymentOrders
                .AsNoTracking()
                .Where(o => o.UserId == userId && o.OrderCode == orderCode)
                .Select(o => new
                {
                    o.Status,
                    o.ExpiresAtUtc,
                    Paid = o.Status == PaymentOrderStatuses.Paid
                })
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound();
            }

            return Json(new
            {
                status = order.Status,
                paid = order.Paid,
                expired = order.ExpiresAtUtc <= DateTime.UtcNow
            });
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
            var parameters = await ReadBodyAsDictionaryAsync(Request.Body);
            await _ipnService.ProcessMoMoIpnAsync(parameters);
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
            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                var firstIp = forwardedFor.Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(firstIp))
                {
                    return firstIp;
                }
            }

            return httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        }

        private static async Task<Dictionary<string, string>> ReadBodyAsDictionaryAsync(Stream body)
        {
            using var document = await JsonDocument.ParseAsync(body);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

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

        private sealed record VnPayIpnResponse(
            [property: JsonPropertyName("RspCode")] string RspCode,
            [property: JsonPropertyName("Message")] string Message);
    }
}
