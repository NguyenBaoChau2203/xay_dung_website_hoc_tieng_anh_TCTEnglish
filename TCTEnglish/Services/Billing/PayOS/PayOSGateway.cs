using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Billing.PayOS
{
    /// <summary>
    /// payOS (bank transfer by QR) behind <see cref="IPaymentGateway"/>.
    ///
    /// The buyer is sent to payOS's hosted page. Neither the return/cancel query nor anything else the browser
    /// brings confirms an order: the return asks the payOS API, the webhook is checked by its signature, and
    /// both end in the single confirmation path.
    /// </summary>
    public sealed class PayOSGateway : IPaymentGateway
    {
        /// <summary>Same as the order's own 15 minutes (<c>BillingService</c>), so the link dies with the order.</summary>
        private static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);

        private readonly IPayOSClient _client;
        private readonly PayOSOptions _options;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<PayOSGateway> _logger;

        public PayOSGateway(
            IPayOSClient client,
            IOptions<PayOSOptions> options,
            IWebHostEnvironment environment,
            ILogger<PayOSGateway> logger)
        {
            _client = client;
            _options = options.Value.Normalize();
            _environment = environment;
            _logger = logger;
        }

        public string ProviderName => PaymentProviders.PayOS;

        public bool IsEnabled => IsMockMode || _options.IsFullyConfigured(_environment.IsDevelopment());

        private bool IsMockMode => _options.IsMockActive(_environment.IsDevelopment());

        private string EffectiveChecksumKey => _options.ResolveChecksumKey(IsMockMode);

        public async Task<ProviderCheckoutResult> CreateCheckoutAsync(
            string orderCode,
            decimal amountVnd,
            string orderDescription,
            string clientIp,
            CancellationToken ct = default)
        {
            if (!IsEnabled)
            {
                return ProviderCheckoutResult.Fail("NOT_CONFIGURED", "Cổng thanh toán payOS chưa sẵn sàng.");
            }

            if (amountVnd < 1000m || amountVnd > 50000000m || amountVnd != decimal.Truncate(amountVnd))
            {
                return ProviderCheckoutResult.Fail("INVALID_AMOUNT", "Số tiền thanh toán payOS không hợp lệ.");
            }

            var amount = Convert.ToInt64(amountVnd, CultureInfo.InvariantCulture);

            if (IsMockMode)
            {
                var mockCode = PayOSOrderCode.Generate(DateTime.UtcNow);
                var mockUrl = $"/Billing/PayOSMock?orderCode={Uri.EscapeDataString(orderCode)}";
                return ProviderCheckoutResult.Ok(
                    mockUrl,
                    mockCode.ToString(CultureInfo.InvariantCulture),
                    mockUrl,
                    null,
                    $"PAYOS_MOCK|orderCode={mockCode}|amount={amount}");
            }

            var returnUrl = $"{_options.PublicBaseUrl}{_options.ReturnPath}?ref={Uri.EscapeDataString(orderCode)}";
            var cancelUrl = $"{_options.PublicBaseUrl}{_options.CancelPath}?ref={Uri.EscapeDataString(orderCode)}";
            var expiredAt = DateTimeOffset.UtcNow.Add(LinkLifetime).ToUnixTimeSeconds();
            var itemName = orderDescription.Length <= 100 ? orderDescription : orderDescription[..100];

            // A taken code is rare (15 digits, time + random); payOS reports it and we try once with a new one.
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var payOsCode = PayOSOrderCode.Generate(DateTime.UtcNow);
                var result = await _client.CreatePaymentLinkAsync(
                    new PayOSCreateLinkRequest(
                        payOsCode,
                        amount,
                        PayOSOrderCode.Description(payOsCode),
                        returnUrl,
                        cancelUrl,
                        expiredAt,
                        itemName),
                    ct);

                if (result.Success)
                {
                    return ProviderCheckoutResult.Ok(
                        result.CheckoutUrl!,
                        payOsCode.ToString(CultureInfo.InvariantCulture),
                        result.CheckoutUrl,
                        null,
                        result.QrCode);
                }

                if (result.IsDuplicateOrderCode && attempt == 0)
                {
                    _logger.LogWarning("payOS order code was already taken; generating another. OrderCode={OrderCode}", orderCode);
                    continue;
                }

                return ProviderCheckoutResult.Fail(
                    result.ErrorCode ?? "PAYOS_CREATE_FAILED",
                    result.ErrorMessage ?? "Không thể tạo phiên thanh toán payOS.");
            }

            return ProviderCheckoutResult.Fail("PAYOS_CREATE_FAILED", "Không thể tạo phiên thanh toán payOS.");
        }

        /// <summary>
        /// The return and cancel pages carry an unsigned query (<c>code</c>, <c>id</c>, <c>cancel</c>, <c>status</c>,
        /// <c>orderCode</c>). Only the numeric <c>orderCode</c> is used, to ask the payOS API what really happened.
        /// </summary>
        public async Task<ProviderCallbackResult> ProcessReturnAsync(
            IDictionary<string, string> queryParams,
            CancellationToken ct = default)
        {
            if (!IsEnabled)
            {
                return new ProviderCallbackResult { IsVerified = false, ErrorMessage = "payOS gateway is not configured." };
            }

            queryParams.TryGetValue("orderCode", out var rawCode);
            if (!PayOSOrderCode.TryParse(rawCode, out var payOsCode))
            {
                return new ProviderCallbackResult { IsVerified = false, ErrorMessage = "Missing or invalid orderCode." };
            }

            var codeText = payOsCode.ToString(CultureInfo.InvariantCulture);

            if (IsMockMode)
            {
                // Local fake page only: there is no API to ask, so the cancel flag of the mock link is believed
                // (never "paid": a mock payment arrives as a signed webhook).
                queryParams.TryGetValue("cancel", out var cancel);
                queryParams.TryGetValue("status", out var mockStatus);
                var cancelled = string.Equals(cancel, "true", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(mockStatus, PayOSStatuses.Cancelled, StringComparison.OrdinalIgnoreCase);
                return new ProviderCallbackResult
                {
                    IsVerified = true,
                    IsPaid = false,
                    ProviderRequestId = codeText,
                    ProviderResponseCode = "00",
                    ProviderTransactionStatus = cancelled ? PayOSStatuses.Cancelled : PayOSStatuses.Pending
                };
            }

            var info = await _client.GetPaymentAsync(payOsCode, ct);
            if (info == null)
            {
                return new ProviderCallbackResult
                {
                    IsVerified = false,
                    ProviderRequestId = codeText,
                    ErrorMessage = "payOS did not answer for this order."
                };
            }

            var paid = info.Status == PayOSStatuses.Paid && info.Amount > 0 && info.AmountPaid == info.Amount;
            return new ProviderCallbackResult
            {
                IsVerified = true,
                IsPaid = paid,
                ProviderRequestId = codeText,
                ProviderTransactionId = info.Reference,
                ProviderResponseCode = "00",
                ProviderTransactionStatus = info.Status
            };
        }

        /// <summary>
        /// <paramref name="parameters"/> is the flattened webhook: <c>code</c>, <c>desc</c>, <c>success</c>,
        /// <c>signature</c> and every field of <c>data</c> as <c>data.&lt;name&gt;</c>.
        /// </summary>
        public Task<ProviderIpnResult> ProcessIpnAsync(
            IDictionary<string, string> parameters,
            CancellationToken ct = default)
        {
            if (!IsEnabled)
            {
                return Task.FromResult(new ProviderIpnResult
                {
                    SignatureValid = false,
                    ErrorMessage = "payOS gateway is not configured."
                });
            }

            var data = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var pair in parameters)
            {
                if (pair.Key.StartsWith("data.", StringComparison.Ordinal))
                {
                    data[pair.Key["data.".Length..]] = pair.Value;
                }
            }

            parameters.TryGetValue("signature", out var signature);
            var valid = data.Count > 0
                        && !string.IsNullOrWhiteSpace(signature)
                        && PayOSSignature.Verify(PayOSSignature.ForData(data, EffectiveChecksumKey), signature);

            parameters.TryGetValue("code", out var topCode);
            parameters.TryGetValue("success", out var success);
            data.TryGetValue("code", out var dataCode);
            data.TryGetValue("orderCode", out var orderCode);
            data.TryGetValue("reference", out var reference);
            data.TryGetValue("desc", out var message);

            var paid = valid
                       && topCode == "00"
                       && dataCode == "00"
                       && !string.Equals(success, "false", StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(new ProviderIpnResult
            {
                SignatureValid = valid,
                IsPaid = paid,
                ProviderRequestId = orderCode,
                ProviderTransactionId = string.IsNullOrWhiteSpace(reference) ? null : reference,
                ProviderResponseCode = dataCode,
                ProviderTransactionStatus = paid ? PayOSStatuses.Paid : dataCode,
                ProviderMessage = message
            });
        }
    }
}
