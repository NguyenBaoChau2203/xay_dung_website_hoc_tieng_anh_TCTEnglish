namespace TCTEnglish.Services.Billing
{
    /// <summary>
    /// Payment gateway provider identifiers.
    /// </summary>
    public static class PaymentProviders
    {
        public const string VNPay = "vnpay";
        public const string MoMo = "momo";
    }

    /// <summary>
    /// Lifecycle statuses for a <see cref="TCTEnglish.Models.PaymentOrder"/>.
    /// </summary>
    public static class PaymentOrderStatuses
    {
        public const string Pending = "pending";
        public const string Paid = "paid";
        public const string Failed = "failed";
        public const string Cancelled = "cancelled";
        public const string Expired = "expired";
        public const string Refunded = "refunded";
        public const string PartiallyRefunded = "partially_refunded";

        /// <summary>
        /// Requires manual admin review before activation.
        /// Used for: late IPN (&gt;24 h after expiry), amount mismatch anomaly, anomalous gateway data.
        /// </summary>
        public const string ManualReview = "manual_review";
    }

    /// <summary>
    /// Lifecycle statuses for a <see cref="TCTVocabulary.Models.UserSubscription"/>.
    /// </summary>
    public static class SubscriptionStatuses
    {
        public const string Active = "active";
        public const string Expired = "expired";
        public const string Cancelled = "cancelled";
        public const string Revoked = "revoked";
    }

    /// <summary>
    /// Processing statuses for inbound <see cref="TCTEnglish.Models.PaymentEvent"/> records.
    /// </summary>
    public static class PaymentEventProcessingStatuses
    {
        public const string Received = "received";
        public const string Processed = "processed";
        public const string Ignored = "ignored";
        public const string Failed = "failed";
    }

    /// <summary>
    /// Structured event types stored in <see cref="TCTEnglish.Models.PaymentEvent.EventType"/>.
    /// These form part of the unique idempotency key: (Provider, EventType, EventKey).
    /// </summary>
    public static class PaymentEventTypes
    {
        /// <summary>Checkout session created and user redirected to gateway.</summary>
        public const string CheckoutCreated = "checkout_created";

        /// <summary>User returned to ReturnUrl after completing (or cancelling) payment at gateway.</summary>
        public const string Return = "return";

        /// <summary>Server-to-server IPN / webhook received from gateway.</summary>
        public const string Ipn = "ipn";

        /// <summary>Server-initiated query to gateway to reconcile order status.</summary>
        public const string Query = "query";

        /// <summary>Admin manually confirmed a pending / manual_review order.</summary>
        public const string ManualConfirm = "manual_confirm";

        /// <summary>Admin manually rejected an order.</summary>
        public const string ManualReject = "manual_reject";

        /// <summary>Order flagged for manual review due to anomaly.</summary>
        public const string ManualReview = "manual_review";

        /// <summary>Subscription activated (grant_premium) after order paid.</summary>
        public const string GrantPremium = "grant_premium";

        /// <summary>Subscription revoked by admin.</summary>
        public const string RevokePremium = "revoke_premium";

        /// <summary>Reconciliation pass checked/resolved the order status.</summary>
        public const string Reconcile = "reconcile";

        /// <summary>Refund notification received from provider.</summary>
        public const string Refund = "refund";
    }
}
