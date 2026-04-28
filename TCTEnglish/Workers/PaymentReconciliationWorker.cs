using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Billing;

namespace TCTEnglish.Workers
{
    /// <summary>
    /// Background worker that reconciles stale pending VNPay orders every
    /// <see cref="ReconciliationWorkerOptions.IntervalMinutes"/> minutes.
    /// Disabled when <see cref="ReconciliationWorkerOptions.Enabled"/> = false.
    /// </summary>
    public class PaymentReconciliationWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ReconciliationWorkerOptions _options;
        private readonly ILogger<PaymentReconciliationWorker> _logger;

        public PaymentReconciliationWorker(
            IServiceScopeFactory scopeFactory,
            IOptions<ReconciliationWorkerOptions> options,
            ILogger<PaymentReconciliationWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("PaymentReconciliationWorker is disabled via configuration.");
                return;
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes));
            _logger.LogInformation(
                "PaymentReconciliationWorker started. Interval={Interval:g}", interval);

            // Initial delay to let the app fully start
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var svc = scope.ServiceProvider
                        .GetRequiredService<IPaymentReconciliationService>();

                    var summary = await svc.ReconcileVnPayPendingOrdersAsync(
                        minPendingMinutes: _options.MinPendingMinutes,
                        ct: stoppingToken);

                    if (summary.Checked > 0)
                    {
                        _logger.LogInformation(
                            "Reconciliation pass complete. Checked={C} Paid={P} Failed={F} " +
                            "ManualReview={M} Expired={E}",
                            summary.Checked, summary.Paid, summary.Failed,
                            summary.ManualReview, summary.Expired);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PaymentReconciliationWorker encountered an error.");
                }

                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }

            _logger.LogInformation("PaymentReconciliationWorker stopped.");
        }
    }

    public class ReconciliationWorkerOptions
    {
        public bool Enabled { get; set; } = false; // opt-in
        public int IntervalMinutes { get; set; } = 5;
        public int MinPendingMinutes { get; set; } = 5;
    }
}
