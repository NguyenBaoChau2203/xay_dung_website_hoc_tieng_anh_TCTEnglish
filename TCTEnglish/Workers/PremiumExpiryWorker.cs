using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using TCTEnglish.Services.Billing;

namespace TCTEnglish.Workers
{
    public class PremiumExpiryWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PremiumExpiryWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

        public PremiumExpiryWorker(
            IServiceProvider serviceProvider,
            ILogger<PremiumExpiryWorker> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var isEnabled = _configuration.GetValue<bool>("Billing:PremiumExpiryWorkerEnabled");
            if (!isEnabled)
            {
                _logger.LogInformation("PremiumExpiryWorker is disabled in configuration.");
                return;
            }

            _logger.LogInformation("PremiumExpiryWorker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("PremiumExpiryWorker running at: {time}", DateTimeOffset.Now);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
                        var expiredCount = await subscriptionService.ExpireSubscriptionsAsync(stoppingToken);

                        if (expiredCount > 0)
                        {
                            _logger.LogInformation("Expired {Count} subscriptions.", expiredCount);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown — do not log as error.
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while executing PremiumExpiryWorker.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("PremiumExpiryWorker is stopping.");
        }
    }
}
