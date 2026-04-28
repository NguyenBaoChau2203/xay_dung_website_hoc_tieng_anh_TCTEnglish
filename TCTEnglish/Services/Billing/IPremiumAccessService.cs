using System.Threading;
using System.Threading.Tasks;
using TCTEnglish.ViewModels.Billing;

namespace TCTEnglish.Services.Billing
{
    public interface IPremiumAccessService
    {
        Task<PremiumAccessSnapshot> GetAccessSnapshotAsync(int userId);

        /// <summary>
        /// Returns true when the user has access to the specified premium feature key.
        /// Handles Admin bypass, legacy Premium role, and active DB subscription.
        /// </summary>
        Task<bool> HasFeatureAsync(int userId, string featureKey, CancellationToken ct = default);
    }
}
