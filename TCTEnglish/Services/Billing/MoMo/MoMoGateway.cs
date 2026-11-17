using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Billing.MoMo
{
    public class MoMoGateway : IPaymentGateway
    {
        private readonly HttpClient _httpClient;
        private readonly MoMoOptions _options;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<MoMoGateway> _logger;

        public MoMoGateway(
            HttpClient httpClient,
            IOptions<MoMoOptions> options,
            IWebHostEnvironment environment,
            ILogger<MoMoGateway> logger)
        {
            _httpClient = httpClient;
            _options = options.Value.Normalize();
            _environment = environment;
            _logger = logger;
        }

        public string ProviderName => PaymentProviders.MoMo;

        public bool IsEnabled => _options.IsFullyConfigured() || IsMockMode;

        private bool IsMockMode =>
            _environment.IsDevelopment() && _options.Enabled && _options.MockModeEnabled;

        private string EffectiveSignatureSecret =>
            string.IsNullOrWhiteSpace(_options.SecretKey) ? _options.MockSecretKey : _options.SecretKey;

        private string EffectiveAccessKey =>
            string.IsNullOrWhiteSpace(_options.AccessKey) ? MoMoSignatureHelper.MockAccessKey : _options.AccessKey;

        public async Task<ProviderCheckoutResult> CreateCheckoutAsync(
            string orderCode,
            decimal amountVnd,
            string orderDescription,
            string clientIp,
            CancellationToken ct = default)
        {
            if (!IsEnabled)
            {
                return ProviderCheckoutResult.Fail(
                    "NOT_CONFIGURED",
                    "Cổng thanh toán MoMo chưa sẵn sàng.");
            }

            if (amountVnd < 1000m || amountVnd > 50000000m)
            {
                return ProviderCheckoutResult.Fail(
                    "INVALID_AMOUNT",
                    "Số tiền thanh toán MoMo không hợp lệ.");
            }

            if (IsMockMode)
            {
                return CreateMockCheckoutResult(orderCode, amountVnd);
            }

            var requestId = $"{orderCode}-{Guid.NewGuid():N}";
            var amount = Convert.ToInt64(decimal.Truncate(amountVnd), CultureInfo.InvariantCulture);

            var rawSignature = MoMoSignatureHelper.BuildCreateRawSignature(
                _options.AccessKey,
                amount,
                string.Empty,
                _options.IpnUrl,
                orderCode,
                orderDescription,
                _options.PartnerCode,
                _options.RedirectUrl,
                requestId,
                _options.RequestType);

            var signature = MoMoSignatureHelper.ComputeHmacSha256(EffectiveSignatureSecret, rawSignature);
            var request = new MoMoCreateRequest
            {
                PartnerCode = _options.PartnerCode,
                AccessKey = _options.AccessKey,
                RequestId = requestId,
                Amount = amount,
                OrderId = orderCode,
                OrderInfo = orderDescription,
                RedirectUrl = _options.RedirectUrl,
                IpnUrl = _options.IpnUrl,
                RequestType = _options.RequestType,
                ExtraData = string.Empty,
                Lang = _options.Lang,
                Signature = signature
            };

            var endpoint = _options.BaseUrl + _options.CreatePath;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsJsonAsync(endpoint, request, timeoutCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MoMo create payment failed. OrderCode={OrderCode}", orderCode);
                return ProviderCheckoutResult.Fail(
                    "MOMO_CREATE_FAILED",
                    "Không thể kết nối cổng MoMo lúc này.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "MoMo create returned non-success status {StatusCode}. OrderCode={OrderCode}",
                    response.StatusCode,
                    orderCode);

                return ProviderCheckoutResult.Fail(
                    "MOMO_CREATE_FAILED",
                    "MoMo từ chối tạo phiên thanh toán.");
            }

            MoMoCreateResponse? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<MoMoCreateResponse>(cancellationToken: timeoutCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MoMo create response parse error. OrderCode={OrderCode}", orderCode);
                return ProviderCheckoutResult.Fail(
                    "MOMO_RESPONSE_INVALID",
                    "Phản hồi từ MoMo không hợp lệ.");
            }

            if (payload == null)
            {
                return ProviderCheckoutResult.Fail(
                    "MOMO_RESPONSE_INVALID",
                    "Phản hồi từ MoMo không hợp lệ.");
            }

            if (payload.ResultCode != 0)
            {
                // MoMo's own wording is for the log, not for the learner.
                _logger.LogWarning(
                    "MoMo create rejected. OrderCode={OrderCode}, ResultCode={ResultCode}, Message={ProviderMessage}",
                    orderCode,
                    payload.ResultCode,
                    payload.Message);
                return ProviderCheckoutResult.Fail(
                    $"MOMO_{payload.ResultCode}",
                    "MoMo không thể tạo giao dịch lúc này.");
            }

            if (string.IsNullOrWhiteSpace(payload.PayUrl)
                && string.IsNullOrWhiteSpace(payload.DeepLink)
                && string.IsNullOrWhiteSpace(payload.QrCodeUrl))
            {
                return ProviderCheckoutResult.Fail(
                    "MOMO_RESPONSE_INVALID",
                    "MoMo không trả về thông tin thanh toán hợp lệ.");
            }

            var redirectUrl = $"/Billing/MoMoQr?orderCode={Uri.EscapeDataString(orderCode)}";
            return ProviderCheckoutResult.Ok(
                redirectUrl,
                payload.RequestId,
                payload.PayUrl,
                payload.DeepLink,
                payload.QrCodeUrl);
        }

        public Task<ProviderCallbackResult> ProcessReturnAsync(
            IDictionary<string, string> queryParams,
            CancellationToken ct = default)
        {
            if (!IsEnabled)
            {
                return Task.FromResult(new ProviderCallbackResult
                {
                    IsVerified = false,
                    IsPaid = false,
                    ErrorMessage = "MoMo gateway is not configured."
                });
            }

            var isVerified = MoMoSignatureHelper.VerifySignature(
                queryParams,
                EffectiveAccessKey,
                EffectiveSignatureSecret,
                out _,
                out _);
            queryParams.TryGetValue("orderId", out var orderCode);
            queryParams.TryGetValue("requestId", out var requestId);
            queryParams.TryGetValue("transId", out var transactionId);
            queryParams.TryGetValue("payType", out var payType);
            queryParams.TryGetValue("resultCode", out var resultCode);
            queryParams.TryGetValue("message", out var message);
            var isPaid = isVerified && string.Equals(resultCode, "0", StringComparison.Ordinal);

            return Task.FromResult(new ProviderCallbackResult
            {
                IsVerified = isVerified,
                IsPaid = isPaid,
                OrderCode = orderCode,
                ProviderRequestId = requestId,
                ProviderTransactionId = transactionId,
                PayType = payType,
                ProviderResponseCode = resultCode,
                ProviderTransactionStatus = resultCode,
                ProviderMessage = message
            });
        }

        public Task<ProviderIpnResult> ProcessIpnAsync(
            IDictionary<string, string> parameters,
            CancellationToken ct = default)
        {
            if (!IsEnabled)
            {
                return Task.FromResult(new ProviderIpnResult
                {
                    SignatureValid = false,
                    IsPaid = false,
                    ErrorMessage = "MoMo gateway is not configured."
                });
            }

            var signatureValid = MoMoSignatureHelper.VerifySignature(
                parameters,
                EffectiveAccessKey,
                EffectiveSignatureSecret,
                out _,
                out _);
            parameters.TryGetValue("orderId", out var orderCode);
            parameters.TryGetValue("requestId", out var requestId);
            parameters.TryGetValue("transId", out var transactionId);
            parameters.TryGetValue("payType", out var payType);
            parameters.TryGetValue("resultCode", out var resultCode);
            parameters.TryGetValue("message", out var message);

            var isPaid = signatureValid && string.Equals(resultCode, "0", StringComparison.Ordinal);

            return Task.FromResult(new ProviderIpnResult
            {
                SignatureValid = signatureValid,
                IsPaid = isPaid,
                OrderCode = orderCode,
                ProviderRequestId = requestId,
                ProviderTransactionId = transactionId,
                PayType = payType,
                ProviderResponseCode = resultCode,
                ProviderTransactionStatus = resultCode,
                ProviderMessage = message
            });
        }

        private static ProviderCheckoutResult CreateMockCheckoutResult(string orderCode, decimal amountVnd)
        {
            var requestId = $"MOMO-MOCK-{Guid.NewGuid():N}";
            var amount = Convert.ToInt64(decimal.Truncate(amountVnd), CultureInfo.InvariantCulture);
            var redirectUrl = $"/Billing/MoMoQr?orderCode={Uri.EscapeDataString(orderCode)}";
            var payUrl = redirectUrl;
            var deepLink = $"momo://mock-pay?orderId={Uri.EscapeDataString(orderCode)}";
            var qrPayload = $"MOMO_MOCK|orderId={orderCode}|amount={amount}";

            return ProviderCheckoutResult.Ok(
                redirectUrl,
                requestId,
                payUrl,
                deepLink,
                qrPayload);
        }
    }
}
