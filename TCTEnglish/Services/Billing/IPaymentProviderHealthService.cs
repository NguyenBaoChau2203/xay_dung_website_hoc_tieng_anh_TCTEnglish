using System.Collections.Generic;
using TCTEnglish.ViewModels.Billing;

namespace TCTEnglish.Services.Billing
{
    /// <summary>
    /// Returns a redacted health snapshot for each configured payment provider.
    /// Secrets are never exposed — only "Present" / "Missing" indicators.
    /// </summary>
    public interface IPaymentProviderHealthService
    {
        /// <summary>Returns health status for all known providers.</summary>
        IReadOnlyList<ProviderHealthViewModel> GetProviderHealthStatus();

        /// <summary>Returns health status for all billing-related background workers.</summary>
        IReadOnlyList<WorkerHealthViewModel> GetWorkerHealthStatus();
    }
}
