using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TCTEnglish.Services.Billing.MoMo
{
    public static class MoMoSignatureHelper
    {
        public const string MockAccessKey = "MOMO_MOCK_ACCESS_KEY";

        private static readonly string[] CallbackFieldOrder =
        {
            "accessKey",
            "amount",
            "extraData",
            "message",
            "orderId",
            "orderInfo",
            "orderType",
            "partnerCode",
            "payType",
            "requestId",
            "responseTime",
            "resultCode",
            "transId"
        };

        private static readonly string[] RequiredCallbackFields =
        {
            "amount",
            "orderId",
            "partnerCode",
            "requestId",
            "responseTime",
            "resultCode",
            "transId"
        };

        public static string ComputeHmacSha256(string secretKey, string rawData)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public static string BuildCreateRawSignature(
            string accessKey,
            long amount,
            string extraData,
            string ipnUrl,
            string orderId,
            string orderInfo,
            string partnerCode,
            string redirectUrl,
            string requestId,
            string requestType)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"accessKey={accessKey}&amount={amount}&extraData={extraData}&ipnUrl={ipnUrl}&orderId={orderId}&orderInfo={orderInfo}&partnerCode={partnerCode}&redirectUrl={redirectUrl}&requestId={requestId}&requestType={requestType}");
        }

        public static string BuildReturnOrIpnRawSignature(
            IDictionary<string, string> parameters,
            string accessKey)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["accessKey"] = accessKey
            };

            foreach (var field in CallbackFieldOrder)
            {
                if (field.Equals("accessKey", StringComparison.Ordinal))
                {
                    continue;
                }

                parameters.TryGetValue(field, out var value);
                values[field] = value ?? string.Empty;
            }

            return string.Join(
                "&",
                CallbackFieldOrder.Select(field => $"{field}={values[field]}"));
        }

        public static bool VerifySignature(
            IDictionary<string, string> parameters,
            string accessKey,
            string secretKey,
            out string computedSignature,
            out string validationError)
        {
            validationError = string.Empty;
            parameters.TryGetValue("signature", out var receivedSignature);
            if (string.IsNullOrWhiteSpace(receivedSignature))
            {
                computedSignature = string.Empty;
                validationError = "Missing signature";
                return false;
            }

            if (string.IsNullOrWhiteSpace(accessKey))
            {
                computedSignature = string.Empty;
                validationError = "Missing accessKey configuration";
                return false;
            }

            foreach (var field in RequiredCallbackFields)
            {
                if (!parameters.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    computedSignature = string.Empty;
                    validationError = $"Missing required field: {field}";
                    return false;
                }
            }

            if (parameters.TryGetValue("accessKey", out var callbackAccessKey)
                && !string.IsNullOrWhiteSpace(callbackAccessKey)
                && !string.Equals(callbackAccessKey.Trim(), accessKey.Trim(), StringComparison.Ordinal))
            {
                computedSignature = string.Empty;
                validationError = "accessKey mismatch";
                return false;
            }

            var raw = BuildReturnOrIpnRawSignature(parameters, accessKey);
            computedSignature = ComputeHmacSha256(secretKey, raw);

            return string.Equals(
                receivedSignature.Trim(),
                computedSignature,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
