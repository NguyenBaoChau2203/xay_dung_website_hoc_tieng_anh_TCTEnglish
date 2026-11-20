namespace TCTEnglish.ViewModels.Billing
{
    /// <summary>The local fake payOS page (Development only): what the buyer would see at payOS.</summary>
    public sealed class PayOSMockViewModel
    {
        /// <summary>Our order code (what the pages and the history use).</summary>
        public string OrderCode { get; init; } = string.Empty;

        /// <summary>The numeric payOS code (stored in <c>ProviderRequestId</c>); the cancel link carries it.</summary>
        public string PayOSCode { get; init; } = string.Empty;

        public string PlanName { get; init; } = string.Empty;

        public string AmountText { get; init; } = string.Empty;

        /// <summary>Pending and not expired: the pay and cancel buttons are shown.</summary>
        public bool IsOpen { get; init; }
    }
}
