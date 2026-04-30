using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TCTEnglish.Models;

namespace TCTEnglish.Services.Billing
{
    /// <summary>
    /// Records immutable audit entries for all sensitive admin billing actions.
    /// </summary>
    public interface IPaymentAuditService
    {
        /// <summary>
        /// Appends an audit record for a sensitive admin action.
        /// Rejects if Reason is empty.
        /// </summary>
        Task<PaymentAdminAction> RecordAsync(
            int adminUserId,
            string actionType,
            string reason,
            long? paymentOrderId = null,
            long? subscriptionId = null,
            string? oldStatus = null,
            string? newStatus = null,
            string? payloadJson = null,
            string? ipAddress = null,
            string? userAgent = null,
            CancellationToken ct = default);

        /// <summary>
        /// Returns audit history for a specific PaymentOrder, newest first.
        /// </summary>
        Task<IReadOnlyList<PaymentAdminAction>> GetForOrderAsync(
            long paymentOrderId, CancellationToken ct = default);

        /// <summary>
        /// Returns audit history for a specific UserSubscription, newest first.
        /// </summary>
        Task<IReadOnlyList<PaymentAdminAction>> GetForSubscriptionAsync(
            long subscriptionId, CancellationToken ct = default);

        /// <summary>
        /// Returns the most recent audit entries globally (for dashboard/feed).
        /// </summary>
        Task<IReadOnlyList<PaymentAdminAction>> GetRecentAsync(
            int count = 50, CancellationToken ct = default);
    }
}
