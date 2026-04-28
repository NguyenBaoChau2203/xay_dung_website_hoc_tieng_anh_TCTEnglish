using System;
using System.Collections.Generic;

namespace TCTEnglish.Models
{
    /// <summary>
    /// Defines an available Premium subscription plan (e.g. "1 tháng", "1 năm").
    /// </summary>
    public class PremiumPlan
    {
        public int Id { get; set; }

        /// <summary>Unique machine-readable code, e.g. "monthly", "yearly".</summary>
        public string Code { get; set; } = null!;

        /// <summary>Display name shown on the pricing page.</summary>
        public string Name { get; set; } = null!;

        /// <summary>Short marketing description.</summary>
        public string Description { get; set; } = null!;

        /// <summary>Price in VND.</summary>
        public decimal PriceVnd { get; set; }

        /// <summary>Duration in calendar days.</summary>
        public int DurationDays { get; set; }

        /// <summary>Whether this plan is currently purchasable.</summary>
        public bool IsActive { get; set; }

        /// <summary>Sort order on the pricing page (lower = first).</summary>
        public int DisplayOrder { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? UpdatedAtUtc { get; set; }

        // ─── Navigation ────────────────────────────────────────────────────────
        public virtual ICollection<PaymentOrder> PaymentOrders { get; set; } = new List<PaymentOrder>();
        public virtual ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
    }
}
