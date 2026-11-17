using System;
using System.Collections.Generic;
using System.Linq;

namespace TCTEnglish.ViewModels.Billing
{
    public class PricingPlanViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal PriceVnd { get; set; }
        public int DurationDays { get; set; }

        public string PriceText { get; set; } = string.Empty;

        /// <summary>"khoảng 33.250 ₫/tháng" for plans of a year or more; empty otherwise.</summary>
        public string PerMonthText { get; set; } = string.Empty;

        /// <summary>Whole percent saved against the cheapest monthly plan; 0 hides the chip.</summary>
        public int SavingPercent { get; set; }

        public bool IsPreselected { get; set; }
    }

    /// <summary>A payment method row ("VNPay", "Ví MoMo").</summary>
    public class PricingProviderViewModel
    {
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Badge { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;

        /// <summary>Enabled and configured; an unavailable method is shown but cannot be chosen.</summary>
        public bool Available { get; init; }

        /// <summary>Local mock mode: the row carries a "Thử nghiệm" chip (development only).</summary>
        public bool IsTest { get; init; }

        public bool IsPreselected { get; set; }
    }

    public class PricingViewModel
    {
        public List<PricingPlanViewModel> Plans { get; set; } = new();
        public PremiumAccessSnapshot CurrentAccess { get; set; } = null!;
        public string? ErrorMessage { get; set; }

        public Dictionary<string, bool> ProviderReadiness { get; set; } = new();

        public bool HasAnyReadyProvider => ProviderReadiness.ContainsValue(true);

        public List<PricingProviderViewModel> Providers { get; set; } = new();

        /// <summary>The page's current-state card (free, Premium with a date, or permanent).</summary>
        public PremiumStatusViewModel Status { get; set; } = null!;

        /// <summary>Server clock and the current end date (UTC milliseconds), for the summary the script keeps in step.</summary>
        public long NowMs { get; set; }
        public long? CurrentEndMs { get; set; }

        /// <summary>Permanent accounts (admin, legacy Premium) have nothing to buy: the checkout column is replaced by a note.</summary>
        public bool CanBuy => CurrentAccess is { IsPermanent: false };

        public PricingPlanViewModel? SelectedPlan => Plans.FirstOrDefault(p => p.IsPreselected) ?? Plans.FirstOrDefault();
        public PricingProviderViewModel? SelectedProvider => Providers.FirstOrDefault(p => p.IsPreselected);
    }
}
