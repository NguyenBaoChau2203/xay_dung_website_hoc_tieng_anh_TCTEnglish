using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TCTEnglish.Models;
using TCTEnglish.ViewModels.Billing;

namespace TCTEnglish.Services.Billing
{
    public interface IBillingService
    {
        /// <summary>
        /// Creates a new checkout order and optionally obtains a redirect URL
        /// from the payment gateway.
        /// </summary>
        Task<CheckoutResult> CreateCheckoutAsync(
            int userId,
            string planCode,
            string provider,
            string clientIp,
            CancellationToken ct = default);

        /// <summary>
        /// Retrieves a payment order by its application-generated order code.
        /// </summary>
        Task<PaymentOrder?> GetOrderByCodeAsync(string orderCode, CancellationToken ct = default);

        /// <summary>
        /// Returns the payment history for a specific user (anti-IDOR: caller must supply userId).
        /// </summary>
        Task<PaymentHistoryViewModel> GetPaymentHistoryAsync(int userId, CancellationToken ct = default);

        /// <summary>
        /// Verifies a VNPay return callback and resolves the current database-backed
        /// payment state for the user-facing result page.
        /// This method never activates Premium from the return URL.
        /// </summary>
        Task<PaymentResultViewModel?> GetVnPayReturnResultAsync(
            IDictionary<string, string> queryParams,
            CancellationToken ct = default);

        /// <summary>
        /// Verifies a MoMo return callback and resolves the current database-backed
        /// payment state for the user-facing result page.
        /// This method never activates Premium from the return URL.
        /// </summary>
        Task<PaymentResultViewModel?> GetMoMoReturnResultAsync(
            IDictionary<string, string> queryParams,
            CancellationToken ct = default);

        /// <summary>
        /// Cleans up pending orders that have expired.
        /// Returns the number of orders expired.
        /// </summary>
        Task<int> CleanupExpiredPendingOrdersAsync(CancellationToken ct = default);
    }
}
