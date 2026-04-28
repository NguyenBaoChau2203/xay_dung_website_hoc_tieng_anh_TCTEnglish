using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Billing.MoMo;
using TCTEnglish.Services.Billing.VnPay;
using TCTEnglish.Tests.TestHelpers;
using Xunit;

namespace TCTEnglish.Tests
{
    public class PaymentProviderHealthServiceTests
    {
        [Fact]
        public void GetProviderHealthStatus_MoMoMissingRequiredFields_IsNotConfigured()
        {
            var service = CreateService(new MoMoOptions
            {
                Enabled = true,
                BaseUrl = "https://test-payment.momo.vn",
                CreatePath = "/v2/gateway/api/create",
                RequestType = "captureWallet",
                Lang = "vi"
            });

            var momo = service.GetProviderHealthStatus()
                .Single(p => p.ProviderCode == PaymentProviders.MoMo);

            Assert.True(momo.Enabled);
            Assert.False(momo.Configured);
            Assert.Contains(momo.MissingFields, x => x.Contains("PartnerCode"));
            Assert.Contains(momo.MissingFields, x => x.Contains("AccessKey"));
            Assert.Contains(momo.MissingFields, x => x.Contains("SecretKey"));
            Assert.Contains(momo.MissingFields, x => x.Contains("RedirectUrl"));
            Assert.Contains(momo.MissingFields, x => x.Contains("IpnUrl"));
        }

        [Fact]
        public void GetProviderHealthStatus_MoMoFullyConfigured_IsConfigured()
        {
            var service = CreateService(new MoMoOptions
            {
                Enabled = true,
                PartnerCode = "MOMO_PARTNER",
                AccessKey = "ACCESS_KEY",
                SecretKey = "SECRET_KEY",
                BaseUrl = "https://test-payment.momo.vn",
                CreatePath = "/v2/gateway/api/create",
                RedirectUrl = "https://example.com/Billing/MoMoReturn",
                IpnUrl = "https://example.com/api/billing/momo/ipn",
                RequestType = "captureWallet",
                Lang = "vi"
            });

            var momo = service.GetProviderHealthStatus()
                .Single(p => p.ProviderCode == PaymentProviders.MoMo);

            Assert.True(momo.Enabled);
            Assert.True(momo.Configured);
            Assert.Empty(momo.MissingFields);
        }

        [Fact]
        public void GetProviderHealthStatus_MoMoMockModeInDevelopment_IsConfigured()
        {
            var service = CreateService(new MoMoOptions
            {
                Enabled = true,
                MockModeEnabled = true
            });

            var momo = service.GetProviderHealthStatus()
                .Single(p => p.ProviderCode == PaymentProviders.MoMo);

            Assert.True(momo.Enabled);
            Assert.True(momo.Configured);
            Assert.Empty(momo.MissingFields);
        }

        [Fact]
        public void GetProviderHealthStatus_VnPayMissingIpnUrl_IsNotConfigured()
        {
            var service = CreateService(
                new MoMoOptions { Enabled = false },
                new VnPayOptions
                {
                    Enabled = true,
                    TmnCode = "TMN123456",
                    HashSecret = "HASH_SECRET_123",
                    BaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
                    ReturnUrl = "https://example.com/billing/vnpay-return",
                    IpnUrl = string.Empty
                });

            var vnPay = service.GetProviderHealthStatus()
                .Single(p => p.ProviderCode == PaymentProviders.VNPay);

            Assert.True(vnPay.Enabled);
            Assert.False(vnPay.Configured);
            Assert.Contains(vnPay.MissingFields, x => x.Contains("IpnUrl"));
            Assert.False(vnPay.HasIpnUrl);
        }

        [Fact]
        public void GetProviderHealthStatus_VnPayFullyConfigured_IsConfigured()
        {
            var service = CreateService(
                new MoMoOptions { Enabled = false },
                new VnPayOptions
                {
                    Enabled = true,
                    TmnCode = "TMN123456",
                    HashSecret = "HASH_SECRET_123",
                    BaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
                    ReturnUrl = "https://example.com/billing/vnpay-return",
                    IpnUrl = "https://example.com/api/billing/vnpay/ipn"
                });

            var vnPay = service.GetProviderHealthStatus()
                .Single(p => p.ProviderCode == PaymentProviders.VNPay);

            Assert.True(vnPay.Enabled);
            Assert.True(vnPay.Configured);
            Assert.Empty(vnPay.MissingFields);
            Assert.True(vnPay.HasIpnUrl);
        }

        private static PaymentProviderHealthService CreateService(
            MoMoOptions moMoOptions,
            VnPayOptions? vnPayOptions = null)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Billing:PremiumExpiryWorkerEnabled"] = "true",
                    ["Billing:PendingPaymentCleanupWorkerEnabled"] = "true"
                })
                .Build();

            var vnPay = vnPayOptions ?? new VnPayOptions
            {
                Enabled = false,
                BaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html"
            };

            return new PaymentProviderHealthService(
                Options.Create(vnPay),
                Options.Create(moMoOptions),
                new StubWebHostEnvironment { EnvironmentName = "Development" },
                config);
        }
    }
}
