using System;
using System.Globalization;
using System.Security.Cryptography;

namespace TCTEnglish.Services.Billing.PayOS
{
    /// <summary>
    /// payOS identifies an order by an integer. We generate a 15-digit one (<c>yyMMddHHmmss</c> + 3 random digits,
    /// below 9 007 199 254 740 991) and keep it in <c>PaymentOrder.ProviderRequestId</c>, so no schema change is needed.
    /// </summary>
    public static class PayOSOrderCode
    {
        /// <summary>The description limit of the payOS API for accounts not linked through payOS (documentation 2026-11-20).</summary>
        public const int MaxDescriptionLength = 9;

        public static long Generate(DateTime utcNow)
        {
            var stamp = utcNow.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture);
            var suffix = RandomNumberGenerator.GetInt32(0, 1000);
            return long.Parse($"{stamp}{suffix:D3}", CultureInfo.InvariantCulture);
        }

        /// <summary>"TCT" + the last six digits of the code: nine characters, the most payOS accepts.</summary>
        public static string Description(long orderCode)
        {
            var digits = orderCode.ToString(CultureInfo.InvariantCulture);
            var tail = digits.Length <= 6 ? digits : digits[^6..];
            var text = "TCT" + tail;
            return text.Length <= MaxDescriptionLength ? text : text[..MaxDescriptionLength];
        }

        public static bool TryParse(string? value, out long orderCode)
            => long.TryParse(value?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out orderCode) && orderCode > 0;
    }
}
