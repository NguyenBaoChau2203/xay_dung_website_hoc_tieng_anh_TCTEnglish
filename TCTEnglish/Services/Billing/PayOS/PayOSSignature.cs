using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TCTEnglish.Services.Billing.PayOS
{
    /// <summary>
    /// payOS signatures: HMAC-SHA256 (lowercase hex) with the channel's checksum key over <c>key=value</c> pairs
    /// joined by <c>&amp;</c> and sorted by key (payOS documentation, "Kiểm tra dữ liệu với signature").
    /// Pure and static so the known-answer vectors can be tested without a server.
    /// </summary>
    public static class PayOSSignature
    {
        /// <summary>Signature of a create-payment-link request: five fields, alphabetical, raw values.</summary>
        public static string ForPaymentRequest(
            long amount, string cancelUrl, string description, long orderCode, string returnUrl, string checksumKey)
        {
            var data = string.Create(
                CultureInfo.InvariantCulture,
                $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}");
            return Hmac(checksumKey, data);
        }

        /// <summary>
        /// Signature of a webhook <c>data</c> object: keys sorted ordinally, a null value is an empty string,
        /// values are used as they are (no URL encoding for payment requests).
        /// </summary>
        public static string ForData(IReadOnlyDictionary<string, string?> data, string checksumKey)
        {
            var text = string.Join(
                "&",
                data.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => $"{kv.Key}={kv.Value ?? string.Empty}"));
            return Hmac(checksumKey, text);
        }

        /// <summary>Constant-time comparison of the received signature with the computed one.</summary>
        public static bool Verify(string expected, string? actual)
            => BillingSignatureComparer.HexEquals(expected, actual);

        /// <summary>
        /// A JSON value as it enters the signed text: a string as it is, a number or object as its raw text,
        /// <c>true</c>/<c>false</c> in lower case, null as an empty string.
        /// </summary>
        public static string ToSignedText(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.GetRawText()
        };

        private static string Hmac(string key, string data)
        {
            var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
