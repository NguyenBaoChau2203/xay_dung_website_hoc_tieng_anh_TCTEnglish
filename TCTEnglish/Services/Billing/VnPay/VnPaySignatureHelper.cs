using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace TCTEnglish.Services.Billing.VnPay
{
    /// <summary>
    /// Stateless helper for building and verifying VNPay HMAC-SHA512 signatures.
    /// Extracted so it can be unit-tested without gateway infrastructure.
    /// </summary>
    public static class VnPaySignatureHelper
    {
        public static string ToVnPayDate(DateTime utcDateTime)
        {
            if (utcDateTime.Kind != DateTimeKind.Utc)
            {
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            }

            var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, GetVietnamTimeZone());
            return vietnamTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        }

        private static TimeZoneInfo GetVietnamTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
        }

        private static string Encode(string value)
            => WebUtility.UrlEncode(value ?? string.Empty) ?? string.Empty;

        private static IEnumerable<KeyValuePair<string, string>> GetSignableParams(
            IDictionary<string, string> parameters)
        {
            return parameters
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                .Where(kv => kv.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
                .Where(kv => !kv.Key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase))
                .Where(kv => !kv.Key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal);
        }

        public static string BuildRawSignatureData(IDictionary<string, string> parameters)
        {
            return string.Join("&",
                GetSignableParams(parameters)
                    .Select(kv => $"{Encode(kv.Key)}={Encode(kv.Value)}"));
        }

        public static string ComputeHmac512(string hashSecret, string data)
        {
            if (string.IsNullOrWhiteSpace(hashSecret))
            {
                throw new InvalidOperationException("VNPay HashSecret is missing.");
            }

            var keyBytes = Encoding.UTF8.GetBytes(hashSecret.Trim());
            var dataBytes = Encoding.UTF8.GetBytes(data ?? string.Empty);

            using var hmac = new HMACSHA512(keyBytes);
            var hash = hmac.ComputeHash(dataBytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public static string Sign(string hashSecret, IDictionary<string, string> parameters)
        {
            var rawData = BuildRawSignatureData(parameters);
            return ComputeHmac512(hashSecret, rawData);
        }

        public static bool Verify(
            string hashSecret,
            IDictionary<string, string> parameters,
            string? receivedHash)
        {
            if (string.IsNullOrWhiteSpace(receivedHash))
            {
                return false;
            }

            var computedHash = Sign(hashSecret, parameters);
            return string.Equals(computedHash, receivedHash, StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildPaymentUrl(
            string baseUrl,
            string hashSecret,
            IDictionary<string, string> parameters)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException("VNPay BaseUrl is missing.");
            }

            var query = BuildRawSignatureData(parameters);
            var secureHash = ComputeHmac512(hashSecret, query);

            var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{baseUrl}{separator}{query}&vnp_SecureHash={secureHash}";
        }
    }
}
