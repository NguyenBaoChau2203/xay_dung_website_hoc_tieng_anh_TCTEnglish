using System.Threading;
using System.Threading.Tasks;

namespace TCTEnglish.Services.Billing
{
    /// <summary>
    /// Reconciles pending payment orders against the provider's query API.
    /// </summary>
    public interface IPaymentReconciliationService
    {
        /// <summary>
        /// Query the provider for any VNPay orders that have been pending for more than
        /// <paramref name="minPendingMinutes"/> minutes and update their status accordingly.
        /// </summary>
        /// <returns>Number of orders reconciled (status changed).</returns>
        Task<ReconciliationSummary> ReconcileVnPayPendingOrdersAsync(
            int minPendingMinutes = 5,
            CancellationToken ct = default);
    }

    public record ReconciliationSummary(
        int Checked,
        int Paid,
        int Failed,
        int ManualReview,
        int Expired);
}
