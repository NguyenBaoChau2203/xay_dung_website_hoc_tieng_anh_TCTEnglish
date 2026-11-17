using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.MoMo;
using TCTEnglish.Services.Billing.VnPay;
using TCTEnglish.Tests.TestHelpers;
using Xunit;

namespace TCTEnglish.Tests
{
    /// <summary>
    /// Three-way answers from the provider queries (plan 4.4). Only a verified Success may confirm an
    /// order; everything else, including a query that failed or a transaction that is not found yet, is Unknown.
    /// </summary>
    public class ProviderQueryClientTests
    {
        private const string Secret = "SECRET123456";
        private const string TmnCode = "TMNCODE1";

        // ─── VNPay ────────────────────────────────────────────────────────────────

        private static VnPayQueryClient VnPayClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            => new(
                new HttpClient(new StubHttpMessageHandler(handler)),
                Options.Create(new VnPayOptions
                {
                    Enabled = true,
                    TmnCode = TmnCode,
                    HashSecret = Secret,
                    QueryDrUrl = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction",
                    QueryIpAddress = "127.0.0.1",
                    QueryDrCommand = "querydr"
                }),
                NullLogger<VnPayQueryClient>.Instance);

        private static HttpResponseMessage SignedVnPayAnswer(
            string responseCode, string transactionStatus, string amount = "39900000",
            string txnRef = "ORD-1", bool validSignature = true)
        {
            var fields = new Dictionary<string, string>
            {
                ["vnp_ResponseId"] = "RID-1",
                ["vnp_Command"] = "querydr",
                ["vnp_ResponseCode"] = responseCode,
                ["vnp_Message"] = "msg",
                ["vnp_TmnCode"] = TmnCode,
                ["vnp_TxnRef"] = txnRef,
                ["vnp_Amount"] = amount,
                ["vnp_BankCode"] = "NCB",
                ["vnp_PayDate"] = "20260428113000",
                ["vnp_TransactionNo"] = "14000001",
                ["vnp_TransactionType"] = "01",
                ["vnp_TransactionStatus"] = transactionStatus,
                ["vnp_OrderInfo"] = "QueryDR ORD-1",
                ["vnp_PromotionCode"] = "",
                ["vnp_PromotionAmount"] = ""
            };
            var raw = string.Join("|", new[]
            {
                fields["vnp_ResponseId"], fields["vnp_Command"], fields["vnp_ResponseCode"], fields["vnp_Message"],
                fields["vnp_TmnCode"], fields["vnp_TxnRef"], fields["vnp_Amount"], fields["vnp_BankCode"],
                fields["vnp_PayDate"], fields["vnp_TransactionNo"], fields["vnp_TransactionType"],
                fields["vnp_TransactionStatus"], fields["vnp_OrderInfo"], fields["vnp_PromotionCode"],
                fields["vnp_PromotionAmount"]
            });
            fields["vnp_SecureHash"] = validSignature
                ? VnPaySignatureHelper.ComputeHmac512(Secret, raw)
                : "0000";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(fields), Encoding.UTF8, "application/json")
            };
        }

        [Fact]
        public async Task VnPay_ResponseCode00_Status00_IsSuccess_WithAmountInVnd()
        {
            var client = VnPayClient(_ => Task.FromResult(SignedVnPayAnswer("00", "00")));

            var result = await client.QueryOrderAsync("ORD-1", DateTime.UtcNow);

            Assert.NotNull(result);
            Assert.Equal(PaymentQueryState.Success, result!.State);
            Assert.Equal(399000L, result.AmountVnd);
            Assert.Equal("20260428113000", result.PayDate);
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task VnPay_ResponseCode00_Status02_IsFailed()
        {
            var client = VnPayClient(_ => Task.FromResult(SignedVnPayAnswer("00", "02")));

            var result = await client.QueryOrderAsync("ORD-1", DateTime.UtcNow);

            Assert.Equal(PaymentQueryState.Failed, result!.State);
            Assert.False(result.IsSuccess);
        }

        [Theory]
        [InlineData("00", "01")] // not completed yet
        [InlineData("00", "04")] // reversed
        [InlineData("00", "05")] // refund processing
        [InlineData("00", "07")] // suspected fraud
        [InlineData("00", "09")] // refund rejected
        [InlineData("00", "")]   // no status at all
        [InlineData("91", "")]   // transaction not found (yet)
        [InlineData("94", "")]   // duplicated request
        [InlineData("99", "00")] // the query itself failed, whatever the status field says
        public async Task VnPay_EverythingElse_IsUnknown_AndNeverSuccess(string responseCode, string status)
        {
            var client = VnPayClient(_ => Task.FromResult(SignedVnPayAnswer(responseCode, status)));

            var result = await client.QueryOrderAsync("ORD-1", DateTime.UtcNow);

            Assert.NotNull(result);
            Assert.Equal(PaymentQueryState.Unknown, result!.State);
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task VnPay_MissingOrUnparseableAmount_LeavesAmountNull()
        {
            var client = VnPayClient(_ => Task.FromResult(SignedVnPayAnswer("00", "00", amount: "")));

            var result = await client.QueryOrderAsync("ORD-1", DateTime.UtcNow);

            Assert.Equal(PaymentQueryState.Success, result!.State);
            Assert.Null(result.AmountVnd);
        }

        [Fact]
        public async Task VnPay_BadSignature_IsNoAnswer()
        {
            var client = VnPayClient(_ => Task.FromResult(SignedVnPayAnswer("00", "00", validSignature: false)));

            Assert.Null(await client.QueryOrderAsync("ORD-1", DateTime.UtcNow));
        }

        [Fact]
        public async Task VnPay_AnswerAboutAnotherOrder_IsNoAnswer()
        {
            var client = VnPayClient(_ => Task.FromResult(SignedVnPayAnswer("00", "00", txnRef: "OTHER")));

            Assert.Null(await client.QueryOrderAsync("ORD-1", DateTime.UtcNow));
        }

        [Fact]
        public async Task VnPay_HttpError_Exception_AndGarbage_AreNoAnswer()
        {
            var http500 = VnPayClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
            var boom = VnPayClient(_ => throw new HttpRequestException("down"));
            var garbage = VnPayClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>not json</html>")
            }));

            Assert.Null(await http500.QueryOrderAsync("ORD-1", DateTime.UtcNow));
            Assert.Null(await boom.QueryOrderAsync("ORD-1", DateTime.UtcNow));
            Assert.Null(await garbage.QueryOrderAsync("ORD-1", DateTime.UtcNow));
        }

        // ─── MoMo ─────────────────────────────────────────────────────────────────

        private static MoMoOptions ConfiguredMoMo() => new()
        {
            Enabled = true,
            PartnerCode = "PC123",
            AccessKey = "AK123",
            SecretKey = "SK123",
            BaseUrl = "https://test-payment.momo.vn",
            QueryPath = "/v2/gateway/api/query",
            RedirectUrl = "https://example.com/Billing/MoMoReturn",
            IpnUrl = "https://example.com/api/billing/momo/ipn"
        };

        private static MoMoQueryClient MoMoClient(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
            MoMoOptions? options = null,
            string environment = "Production")
            => new(
                new HttpClient(new StubHttpMessageHandler(handler)),
                Options.Create(options ?? ConfiguredMoMo()),
                new StubWebHostEnvironment { EnvironmentName = environment },
                NullLogger<MoMoQueryClient>.Instance);

        private static HttpResponseMessage MoMoAnswer(
            int resultCode, long amount = 399000, string orderId = "ORD-1", string partner = "PC123", long transId = 987654321)
            => new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    partnerCode = partner,
                    orderId,
                    requestId = "x",
                    amount,
                    transId,
                    payType = "qr",
                    resultCode,
                    message = "m"
                }), Encoding.UTF8, "application/json")
            };

        [Fact]
        public async Task MoMo_Query_SendsTheSignedRequestToTheQueryPath()
        {
            HttpRequestMessage? seen = null;
            string? body = null;
            var client = MoMoClient(async (request, ct) =>
            {
                seen = request;
                body = await request.Content!.ReadAsStringAsync(ct);
                return MoMoAnswer(0);
            });

            var result = await client.QueryOrderAsync("ORD-1");

            Assert.Equal("https://test-payment.momo.vn/v2/gateway/api/query", seen!.RequestUri!.ToString());
            using var json = JsonDocument.Parse(body!);
            var root = json.RootElement;
            Assert.Equal("PC123", root.GetProperty("partnerCode").GetString());
            Assert.Equal("ORD-1", root.GetProperty("orderId").GetString());
            var requestId = root.GetProperty("requestId").GetString();
            var expected = MoMoSignatureHelper.ComputeHmacSha256(
                "SK123", $"accessKey=AK123&orderId=ORD-1&partnerCode=PC123&requestId={requestId}");
            Assert.Equal(expected, root.GetProperty("signature").GetString());
            Assert.DoesNotContain("SK123", body);
            Assert.DoesNotContain("AK123", body);
            Assert.Equal(PaymentQueryState.Success, result.State);
        }

        [Fact]
        public async Task MoMo_ResultCode0_IsSuccess_WithAmountAndTransaction()
        {
            var client = MoMoClient((_, _) => Task.FromResult(MoMoAnswer(0, amount: 399000, transId: 555)));

            var result = await client.QueryOrderAsync("ORD-1");

            Assert.Equal(PaymentQueryState.Success, result.State);
            Assert.Equal(399000L, result.AmountVnd);
            Assert.Equal("555", result.TransactionId);
            Assert.Equal("qr", result.PayType);
        }

        [Theory]
        [InlineData(1001)]
        [InlineData(1003)]
        [InlineData(1005)]
        [InlineData(1006)]
        public async Task MoMo_FinalFailureCodes_AreFailed(int code)
        {
            var client = MoMoClient((_, _) => Task.FromResult(MoMoAnswer(code)));

            var result = await client.QueryOrderAsync("ORD-1");

            Assert.Equal(PaymentQueryState.Failed, result.State);
            Assert.Null(result.AmountVnd);
        }

        [Theory]
        [InlineData(1000)] // initiated
        [InlineData(7000)] // processing
        [InlineData(7002)] // processing by the provider
        [InlineData(9000)] // authorised, not captured
        [InlineData(10)]   // system under maintenance
        [InlineData(99)]
        public async Task MoMo_OpenOrUnclearCodes_AreUnknown(int code)
        {
            var client = MoMoClient((_, _) => Task.FromResult(MoMoAnswer(code)));

            var result = await client.QueryOrderAsync("ORD-1");

            Assert.Equal(PaymentQueryState.Unknown, result.State);
        }

        [Fact]
        public async Task MoMo_AnswerForAnotherOrderOrPartner_IsUnknown()
        {
            var otherOrder = MoMoClient((_, _) => Task.FromResult(MoMoAnswer(0, orderId: "SOMEONE-ELSE")));
            var otherPartner = MoMoClient((_, _) => Task.FromResult(MoMoAnswer(0, partner: "OTHER")));

            Assert.Equal(PaymentQueryState.Unknown, (await otherOrder.QueryOrderAsync("ORD-1")).State);
            Assert.Equal(PaymentQueryState.Unknown, (await otherPartner.QueryOrderAsync("ORD-1")).State);
        }

        [Fact]
        public async Task MoMo_HttpError_BadJson_Exception_AndTimeout_AreUnknown()
        {
            var http500 = MoMoClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
            var badJson = MoMoClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("nope")
            }));
            var boom = MoMoClient((_, _) => throw new HttpRequestException("down"));
            // What HttpClient throws when its own timeout fires: a cancellation the caller did not request.
            var timeout = MoMoClient((_, _) => throw new TaskCanceledException("timeout"));

            Assert.Equal(PaymentQueryState.Unknown, (await http500.QueryOrderAsync("ORD-1")).State);
            Assert.Equal(PaymentQueryState.Unknown, (await badJson.QueryOrderAsync("ORD-1")).State);
            Assert.Equal(PaymentQueryState.Unknown, (await boom.QueryOrderAsync("ORD-1")).State);
            Assert.Equal(PaymentQueryState.Unknown, (await timeout.QueryOrderAsync("ORD-1")).State);
        }

        [Fact]
        public async Task MoMo_CallerCancellation_StillCancels()
        {
            using var cts = new CancellationTokenSource();
            var client = MoMoClient(async (_, ct) =>
            {
                cts.Cancel();
                await Task.Delay(Timeout.Infinite, ct);
                return MoMoAnswer(0);
            });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.QueryOrderAsync("ORD-1", cts.Token));
        }

        [Fact]
        public async Task MoMo_MockMode_NeverCallsOut_AndIsUnknown()
        {
            var options = ConfiguredMoMo();
            options.MockModeEnabled = true;
            var client = MoMoClient((_, _) => throw new InvalidOperationException("must not be called"), options, "Development");

            Assert.Equal(PaymentQueryState.Unknown, (await client.QueryOrderAsync("ORD-1")).State);
        }

        [Fact]
        public async Task MoMo_NotConfigured_NeverCallsOut_AndIsUnknown()
        {
            var options = ConfiguredMoMo();
            options.Enabled = false;
            var client = MoMoClient((_, _) => throw new InvalidOperationException("must not be called"), options);

            Assert.Equal(PaymentQueryState.Unknown, (await client.QueryOrderAsync("ORD-1")).State);
        }

        [Fact]
        public async Task MoMo_MockFlagOutsideDevelopment_IsIgnored_AndTheRealQueryRuns()
        {
            var options = ConfiguredMoMo();
            options.MockModeEnabled = true;
            var called = false;
            var client = MoMoClient((_, _) => { called = true; return Task.FromResult(MoMoAnswer(0)); }, options, "Production");

            var result = await client.QueryOrderAsync("ORD-1");

            Assert.True(called);
            Assert.Equal(PaymentQueryState.Success, result.State);
        }
    }
}
