using System;

namespace TCTEnglish.Services.Billing
{
    /// <summary>
    /// What a provider's own server said about an order when we asked it.
    /// <see cref="Unknown"/> covers everything that does not settle the order: the query itself
    /// failed, the transaction was not found yet, or it is still processing. It never changes an order.
    /// </summary>
    public enum PaymentQueryState
    {
        Unknown,
        Success,
        Failed
    }

    /// <summary>Provider-neutral answer of a verified server-to-server query.</summary>
    public sealed record PaymentQueryResult(
        PaymentQueryState State,
        long? AmountVnd = null,
        string? TransactionId = null,
        string? ResponseCode = null,
        string? TransactionStatus = null,
        string? PayType = null,
        string? BankCode = null,
        string? Message = null)
    {
        public static PaymentQueryResult Unknown(string? message = null)
            => new(PaymentQueryState.Unknown, Message: message);
    }
}
