using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Billing.VnPay
{
    public class VnPayQueryClient : IVnPayQueryClient
    {
        private readonly HttpClient _httpClient;
        private readonly VnPayOptions _options;
        private readonly ILogger<VnPayQueryClient> _logger;

        public VnPayQueryClient(
            HttpClient httpClient,
            IOptions<VnPayOptions> options,
            ILogger<VnPayQueryClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value.Normalize();
            _logger = logger;
        }

        public async Task<VnPayQueryResult?> QueryOrderAsync(
            string orderCode,
            DateTime transactionCreatedAtUtc,
            string? providerTransactionNo = null,
            CancellationToken ct = default)
        {
            if (!CanQuery())
            {
                return null;
            }

            var requestId = $"qdr-{Guid.NewGuid():N}";
            var createDate = VnPaySignatureHelper.ToVnPayDate(DateTime.UtcNow);
            var transactionDate = VnPaySignatureHelper.ToVnPayDate(transactionCreatedAtUtc);
            var ipAddress = string.IsNullOrWhiteSpace(_options.QueryIpAddress)
                ? "127.0.0.1"
                : _options.QueryIpAddress.Trim();
            var orderInfo = $"QueryDR {orderCode}";

            var secureHash = BuildQueryRequestHash(
                requestId,
                _options.Version,
                _options.QueryDrCommand,
                _options.TmnCode,
                orderCode,
                transactionDate,
                createDate,
                ipAddress,
                orderInfo,
                _options.HashSecret);

            var request = new Dictionary<string, string?>
            {
                ["vnp_RequestId"] = requestId,
                ["vnp_Version"] = _options.Version,
                ["vnp_Command"] = _options.QueryDrCommand,
                ["vnp_TmnCode"] = _options.TmnCode,
                ["vnp_TxnRef"] = orderCode,
                ["vnp_OrderInfo"] = orderInfo,
                ["vnp_TransactionNo"] = providerTransactionNo,
                ["vnp_TransactionDate"] = transactionDate,
                ["vnp_CreateDate"] = createDate,
                ["vnp_IpAddr"] = ipAddress,
                ["vnp_SecureHash"] = secureHash
            };

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsJsonAsync(_options.QueryDrUrl, request, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VNPay QueryDR HTTP request failed. OrderCode={OrderCode}", orderCode);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "VNPay QueryDR returned non-success status {StatusCode}. OrderCode={OrderCode}",
                    response.StatusCode,
                    orderCode);
                return null;
            }

            Dictionary<string, string> payload;
            try
            {
                payload = await ReadStringDictionaryAsync(response, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VNPay QueryDR response parse failed. OrderCode={OrderCode}", orderCode);
                return null;
            }

            if (!VerifyQueryResponseSignature(payload))
            {
                _logger.LogWarning("VNPay QueryDR invalid response signature. OrderCode={OrderCode}", orderCode);
                return null;
            }

            payload.TryGetValue("vnp_ResponseCode", out var responseCode);
            payload.TryGetValue("vnp_TransactionStatus", out var transactionStatus);
            payload.TryGetValue("vnp_TransactionNo", out var transactionNo);
            payload.TryGetValue("vnp_BankCode", out var bankCode);

            return new VnPayQueryResult
            {
                IsSuccess = string.Equals(responseCode, "00", StringComparison.Ordinal)
                    && string.Equals(transactionStatus, "00", StringComparison.Ordinal),
                IsCancelled = string.Equals(transactionStatus, "02", StringComparison.Ordinal)
                    || string.Equals(responseCode, "24", StringComparison.Ordinal),
                ResponseCode = responseCode,
                TransactionNo = transactionNo,
                TransactionStatus = transactionStatus,
                BankCode = bankCode
            };
        }

        private bool CanQuery()
        {
            return _options.Enabled
                   && !string.IsNullOrWhiteSpace(_options.TmnCode)
                   && !string.IsNullOrWhiteSpace(_options.HashSecret)
                   && VnPayOptions.IsValidHttpsUrl(_options.QueryDrUrl);
        }

        private static string BuildQueryRequestHash(
            string requestId,
            string version,
            string command,
            string tmnCode,
            string txnRef,
            string transactionDate,
            string createDate,
            string ipAddress,
            string orderInfo,
            string secret)
        {
            var raw = string.Join("|", new[]
            {
                requestId,
                version,
                command,
                tmnCode,
                txnRef,
                transactionDate,
                createDate,
                ipAddress,
                orderInfo
            });

            return VnPaySignatureHelper.ComputeHmac512(secret, raw);
        }

        private bool VerifyQueryResponseSignature(IDictionary<string, string> payload)
        {
            if (!payload.TryGetValue("vnp_SecureHash", out var receivedHash)
                || string.IsNullOrWhiteSpace(receivedHash))
            {
                return false;
            }

            var raw = string.Join("|", new[]
            {
                GetOrEmpty(payload, "vnp_ResponseId"),
                GetOrEmpty(payload, "vnp_Command"),
                GetOrEmpty(payload, "vnp_ResponseCode"),
                GetOrEmpty(payload, "vnp_Message"),
                GetOrEmpty(payload, "vnp_TmnCode"),
                GetOrEmpty(payload, "vnp_TxnRef"),
                GetOrEmpty(payload, "vnp_Amount"),
                GetOrEmpty(payload, "vnp_BankCode"),
                GetOrEmpty(payload, "vnp_PayDate"),
                GetOrEmpty(payload, "vnp_TransactionNo"),
                GetOrEmpty(payload, "vnp_TransactionType"),
                GetOrEmpty(payload, "vnp_TransactionStatus"),
                GetOrEmpty(payload, "vnp_OrderInfo"),
                GetOrEmpty(payload, "vnp_PromotionCode"),
                GetOrEmpty(payload, "vnp_PromotionAmount")
            });

            var computedHash = VnPaySignatureHelper.ComputeHmac512(_options.HashSecret, raw);
            return string.Equals(computedHash, receivedHash.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetOrEmpty(IDictionary<string, string> payload, string key)
            => payload.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;

        private static async Task<Dictionary<string, string>> ReadStringDictionaryAsync(
            HttpResponseMessage response,
            CancellationToken ct)
        {
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
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
                    JsonValueKind.Null => string.Empty,
                    _ => property.Value.GetRawText()
                };
            }

            return result;
        }
    }
}
