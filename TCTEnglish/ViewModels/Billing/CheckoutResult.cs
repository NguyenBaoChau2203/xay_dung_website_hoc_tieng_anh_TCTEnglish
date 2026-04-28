namespace TCTEnglish.ViewModels.Billing
{
    /// <summary>
    /// Server response after creating a checkout order.
    /// </summary>
    public class CheckoutResult
    {
        public bool Success { get; init; }
        public string? RedirectUrl { get; init; }
        public string? OrderCode { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }

        public static CheckoutResult Ok(string orderCode, string? redirectUrl)
            => new() { Success = true, OrderCode = orderCode, RedirectUrl = redirectUrl };

        public static CheckoutResult Fail(string errorCode, string errorMessage)
            => new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
    }
}
