using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.VnPay;

namespace TCTEnglish.Tests
{
    public class VnPayGatewayTests
    {
        // ───────────────────────────────────────────────────────────────────────
        //  Helpers
        // ───────────────────────────────────────────────────────────────────────

        private static VnPayGateway CreateGateway(
            bool enabled = true,
            string hashSecret = "TESTSECRET1234567890",
            string tmnCode = "TMNCODETEST",
            string baseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
            string returnUrl = "https://example.com/billing/return")
        {
            var opts = Options.Create(new VnPayOptions
            {
                Enabled = enabled,
                TmnCode = tmnCode,
                HashSecret = hashSecret,
                BaseUrl = baseUrl,
                ReturnUrl = returnUrl,
                IpnUrl = "https://example.com/billing/ipn",
                Version = "2.1.0",
                Command = "pay",
                Locale = "vn",
                Currency = "VND",
                OrderType = "other"
            });
            return new VnPayGateway(opts, NullLogger<VnPayGateway>.Instance);
        }

        private static Dictionary<string, string> BuildSignedCallbackParameters(
            string secret,
            string responseCode = "00",
            string transactionStatus = "00")
        {
            var parameters = new Dictionary<string, string>
            {
                ["vnp_TxnRef"] = "ORD-123",
                ["vnp_Amount"] = "4900000",
                ["vnp_ResponseCode"] = responseCode,
                ["vnp_TransactionStatus"] = transactionStatus,
                ["vnp_TransactionNo"] = "14000001"
            };

            parameters["vnp_SecureHash"] = VnPaySignatureHelper.Sign(secret, parameters);
            return parameters;
        }

        private static Dictionary<string, string> ReadQuery(string redirectUrl)
        {
            var uri = new Uri(redirectUrl);
            return uri.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2))
                .ToDictionary(
                    pair => Uri.UnescapeDataString(pair[0]),
                    pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty);
        }

        // ───────────────────────────────────────────────────────────────────────
        //  Amount conversion
        // ───────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateCheckout_Amount_IsMultipliedBy100()
        {
            var gw = CreateGateway();

            var result = await gw.CreateCheckoutAsync("ORD-001", 49000, "Test", "127.0.0.1");

            Assert.True(result.Success);
            Assert.Contains("vnp_Amount=4900000", result.RedirectUrl!);
        }

        [Fact]
        public async Task CreateCheckout_UsesSandboxEndpointAndRequiredVnPayParameters()
        {
            var gw = CreateGateway(baseUrl: VnPayOptions.SandboxPaymentUrl);

            var result = await gw.CreateCheckoutAsync("ORD-REQ", 49000, "Test", "127.0.0.1");

            Assert.True(result.Success);
            Assert.StartsWith(VnPayOptions.SandboxPaymentUrl + "?", result.RedirectUrl);

            var query = ReadQuery(result.RedirectUrl!);
            var requiredKeys = new[]
            {
                "vnp_Version",
                "vnp_Command",
                "vnp_TmnCode",
                "vnp_Amount",
                "vnp_CreateDate",
                "vnp_CurrCode",
                "vnp_IpAddr",
                "vnp_Locale",
                "vnp_OrderInfo",
                "vnp_OrderType",
                "vnp_ReturnUrl",
                "vnp_TxnRef",
                "vnp_SecureHash"
            };

            foreach (var key in requiredKeys)
            {
                Assert.True(query.TryGetValue(key, out var value), $"Missing {key}");
                Assert.False(string.IsNullOrWhiteSpace(value), $"{key} should not be blank");
            }
        }

        [Fact]
        public async Task CreateCheckout_TrimsConfiguredStringsBeforeBuildingUrl()
        {
            var gw = CreateGateway(
                tmnCode: " TMNCODE1 ",
                baseUrl: " https://sandbox.vnpayment.vn/paymentv2/vpcpay.html ",
                returnUrl: " https://example.com/billing/return ");

            var result = await gw.CreateCheckoutAsync("ORD-TRIM", 49000, "Test", "127.0.0.1");

            Assert.True(result.Success);
            var query = ReadQuery(result.RedirectUrl!);
            Assert.Equal("TMNCODE1", query["vnp_TmnCode"]);
            Assert.Equal("https://example.com/billing/return", query["vnp_ReturnUrl"]);
        }

        // ───────────────────────────────────────────────────────────────────────
        //  OrderCode in URL
        // ───────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateCheckout_TxnRef_IsOrderCode()
        {
            var gw = CreateGateway();

            var result = await gw.CreateCheckoutAsync("TCT-ABCDEF-1234", 49000, "Test", "127.0.0.1");

            Assert.True(result.Success);
            Assert.Contains("vnp_TxnRef=", result.RedirectUrl!);
            // URL-encoded orderCode should be present
            Assert.Contains("TCT", result.RedirectUrl!);
        }

        // ───────────────────────────────────────────────────────────────────────
        //  Secret never appears in URL or result
        // ───────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateCheckout_HashSecret_NotInRedirectUrl()
        {
            const string secret = "MY_SUPER_SECRET_KEY_XYZ";
            var gw = CreateGateway(hashSecret: secret);

            var result = await gw.CreateCheckoutAsync("ORD-002", 49000, "Test", "127.0.0.1");

            Assert.True(result.Success);
            Assert.DoesNotContain(secret, result.RedirectUrl!);
        }

        // ───────────────────────────────────────────────────────────────────────
        //  Disabled / missing config
        // ───────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateCheckout_Disabled_FailsSafely()
        {
            var gw = CreateGateway(enabled: false);

            var result = await gw.CreateCheckoutAsync("ORD-003", 49000, "Test", "127.0.0.1");

            Assert.False(result.Success);
            Assert.Equal("NOT_CONFIGURED", result.ErrorCode);
            // Error message must not reveal config details
            Assert.DoesNotContain("HashSecret", result.ErrorMessage ?? "");
            Assert.DoesNotContain("TmnCode", result.ErrorMessage ?? "");
        }

        [Fact]
        public async Task CreateCheckout_MissingTmnCode_FailsSafely()
        {
            var gw = CreateGateway(enabled: true, tmnCode: "");

            var result = await gw.CreateCheckoutAsync("ORD-004", 49000, "Test", "127.0.0.1");

            Assert.False(result.Success);
            Assert.Equal("NOT_CONFIGURED", result.ErrorCode);
        }

        [Fact]
        public async Task CreateCheckout_MissingIpnUrl_FailsBeforeRedirectingToVnPay()
        {
            var opts = Options.Create(new VnPayOptions
            {
                Enabled = true,
                TmnCode = "TMNCODETEST",
                HashSecret = "TESTSECRET1234567890",
                BaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
                ReturnUrl = "https://example.com/billing/return",
                IpnUrl = string.Empty,
                Version = "2.1.0",
                Command = "pay",
                Locale = "vn",
                Currency = "VND",
                OrderType = "other"
            });

            var gw = new VnPayGateway(opts, NullLogger<VnPayGateway>.Instance);
            var result = await gw.CreateCheckoutAsync("ORD-MISSING-IPN", 49000, "Test", "127.0.0.1");

            Assert.False(result.Success);
            Assert.Equal("NOT_CONFIGURED", result.ErrorCode);
            Assert.Null(result.RedirectUrl);
        }

        [Fact]
        public async Task CreateCheckout_PlaceholderTmnCode_FailsBeforeRedirectingToVnPay()
        {
            var gw = CreateGateway(enabled: true, tmnCode: "TESTCODE");

            var result = await gw.CreateCheckoutAsync("ORD-PLACEHOLDER-TMN", 49000, "Test", "127.0.0.1");

            Assert.False(result.Success);
            Assert.Equal("NOT_CONFIGURED", result.ErrorCode);
            Assert.Null(result.RedirectUrl);
        }

        [Fact]
        public async Task CreateCheckout_PlaceholderHashSecret_FailsBeforeRedirectingToVnPay()
        {
            var gw = CreateGateway(
                enabled: true,
                hashSecret: "TESTSECRETTESTSECRETTESTSECRET12");

            var result = await gw.CreateCheckoutAsync("ORD-PLACEHOLDER-SECRET", 49000, "Test", "127.0.0.1");

            Assert.False(result.Success);
            Assert.Equal("NOT_CONFIGURED", result.ErrorCode);
            Assert.Null(result.RedirectUrl);
        }

        [Fact]
        public async Task CreateCheckout_ErrorPageBaseUrl_FailsBeforeRedirectingToVnPay()
        {
            var gw = CreateGateway(
                baseUrl: "https://sandbox.vnpayment.vn/paymentv2/Payment/Error.html?code=72");

            var result = await gw.CreateCheckoutAsync("ORD-BAD-BASE", 49000, "Test", "127.0.0.1");

            Assert.False(result.Success);
            Assert.Equal("NOT_CONFIGURED", result.ErrorCode);
            Assert.Null(result.RedirectUrl);
        }

        // ───────────────────────────────────────────────────────────────────────
        //  IsEnabled reflects config
        // ───────────────────────────────────────────────────────────────────────

        [Fact]
        public void IsEnabled_WhenFullyConfigured_ReturnsTrue()
        {
            var gw = CreateGateway();
            Assert.True(gw.IsEnabled);
        }

        [Fact]
        public void IsEnabled_WhenDisabledFlag_ReturnsFalse()
        {
            var gw = CreateGateway(enabled: false);
            Assert.False(gw.IsEnabled);
        }

        [Fact]
        public async Task ProcessReturnAsync_ValidSignature_UsesChecksumAndTxnStatus()
        {
            const string secret = "TESTSECRET1234567890";
            var gw = CreateGateway(hashSecret: secret);
            var parameters = BuildSignedCallbackParameters(secret);

            var result = await gw.ProcessReturnAsync(parameters);

            Assert.True(result.IsVerified);
            Assert.True(result.IsPaid);
            Assert.Equal("00", result.ProviderResponseCode);
            Assert.Equal("00", result.ProviderTransactionStatus);
        }

        [Fact]
        public async Task ProcessReturnAsync_TamperedPayload_FailsVerification()
        {
            const string secret = "TESTSECRET1234567890";
            var gw = CreateGateway(hashSecret: secret);
            var parameters = BuildSignedCallbackParameters(secret);
            parameters["vnp_Amount"] = "1";

            var result = await gw.ProcessReturnAsync(parameters);

            Assert.False(result.IsVerified);
            Assert.False(result.IsPaid);
        }
    }

    // ───────────────────────────────────────────────────────────────────────────
    //  VnPaySignatureHelper tests (pure unit, no gateway infra)
    // ───────────────────────────────────────────────────────────────────────────

    public class VnPaySignatureHelperTests
    {
        [Fact]
        public void ComputeHmac512_DeterministicForSameInput()
        {
            const string secret = "SECRETKEY";
            const string data = "vnp_Amount=4900000&vnp_TxnRef=ORD-001";

            var hash1 = VnPaySignatureHelper.ComputeHmac512(secret, data);
            var hash2 = VnPaySignatureHelper.ComputeHmac512(secret, data);

            Assert.Equal(hash1, hash2);
            Assert.NotEmpty(hash1);
        }

        [Fact]
        public void ComputeHmac512_DifferentForDifferentData()
        {
            const string secret = "SECRETKEY";

            var hash1 = VnPaySignatureHelper.ComputeHmac512(secret, "data1");
            var hash2 = VnPaySignatureHelper.ComputeHmac512(secret, "data2");

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void BuildRawSignatureData_SortsKeysAscending()
        {
            var params1 = new Dictionary<string, string>
            {
                ["vnp_TxnRef"] = "ORD",
                ["vnp_Amount"] = "4900000",
                ["vnp_TmnCode"] = "TMN"
            };

            var raw = VnPaySignatureHelper.BuildRawSignatureData(params1);

            // Should start with vnp_Amount (A < T alphabetically)
            Assert.StartsWith("vnp_Amount=", raw);
        }

        [Fact]
        public void BuildRawSignatureData_ExcludesSecureHashFields()
        {
            var parms = new Dictionary<string, string>
            {
                ["vnp_Amount"] = "4900000",
                ["vnp_SecureHash"] = "SOMEHASH",
                ["vnp_SecureHashType"] = "SHA512"
            };

            var raw = VnPaySignatureHelper.BuildRawSignatureData(parms);

            Assert.DoesNotContain("vnp_SecureHash", raw);
            Assert.DoesNotContain("SHA512", raw);
            Assert.Contains("vnp_Amount", raw);
        }

        [Fact]
        public void BuildRawSignatureData_EncodesVietnameseAndSpecialCharacters()
        {
            var parms = new Dictionary<string, string>
            {
                ["vnp_OrderInfo"] = "Thanh toan gói Premium tháng 4: A/B?x=1&y=2",
                ["vnp_TxnRef"] = "ORD-ENC-01"
            };

            var raw = VnPaySignatureHelper.BuildRawSignatureData(parms);

            Assert.Contains("vnp_OrderInfo=Thanh+toan+g%C3%B3i+Premium+th%C3%A1ng+4%3A+A%2FB%3Fx%3D1%26y%3D2", raw);
            Assert.Contains("vnp_TxnRef=ORD-ENC-01", raw);
        }

        [Fact]
        public void Verify_ReturnsTrueForCorrectSignature()
        {
            const string secret = "TEST_SECRET";
            var parms = new Dictionary<string, string>
            {
                ["vnp_Amount"] = "4900000",
                ["vnp_TxnRef"] = "ORD-123"
            };

            var signature = VnPaySignatureHelper.Sign(secret, parms);
            var verified = VnPaySignatureHelper.Verify(secret, parms, signature);

            Assert.True(verified);
        }

        [Fact]
        public void Verify_ReturnsFalseForTamperedParam()
        {
            const string secret = "TEST_SECRET";
            var parms = new Dictionary<string, string>
            {
                ["vnp_Amount"] = "4900000",
                ["vnp_TxnRef"] = "ORD-123"
            };

            var signature = VnPaySignatureHelper.Sign(secret, parms);

            // Tamper with amount after signing
            parms["vnp_Amount"] = "1"; // attacker changed the amount

            var verified = VnPaySignatureHelper.Verify(secret, parms, signature);
            Assert.False(verified);
        }

        [Fact]
        public void Verify_WithVietnameseAndUrlCharacters_ReturnsTrue()
        {
            const string secret = "TEST_SECRET";
            var parms = new Dictionary<string, string>
            {
                ["vnp_Amount"] = "4900000",
                ["vnp_TxnRef"] = "ORD-UTF8-123",
                ["vnp_OrderInfo"] = "Thanh toán Premium: https://example.com/a?x=1&y=2"
            };

            var signature = VnPaySignatureHelper.Sign(secret, parms);
            var verified = VnPaySignatureHelper.Verify(secret, parms, signature);

            Assert.True(verified);
        }

        [Fact]
        public void BuildPaymentUrl_ContainsSecureHashParam()
        {
            var parms = new Dictionary<string, string>
            {
                ["vnp_Amount"] = "4900000",
                ["vnp_TxnRef"] = "ORD-ABC"
            };

            var url = VnPaySignatureHelper.BuildPaymentUrl(
                "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
                "SECRET123",
                parms);

            Assert.Contains("vnp_SecureHash=", url);
            Assert.StartsWith("https://sandbox.vnpayment.vn/", url);
        }

        [Fact]
        public void ToVnPayDate_ReturnsCorrectFormat()
        {
            // Fixed UTC time: 2024-06-15 03:00:00 UTC = 2024-06-15 10:00:00 GMT+7
            var utc = new DateTime(2024, 6, 15, 3, 0, 0, DateTimeKind.Utc);
            var vnDate = VnPaySignatureHelper.ToVnPayDate(utc);

            Assert.Equal(14, vnDate.Length);
            Assert.Equal("20240615100000", vnDate);
        }
    }
}

