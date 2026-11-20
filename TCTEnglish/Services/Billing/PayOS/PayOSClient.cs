using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Billing.PayOS
{
    public sealed record PayOSCreateLinkRequest(
        long OrderCode,
        long Amount,
        string Description,
        string ReturnUrl,
        string CancelUrl,
        long? ExpiredAtUnixSeconds,
        string? ItemName);

    public sealed record PayOSCreateLinkResult(
        bool Success,
        string? CheckoutUrl = null,
        string? QrCode = null,
        string? PaymentLinkId = null,
        string? ErrorCode = null,
        string? ErrorMessage = null,
        bool IsDuplicateOrderCode = false);

    /// <summary>What payOS says about one payment link (the "get payment link information" API).</summary>
    public sealed record PayOSPaymentInfo(
        long OrderCode,
        long Amount,
        long AmountPaid,
        string Status,
        string? PaymentLinkId,
        string? Reference);

    public static class PayOSStatuses
    {
        public const string Pending = "PENDING";
        public const string Processing = "PROCESSING";
        public const string Paid = "PAID";
        public const string Cancelled = "CANCELLED";
        public const string Expired = "EXPIRED";
    }

    /// <summary>The three payOS API calls the gateway, the query service and the cleanup need.</summary>
    public interface IPayOSClient
    {
        Task<PayOSCreateLinkResult> CreatePaymentLinkAsync(PayOSCreateLinkRequest request, CancellationToken ct = default);

        /// <summary>Null when payOS could not be reached or did not answer for this order.</summary>
        Task<PayOSPaymentInfo?> GetPaymentAsync(long orderCode, CancellationToken ct = default);

        /// <summary>Best effort; true when payOS accepted the cancellation.</summary>
        Task<bool> CancelAsync(long orderCode, string reason, CancellationToken ct = default);
    }

    /// <summary>
    /// Typed <see cref="HttpClient"/> for the payOS API. Never logs a key, a signature or a response body.
    /// </summary>
    public sealed class PayOSClient : IPayOSClient
    {
        private const string SuccessCode = "00";
        private const string DuplicateOrderCode = "231";

        private readonly HttpClient _httpClient;
        private readonly PayOSOptions _options;
        private readonly ILogger<PayOSClient> _logger;

        public PayOSClient(HttpClient httpClient, IOptions<PayOSOptions> options, ILogger<PayOSClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value.Normalize();
            _logger = logger;
        }

        public async Task<PayOSCreateLinkResult> CreatePaymentLinkAsync(
            PayOSCreateLinkRequest request, CancellationToken ct = default)
        {
            var body = new JsonObject
            {
                ["orderCode"] = request.OrderCode,
                ["amount"] = request.Amount,
                ["description"] = request.Description,
                ["cancelUrl"] = request.CancelUrl,
                ["returnUrl"] = request.ReturnUrl,
                ["signature"] = PayOSSignature.ForPaymentRequest(
                    request.Amount, request.CancelUrl, request.Description, request.OrderCode,
                    request.ReturnUrl, _options.ChecksumKey)
            };
            if (request.ExpiredAtUnixSeconds is { } expiredAt)
            {
                body["expiredAt"] = expiredAt;
            }

            if (!string.IsNullOrWhiteSpace(request.ItemName))
            {
                body["items"] = new JsonArray(new JsonObject
                {
                    ["name"] = request.ItemName,
                    ["quantity"] = 1,
                    ["price"] = request.Amount
                });
            }

            var (document, failure) = await SendAsync(HttpMethod.Post, "/v2/payment-requests", body, ct);
            if (document == null)
            {
                return new PayOSCreateLinkResult(false, ErrorCode: "PAYOS_UNAVAILABLE", ErrorMessage: failure);
            }

            using (document)
            {
                var root = document.RootElement;
                var code = ReadString(root, "code");
                if (code != SuccessCode)
                {
                    var desc = ReadString(root, "desc");
                    _logger.LogWarning(
                        "payOS create link rejected. OrderCode={OrderCode}, Code={Code}", request.OrderCode, code);
                    var duplicate = code == DuplicateOrderCode
                                    || (desc?.Contains("tồn tại", StringComparison.OrdinalIgnoreCase) ?? false);
                    return new PayOSCreateLinkResult(
                        false,
                        ErrorCode: $"PAYOS_{code ?? "ERROR"}",
                        ErrorMessage: "payOS không thể tạo giao dịch lúc này.",
                        IsDuplicateOrderCode: duplicate);
                }

                if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                {
                    return new PayOSCreateLinkResult(
                        false, ErrorCode: "PAYOS_RESPONSE_INVALID", ErrorMessage: "Phản hồi từ payOS không hợp lệ.");
                }

                var checkoutUrl = ReadString(data, "checkoutUrl");
                if (string.IsNullOrWhiteSpace(checkoutUrl)
                    || !Uri.TryCreate(checkoutUrl, UriKind.Absolute, out var checkoutUri)
                    || !string.Equals(checkoutUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    return new PayOSCreateLinkResult(
                        false, ErrorCode: "PAYOS_RESPONSE_INVALID", ErrorMessage: "payOS không trả về trang thanh toán hợp lệ.");
                }

                return new PayOSCreateLinkResult(
                    true,
                    CheckoutUrl: checkoutUrl,
                    QrCode: ReadString(data, "qrCode"),
                    PaymentLinkId: ReadString(data, "paymentLinkId"));
            }
        }

        public async Task<PayOSPaymentInfo?> GetPaymentAsync(long orderCode, CancellationToken ct = default)
        {
            var (document, _) = await SendAsync(
                HttpMethod.Get, $"/v2/payment-requests/{orderCode.ToString(CultureInfo.InvariantCulture)}", null, ct);
            if (document == null)
            {
                return null;
            }

            using (document)
            {
                var root = document.RootElement;
                if (ReadString(root, "code") != SuccessCode
                    || !root.TryGetProperty("data", out var data)
                    || data.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var answeredFor = ReadLong(data, "orderCode");
                if (answeredFor != orderCode)
                {
                    _logger.LogWarning("payOS answered for a different order than the one asked. OrderCode={OrderCode}", orderCode);
                    return null;
                }

                return new PayOSPaymentInfo(
                    answeredFor.Value,
                    ReadLong(data, "amount") ?? 0,
                    ReadLong(data, "amountPaid") ?? 0,
                    (ReadString(data, "status") ?? string.Empty).Trim().ToUpperInvariant(),
                    ReadString(data, "id") ?? ReadString(data, "paymentLinkId"),
                    ReadFirstReference(data));
            }
        }

        public async Task<bool> CancelAsync(long orderCode, string reason, CancellationToken ct = default)
        {
            var body = new JsonObject { ["cancellationReason"] = reason };
            var (document, _) = await SendAsync(
                HttpMethod.Post,
                $"/v2/payment-requests/{orderCode.ToString(CultureInfo.InvariantCulture)}/cancel",
                body,
                ct);
            if (document == null)
            {
                return false;
            }

            using (document)
            {
                return ReadString(document.RootElement, "code") == SuccessCode;
            }
        }

        private async Task<(JsonDocument? Document, string? Failure)> SendAsync(
            HttpMethod method, string path, JsonObject? body, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(method, _options.BaseUrl + path);
            request.Headers.TryAddWithoutValidation("x-client-id", _options.ClientId);
            request.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);
            if (body != null)
            {
                request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            try
            {
                using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("payOS returned status {StatusCode} for {Method} {Path}.", (int)response.StatusCode, method, path);
                    return (null, "payOS từ chối yêu cầu.");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
                return (await JsonDocument.ParseAsync(stream, cancellationToken: timeoutCts.Token), null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
            {
                // The message of these exceptions can carry the URL; it holds no key. The body is never logged.
                _logger.LogWarning(ex, "payOS call failed. {Method} {Path}", method, path);
                return (null, "Không thể kết nối payOS lúc này.");
            }
        }

        private static string? ReadString(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null
            };
        }

        private static long? ReadLong(JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            {
                return number;
            }

            return value.ValueKind == JsonValueKind.String
                   && long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }

        private static string? ReadFirstReference(JsonElement data)
        {
            if (data.TryGetProperty("transactions", out var transactions)
                && transactions.ValueKind == JsonValueKind.Array)
            {
                foreach (var transaction in transactions.EnumerateArray())
                {
                    var reference = ReadString(transaction, "reference");
                    if (!string.IsNullOrWhiteSpace(reference))
                    {
                        return reference;
                    }
                }
            }

            return null;
        }
    }
}
