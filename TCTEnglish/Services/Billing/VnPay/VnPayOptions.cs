using System;
using System.Collections.Generic;

namespace TCTEnglish.Services.Billing.VnPay
{
    /// <summary>
    /// Strongly-typed configuration for the VNPay payment gateway.
    /// Bind from "VNPay" configuration section.
    /// Secrets (TmnCode, HashSecret) must be supplied via User Secrets or environment variables —
    /// never committed to source control.
    /// </summary>
    public class VnPayOptions
    {
        public const string SectionName = "VNPay";
        public const string SandboxPaymentUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        public const string SandboxQueryDrUrl = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";

        private static readonly string[] PlaceholderTmnCodes =
        {
            "YOUR_SANDBOX_TMN_CODE",
            "YOUR_REAL_TMN_CODE",
            "TESTCODE"
        };

        private static readonly string[] PlaceholderHashSecrets =
        {
            "YOUR_SANDBOX_HASH_SECRET",
            "YOUR_REAL_HASH_SECRET",
            "TESTSECRETTESTSECRETTESTSECRET12"
        };

        /// <summary>Set to true only when TmnCode and HashSecret are provided.</summary>
        public bool Enabled { get; set; } = false;

        public string Version { get; set; } = "2.1.0";
        public string Command { get; set; } = "pay";

        /// <summary>Terminal code issued by VNPay. MUST be set via User Secrets or env var.</summary>
        public string TmnCode { get; set; } = string.Empty;

        /// <summary>HMAC-SHA512 secret key. MUST be set via User Secrets or env var. Never log.</summary>
        public string HashSecret { get; set; } = string.Empty;

        /// <summary>VNPay sandbox: https://sandbox.vnpayment.vn/paymentv2/vpcpay.html</summary>
        public string BaseUrl { get; set; } = SandboxPaymentUrl;

        /// <summary>Absolute URL VNPay redirects the browser to after payment.</summary>
        public string ReturnUrl { get; set; } = string.Empty;

        /// <summary>Absolute URL VNPay calls server-to-server (IPN).</summary>
        public string IpnUrl { get; set; } = string.Empty;

        /// <summary>Absolute URL for VNPay QueryDR API.</summary>
        public string QueryDrUrl { get; set; } = SandboxQueryDrUrl;

        /// <summary>Source IP address sent to VNPay QueryDR.</summary>
        public string QueryIpAddress { get; set; } = "127.0.0.1";

        /// <summary>QueryDR command value (default: querydr).</summary>
        public string QueryDrCommand { get; set; } = "querydr";

        public string Locale { get; set; } = "vn";
        public string Currency { get; set; } = "VND";
        public string OrderType { get; set; } = "other";

        public VnPayOptions Normalize()
        {
            return new VnPayOptions
            {
                Enabled = Enabled,
                Version = (Version ?? string.Empty).Trim(),
                Command = (Command ?? string.Empty).Trim(),
                TmnCode = (TmnCode ?? string.Empty).Trim(),
                HashSecret = (HashSecret ?? string.Empty).Trim(),
                BaseUrl = (BaseUrl ?? string.Empty).Trim(),
                ReturnUrl = (ReturnUrl ?? string.Empty).Trim(),
                IpnUrl = (IpnUrl ?? string.Empty).Trim(),
                QueryDrUrl = (QueryDrUrl ?? string.Empty).Trim(),
                QueryIpAddress = (QueryIpAddress ?? string.Empty).Trim(),
                QueryDrCommand = string.IsNullOrWhiteSpace(QueryDrCommand) ? "querydr" : QueryDrCommand.Trim(),
                Locale = (Locale ?? string.Empty).Trim(),
                Currency = (Currency ?? string.Empty).Trim(),
                OrderType = (OrderType ?? string.Empty).Trim()
            };
        }

        /// <summary>
        /// Returns true when all required fields are populated and the payment
        /// endpoint is a VNPay checkout URL, not an error page.
        /// </summary>
        public bool IsFullyConfigured()
            => GetConfigurationErrors().Count == 0;

        public IReadOnlyList<string> GetConfigurationErrors()
        {
            var errors = new List<string>();
            var normalized = Normalize();

            if (!normalized.Enabled)
                errors.Add("Enabled = false");

            if (string.IsNullOrWhiteSpace(normalized.TmnCode))
                errors.Add("TmnCode");
            else if (IsPlaceholderTmnCode(normalized.TmnCode))
                errors.Add("TmnCode appears to be a placeholder/test value");

            if (string.IsNullOrWhiteSpace(normalized.HashSecret))
                errors.Add("HashSecret");
            else if (IsPlaceholderHashSecret(normalized.HashSecret))
                errors.Add("HashSecret appears to be a placeholder/test value");

            if (!IsValidPaymentBaseUrl(normalized.BaseUrl))
                errors.Add("BaseUrl must be a HTTPS VNPay vpcpay.html payment endpoint");

            if (string.IsNullOrWhiteSpace(normalized.ReturnUrl) || !IsValidHttpsUrl(normalized.ReturnUrl))
                errors.Add("ReturnUrl must be absolute HTTPS URL");

            if (string.IsNullOrWhiteSpace(normalized.IpnUrl) || !IsValidHttpsUrl(normalized.IpnUrl))
                errors.Add("IpnUrl must be absolute HTTPS URL");

            return errors;
        }

        public static bool IsPlaceholderTmnCode(string? tmnCode)
            => IsPlaceholderValue(tmnCode, PlaceholderTmnCodes);

        public static bool IsPlaceholderHashSecret(string? hashSecret)
            => IsPlaceholderValue(hashSecret, PlaceholderHashSecrets);

        public static bool IsValidPaymentBaseUrl(string? baseUrl)
        {
            if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var uri))
                return false;

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(uri.Query))
                return false;

            var path = uri.AbsolutePath.TrimEnd('/');
            if (path.Contains("Error.html", StringComparison.OrdinalIgnoreCase))
                return false;

            return path.EndsWith("vpcpay.html", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsValidHttpsUrl(string? url)
        {
            return Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
                   && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPlaceholderValue(string? value, IEnumerable<string> placeholders)
        {
            var normalized = value?.Trim();
            if (string.IsNullOrEmpty(normalized))
                return false;

            foreach (var placeholder in placeholders)
            {
                if (string.Equals(normalized, placeholder, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
