using System.Collections.Generic;
using TCTEnglish.Services.Billing.VnPay;
using Xunit;

namespace TCTEnglish.Tests
{
    public class VnPaySignatureHelperRequiredTests
    {
        [Fact]
        public void BuildRawSignatureData_ShouldSortParamsAscending()
        {
            var data = new Dictionary<string, string>
            {
                ["vnp_TxnRef"] = "123",
                ["vnp_Amount"] = "1000000",
                ["vnp_Command"] = "pay"
            };

            var raw = VnPaySignatureHelper.BuildRawSignatureData(data);
            Assert.StartsWith("vnp_Amount=1000000&vnp_Command=pay&vnp_TxnRef=123", raw);
        }

        [Fact]
        public void BuildRawSignatureData_ShouldEncodeSpacesAsPlus()
        {
            var data = new Dictionary<string, string>
            {
                ["vnp_OrderInfo"] = "Thanh toan don hang :5"
            };

            var raw = VnPaySignatureHelper.BuildRawSignatureData(data);
            Assert.Contains("Thanh+toan+don+hang+%3A5", raw);
            Assert.DoesNotContain("%20", raw);
        }

        [Fact]
        public void BuildRawSignatureData_ShouldIgnoreSecureHash()
        {
            var data = new Dictionary<string, string>
            {
                ["vnp_Amount"] = "1000000",
                ["vnp_SecureHash"] = "abc",
                ["vnp_SecureHashType"] = "HmacSHA512"
            };

            var raw = VnPaySignatureHelper.BuildRawSignatureData(data);
            Assert.Equal("vnp_Amount=1000000", raw);
        }

        [Fact]
        public void Verify_ShouldReturnFalse_WhenAmountChanged()
        {
            var secret = "TEST_SECRET";

            var original = new Dictionary<string, string>
            {
                ["vnp_Amount"] = "1000000",
                ["vnp_Command"] = "pay",
                ["vnp_TxnRef"] = "TCT123"
            };

            var hash = VnPaySignatureHelper.Sign(secret, original);

            var tampered = new Dictionary<string, string>
            {
                ["vnp_Amount"] = "2000000",
                ["vnp_Command"] = "pay",
                ["vnp_TxnRef"] = "TCT123"
            };

            Assert.False(VnPaySignatureHelper.Verify(secret, tampered, hash));
        }
    }
}
