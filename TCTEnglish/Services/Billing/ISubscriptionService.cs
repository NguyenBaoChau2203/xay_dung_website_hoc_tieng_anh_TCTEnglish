using System.Threading;
using System.Threading.Tasks;
using TCTEnglish.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Billing
{
    public interface ISubscriptionService
    {
        /// <summary>
        /// Creates or extends a subscription from a paid order.
        /// Idempotent: calling twice for the same order does not stack duration.
        /// </summary>
        Task<UserSubscription> ActivateFromPaidOrderAsync(long paymentOrderId, CancellationToken ct = default);

        /// <summary>
        /// Marks all overdue active subscriptions as expired and
        /// downgrades users who no longer have any active entitlement.
        /// Returns the number of subscriptions expired.
        /// </summary>
        Task<int> ExpireSubscriptionsAsync(CancellationToken ct = default);

        /// <summary>
        /// Admin-initiated revocation of a user's active subscription.
        /// </summary>
        Task<OperationResult> RevokeAsync(int userId, string reason, CancellationToken ct = default);

        /// <summary>
        /// Admin-initiated manual grant or extension of Premium without a payment order.
        /// </summary>
        Task<OperationResult> GrantManualAsync(
            int userId,
            int planId,
            int? durationDays,
            string reason,
            CancellationToken ct = default);

        /// <summary>
        /// Returns the user's currently active subscription, or null.
        /// </summary>
        Task<UserSubscription?> GetActiveSubscriptionAsync(int userId, CancellationToken ct = default);
    }
}
