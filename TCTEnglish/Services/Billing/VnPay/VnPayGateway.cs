using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Billing.VnPay
{
    /// <summary>
    /// Production VNPay payment gateway implementation.
    /// Secrets are injected via <see cref="VnPayOptions"/>  — never logged or exposed.
    /// </summary>
    public class VnPayGateway : IPaymentGateway
    {
        private static readonly TimeSpan DefaultOrderWindow = TimeSpan.FromMinutes(15);

        private readonly VnPayOptions _opts;
        private readonly ILogger<VnPayGateway> _logger;
        private readonly IHostEnvironment? _environment;

        public VnPayGateway(
            IOptions<VnPayOptions> options,
            ILogger<VnPayGateway> logger,
            IHostEnvironment? environment = null)
        {
            _opts = options.Value.Normalize();
            _logger = logger;
            _environment = environment;
        }

        public string ProviderName => PaymentProviders.VNPay;

        public bool IsEnabled => _opts.IsFullyConfigured();

        // ───────────────────────────────────────────────────────────────────────
        //  Checkout
        // ───────────────────────────────────────────────────────────────────────

        public Task<ProviderCheckoutResult> CreateCheckoutAsync(
            string orderCode,
            decimal amountVnd,
            string orderDescription,
            string clientIp,
            CancellationToken ct = default)
        {
            if (!IsEnabled)
            {
                _logger.LogWarning(
                    "VNPay CreateCheckoutAsync called but gateway is not configured. OrderCode={OrderCode}, ConfigErrors={ConfigErrors}",
                    orderCode,
                    string.Join(", ", _opts.GetConfigurationErrors()));
                // Intentionally vague — do not reveal which field is missing
                return Task.FromResult(ProviderCheckoutResult.Fail(
                    "NOT_CONFIGURED",
                    "Cổng thanh toán VNPay chưa được cấu hình. Vui lòng liên hệ quản trị viên."));
            }

            try
            {
                var nowUtc = DateTime.UtcNow;
                var createDate = VnPaySignatureHelper.ToVnPayDate(nowUtc);
                var expireDate = VnPaySignatureHelper.ToVnPayDate(nowUtc.Add(DefaultOrderWindow));

                // VNPay requires VND amount multiplied by 100.
                var amountInVnd = decimal.Round(amountVnd, 0, MidpointRounding.AwayFromZero);
                var amountForVnPay = (amountInVnd * 100m).ToString("0", CultureInfo.InvariantCulture);

                var parameters = new Dictionary<string, string>
                {
                    ["vnp_Version"]    = _opts.Version,
                    ["vnp_Command"]    = _opts.Command,
                    ["vnp_TmnCode"]    = _opts.TmnCode,
                    ["vnp_Amount"]     = amountForVnPay,
                    ["vnp_CreateDate"] = createDate,
                    ["vnp_CurrCode"]   = _opts.Currency,
                    ["vnp_IpAddr"]     = clientIp,
                    ["vnp_Locale"]     = _opts.Locale,
                    ["vnp_OrderInfo"]  = orderDescription,
                    ["vnp_OrderType"]  = _opts.OrderType,
                    ["vnp_ReturnUrl"]  = _opts.ReturnUrl,
                    ["vnp_TxnRef"]     = orderCode,
                    ["vnp_ExpireDate"] = expireDate,
                };

                LogDevelopmentCheckoutFields(
                    _opts.BaseUrl,
                    _opts.TmnCode,
                    _opts.ReturnUrl,
                    _opts.IpnUrl,
                    amountVnd,
                    orderCode);

                // Hash secret is used only inside the helper — never leaked to result or logs
                var redirectUrl = VnPaySignatureHelper.BuildPaymentUrl(
                    _opts.BaseUrl, _opts.HashSecret, parameters);

                _logger.LogInformation(
                    "VNPay checkout URL created. OrderCode={OrderCode}", orderCode);

                return Task.FromResult(ProviderCheckoutResult.Ok(redirectUrl));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "VNPay CreateCheckoutAsync failed unexpectedly. OrderCode={OrderCode}", orderCode);
                return Task.FromResult(ProviderCheckoutResult.Fail(
                    "GATEWAY_ERROR",
                    "Không thể tạo phiên thanh toán VNPay. Vui lòng thử lại."));
            }
        }

        // ───────────────────────────────────────────────────────────────────────
        //  Return URL callback
        // ───────────────────────────────────────────────────────────────────────

        public Task<ProviderCallbackResult> ProcessReturnAsync(
            IDictionary<string, string> queryParams,
            CancellationToken ct = default)
        {
            if (!IsEnabled)
                return Task.FromResult(new ProviderCallbackResult
                {
                    IsVerified = false, IsPaid = false,
                    ErrorMessage = "VNPay gateway is not configured."
                });

            queryParams.TryGetValue("vnp_SecureHash", out var receivedHash);
            var isVerified = !string.IsNullOrEmpty(receivedHash)
                             && VnPaySignatureHelper.Verify(_opts.HashSecret, queryParams, receivedHash);

            queryParams.TryGetValue("vnp_ResponseCode", out var responseCode);
            queryParams.TryGetValue("vnp_TransactionNo", out var txnId);
            queryParams.TryGetValue("vnp_TransactionStatus", out var txnStatus);
            var isPaid = isVerified && responseCode == "00" && txnStatus == "00";

            return Task.FromResult(new ProviderCallbackResult
            {
                IsVerified = isVerified,
                IsPaid = isPaid,
                ProviderTransactionId = txnId,
                ProviderResponseCode = responseCode,
                ProviderTransactionStatus = txnStatus
            });
        }

        // ───────────────────────────────────────────────────────────────────────
        //  IPN (server-to-server)
        // ───────────────────────────────────────────────────────────────────────

        public Task<ProviderIpnResult> ProcessIpnAsync(
            IDictionary<string, string> parameters,
            CancellationToken ct = default)
        {
            if (!IsEnabled)
                return Task.FromResult(new ProviderIpnResult
                {
                    SignatureValid = false, IsPaid = false,
                    ErrorMessage = "VNPay gateway is not configured."
                });

            parameters.TryGetValue("vnp_SecureHash", out var receivedHash);
            var signatureValid = !string.IsNullOrEmpty(receivedHash)
                                 && VnPaySignatureHelper.Verify(_opts.HashSecret, parameters, receivedHash);

            parameters.TryGetValue("vnp_TxnRef", out var orderCode);
            parameters.TryGetValue("vnp_ResponseCode", out var responseCode);
            parameters.TryGetValue("vnp_TransactionNo", out var txnId);
            parameters.TryGetValue("vnp_TransactionStatus", out var txnStatus);
            var isPaid = signatureValid && responseCode == "00" && txnStatus == "00";

            return Task.FromResult(new ProviderIpnResult
            {
                SignatureValid = signatureValid,
                IsPaid = isPaid,
                OrderCode = orderCode,
                ProviderTransactionId = txnId,
                ProviderResponseCode = responseCode,
                ProviderTransactionStatus = txnStatus
            });
        }

        private void LogDevelopmentCheckoutFields(
            string baseUrl,
            string tmnCode,
            string returnUrl,
            string ipnUrl,
            decimal amountVnd,
            string txnRef)
        {
            if (_environment?.IsDevelopment() != true)
                return;

            _logger.LogInformation(
                "VNPay checkout fields. Provider={Provider}, BaseUrl={BaseUrl}, TmnCode={TmnCode}, ReturnUrl={ReturnUrl}, IpnUrl={IpnUrl}, Amount={Amount}, TxnRef={TxnRef}",
                ProviderName,
                baseUrl,
                MaskTmnCode(tmnCode),
                returnUrl,
                string.IsNullOrWhiteSpace(ipnUrl) ? "(not configured)" : ipnUrl,
                amountVnd,
                txnRef);
        }

        private static string MaskTmnCode(string tmnCode)
        {
            if (string.IsNullOrWhiteSpace(tmnCode))
                return "(empty)";

            return tmnCode.Length <= 4
                ? "****"
                : $"{tmnCode[..2]}****{tmnCode[^2..]}";
        }
    }
}
