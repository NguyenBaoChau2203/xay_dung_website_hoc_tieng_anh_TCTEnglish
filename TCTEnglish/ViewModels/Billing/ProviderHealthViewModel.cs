using System.Collections.Generic;

namespace TCTEnglish.ViewModels.Billing
{
    /// <summary>
    /// Redacted health snapshot for a single payment provider.
    /// Secrets are NEVER stored here — only boolean present/missing flags.
    /// </summary>
    public class ProviderHealthViewModel
    {
        public string ProviderCode { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>The "Enabled" flag from configuration (e.g. VNPay:Enabled).</summary>
        public bool Enabled { get; init; }

        /// <summary>True when all required fields are non-empty (i.e. IsFullyConfigured()).</summary>
        public bool Configured { get; init; }

        /// <summary>Whether a BaseUrl / endpoint URL is present (non-empty).</summary>
        public bool HasBaseUrl { get; init; }

        /// <summary>Whether a ReturnUrl is present.</summary>
        public bool HasReturnUrl { get; init; }

        /// <summary>Whether an IPN/webhook URL is present.</summary>
        public bool HasIpnUrl { get; init; }

        /// <summary>Whether a Merchant/Partner/Terminal code is present (not the secret value).</summary>
        public bool HasMerchantCode { get; init; }

        /// <summary>Whether a secret key (HashSecret, SecretKey, etc.) is present.</summary>
        public bool HasSecret { get; init; }

        /// <summary>Human-readable labels for fields that are missing.</summary>
        public IReadOnlyList<string> MissingFields { get; init; } = new List<string>();

        /// <summary>Overall status string for display.</summary>
        public string Status => Enabled && Configured ? "Ready"
                              : Enabled && !Configured ? "Misconfigured"
                              : "Disabled";

        public string StatusBadgeClass => Status switch
        {
            "Ready"         => "badge bg-success",
            "Misconfigured" => "badge bg-warning text-dark",
            _               => "badge bg-secondary"
        };
    }

    /// <summary>Health snapshot for a billing-related background worker.</summary>
    public class WorkerHealthViewModel
    {
        public string WorkerName { get; init; } = string.Empty;
        public bool Enabled { get; init; }
        public string Description { get; init; } = string.Empty;

        public string StatusBadgeClass => Enabled ? "badge bg-success" : "badge bg-secondary";
        public string StatusLabel => Enabled ? "Đang chạy" : "Đã tắt";
    }

    /// <summary>Root ViewModel for the admin Payment Health page.</summary>
    public class PaymentHealthViewModel
    {
        public IReadOnlyList<ProviderHealthViewModel> Providers { get; init; }
            = new List<ProviderHealthViewModel>();

        public IReadOnlyList<WorkerHealthViewModel> Workers { get; init; }
            = new List<WorkerHealthViewModel>();
    }
}
