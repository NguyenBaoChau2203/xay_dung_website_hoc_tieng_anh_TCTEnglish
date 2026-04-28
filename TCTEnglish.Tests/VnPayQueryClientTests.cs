using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.VnPay;
using TCTEnglish.Tests.TestHelpers;
using Xunit;

namespace TCTEnglish.Tests
{
    public class VnPayQueryClientTests
    {
        [Fact]
        public async Task QueryOrderAsync_DisabledConfig_ReturnsNull()
        {
            var client = new HttpClient(new StubHttpMessageHandler(_ =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

            var options = Options.Create(new VnPayOptions
            {
                Enabled = false
            });

            var queryClient = new VnPayQueryClient(client, options, NullLogger<VnPayQueryClient>.Instance);
            var result = await queryClient.QueryOrderAsync("ORD-1", DateTime.UtcNow);

            Assert.Null(result);
        }

        [Fact]
        public async Task QueryOrderAsync_ValidSignedResponse_ReturnsSuccessResult()
        {
            const string secret = "SECRET123456";
            const string tmnCode = "TMNCODE1";

            var options = Options.Create(new VnPayOptions
            {
                Enabled = true,
                TmnCode = tmnCode,
                HashSecret = secret,
                QueryDrUrl = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction",
                QueryIpAddress = "127.0.0.1",
                QueryDrCommand = "querydr"
            });

            var httpClient = new HttpClient(new StubHttpMessageHandler(async request =>
            {
                var body = await request.Content!.ReadAsStringAsync();
                Assert.Contains("\"vnp_Command\":\"querydr\"", body);
                Assert.Contains("\"vnp_TxnRef\":\"ORD-1\"", body);

                var payload = new
                {
                    vnp_ResponseId = "RID-1",
                    vnp_Command = "querydr",
                    vnp_ResponseCode = "00",
                    vnp_Message = "Success",
                    vnp_TmnCode = tmnCode,
                    vnp_TxnRef = "ORD-1",
                    vnp_Amount = "4900000",
                    vnp_BankCode = "NCB",
                    vnp_PayDate = "20260428113000",
                    vnp_TransactionNo = "14000001",
                    vnp_TransactionType = "01",
                    vnp_TransactionStatus = "00",
                    vnp_OrderInfo = "QueryDR ORD-1",
                    vnp_PromotionCode = "",
                    vnp_PromotionAmount = ""
                };

                var raw = string.Join("|", new[]
                {
                    payload.vnp_ResponseId,
                    payload.vnp_Command,
                    payload.vnp_ResponseCode,
                    payload.vnp_Message,
                    payload.vnp_TmnCode,
                    payload.vnp_TxnRef,
                    payload.vnp_Amount,
                    payload.vnp_BankCode,
                    payload.vnp_PayDate,
                    payload.vnp_TransactionNo,
                    payload.vnp_TransactionType,
                    payload.vnp_TransactionStatus,
                    payload.vnp_OrderInfo,
                    payload.vnp_PromotionCode,
                    payload.vnp_PromotionAmount
                });

                var secureHash = VnPaySignatureHelper.ComputeHmac512(secret, raw);
                var json = JsonSerializer.Serialize(new
                {
                    payload.vnp_ResponseId,
                    payload.vnp_Command,
                    payload.vnp_ResponseCode,
                    payload.vnp_Message,
                    payload.vnp_TmnCode,
                    payload.vnp_TxnRef,
                    payload.vnp_Amount,
                    payload.vnp_BankCode,
                    payload.vnp_PayDate,
                    payload.vnp_TransactionNo,
                    payload.vnp_TransactionType,
                    payload.vnp_TransactionStatus,
                    payload.vnp_OrderInfo,
                    payload.vnp_PromotionCode,
                    payload.vnp_PromotionAmount,
                    vnp_SecureHash = secureHash
                });

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }));

            var queryClient = new VnPayQueryClient(httpClient, options, NullLogger<VnPayQueryClient>.Instance);
            var result = await queryClient.QueryOrderAsync("ORD-1", DateTime.UtcNow);

            Assert.NotNull(result);
            Assert.True(result!.IsSuccess);
            Assert.False(result.IsCancelled);
            Assert.Equal("00", result.ResponseCode);
            Assert.Equal("14000001", result.TransactionNo);
            Assert.Equal("00", result.TransactionStatus);
        }
    }
}
