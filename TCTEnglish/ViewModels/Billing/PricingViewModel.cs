using System.Collections.Generic;

namespace TCTEnglish.ViewModels.Billing
{
    public class PricingPlanViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal PriceVnd { get; set; }
        public int DurationDays { get; set; }
    }

    public class PricingViewModel
    {
        public List<PricingPlanViewModel> Plans { get; set; } = new();
        public PremiumAccessSnapshot CurrentAccess { get; set; } = null!;
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Maps provider code (e.g. "vnpay") → whether it is fully configured and enabled.
        /// Used by the Pricing view to disable checkout buttons for unconfigured providers.
        /// </summary>
        public Dictionary<string, bool> ProviderReadiness { get; set; } = new();

        /// <summary>Returns true when at least one provider is ready.</summary>
        public bool HasAnyReadyProvider => ProviderReadiness.ContainsValue(true);
    }
}

