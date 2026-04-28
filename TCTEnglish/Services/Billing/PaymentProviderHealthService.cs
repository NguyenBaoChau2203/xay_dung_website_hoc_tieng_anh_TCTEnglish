using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Billing.MoMo;
using TCTEnglish.Services.Billing.VnPay;
using TCTEnglish.ViewModels.Billing;

namespace TCTEnglish.Services.Billing
{
    public class PaymentProviderHealthService : IPaymentProviderHealthService
    {
        private readonly VnPayOptions _vnPayOptions;
        private readonly MoMoOptions _moMoOptions;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public PaymentProviderHealthService(
            IOptions<VnPayOptions> vnPayOptions,
            IOptions<MoMoOptions> moMoOptions,
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            _vnPayOptions = vnPayOptions.Value.Normalize();
            _moMoOptions = moMoOptions.Value.Normalize();
            _environment = environment;
            _configuration = configuration;
        }

        public IReadOnlyList<ProviderHealthViewModel> GetProviderHealthStatus()
        {
            return new List<ProviderHealthViewModel>
            {
                BuildVnPayHealth(),
                BuildMoMoHealth(),
                BuildBankTransferHealth()
            };
        }

        public IReadOnlyList<WorkerHealthViewModel> GetWorkerHealthStatus()
        {
            return new List<WorkerHealthViewModel>
            {
                new()
                {
                    WorkerName = "PremiumExpiryWorker",
                    Description = "Tu dong ha quyen nguoi dung ve Standard khi subscription het han (moi 1 gio).",
                    Enabled = _configuration.GetValue<bool>("Billing:PremiumExpiryWorkerEnabled")
                },
                new()
                {
                    WorkerName = "PendingPaymentCleanupWorker",
                    Description = "Tu dong huy cac PaymentOrder Pending da qua han 15 phut (moi 30 phut).",
                    Enabled = _configuration.GetValue<bool>("Billing:PendingPaymentCleanupWorkerEnabled")
                }
            };
        }

        private ProviderHealthViewModel BuildVnPayHealth()
        {
            var opts = _vnPayOptions;
            var missing = new List<string>();

            var hasBaseUrl = VnPayOptions.IsValidPaymentBaseUrl(opts.BaseUrl);
            var hasReturnUrl = VnPayOptions.IsValidHttpsUrl(opts.ReturnUrl);
            var hasIpnUrl = VnPayOptions.IsValidHttpsUrl(opts.IpnUrl);
            var hasMerchantCode = !string.IsNullOrWhiteSpace(opts.TmnCode);
            var hasSecret = !string.IsNullOrWhiteSpace(opts.HashSecret);

            if (!opts.Enabled) missing.Add("Enabled = false");
            if (!hasBaseUrl) missing.Add("BaseUrl (must be VNPay vpcpay.html payment endpoint)");
            if (!hasReturnUrl) missing.Add("ReturnUrl must be absolute HTTPS URL");
            if (!hasIpnUrl) missing.Add("IpnUrl must be absolute HTTPS URL");
            if (!hasMerchantCode) missing.Add("TmnCode (merchant code)");
            else if (VnPayOptions.IsPlaceholderTmnCode(opts.TmnCode))
                missing.Add("TmnCode appears to be a placeholder/test value");
            if (!hasSecret) missing.Add("HashSecret");
            else if (VnPayOptions.IsPlaceholderHashSecret(opts.HashSecret))
                missing.Add("HashSecret appears to be a placeholder/test value");

            return new ProviderHealthViewModel
            {
                ProviderCode = PaymentProviders.VNPay,
                DisplayName = "VNPay",
                Enabled = opts.Enabled,
                Configured = opts.IsFullyConfigured(),
                HasBaseUrl = hasBaseUrl,
                HasReturnUrl = hasReturnUrl,
                HasIpnUrl = hasIpnUrl,
                HasMerchantCode = hasMerchantCode,
                HasSecret = hasSecret,
                MissingFields = missing
            };
        }

        private ProviderHealthViewModel BuildMoMoHealth()
        {
            var opts = _moMoOptions;
            var mockReady = _environment.IsDevelopment() && opts.Enabled && opts.MockModeEnabled;
            var missing = opts.GetConfigurationErrors().ToList();
            if (mockReady)
            {
                missing.Clear();
            }

            return new ProviderHealthViewModel
            {
                ProviderCode = PaymentProviders.MoMo,
                DisplayName = "MoMo",
                Enabled = opts.Enabled,
                Configured = opts.IsFullyConfigured() || mockReady,
                HasBaseUrl = MoMoOptions.IsValidHttpsBaseUrl(opts.BaseUrl),
                HasReturnUrl = MoMoOptions.IsValidHttpsUrl(opts.RedirectUrl),
                HasIpnUrl = MoMoOptions.IsValidHttpsUrl(opts.IpnUrl),
                HasMerchantCode = !string.IsNullOrWhiteSpace(opts.PartnerCode),
                HasSecret = !string.IsNullOrWhiteSpace(opts.SecretKey) && !string.IsNullOrWhiteSpace(opts.AccessKey),
                MissingFields = missing
            };
        }

        private static ProviderHealthViewModel BuildBankTransferHealth()
        {
            return new ProviderHealthViewModel
            {
                ProviderCode = "bank_transfer",
                DisplayName = "Bank Transfer",
                Enabled = false,
                Configured = false,
                HasBaseUrl = false,
                HasReturnUrl = false,
                HasIpnUrl = false,
                HasMerchantCode = false,
                HasSecret = false,
                MissingFields = new[] { "Chua implement" }
            };
        }
    }
}
