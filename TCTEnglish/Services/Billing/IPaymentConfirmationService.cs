using System;
using System.Threading;
using System.Threading.Tasks;

namespace TCTEnglish.Services.Billing
{
    /// <summary>
    /// Everything the confirmation service needs to mark one order paid.
    /// <see cref="VerifiedAmountVnd"/> is required unless <see cref="Source"/> is Admin.
    /// </summary>
    public sealed record PaymentConfirmation(
        long OrderId,
        string Provider,
        PaymentTransitionSource Source,
        long? VerifiedAmountVnd,
        string EventType,
        string EventKey,
        string PayloadJson,
        string? ProviderTransactionId = null,
        string? ProviderRequestId = null,
        string? ResponseCode = null,
        string? TransactionStatus = null,
        string? PayType = null,
        string? BankCode = null,
        string? BankTranNo = null,
        string? CardType = null,
        string? AdminReason = null,
        int? AdminUserId = null);

    public enum PaymentConfirmationOutcome
    {
        Activated,
        AlreadyPaid,
        SentToManualReview,
        RejectedState,
        RejectedAmount,
        OrderNotFound
    }

    public sealed record PaymentConfirmationResult(
        PaymentConfirmationOutcome Outcome,
        DateTime? PremiumEndsAtUtc);

    /// <summary>
    /// The only place that sets <c>PaymentOrder.Status</c> to <c>paid</c> and activates Premium.
    /// IPN handlers, provider queries, reconciliation and admin confirmation all call it.
    /// </summary>
    public interface IPaymentConfirmationService
    {
        Task<PaymentConfirmationResult> ConfirmPaidAsync(
            PaymentConfirmation confirmation,
            CancellationToken ct = default);
    }
}
