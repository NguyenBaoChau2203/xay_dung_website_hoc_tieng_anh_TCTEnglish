using System;
using System.Collections.Generic;

namespace TCTEnglish.Services.Billing.MoMo
{
    public class MoMoOptions
    {
        public const string SectionName = "MoMo";
        public const string SandboxBaseUrl = "https://test-payment.momo.vn";
        public const string ProductionBaseUrl = "https://payment.momo.vn";

        private static readonly string[] PlaceholderValues =
        {
            "YOUR_PARTNER_CODE",
            "YOUR_ACCESS_KEY",
            "YOUR_SECRET_KEY",
            "CHANGE_ME",
            "TEST"
        };

        public bool Enabled { get; set; }
        public string PartnerCode { get; set; } = string.Empty;
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = SandboxBaseUrl;
        public string CreatePath { get; set; } = "/v2/gateway/api/create";
        public string QueryPath { get; set; } = "/v2/gateway/api/query";
        public string RedirectUrl { get; set; } = string.Empty;
        public string IpnUrl { get; set; } = string.Empty;
        public string RequestType { get; set; } = "captureWallet";
        public string Lang { get; set; } = "vi";
        public int TimeoutSeconds { get; set; } = 35;
        public bool MockModeEnabled { get; set; }
        public string MockSecretKey { get; set; } = "momo-local-mock-secret";

        public MoMoOptions Normalize()
        {
            return new MoMoOptions
            {
                Enabled = Enabled,
                PartnerCode = (PartnerCode ?? string.Empty).Trim(),
                AccessKey = (AccessKey ?? string.Empty).Trim(),
                SecretKey = (SecretKey ?? string.Empty).Trim(),
                BaseUrl = (BaseUrl ?? string.Empty).Trim().TrimEnd('/'),
                CreatePath = NormalizePath(CreatePath),
                QueryPath = NormalizePath(QueryPath),
                RedirectUrl = (RedirectUrl ?? string.Empty).Trim(),
                IpnUrl = (IpnUrl ?? string.Empty).Trim(),
                RequestType = string.IsNullOrWhiteSpace(RequestType) ? "captureWallet" : RequestType.Trim(),
                Lang = string.IsNullOrWhiteSpace(Lang) ? "vi" : Lang.Trim(),
                TimeoutSeconds = TimeoutSeconds < 30 ? 30 : TimeoutSeconds,
                MockModeEnabled = MockModeEnabled,
                MockSecretKey = string.IsNullOrWhiteSpace(MockSecretKey)
                    ? "momo-local-mock-secret"
                    : MockSecretKey.Trim()
            };
        }

        public bool IsFullyConfigured()
            => GetConfigurationErrors().Count == 0;

        public IReadOnlyList<string> GetConfigurationErrors()
        {
            var normalized = Normalize();
            var errors = new List<string>();

            if (!normalized.Enabled)
                errors.Add("Enabled = false");

            if (string.IsNullOrWhiteSpace(normalized.PartnerCode))
                errors.Add("PartnerCode");
            else if (IsPlaceholderValue(normalized.PartnerCode))
                errors.Add("PartnerCode appears to be a placeholder/test value");

            if (string.IsNullOrWhiteSpace(normalized.AccessKey))
                errors.Add("AccessKey");
            else if (IsPlaceholderValue(normalized.AccessKey))
                errors.Add("AccessKey appears to be a placeholder/test value");

            if (string.IsNullOrWhiteSpace(normalized.SecretKey))
                errors.Add("SecretKey");
            else if (IsPlaceholderValue(normalized.SecretKey))
                errors.Add("SecretKey appears to be a placeholder/test value");

            if (!IsValidHttpsBaseUrl(normalized.BaseUrl))
                errors.Add("BaseUrl must be absolute HTTPS URL");

            if (string.IsNullOrWhiteSpace(normalized.CreatePath) || !normalized.CreatePath.StartsWith('/'))
                errors.Add("CreatePath");

            if (string.IsNullOrWhiteSpace(normalized.RedirectUrl) || !IsValidHttpsUrl(normalized.RedirectUrl))
                errors.Add("RedirectUrl must be absolute HTTPS URL");

            if (string.IsNullOrWhiteSpace(normalized.IpnUrl) || !IsValidHttpsUrl(normalized.IpnUrl))
                errors.Add("IpnUrl must be absolute HTTPS URL");

            if (!string.Equals(normalized.RequestType, "captureWallet", StringComparison.Ordinal))
                errors.Add("RequestType must be captureWallet");

            if (!string.Equals(normalized.Lang, "vi", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalized.Lang, "en", StringComparison.OrdinalIgnoreCase))
                errors.Add("Lang must be vi or en");

            return errors;
        }

        public static bool IsValidHttpsUrl(string? url)
        {
            return Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
                   && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsValidHttpsBaseUrl(string? url)
        {
            if (!IsValidHttpsUrl(url))
                return false;

            var uri = new Uri(url!.Trim());
            return string.IsNullOrWhiteSpace(uri.Query)
                   && string.IsNullOrWhiteSpace(uri.Fragment);
        }

        private static string NormalizePath(string? path)
        {
            var value = (path ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return value.StartsWith('/') ? value : "/" + value;
        }

        private static bool IsPlaceholderValue(string value)
        {
            foreach (var placeholder in PlaceholderValues)
            {
                if (string.Equals(value, placeholder, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
