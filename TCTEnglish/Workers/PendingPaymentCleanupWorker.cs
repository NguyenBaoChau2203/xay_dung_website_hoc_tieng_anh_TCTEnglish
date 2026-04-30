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
    public class PendingPaymentCleanupWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PendingPaymentCleanupWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);

        public PendingPaymentCleanupWorker(
            IServiceProvider serviceProvider,
            ILogger<PendingPaymentCleanupWorker> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var isEnabled = _configuration.GetValue<bool>("Billing:PendingPaymentCleanupWorkerEnabled");
            if (!isEnabled)
            {
                _logger.LogInformation("PendingPaymentCleanupWorker is disabled in configuration.");
                return;
            }

            _logger.LogInformation("PendingPaymentCleanupWorker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("PendingPaymentCleanupWorker running at: {time}", DateTimeOffset.Now);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var billingService = scope.ServiceProvider.GetRequiredService<IBillingService>();
                        var expiredCount = await billingService.CleanupExpiredPendingOrdersAsync(stoppingToken);

                        if (expiredCount > 0)
                        {
                            _logger.LogInformation("Cleaned up {Count} expired pending payment orders.", expiredCount);
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
                    _logger.LogError(ex, "An error occurred while executing PendingPaymentCleanupWorker.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("PendingPaymentCleanupWorker is stopping.");
        }
    }
}
