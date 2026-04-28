using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.MoMo;
using TCTEnglish.Tests.TestHelpers;
using Xunit;

namespace TCTEnglish.Tests
{
    public class MoMoGatewayTests
    {
        private static MoMoGateway CreateGateway(
            Func<HttpRequestMessage, Task<HttpResponseMessage>> handler,
            MoMoOptions? overrideOptions = null,
            string environmentName = "Production")
        {
            var options = Options.Create(overrideOptions ?? new MoMoOptions
            {
                Enabled = true,
                PartnerCode = "MOMO",
                AccessKey = "ACCESS",
                SecretKey = "SECRET",
                BaseUrl = "https://test-payment.momo.vn",
                CreatePath = "/v2/gateway/api/create",
                RedirectUrl = "https://example.com/Billing/MoMoReturn",
                IpnUrl = "https://example.com/api/billing/momo/ipn",
                RequestType = "captureWallet",
                Lang = "vi"
            });

            var client = new HttpClient(new StubHttpMessageHandler(handler));
            var env = new StubWebHostEnvironment { EnvironmentName = environmentName };
            return new MoMoGateway(client, options, env, NullLogger<MoMoGateway>.Instance);
        }

        [Fact]
        public void IsEnabled_MissingRequiredCredentials_ReturnsFalse()
        {
            var options = Options.Create(new MoMoOptions
            {
                Enabled = true,
                BaseUrl = "https://test-payment.momo.vn",
                CreatePath = "/v2/gateway/api/create",
                RequestType = "captureWallet",
                Lang = "vi"
            });

            var client = new HttpClient(new StubHttpMessageHandler(_ =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

            var env = new StubWebHostEnvironment { EnvironmentName = "Production" };
            var gateway = new MoMoGateway(client, options, env, NullLogger<MoMoGateway>.Instance);
            Assert.False(gateway.IsEnabled);
        }

        [Fact]
        public async Task CreateCheckout_MockModeEnabledInDevelopment_ReturnsMockQrRedirect()
        {
            var gateway = CreateGateway(
                _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)),
                new MoMoOptions
                {
                    Enabled = true,
                    MockModeEnabled = true,
                    MockSecretKey = "mock-secret"
                },
                environmentName: "Development");

            var result = await gateway.CreateCheckoutAsync("ORD-MOCK", 49000m, "Test", "127.0.0.1");

            Assert.True(gateway.IsEnabled);
            Assert.True(result.Success);
            Assert.Equal("/Billing/MoMoQr?orderCode=ORD-MOCK", result.RedirectUrl);
            Assert.StartsWith("MOMO-MOCK-", result.ProviderRequestId);
            Assert.StartsWith("momo://mock-pay?", result.ProviderDeepLink);
        }

        [Fact]
        public async Task CreateCheckout_Success_ReturnsInternalQrRedirect()
        {
            HttpRequestMessage? capturedRequest = null;
            var gateway = CreateGateway(request =>
            {
                capturedRequest = request;
                var json = JsonSerializer.Serialize(new
                {
                    resultCode = 0,
                    message = "Successful.",
                    requestId = "REQ-1",
                    orderId = "ORD-1",
                    payUrl = "https://test-payment.momo.vn/pay",
                    deeplink = "momo://pay",
                    qrCodeUrl = "000201010212..."
                });

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            });

            var result = await gateway.CreateCheckoutAsync("ORD-1", 49000m, "Test", "127.0.0.1");

            Assert.True(result.Success);
            Assert.Equal("/Billing/MoMoQr?orderCode=ORD-1", result.RedirectUrl);
            Assert.Equal("REQ-1", result.ProviderRequestId);
            Assert.Equal("https://test-payment.momo.vn/pay", result.ProviderPaymentUrl);
            Assert.Equal("momo://pay", result.ProviderDeepLink);
            Assert.Equal("000201010212...", result.ProviderQrCodePayload);
            Assert.NotNull(capturedRequest);
            Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
            Assert.Contains("/v2/gateway/api/create", capturedRequest.RequestUri!.AbsoluteUri);
        }

        [Fact]
        public async Task CreateCheckout_ResultCodeNonZero_ReturnsFail()
        {
            var gateway = CreateGateway(_ =>
            {
                var json = JsonSerializer.Serialize(new
                {
                    resultCode = 1006,
                    message = "Canceled."
                });

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            });

            var result = await gateway.CreateCheckoutAsync("ORD-1", 49000m, "Test", "127.0.0.1");
            Assert.False(result.Success);
        }
    }
}
