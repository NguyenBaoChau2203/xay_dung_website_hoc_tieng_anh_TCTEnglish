using System.Collections.Generic;
using TCTEnglish.Services.Billing.MoMo;
using Xunit;

namespace TCTEnglish.Tests
{
    public class MoMoSignatureHelperTests
    {
        [Fact]
        public void ComputeHmacSha256_ReturnsLowerHex()
        {
            var hash = MoMoSignatureHelper.ComputeHmacSha256("secret", "a=1&b=2");
            Assert.Equal(hash.ToLowerInvariant(), hash);
            Assert.NotEmpty(hash);
        }

        [Fact]
        public void VerifySignature_ReturnsTrueForValidPayload()
        {
            var payload = new Dictionary<string, string>
            {
                ["amount"] = "49000",
                ["extraData"] = "",
                ["message"] = "Successful.",
                ["orderId"] = "ORD-1",
                ["orderInfo"] = "Premium payment ORD-1",
                ["orderType"] = "momo_wallet",
                ["partnerCode"] = "MOMO",
                ["payType"] = "qr",
                ["requestId"] = "REQ-1",
                ["responseTime"] = "1714280700123",
                ["resultCode"] = "0",
                ["transId"] = "123456789"
            };

            var raw = MoMoSignatureHelper.BuildReturnOrIpnRawSignature(payload, "access");
            payload["signature"] = MoMoSignatureHelper.ComputeHmacSha256("secret", raw);

            var ok = MoMoSignatureHelper.VerifySignature(payload, "access", "secret", out _, out _);
            Assert.True(ok);
        }

        [Fact]
        public void VerifySignature_ReturnsFalseWhenPayloadTampered()
        {
            var payload = new Dictionary<string, string>
            {
                ["amount"] = "49000",
                ["extraData"] = "",
                ["message"] = "Successful.",
                ["orderId"] = "ORD-1",
                ["orderInfo"] = "Premium payment ORD-1",
                ["orderType"] = "momo_wallet",
                ["partnerCode"] = "MOMO",
                ["payType"] = "qr",
                ["requestId"] = "REQ-1",
                ["responseTime"] = "1714280700123",
                ["resultCode"] = "0",
                ["transId"] = "123456789"
            };

            var raw = MoMoSignatureHelper.BuildReturnOrIpnRawSignature(payload, "access");
            payload["signature"] = MoMoSignatureHelper.ComputeHmacSha256("secret", raw);
            payload["amount"] = "1";

            var ok = MoMoSignatureHelper.VerifySignature(payload, "access", "secret", out _, out _);
            Assert.False(ok);
        }

        [Fact]
        public void VerifySignature_MissingRequiredField_ReturnsFalse()
        {
            var payload = new Dictionary<string, string>
            {
                ["orderId"] = "ORD-1",
                ["requestId"] = "REQ-1",
                ["resultCode"] = "0",
                ["signature"] = "abc"
            };

            var ok = MoMoSignatureHelper.VerifySignature(payload, "access", "secret", out _, out var error);
            Assert.False(ok);
            Assert.Contains("Missing required field", error);
        }

        [Fact]
        public void VerifySignature_ExtraUnknownField_DoesNotAffectSignature()
        {
            var payload = new Dictionary<string, string>
            {
                ["amount"] = "49000",
                ["extraData"] = "",
                ["message"] = "Successful.",
                ["orderId"] = "ORD-1",
                ["orderInfo"] = "Premium payment ORD-1",
                ["orderType"] = "momo_wallet",
                ["partnerCode"] = "MOMO",
                ["payType"] = "qr",
                ["requestId"] = "REQ-1",
                ["responseTime"] = "1714280700123",
                ["resultCode"] = "0",
                ["transId"] = "123456789",
                ["foo"] = "bar"
            };

            var raw = MoMoSignatureHelper.BuildReturnOrIpnRawSignature(payload, "access");
            payload["signature"] = MoMoSignatureHelper.ComputeHmacSha256("secret", raw);

            var ok = MoMoSignatureHelper.VerifySignature(payload, "access", "secret", out _, out _);
            Assert.True(ok);
        }
    }
}
