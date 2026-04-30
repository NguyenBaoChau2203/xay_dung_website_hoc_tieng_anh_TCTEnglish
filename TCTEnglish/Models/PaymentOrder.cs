using System;
using System.Collections.Generic;
using TCTVocabulary.Models;

namespace TCTEnglish.Models
{
    /// <summary>
    /// Represents a single payment attempt by a user for a specific plan.
    /// </summary>
    public class PaymentOrder
    {
        public long Id { get; set; }

        /// <summary>Application-generated unique order code sent to the gateway.</summary>
        public string OrderCode { get; set; } = null!;

        public int UserId { get; set; }

        public int PlanId { get; set; }

        /// <summary>Payment gateway identifier (see <see cref="TCTEnglish.Services.Billing.PaymentProviders"/>).</summary>
        public string Provider { get; set; } = null!;

        public decimal AmountVnd { get; set; }

        /// <summary>ISO 4217 currency code, typically "VND".</summary>
        public string Currency { get; set; } = "VND";

        /// <summary>Current lifecycle status (see <see cref="TCTEnglish.Services.Billing.PaymentOrderStatuses"/>).</summary>
        public string Status { get; set; } = null!;

        /// <summary>Raw status string as received from the provider (before normalization).</summary>
        public string? RawStatus { get; set; }

        // ─── Timestamps ───────────────────────────────────────────────────────────

        public DateTime CreatedAtUtc { get; set; }

        /// <summary>Last time this record was mutated (set on every status change).</summary>
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>Deadline by which the payment must be completed.</summary>
        public DateTime ExpiresAtUtc { get; set; }

        /// <summary>Timestamp when the gateway confirmed payment (IPN/webhook success).</summary>
        public DateTime? PaidAtUtc { get; set; }

        /// <summary>Timestamp when an admin confirmed the order manually.</summary>
        public DateTime? ConfirmedAtUtc { get; set; }

        /// <summary>UserId of the admin who confirmed the order (manual confirm flow).</summary>
        public int? ConfirmedByUserId { get; set; }

        /// <summary>Timestamp when the linked subscription was activated. Set by SubscriptionService.</summary>
        public DateTime? ActivatedAtUtc { get; set; }

        // ─── Provider data ────────────────────────────────────────────────────────

        /// <summary>Transaction ID returned by the payment provider (e.g. vnp_TransactionNo).</summary>
        public string? ProviderTransactionId { get; set; }
        public string? ProviderRequestId { get; set; }
        public string? ProviderPaymentUrl { get; set; }
        public string? ProviderDeepLink { get; set; }
        public string? ProviderQrCodePayload { get; set; }

        /// <summary>Response/result code from the provider (e.g. vnp_ResponseCode).</summary>
        public string? ProviderResponseCode { get; set; }

        /// <summary>Provider-side transaction status string (e.g. vnp_TransactionStatus).</summary>
        public string? ProviderTransactionStatus { get; set; }

        /// <summary>Bank code (e.g. vnp_BankCode: VCB, TCB, NCB…).</summary>
        public string? BankCode { get; set; }

        /// <summary>Bank-internal transaction number (e.g. vnp_BankTranNo).</summary>
        public string? BankTransactionNo { get; set; }

        /// <summary>Card type (e.g. vnp_CardType: ATM, Credit).</summary>
        public string? CardType { get; set; }

        /// <summary>Payment method within the gateway (e.g. vnp_PayType: Qr, Banking).</summary>
        public string? PayType { get; set; }

        // ─── Failure / audit ─────────────────────────────────────────────────────

        /// <summary>Human-readable failure reason if the payment failed.</summary>
        public string? FailureMessage { get; set; }

        /// <summary>Raw JSON payload from the return URL callback.</summary>
        public string? ReturnPayloadJson { get; set; }

        /// <summary>Raw JSON payload from the IPN callback.</summary>
        public string? IpnPayloadJson { get; set; }

        // ─── Navigation ────────────────────────────────────────────────────────
        public virtual User User { get; set; } = null!;
        public virtual PremiumPlan Plan { get; set; } = null!;
        public virtual ICollection<PaymentEvent> PaymentEvents { get; set; } = new List<PaymentEvent>();
    }
}
