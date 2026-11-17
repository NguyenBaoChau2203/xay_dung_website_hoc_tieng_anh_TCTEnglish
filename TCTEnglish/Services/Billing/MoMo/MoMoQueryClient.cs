using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Billing.MoMo
{
    public sealed class MoMoQueryRequest
    {
        [JsonPropertyName("partnerCode")]
        public string PartnerCode { get; set; } = string.Empty;

        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = string.Empty;

        [JsonPropertyName("orderId")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("lang")]
        public string Lang { get; set; } = "vi";

        [JsonPropertyName("signature")]
        public string Signature { get; set; } = string.Empty;
    }

    public sealed class MoMoQueryResponse
    {
        [JsonPropertyName("partnerCode")]
        public string? PartnerCode { get; set; }

        [JsonPropertyName("orderId")]
        public string? OrderId { get; set; }

        [JsonPropertyName("requestId")]
        public string? RequestId { get; set; }

        [JsonPropertyName("amount")]
        public long Amount { get; set; }

        [JsonPropertyName("transId")]
        public long TransId { get; set; }

        [JsonPropertyName("payType")]
        public string? PayType { get; set; }

        [JsonPropertyName("resultCode")]
        public int ResultCode { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    /// <summary>Asks MoMo's own server what happened to an order (the MoMo "query" API).</summary>
    public interface IMoMoQueryClient
    {
        /// <summary>
        /// Never throws. Anything that does not settle the order (not configured, mock mode, network
        /// error, bad body, an answer about another order, a still-open transaction) is
        /// <see cref="PaymentQueryState.Unknown"/>.
        /// </summary>
        Task<PaymentQueryResult> QueryOrderAsync(string orderCode, CancellationToken ct = default);
    }

    /// <summary>
    /// The request is the public MoMo v2 query: HMAC-SHA256 over
    /// <c>accessKey=…&amp;orderId=…&amp;partnerCode=…&amp;requestId=…</c>. It has <b>not</b> been verified against a
    /// live MoMo sandbox from this repository, so it only runs when the gateway is fully configured and never in
    /// local mock mode (where the mock button is the confirmation path).
    /// </summary>
    public sealed class MoMoQueryClient : IMoMoQueryClient
    {
        // Final failures of the MoMo v2 result-code table. Everything else (initiated 1000, processing 7000/7002,
        // authorised 9000, system errors…) is not final and stays Unknown.
        private static readonly HashSet<int> FinalFailureCodes = new() { 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1017 };

        private readonly HttpClient _httpClient;
        private readonly MoMoOptions _options;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<MoMoQueryClient> _logger;

        public MoMoQueryClient(
            HttpClient httpClient,
            IOptions<MoMoOptions> options,
            IWebHostEnvironment environment,
            ILogger<MoMoQueryClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value.Normalize();
            _environment = environment;
            _logger = logger;
        }

        private bool IsMockMode => _environment.IsDevelopment() && _options.Enabled && _options.MockModeEnabled;

        public async Task<PaymentQueryResult> QueryOrderAsync(string orderCode, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return PaymentQueryResult.Unknown("no order code");
            }

            if (IsMockMode)
            {
                return PaymentQueryResult.Unknown("mock mode");
            }

            if (!_options.IsFullyConfigured() || string.IsNullOrWhiteSpace(_options.QueryPath))
            {
                return PaymentQueryResult.Unknown("not configured");
            }

            var requestId = $"qry-{Guid.NewGuid():N}";
            var raw = $"accessKey={_options.AccessKey}&orderId={orderCode}&partnerCode={_options.PartnerCode}&requestId={requestId}";
            var request = new MoMoQueryRequest
            {
                PartnerCode = _options.PartnerCode,
                RequestId = requestId,
                OrderId = orderCode,
                Lang = _options.Lang,
                Signature = MoMoSignatureHelper.ComputeHmacSha256(_options.SecretKey, raw)
            };

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            MoMoQueryResponse? payload;
            try
            {
                using var response = await _httpClient.PostAsJsonAsync(
                    _options.BaseUrl + _options.QueryPath, request, timeoutCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "MoMo query returned status {StatusCode}. OrderCode={OrderCode}", response.StatusCode, orderCode);
                    return PaymentQueryResult.Unknown($"http {(int)response.StatusCode}");
                }

                payload = await response.Content.ReadFromJsonAsync<MoMoQueryResponse>(cancellationToken: timeoutCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Timeout (our own linked token), network error or a body that is not JSON.
                _logger.LogWarning(ex, "MoMo query failed. OrderCode={OrderCode}", orderCode);
                return PaymentQueryResult.Unknown("query failed");
            }

            if (payload == null
                || !string.Equals(payload.OrderId, orderCode, StringComparison.Ordinal)
                || !string.Equals(payload.PartnerCode, _options.PartnerCode, StringComparison.Ordinal))
            {
                _logger.LogWarning("MoMo query answered for a different order or partner. OrderCode={OrderCode}", orderCode);
                return PaymentQueryResult.Unknown("answer does not match the order");
            }

            var code = payload.ResultCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var state = payload.ResultCode == 0
                ? PaymentQueryState.Success
                : FinalFailureCodes.Contains(payload.ResultCode) ? PaymentQueryState.Failed : PaymentQueryState.Unknown;

            return new PaymentQueryResult(
                state,
                AmountVnd: state == PaymentQueryState.Success ? payload.Amount : null,
                TransactionId: payload.TransId > 0 ? payload.TransId.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
                ResponseCode: code,
                TransactionStatus: code,
                PayType: payload.PayType,
                Message: payload.Message);
        }
    }
}
