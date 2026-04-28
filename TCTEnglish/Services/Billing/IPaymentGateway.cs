using System.Threading;
using System.Threading.Tasks;

namespace TCTEnglish.Services.Billing
{
    /// <summary>
    /// Result returned by a gateway when initiating a checkout session.
    /// </summary>
    public class ProviderCheckoutResult
    {
        public bool Success { get; init; }

        /// <summary>URL the user should be redirected to for payment.</summary>
        public string? RedirectUrl { get; init; }
        public string? ProviderRequestId { get; init; }
        public string? ProviderPaymentUrl { get; init; }
        public string? ProviderDeepLink { get; init; }
        public string? ProviderQrCodePayload { get; init; }

        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }

        public static ProviderCheckoutResult Ok(string redirectUrl)
            => new() { Success = true, RedirectUrl = redirectUrl };

        public static ProviderCheckoutResult Ok(
            string redirectUrl,
            string? providerRequestId,
            string? providerPaymentUrl,
            string? providerDeepLink,
            string? providerQrCodePayload)
            => new()
            {
                Success = true,
                RedirectUrl = redirectUrl,
                ProviderRequestId = providerRequestId,
                ProviderPaymentUrl = providerPaymentUrl,
                ProviderDeepLink = providerDeepLink,
                ProviderQrCodePayload = providerQrCodePayload
            };

        public static ProviderCheckoutResult Fail(string errorCode, string errorMessage)
            => new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
    }

    /// <summary>
    /// Result from processing a provider return-URL callback.
    /// </summary>
    public class ProviderCallbackResult
    {
        public bool IsVerified { get; init; }
        public bool IsPaid { get; init; }
        public string? OrderCode { get; init; }
        public string? ProviderRequestId { get; init; }
        public string? ProviderTransactionId { get; init; }
        public string? PayType { get; init; }
        public string? ProviderResponseCode { get; init; }
        public string? ProviderTransactionStatus { get; init; }
        public string? ProviderMessage { get; init; }
        public string? ErrorMessage { get; init; }
    }

    /// <summary>
    /// Result from processing a provider IPN (server-to-server) callback.
    /// </summary>
    public class ProviderIpnResult
    {
        public bool SignatureValid { get; init; }
        public bool IsPaid { get; init; }
        public string? OrderCode { get; init; }
        public string? ProviderRequestId { get; init; }
        public string? ProviderTransactionId { get; init; }
        public string? PayType { get; init; }
        public string? ProviderResponseCode { get; init; }
        public string? ProviderTransactionStatus { get; init; }
        public string? ProviderMessage { get; init; }
        public string? ErrorMessage { get; init; }
    }

    /// <summary>
    /// Abstraction for a payment gateway provider.
    /// </summary>
    public interface IPaymentGateway
    {
        /// <summary>Machine-readable provider name (e.g. "vnpay", "momo").</summary>
        string ProviderName { get; }

        /// <summary>Whether this gateway is ready to accept payments.</summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Creates a checkout session and returns a redirect URL.
        /// </summary>
        Task<ProviderCheckoutResult> CreateCheckoutAsync(
            string orderCode,
            decimal amountVnd,
            string orderDescription,
            string clientIp,
            CancellationToken ct = default);

        /// <summary>
        /// Verifies and parses the provider's return-URL query string.
        /// </summary>
        Task<ProviderCallbackResult> ProcessReturnAsync(
            IDictionary<string, string> queryParams,
            CancellationToken ct = default);

        /// <summary>
        /// Verifies and parses the provider's IPN (server-to-server) callback.
        /// </summary>
        Task<ProviderIpnResult> ProcessIpnAsync(
            IDictionary<string, string> parameters,
            CancellationToken ct = default);
    }
}
