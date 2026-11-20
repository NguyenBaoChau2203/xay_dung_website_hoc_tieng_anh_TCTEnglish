using System.Collections.Generic;
using System.Text.Json;
using TCTEnglish.Services.Billing.PayOS;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>
    /// Known-answer tests for the payOS signatures. Vector 1 is the sample of the official documentation page
    /// "Kiểm tra dữ liệu với signature" (its JS/Python code samples sign the webhook data below with this key and
    /// get <c>412e915d…</c>; the sample payload on the webhook page still shows an older value). Vector 2 was
    /// computed with an independent implementation (<c>openssl dgst -sha256 -hmac</c>).
    /// </summary>
    public class PayOSSignatureTests
    {
        private const string DocsChecksumKey = "1a54716c8f0efb2744fb28b6e38b25da7f67a925d98bc1c18bd8faaecadd7675";
        private const string DocsWebhookSignature = "412e915d2871504ed31be63c8f62a149a4410d34c4c42affc9006ef9917eaa03";

        private static Dictionary<string, string?> DocsWebhookData() => new()
        {
            ["orderCode"] = "123",
            ["amount"] = "3000",
            ["description"] = "VQRIO123",
            ["accountNumber"] = "12345678",
            ["reference"] = "TF230204212323",
            ["transactionDateTime"] = "2023-02-04 18:25:00",
            ["currency"] = "VND",
            ["paymentLinkId"] = "124c33293c43417ab7879e14c8d9eb18",
            ["code"] = "00",
            ["desc"] = "Thành công",
            ["counterAccountBankId"] = "",
            ["counterAccountBankName"] = "",
            ["counterAccountName"] = "",
            ["counterAccountNumber"] = "",
            ["virtualAccountName"] = "",
            ["virtualAccountNumber"] = ""
        };

        [Fact]
        public void ForData_MatchesTheDocumentationVector()
        {
            Assert.Equal(DocsWebhookSignature, PayOSSignature.ForData(DocsWebhookData(), DocsChecksumKey));
        }

        [Fact]
        public void ForData_DoesNotDependOnTheOrderOfTheFields_AndTreatsNullAsEmpty()
        {
            var shuffled = new Dictionary<string, string?>();
            foreach (var key in new List<string>(DocsWebhookData().Keys))
            {
                shuffled[key] = DocsWebhookData()[key];
            }

            // Re-insert in reverse order, and make two of the empty fields null.
            var reversed = new Dictionary<string, string?>();
            var keys = new List<string>(shuffled.Keys);
            keys.Reverse();
            foreach (var key in keys)
            {
                reversed[key] = key is "counterAccountName" or "virtualAccountNumber" ? null : shuffled[key];
            }

            Assert.Equal(DocsWebhookSignature, PayOSSignature.ForData(reversed, DocsChecksumKey));
        }

        [Fact]
        public void ForData_ChangingAnyValue_ChangesTheSignature()
        {
            var data = DocsWebhookData();
            data["amount"] = "3001";

            Assert.NotEqual(DocsWebhookSignature, PayOSSignature.ForData(data, DocsChecksumKey));
        }

        [Fact]
        public void ForPaymentRequest_MatchesTheIndependentVector()
        {
            var signature = PayOSSignature.ForPaymentRequest(
                amount: 10000,
                cancelUrl: "https://example.com/cancel?ref=TCT-1",
                description: "TCT123456",
                orderCode: 250101120000123,
                returnUrl: "https://example.com/return?ref=TCT-1",
                checksumKey: DocsChecksumKey);

            Assert.Equal("db157a992ac879909ced8aa86cdfee5600de78a25e1231a7f37300b423db2a92", signature);
        }

        [Fact]
        public void Verify_IsCaseInsensitive_AndRejectsMissingOrWrongValues()
        {
            Assert.True(PayOSSignature.Verify(DocsWebhookSignature, DocsWebhookSignature.ToUpperInvariant()));
            Assert.False(PayOSSignature.Verify(DocsWebhookSignature, null));
            Assert.False(PayOSSignature.Verify(DocsWebhookSignature, ""));
            Assert.False(PayOSSignature.Verify(DocsWebhookSignature, "deadbeef"));
        }

        [Theory]
        [InlineData("\"text\"", "text")]
        [InlineData("3000", "3000")]
        [InlineData("true", "true")]
        [InlineData("false", "false")]
        [InlineData("null", "")]
        [InlineData("[1,2]", "[1,2]")]
        public void ToSignedText_FollowsTheDocumentedConversion(string json, string expected)
        {
            using var document = JsonDocument.Parse(json);

            Assert.Equal(expected, PayOSSignature.ToSignedText(document.RootElement));
        }
    }
}
