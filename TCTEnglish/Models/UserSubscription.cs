using System;
using TCTVocabulary.Models;

namespace TCTEnglish.Models
{
    /// <summary>
    /// Tracks a user's active or historical Premium subscription period.
    /// </summary>
    public class UserSubscription
    {
        public long Id { get; set; }

        public int UserId { get; set; }

        public int PlanId { get; set; }

        /// <summary>Current lifecycle status (see <see cref="TCTEnglish.Services.Billing.SubscriptionStatuses"/>).</summary>
        public string Status { get; set; } = null!;

        public DateTime StartsAtUtc { get; set; }

        public DateTime EndsAtUtc { get; set; }

        /// <summary>The payment order that activated this subscription (null for admin-granted).</summary>
        public long? ActivatedByPaymentOrderId { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? CancelledAtUtc { get; set; }

        /// <summary>User- or admin-provided reason for cancellation.</summary>
        public string? CancelReason { get; set; }

        // ─── Navigation ────────────────────────────────────────────────────────
        public virtual User User { get; set; } = null!;
        public virtual PremiumPlan Plan { get; set; } = null!;
        public virtual PaymentOrder? ActivatedByPaymentOrder { get; set; }
    }
}
