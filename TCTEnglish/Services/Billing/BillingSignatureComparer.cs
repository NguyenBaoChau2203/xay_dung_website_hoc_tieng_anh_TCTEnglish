using System.Security.Cryptography;
using System.Text;

namespace TCTEnglish.Services.Billing
{
    /// <summary>
    /// Compares a provider's hex signature with the one we computed, without leaking where they differ.
    /// Every provider (VNPay, MoMo, payOS) goes through here.
    /// </summary>
    public static class BillingSignatureComparer
    {
        /// <summary>
        /// True only when both values are present and hold the same hex digest (case and surrounding
        /// whitespace are ignored). Constant time for equal-length input.
        /// </summary>
        public static bool HexEquals(string? expected, string? actual)
        {
            if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
            {
                return false;
            }

            var left = Encoding.UTF8.GetBytes(expected.Trim().ToLowerInvariant());
            var right = Encoding.UTF8.GetBytes(actual.Trim().ToLowerInvariant());
            return CryptographicOperations.FixedTimeEquals(left, right);
        }
    }
}
