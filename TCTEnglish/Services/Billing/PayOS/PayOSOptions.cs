using System;
using System.Collections.Generic;

namespace TCTEnglish.Services.Billing.PayOS
{
    /// <summary>
    /// Settings of the payOS provider (bank transfer by QR). The three keys come only from user-secrets or the
    /// environment (<c>PayOS__ClientId</c>, <c>PayOS__ApiKey</c>, <c>PayOS__ChecksumKey</c>), never from a file in Git.
    /// </summary>
    public sealed class PayOSOptions
    {
        public const string SectionName = "PayOS";
        public const string DefaultBaseUrl = "https://api-merchant.payos.vn";
        public const string DefaultMockChecksumKey = "payos-local-mock-checksum";

        private static readonly string[] PlaceholderValues =
        {
            "YOUR_CLIENT_ID",
            "YOUR_API_KEY",
            "YOUR_CHECKSUM_KEY",
            "CHANGE_ME",
            "TEST"
        };

        public bool Enabled { get; set; }
        public string BaseUrl { get; set; } = DefaultBaseUrl;
        public string ClientId { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ChecksumKey { get; set; } = string.Empty;
        public string ReturnPath { get; set; } = "/Billing/PayOSReturn";
        public string CancelPath { get; set; } = "/Billing/PayOSCancel";

        /// <summary>The public address of this site (payOS sends the buyer back to it), for example <c>https://tctenglish.example</c>.</summary>
        public string PublicBaseUrl { get; set; } = string.Empty;

        /// <summary>Development only: a local fake checkout page that needs no key and no network.</summary>
        public bool MockMode { get; set; }

        /// <summary>Signs the mock webhook when no real checksum key is set. Not a secret.</summary>
        public string MockChecksumKey { get; set; } = DefaultMockChecksumKey;

        public int TimeoutSeconds { get; set; } = 15;

        public PayOSOptions Normalize()
        {
            return new PayOSOptions
            {
                Enabled = Enabled,
                BaseUrl = (BaseUrl ?? string.Empty).Trim().TrimEnd('/'),
                ClientId = (ClientId ?? string.Empty).Trim(),
                ApiKey = (ApiKey ?? string.Empty).Trim(),
                ChecksumKey = (ChecksumKey ?? string.Empty).Trim(),
                ReturnPath = NormalizePath(ReturnPath),
                CancelPath = NormalizePath(CancelPath),
                PublicBaseUrl = (PublicBaseUrl ?? string.Empty).Trim().TrimEnd('/'),
                MockMode = MockMode,
                MockChecksumKey = string.IsNullOrWhiteSpace(MockChecksumKey) ? DefaultMockChecksumKey : MockChecksumKey.Trim(),
                TimeoutSeconds = TimeoutSeconds < 5 ? 5 : TimeoutSeconds > 60 ? 60 : TimeoutSeconds
            };
        }

        /// <summary>The key that signs and verifies webhooks: the real one, or the local mock key while the mock has no key.</summary>
        public string ResolveChecksumKey(bool mockActive)
            => mockActive && string.IsNullOrWhiteSpace(ChecksumKey) ? MockChecksumKey : ChecksumKey;

        public bool HasApiKeys => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ApiKey);

        /// <summary>The mock is allowed only in Development, and only when the provider is switched on too.</summary>
        public bool IsMockActive(bool isDevelopment) => isDevelopment && Enabled && MockMode;

        public bool IsFullyConfigured(bool isDevelopment) => GetConfigurationErrors(isDevelopment).Count == 0;

        /// <summary>Names of what is missing; never the values.</summary>
        public IReadOnlyList<string> GetConfigurationErrors(bool isDevelopment)
        {
            var normalized = Normalize();
            var errors = new List<string>();

            if (!normalized.Enabled)
                errors.Add("Enabled = false");

            AddKeyError(errors, "ClientId", normalized.ClientId);
            AddKeyError(errors, "ApiKey", normalized.ApiKey);
            AddKeyError(errors, "ChecksumKey", normalized.ChecksumKey);

            if (!IsAbsoluteUrl(normalized.BaseUrl, httpsOnly: true))
                errors.Add("BaseUrl must be an absolute HTTPS URL");

            if (!IsAbsoluteUrl(normalized.PublicBaseUrl, httpsOnly: !isDevelopment))
                errors.Add(isDevelopment
                    ? "PublicBaseUrl must be an absolute http(s) URL"
                    : "PublicBaseUrl must be an absolute HTTPS URL");

            if (!normalized.ReturnPath.StartsWith('/'))
                errors.Add("ReturnPath");

            if (!normalized.CancelPath.StartsWith('/'))
                errors.Add("CancelPath");

            return errors;
        }

        public static bool IsAbsoluteUrl(string? url, bool httpsOnly)
        {
            if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri))
                return false;

            if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                return false;

            return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                   || (!httpsOnly && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase));
        }

        private static void AddKeyError(List<string> errors, string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add(name);
                return;
            }

            foreach (var placeholder in PlaceholderValues)
            {
                if (string.Equals(value, placeholder, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"{name} appears to be a placeholder/test value");
                    return;
                }
            }
        }

        private static string NormalizePath(string? path)
        {
            var value = (path ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return value.StartsWith('/') ? value : "/" + value;
        }
    }
}
