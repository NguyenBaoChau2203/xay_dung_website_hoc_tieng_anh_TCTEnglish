using System;
using System.Collections.Generic;

namespace TCTEnglish.ViewModels.Billing
{
    public class PremiumAccessSnapshot
    {
        public bool IsAuthenticated { get; init; }
        public bool IsPremium { get; init; }
        public bool IsAdmin { get; init; }
        public string Role { get; init; } = string.Empty;
        public DateTime? PremiumEndsAtUtc { get; init; }
        public IReadOnlySet<string> Features { get; init; } = new HashSet<string>();

        public bool HasFeature(string featureKey)
        {
            return Features.Contains(featureKey);
        }
    }
}
