using System;

namespace TCTEnglish.Services.Billing
{
    /// <summary>Who is asking an order to change state.</summary>
    public enum PaymentTransitionSource
    {
        /// <summary>A signed server-to-server callback (IPN) from the provider.</summary>
        ProviderCallback,

        /// <summary>A verified server-to-server query answer from the provider.</summary>
        ProviderQuery,

        /// <summary>The cleanup worker expiring stale pending orders.</summary>
        Cleanup,

        /// <summary>An administrator resolving an order by hand.</summary>
        Admin
    }

    /// <summary>
    /// Pure rules for <see cref="PaymentOrderStatuses"/> transitions. Every writer of
    /// <c>PaymentOrder.Status</c> asks this class first, so a late failure callback can no
    /// longer overwrite <c>manual_review</c>/<c>refunded</c> and a success callback can no
    /// longer revive a <c>refunded</c> order.
    /// </summary>
    public static class PaymentOrderStateMachine
    {
        private static bool IsProviderLike(PaymentTransitionSource source)
            => source is PaymentTransitionSource.ProviderCallback or PaymentTransitionSource.ProviderQuery;

        private static string Normalize(string? status)
            => (status ?? string.Empty).Trim().ToLowerInvariant();

        /// <summary>
        /// Whether the order may become <c>paid</c>. The 24 hour late rule is applied by the
        /// confirmation service on top of this table.
        /// </summary>
        public static bool CanMarkPaid(string current, PaymentTransitionSource source)
        {
            return Normalize(current) switch
            {
                PaymentOrderStatuses.Pending => IsProviderLike(source),
                PaymentOrderStatuses.Expired => IsProviderLike(source),
                PaymentOrderStatuses.Failed => IsProviderLike(source),
                PaymentOrderStatuses.Cancelled => IsProviderLike(source),
                PaymentOrderStatuses.ManualReview => source == PaymentTransitionSource.Admin,
                _ => false
            };
        }

        /// <summary>Whether the order may become <c>failed</c> or <c>cancelled</c>.</summary>
        public static bool CanMarkFailedOrCancelled(string current, PaymentTransitionSource source)
        {
            return Normalize(current) switch
            {
                PaymentOrderStatuses.Pending => IsProviderLike(source),
                PaymentOrderStatuses.ManualReview => source == PaymentTransitionSource.Admin,
                _ => false
            };
        }

        /// <summary>Whether the cleanup worker may expire the order.</summary>
        public static bool CanExpire(string current)
            => Normalize(current) == PaymentOrderStatuses.Pending;

        /// <summary>Whether the order may be sent to manual review by the given source.</summary>
        public static bool CanSendToManualReview(string current, PaymentTransitionSource source)
        {
            return Normalize(current) switch
            {
                PaymentOrderStatuses.Pending => source != PaymentTransitionSource.Cleanup,
                PaymentOrderStatuses.Expired => source != PaymentTransitionSource.Cleanup,
                PaymentOrderStatuses.Failed => source == PaymentTransitionSource.Admin,
                PaymentOrderStatuses.Cancelled => source == PaymentTransitionSource.Admin,
                _ => false
            };
        }

        /// <summary>Whether any non-cleanup source may send the order to manual review.</summary>
        public static bool CanSendToManualReview(string current)
            => CanSendToManualReview(current, PaymentTransitionSource.ProviderCallback)
               || CanSendToManualReview(current, PaymentTransitionSource.Admin);

        /// <summary>
        /// Whether the order has reached a final outcome that polling no longer needs to wait for.
        /// <c>pending</c> and <c>manual_review</c> are the only open states.
        /// </summary>
        public static bool IsTerminal(string current)
        {
            return Normalize(current) switch
            {
                PaymentOrderStatuses.Pending => false,
                PaymentOrderStatuses.ManualReview => false,
                _ => true
            };
        }
    }
}
