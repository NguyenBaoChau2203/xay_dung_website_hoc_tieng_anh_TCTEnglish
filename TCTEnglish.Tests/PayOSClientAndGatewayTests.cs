using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.PayOS;
using TCTEnglish.Tests.TestHelpers;
using Xunit;

namespace TCTEnglish.Tests
{
    public class PayOSClientAndGatewayTests
    {
        private const string ClientId = "test-client-id-0001";
        private const string ApiKey = "test-api-key-0002";
        private const string ChecksumKey = "test-checksum-key-0003";

        private static PayOSOptions Options(Action<PayOSOptions>? change = null)
        {
            var options = new PayOSOptions
            {
                Enabled = true,
                ClientId = ClientId,
                ApiKey = ApiKey,
                ChecksumKey = ChecksumKey,
                PublicBaseUrl = "https://tct.example.com"
            };
            change?.Invoke(options);
            return options;
        }

        // ── client ───────────────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateLink_SendsKeysAndASignedBody_AndReadsTheCheckoutPage()
        {
            var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
                "{\"code\":\"00\",\"desc\":\"success\",\"data\":{\"checkoutUrl\":\"https://pay.payos.vn/web/abc\",\"qrCode\":\"000201...\",\"paymentLinkId\":\"abc\",\"orderCode\":123}}"));
            var client = NewClient(handler, Options(), out _);

            var result = await client.CreatePaymentLinkAsync(new PayOSCreateLinkRequest(
                250101120000123, 49000, "TCT000123", "https://tct.example.com/Billing/PayOSReturn?ref=TCT-1",
                "https://tct.example.com/Billing/PayOSCancel?ref=TCT-1", 1900000000, "Premium"));

            Assert.True(result.Success);
            Assert.Equal("https://pay.payos.vn/web/abc", result.CheckoutUrl);
            Assert.Equal("000201...", result.QrCode);
            Assert.Equal("abc", result.PaymentLinkId);

            var sent = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, sent.Method);
            Assert.Equal("https://api-merchant.payos.vn/v2/payment-requests", sent.Uri);
            Assert.Equal(ClientId, sent.Headers["x-client-id"]);
            Assert.Equal(ApiKey, sent.Headers["x-api-key"]);

            using var body = JsonDocument.Parse(sent.Body!);
            var root = body.RootElement;
            Assert.Equal(250101120000123, root.GetProperty("orderCode").GetInt64());
            Assert.Equal(49000, root.GetProperty("amount").GetInt64());
            Assert.Equal("TCT000123", root.GetProperty("description").GetString());
            Assert.Equal(1900000000, root.GetProperty("expiredAt").GetInt64());
            Assert.Equal(
                PayOSSignature.ForPaymentRequest(
                    49000, "https://tct.example.com/Billing/PayOSCancel?ref=TCT-1", "TCT000123", 250101120000123,
                    "https://tct.example.com/Billing/PayOSReturn?ref=TCT-1", ChecksumKey),
                root.GetProperty("signature").GetString());
            Assert.DoesNotContain(ChecksumKey, sent.Body!, StringComparison.Ordinal);
            Assert.DoesNotContain(ApiKey, sent.Body!, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CreateLink_ARefusal_IsAGenericError_AndTheDuplicateCodeIsFlagged()
        {
            var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "{\"code\":\"231\",\"desc\":\"Đơn thanh toán đã tồn tại\"}"));
            var client = NewClient(handler, Options(), out _);

            var result = await client.CreatePaymentLinkAsync(
                new PayOSCreateLinkRequest(1, 49000, "TCT000001", "https://a.example/r", "https://a.example/c", null, null));

            Assert.False(result.Success);
            Assert.True(result.IsDuplicateOrderCode);
            Assert.Equal("PAYOS_231", result.ErrorCode);
            Assert.DoesNotContain("tồn tại", result.ErrorMessage!, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("{\"code\":\"00\",\"data\":{\"checkoutUrl\":\"http://pay.payos.vn/web/abc\"}}")]
        [InlineData("{\"code\":\"00\",\"data\":{\"checkoutUrl\":\"\"}}")]
        [InlineData("{\"code\":\"00\"}")]
        [InlineData("not json")]
        public async Task CreateLink_AnUnusableAnswer_IsAFailure(string body)
        {
            var client = NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, body)), Options(), out _);

            var result = await client.CreatePaymentLinkAsync(
                new PayOSCreateLinkRequest(1, 49000, "TCT000001", "https://a.example/r", "https://a.example/c", null, null));

            Assert.False(result.Success);
        }

        [Fact]
        public async Task GetPayment_ReadsTheStatusAmountsAndReference()
        {
            var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
                "{\"code\":\"00\",\"data\":{\"id\":\"link-1\",\"orderCode\":250101120000123,\"amount\":49000,\"amountPaid\":49000,\"status\":\"paid\",\"transactions\":[{\"reference\":\"FT123\"}]}}"));
            var client = NewClient(handler, Options(), out _);

            var info = await client.GetPaymentAsync(250101120000123);

            Assert.NotNull(info);
            Assert.Equal(PayOSStatuses.Paid, info!.Status);
            Assert.Equal(49000, info.Amount);
            Assert.Equal(49000, info.AmountPaid);
            Assert.Equal("link-1", info.PaymentLinkId);
            Assert.Equal("FT123", info.Reference);
            Assert.Equal(HttpMethod.Get, handler.Requests.Single().Method);
            Assert.EndsWith("/v2/payment-requests/250101120000123", handler.Requests.Single().Uri, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetPayment_AnswerForAnotherOrder_IsNotBelieved()
        {
            var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
                "{\"code\":\"00\",\"data\":{\"orderCode\":999,\"amount\":49000,\"amountPaid\":49000,\"status\":\"PAID\"}}"));

            Assert.Null(await NewClient(handler, Options(), out _).GetPaymentAsync(250101120000123));
        }

        [Fact]
        public async Task GetPayment_ServerErrorNetworkErrorAndTimeout_AreNullNotExceptions()
        {
            Assert.Null(await NewClient(new StubHandler(_ => Json(HttpStatusCode.InternalServerError, "{}")), Options(), out _).GetPaymentAsync(1));
            Assert.Null(await NewClient(new StubHandler(_ => throw new HttpRequestException("down")), Options(), out _).GetPaymentAsync(1));
            Assert.Null(await NewClient(new StubHandler(_ => throw new TaskCanceledException("timeout")), Options(), out _).GetPaymentAsync(1));
            Assert.Null(await NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, "{\"code\":\"101\"}")), Options(), out _).GetPaymentAsync(1));
        }

        [Fact]
        public async Task Cancel_PostsTheReason_AndReportsTheOutcome()
        {
            var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "{\"code\":\"00\",\"data\":{\"status\":\"CANCELLED\"}}"));
            var client = NewClient(handler, Options(), out _);

            Assert.True(await client.CancelAsync(250101120000123, "Order expired"));

            var sent = handler.Requests.Single();
            Assert.Equal(HttpMethod.Post, sent.Method);
            Assert.EndsWith("/v2/payment-requests/250101120000123/cancel", sent.Uri, StringComparison.Ordinal);
            Assert.Contains("Order expired", sent.Body!, StringComparison.Ordinal);
            Assert.False(await NewClient(new StubHandler(_ => Json(HttpStatusCode.OK, "{\"code\":\"20\"}")), Options(), out _).CancelAsync(1, "x"));
        }

        [Fact]
        public async Task TheClientNeverLogsAKeyASignatureOrABody()
        {
            var logger = new ListLogger<PayOSClient>();
            var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "{\"code\":\"20\",\"desc\":\"private-desc\",\"signature\":\"sig-abc\"}"));
            var client = new PayOSClient(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(Options()), logger);

            await client.CreatePaymentLinkAsync(
                new PayOSCreateLinkRequest(1, 49000, "TCT000001", "https://a.example/r", "https://a.example/c", null, null));
            await client.GetPaymentAsync(1);
            await new PayOSClient(new HttpClient(new StubHandler(_ => throw new HttpRequestException("boom"))),
                Microsoft.Extensions.Options.Options.Create(Options()), logger).GetPaymentAsync(1);

            var all = string.Join("\n", logger.Messages);
            Assert.NotEmpty(logger.Messages);
            foreach (var secret in new[] { ClientId, ApiKey, ChecksumKey, "sig-abc", "private-desc" })
            {
                Assert.DoesNotContain(secret, all, StringComparison.Ordinal);
            }
        }

        // ── gateway ──────────────────────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Checkout_Real_SendsAShortDescription_AbsoluteUrlsCarryingOurRef_AndTheLinkExpiry()
        {
            var client = new FakePayOSClient
            {
                Create = _ => new PayOSCreateLinkResult(true, "https://pay.payos.vn/web/xyz", "QR-PAYLOAD", "xyz")
            };
            var gateway = NewGateway(client, Options(), "Production");

            var before = DateTimeOffset.UtcNow;
            var result = await gateway.CreateCheckoutAsync("TCT-20261120-AB12CD34", 49000m, "Thanh toan goi Premium p don hang x", "1.2.3.4");

            Assert.True(result.Success);
            Assert.Equal("https://pay.payos.vn/web/xyz", result.RedirectUrl);
            Assert.Equal("https://pay.payos.vn/web/xyz", result.ProviderPaymentUrl);
            Assert.Equal("QR-PAYLOAD", result.ProviderQrCodePayload);
            Assert.Null(result.ProviderDeepLink);

            var sent = Assert.Single(client.CreateRequests);
            Assert.True(PayOSOrderCode.TryParse(result.ProviderRequestId, out var code));
            Assert.Equal(code, sent.OrderCode);
            Assert.Equal(49000, sent.Amount);
            Assert.Equal(PayOSOrderCode.Description(code), sent.Description);
            Assert.True(sent.Description.Length <= 9);
            Assert.Equal("https://tct.example.com/Billing/PayOSReturn?ref=TCT-20261120-AB12CD34", sent.ReturnUrl);
            Assert.Equal("https://tct.example.com/Billing/PayOSCancel?ref=TCT-20261120-AB12CD34", sent.CancelUrl);
            Assert.InRange(sent.ExpiredAtUnixSeconds!.Value, before.AddMinutes(14).ToUnixTimeSeconds(), before.AddMinutes(16).ToUnixTimeSeconds());
        }

        [Fact]
        public async Task Checkout_Real_ATakenCodeIsRetriedOnce_WithANewCode()
        {
            var calls = 0;
            var client = new FakePayOSClient
            {
                Create = _ => ++calls == 1
                    ? new PayOSCreateLinkResult(false, ErrorCode: "PAYOS_231", ErrorMessage: "x", IsDuplicateOrderCode: true)
                    : new PayOSCreateLinkResult(true, "https://pay.payos.vn/web/xyz", null, "xyz")
            };

            var result = await NewGateway(client, Options(), "Production").CreateCheckoutAsync("TCT-1", 49000m, "d", "ip");

            Assert.True(result.Success);
            Assert.Equal(2, client.CreateRequests.Count);
            Assert.Equal(client.CreateRequests[1].OrderCode.ToString(), result.ProviderRequestId);
        }

        [Fact]
        public async Task Checkout_Real_AnotherFailure_IsNotRetried_AndKeepsOurWording()
        {
            var client = new FakePayOSClient
            {
                Create = _ => new PayOSCreateLinkResult(false, ErrorCode: "PAYOS_20", ErrorMessage: "payOS không thể tạo giao dịch lúc này.")
            };

            var result = await NewGateway(client, Options(), "Production").CreateCheckoutAsync("TCT-1", 49000m, "d", "ip");

            Assert.False(result.Success);
            Assert.Equal("PAYOS_20", result.ErrorCode);
            Assert.Single(client.CreateRequests);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(999)]
        [InlineData(50000001)]
        public async Task Checkout_AnAmountOutOfRange_IsRefused_WithoutCallingPayOS(int amount)
        {
            var client = new FakePayOSClient();

            var result = await NewGateway(client, Options(), "Production").CreateCheckoutAsync("TCT-1", amount, "d", "ip");

            Assert.False(result.Success);
            Assert.Equal("INVALID_AMOUNT", result.ErrorCode);
            Assert.Empty(client.CreateRequests);
        }

        [Fact]
        public async Task Checkout_AFractionalAmount_IsRefused()
        {
            var result = await NewGateway(new FakePayOSClient(), Options(), "Production").CreateCheckoutAsync("TCT-1", 49000.5m, "d", "ip");

            Assert.Equal("INVALID_AMOUNT", result.ErrorCode);
        }

        [Fact]
        public async Task Checkout_NotConfigured_IsRefused()
        {
            var gateway = NewGateway(new FakePayOSClient(), new PayOSOptions(), "Production");

            Assert.False(gateway.IsEnabled);
            Assert.Equal("NOT_CONFIGURED", (await gateway.CreateCheckoutAsync("TCT-1", 49000m, "d", "ip")).ErrorCode);
        }

        [Fact]
        public async Task Mock_InDevelopment_NeedsNoKey_AndNeverCallsPayOS()
        {
            var client = new FakePayOSClient();
            var gateway = NewGateway(client, new PayOSOptions { Enabled = true, MockMode = true }, "Development");

            var result = await gateway.CreateCheckoutAsync("TCT-20261120-AB12CD34", 49000m, "d", "ip");

            Assert.True(gateway.IsEnabled);
            Assert.True(result.Success);
            Assert.Equal("/Billing/PayOSMock?orderCode=TCT-20261120-AB12CD34", result.RedirectUrl);
            Assert.True(PayOSOrderCode.TryParse(result.ProviderRequestId, out _));
            Assert.StartsWith("PAYOS_MOCK|", result.ProviderQrCodePayload, StringComparison.Ordinal);
            Assert.Empty(client.CreateRequests);
        }

        [Theory]
        [InlineData("Production")]
        [InlineData("Staging")]
        public void Mock_OutsideDevelopment_IsNotEnabled(string environment)
        {
            Assert.False(NewGateway(new FakePayOSClient(), new PayOSOptions { Enabled = true, MockMode = true }, environment).IsEnabled);
        }

        [Fact]
        public async Task Return_PaidAtPayOS_WithTheFullAmount_IsPaid()
        {
            var client = new FakePayOSClient { Get = _ => new PayOSPaymentInfo(123, 49000, 49000, PayOSStatuses.Paid, "link", "FT9") };

            var result = await NewGateway(client, Options(), "Production").ProcessReturnAsync(
                new Dictionary<string, string> { ["orderCode"] = "123", ["status"] = "PAID", ["code"] = "00" });

            Assert.True(result.IsVerified);
            Assert.True(result.IsPaid);
            Assert.Equal("123", result.ProviderRequestId);
            Assert.Equal("FT9", result.ProviderTransactionId);
            Assert.Equal(PayOSStatuses.Paid, result.ProviderTransactionStatus);
        }

        [Fact]
        public async Task Return_TheQueryClaimsPaid_ButPayOSSaysPending_IsNotPaid()
        {
            var client = new FakePayOSClient { Get = _ => new PayOSPaymentInfo(123, 49000, 0, PayOSStatuses.Pending, "link", null) };

            var result = await NewGateway(client, Options(), "Production").ProcessReturnAsync(
                new Dictionary<string, string> { ["orderCode"] = "123", ["status"] = "PAID", ["code"] = "00", ["cancel"] = "false" });

            Assert.True(result.IsVerified);
            Assert.False(result.IsPaid);
            Assert.Equal(PayOSStatuses.Pending, result.ProviderTransactionStatus);
        }

        [Fact]
        public async Task Return_PartlyPaid_IsNotPaid()
        {
            var client = new FakePayOSClient { Get = _ => new PayOSPaymentInfo(123, 49000, 20000, PayOSStatuses.Paid, "link", "FT") };

            var result = await NewGateway(client, Options(), "Production").ProcessReturnAsync(new Dictionary<string, string> { ["orderCode"] = "123" });

            Assert.False(result.IsPaid);
        }

        [Fact]
        public async Task Return_PayOSDidNotAnswer_IsNotVerified()
        {
            var client = new FakePayOSClient { Get = _ => null };

            var result = await NewGateway(client, Options(), "Production").ProcessReturnAsync(
                new Dictionary<string, string> { ["orderCode"] = "123", ["status"] = "PAID" });

            Assert.False(result.IsVerified);
            Assert.False(result.IsPaid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("-1")]
        public async Task Return_WithoutAValidCode_IsNotVerified_AndNothingIsAsked(string? code)
        {
            var client = new FakePayOSClient();
            var query = new Dictionary<string, string>();
            if (code != null) query["orderCode"] = code;

            var result = await NewGateway(client, Options(), "Production").ProcessReturnAsync(query);

            Assert.False(result.IsVerified);
            Assert.Empty(client.GetCalls);
        }

        [Fact]
        public async Task Return_InMock_NeverReportsPaid_AndBelievesOnlyTheCancelFlag()
        {
            var gateway = NewGateway(new FakePayOSClient(), new PayOSOptions { Enabled = true, MockMode = true }, "Development");

            var paid = await gateway.ProcessReturnAsync(new Dictionary<string, string> { ["orderCode"] = "123", ["status"] = "PAID" });
            var cancelled = await gateway.ProcessReturnAsync(new Dictionary<string, string> { ["orderCode"] = "123", ["cancel"] = "true" });

            Assert.True(paid.IsVerified);
            Assert.False(paid.IsPaid);
            Assert.Equal(PayOSStatuses.Pending, paid.ProviderTransactionStatus);
            Assert.Equal(PayOSStatuses.Cancelled, cancelled.ProviderTransactionStatus);
        }

        [Fact]
        public async Task Webhook_ADocumentedSignedPayment_IsValidAndPaid()
        {
            var gateway = NewGateway(new FakePayOSClient(), Options(o => o.ChecksumKey = DocsKey), "Production");

            var result = await gateway.ProcessIpnAsync(DocsWebhook());

            Assert.True(result.SignatureValid);
            Assert.True(result.IsPaid);
            Assert.Equal("123", result.ProviderRequestId);
            Assert.Equal("TF230204212323", result.ProviderTransactionId);
            Assert.Equal("00", result.ProviderResponseCode);
            Assert.Equal(PayOSStatuses.Paid, result.ProviderTransactionStatus);
        }

        [Fact]
        public async Task Webhook_ATamperedAmountAWrongKeyOrNoSignature_IsInvalid()
        {
            var gateway = NewGateway(new FakePayOSClient(), Options(o => o.ChecksumKey = DocsKey), "Production");

            var tampered = DocsWebhook();
            tampered["data.amount"] = "1";
            var noSignature = DocsWebhook();
            noSignature.Remove("signature");
            var onlyTop = new Dictionary<string, string> { ["code"] = "00", ["signature"] = DocsSignature };

            Assert.False((await gateway.ProcessIpnAsync(tampered)).SignatureValid);
            Assert.False((await gateway.ProcessIpnAsync(noSignature)).SignatureValid);
            Assert.False((await gateway.ProcessIpnAsync(onlyTop)).SignatureValid);
            Assert.False((await NewGateway(new FakePayOSClient(), Options(o => o.ChecksumKey = "another-key"), "Production")
                .ProcessIpnAsync(DocsWebhook())).SignatureValid);
        }

        [Theory]
        [InlineData("code", "01")]
        [InlineData("data.code", "01")]
        [InlineData("success", "false")]
        public async Task Webhook_ValidlySignedButNotASuccess_IsNotPaid(string field, string value)
        {
            // Re-sign the changed data so only the success rule is under test.
            var parameters = DocsWebhook();
            parameters[field] = value;
            if (field.StartsWith("data.", StringComparison.Ordinal))
            {
                var data = parameters.Where(kv => kv.Key.StartsWith("data.", StringComparison.Ordinal))
                    .ToDictionary(kv => kv.Key["data.".Length..], kv => (string?)kv.Value);
                parameters["signature"] = PayOSSignature.ForData(data, DocsKey);
            }

            var result = await NewGateway(new FakePayOSClient(), Options(o => o.ChecksumKey = DocsKey), "Production").ProcessIpnAsync(parameters);

            Assert.True(result.SignatureValid);
            Assert.False(result.IsPaid);
        }

        [Fact]
        public async Task Webhook_InMock_IsSignedWithTheMockKey()
        {
            var options = new PayOSOptions { Enabled = true, MockMode = true };
            var data = new Dictionary<string, string?> { ["orderCode"] = "55", ["amount"] = "49000", ["code"] = "00", ["reference"] = "MOCK-1" };
            var parameters = new Dictionary<string, string>
            {
                ["code"] = "00",
                ["success"] = "true",
                ["signature"] = PayOSSignature.ForData(data, PayOSOptions.DefaultMockChecksumKey)
            };
            foreach (var pair in data) parameters["data." + pair.Key] = pair.Value!;

            var result = await NewGateway(new FakePayOSClient(), options, "Development").ProcessIpnAsync(parameters);

            Assert.True(result.SignatureValid);
            Assert.True(result.IsPaid);
        }

        private const string DocsKey = "1a54716c8f0efb2744fb28b6e38b25da7f67a925d98bc1c18bd8faaecadd7675";
        private const string DocsSignature = "412e915d2871504ed31be63c8f62a149a4410d34c4c42affc9006ef9917eaa03";

        private static Dictionary<string, string> DocsWebhook() => new()
        {
            ["code"] = "00",
            ["desc"] = "success",
            ["success"] = "true",
            ["signature"] = DocsSignature,
            ["data.orderCode"] = "123",
            ["data.amount"] = "3000",
            ["data.description"] = "VQRIO123",
            ["data.accountNumber"] = "12345678",
            ["data.reference"] = "TF230204212323",
            ["data.transactionDateTime"] = "2023-02-04 18:25:00",
            ["data.currency"] = "VND",
            ["data.paymentLinkId"] = "124c33293c43417ab7879e14c8d9eb18",
            ["data.code"] = "00",
            ["data.desc"] = "Thành công",
            ["data.counterAccountBankId"] = "",
            ["data.counterAccountBankName"] = "",
            ["data.counterAccountName"] = "",
            ["data.counterAccountNumber"] = "",
            ["data.virtualAccountName"] = "",
            ["data.virtualAccountNumber"] = ""
        };

        // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

        private static PayOSClient NewClient(StubHandler handler, PayOSOptions options, out ListLogger<PayOSClient> logger)
        {
            logger = new ListLogger<PayOSClient>();
            return new PayOSClient(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(options), logger);
        }

        private static PayOSGateway NewGateway(FakePayOSClient client, PayOSOptions options, string environment)
            => new(
                client,
                Microsoft.Extensions.Options.Options.Create(options),
                new StubWebHostEnvironment { EnvironmentName = environment },
                NullLogger<PayOSGateway>.Instance);

        private static HttpResponseMessage Json(HttpStatusCode status, string body)
            => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private sealed record SentRequest(HttpMethod Method, string Uri, Dictionary<string, string> Headers, string? Body);

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

            public List<SentRequest> Requests { get; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
                var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                Requests.Add(new SentRequest(request.Method, request.RequestUri!.ToString(), headers, body));
                return _respond(request);
            }
        }

        private sealed class FakePayOSClient : IPayOSClient
        {
            public Func<PayOSCreateLinkRequest, PayOSCreateLinkResult> Create { get; set; } =
                _ => new PayOSCreateLinkResult(false, ErrorCode: "UNSET");

            public Func<long, PayOSPaymentInfo?> Get { get; set; } = _ => null;

            public List<PayOSCreateLinkRequest> CreateRequests { get; } = new();
            public List<long> GetCalls { get; } = new();

            public Task<PayOSCreateLinkResult> CreatePaymentLinkAsync(PayOSCreateLinkRequest request, CancellationToken ct = default)
            {
                CreateRequests.Add(request);
                return Task.FromResult(Create(request));
            }

            public Task<PayOSPaymentInfo?> GetPaymentAsync(long orderCode, CancellationToken ct = default)
            {
                GetCalls.Add(orderCode);
                return Task.FromResult(Get(orderCode));
            }

            public Task<bool> CancelAsync(long orderCode, string reason, CancellationToken ct = default) => Task.FromResult(true);
        }

        private sealed class ListLogger<T> : ILogger<T>
        {
            public List<string> Messages { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Messages.Add(formatter(state, exception));
                if (exception != null)
                {
                    Messages.Add(exception.ToString());
                }
            }
        }
    }
}
