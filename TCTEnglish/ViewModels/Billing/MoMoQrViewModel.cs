using System;

namespace TCTEnglish.ViewModels.Billing
{
    public class MoMoQrViewModel
    {
        public string OrderCode { get; init; } = string.Empty;
        public string PlanName { get; init; } = "Premium";
        public decimal AmountVnd { get; init; }
        public DateTime ExpiresAtUtc { get; init; }
        public string? ProviderPaymentUrl { get; init; }
        public string? ProviderDeepLink { get; init; }
        public string? ProviderQrCodePayload { get; init; }
        public bool IsExpired { get; init; }
        public bool IsPaid { get; init; }
        public bool IsMockMode { get; init; }
    }
}
