using System;

namespace TCTEnglish.Models
{
    /// <summary>
    /// Immutable audit record for every inbound webhook / IPN / return callback and outbound event.
    /// Idempotency is enforced via the unique constraint on (Provider, EventType, EventKey).
    /// </summary>
    public class PaymentEvent
    {
        public long Id { get; set; }

        /// <summary>Payment gateway identifier (see <see cref="TCTEnglish.Services.Billing.PaymentProviders"/>).</summary>
        public string Provider { get; set; } = null!;

        /// <summary>
        /// Structured event type (see <see cref="TCTEnglish.Services.Billing.PaymentEventTypes"/>).
        /// Examples: checkout_created, return, ipn, manual_confirm, manual_review, grant_premium.
        /// </summary>
        public string EventType { get; set; } = null!;

        /// <summary>
        /// Canonical idempotency key: unique event identifier built from provider-specific fields.
        /// For VNPay IPN: "{vnp_TxnRef}:{vnp_TransactionNo}:{vnp_ResponseCode}"
        /// For VNPay Return: "{vnp_TxnRef}:{vnp_ResponseCode}:{hash7}"
        /// For checkout: "{orderCode}"
        /// DB unique constraint: (Provider, EventType, EventKey).
        /// </summary>
        public string EventKey { get; set; } = null!;

        /// <summary>Associated order, if resolvable from the payload.</summary>
        public long? PaymentOrderId { get; set; }

        /// <summary>Whether the HMAC / signature verification passed.</summary>
        public bool SignatureValid { get; set; }

        /// <summary>Raw provider result/response code (e.g. vnp_ResponseCode = "00").</summary>
        public string? ResultCode { get; set; }

        /// <summary>Full raw JSON payload for forensic replay (secrets excluded).</summary>
        public string PayloadJson { get; set; } = null!;

        /// <summary>Processing status (see <see cref="TCTEnglish.Services.Billing.PaymentEventProcessingStatuses"/>).</summary>
        public string ProcessingStatus { get; set; } = null!;

        /// <summary>Optional processing outcome message or error detail.</summary>
        public string? ProcessingMessage { get; set; }

        public DateTime ReceivedAtUtc { get; set; }

        public DateTime? ProcessedAtUtc { get; set; }

        // ─── Navigation ────────────────────────────────────────────────────────
        public virtual PaymentOrder? PaymentOrder { get; set; }
    }
}
