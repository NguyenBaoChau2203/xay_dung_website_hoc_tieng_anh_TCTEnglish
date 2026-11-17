using System;
using TCTEnglish.Services.Billing;

namespace TCTEnglish.ViewModels.Billing
{
    /// <summary>
    /// The outcome of a provider's return, as the return handler needs it: who owns the order, which one it is, and its
    /// state. What the learner reads about an order lives on <see cref="OrderPageViewModel"/> and
    /// <see cref="TCTEnglish.Services.Billing.PaymentOrderLabels"/>.
    /// </summary>
    public class PaymentResultViewModel
    {
        public int? OwnerUserId { get; init; }
        public string OrderCode { get; init; } = string.Empty;
        public string PlanName { get; init; } = "Premium";
        public decimal AmountVnd { get; init; }
        public string OrderStatus { get; init; } = PaymentOrderStatuses.Pending;
        public bool HasValidReturnSignature { get; init; }
        public bool CanShowOrderSummary { get; init; }
        public DateTime? PaidAtUtc { get; init; }

        /// <summary>The provider's return page said the buyer paid. A hint for wording only; never a confirmation.</summary>
        public bool ReturnReportedSuccess { get; init; }

        public string NormalizedStatus => (OrderStatus ?? string.Empty).Trim().ToLowerInvariant();

        public bool IsPaid => HasValidReturnSignature
            && NormalizedStatus == PaymentOrderStatuses.Paid;

        public bool IsPending => HasValidReturnSignature
            && NormalizedStatus == PaymentOrderStatuses.Pending;
    }
}
