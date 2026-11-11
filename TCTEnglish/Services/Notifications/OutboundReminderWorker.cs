using TCTEnglish.Services.Accounts;

namespace TCTEnglish.Services.Notifications;

public sealed class OutboundReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboundReminderWorker> _logger;
    private readonly string _workerId = Guid.NewGuid().ToString("N");

    public OutboundReminderWorker(IServiceScopeFactory scopeFactory, ILogger<OutboundReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOutboundDeliveryProcessor>();
                await processor.ScheduleAsync(stoppingToken);
                await processor.DispatchDueAsync(_workerId, stoppingToken);
                var cleanup = scope.ServiceProvider.GetRequiredService<IAccountFileCleanupProcessor>();
                await cleanup.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Outbound reminder cycle failed ({Category}).", ex.GetType().Name);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
