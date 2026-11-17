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
        /// <summary>End of the latest active subscription (also set for accounts whose role is already Premium).</summary>
        public DateTime? PremiumEndsAtUtc { get; init; }

        /// <summary>Admin, or a legacy Premium role with no subscription row at all: no end date, no purchase needed.</summary>
        public bool IsPermanent { get; init; }

        /// <summary>Whole days left (rounded up) when there is an end date; null otherwise.</summary>
        public int? DaysLeft { get; init; }
        public IReadOnlySet<string> Features { get; init; } = new HashSet<string>();

        public bool HasFeature(string featureKey)
        {
            return Features.Contains(featureKey);
        }
    }
}
