using System;
using System.ComponentModel.DataAnnotations;
using TCTVocabulary.Models;

namespace TCTEnglish.Models
{
    /// <summary>
    /// Immutable audit record for every sensitive admin action on the billing system.
    /// Never update or delete rows — always append-only.
    /// </summary>
    public class PaymentAdminAction
    {
        public long Id { get; set; }

        /// <summary>Related PaymentOrder, if applicable.</summary>
        public long? PaymentOrderId { get; set; }

        /// <summary>Related UserSubscription, if applicable.</summary>
        public long? SubscriptionId { get; set; }

        /// <summary>The admin who performed the action.</summary>
        public int AdminUserId { get; set; }

        /// <summary>Machine-readable action type, e.g. "grant_premium", "revoke_premium", "mark_manual_review".</summary>
        [Required, MaxLength(60)]
        public string ActionType { get; set; } = string.Empty;

        /// <summary>Human-readable justification entered by the admin. Required for all sensitive actions.</summary>
        [Required, MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;

        /// <summary>Status of the order/subscription before the action.</summary>
        [MaxLength(50)]
        public string? OldStatus { get; set; }

        /// <summary>Status of the order/subscription after the action.</summary>
        [MaxLength(50)]
        public string? NewStatus { get; set; }

        /// <summary>
        /// Optional JSON blob for extra context (e.g. plan name, duration days).
        /// MUST NOT contain secrets or provider credentials.
        /// </summary>
        public string? PayloadJson { get; set; }

        /// <summary>UTC timestamp when the action was recorded.</summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>IP address of the admin, captured from HttpContext.</summary>
        [MaxLength(50)]
        public string? IpAddress { get; set; }

        /// <summary>User-Agent header of the admin's browser.</summary>
        [MaxLength(500)]
        public string? UserAgent { get; set; }

        // ── Navigation properties ───────────────────────────────────────────────
        public PaymentOrder? PaymentOrder { get; set; }
        public UserSubscription? Subscription { get; set; }
        public User? AdminUser { get; set; }
    }

    /// <summary>Constants for PaymentAdminAction.ActionType values.</summary>
    public static class AdminActionTypes
    {
        public const string GrantPremium       = "grant_premium";
        public const string RevokePremium      = "revoke_premium";
        public const string MarkManualReview   = "mark_manual_review";
        public const string ResolveManualReview = "resolve_manual_review";
        public const string ReplayReconcile    = "replay_reconcile";
        public const string QueryProvider      = "query_provider";
        public const string RefundFull         = "refund_full";
        public const string RefundPartial      = "refund_partial";
        public const string ConfirmBankTransfer = "confirm_bank_transfer";
        public const string RejectBankTransfer  = "reject_bank_transfer";
    }
}
