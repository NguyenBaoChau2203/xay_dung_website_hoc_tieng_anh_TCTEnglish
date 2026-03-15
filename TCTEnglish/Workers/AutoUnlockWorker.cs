using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TCTVocabulary.Hubs;
using TCTVocabulary.Models;
using TCTVocabulary.Realtime;
using TCTVocabulary.Services;

namespace TCTVocabulary.Workers
{
    public class AutoUnlockWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AutoUnlockWorker> _logger;
        private readonly IHubContext<ClassChatHub> _hubContext;
        private readonly bool _enabled;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        public AutoUnlockWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<AutoUnlockWorker> logger,
            IHubContext<ClassChatHub> hubContext,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _hubContext = hubContext;
            _enabled = configuration.GetValue<bool>("BackgroundJobs:AutoUnlockWorkerEnabled");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                _logger.LogWarning(
                    "AutoUnlockWorker is disabled (BackgroundJobs:AutoUnlockWorkerEnabled=false). " +
                    "Enable it after LockExpiry data/schema are cleaned.");
                return;
            }

            _logger.LogInformation("AutoUnlockWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiredLocksAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AutoUnlockWorker encountered an error.");
                }

                await Task.Delay(Interval, stoppingToken);
            }

            _logger.LogInformation("AutoUnlockWorker stopped.");
        }

        private async Task ProcessExpiredLocksAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IAppEmailSender>();

            var now = DateTime.UtcNow;

            var expiredUsers = await context.Users
                .Where(u => u.Status == UserStatus.Blocked
                         && u.LockExpiry.HasValue
                         && u.LockExpiry.Value <= now
                         && u.LockExpiry.Value < DateTime.MaxValue)
                .ToListAsync(stoppingToken);

            if (expiredUsers.Count == 0)
            {
                return;
            }

            _logger.LogInformation("AutoUnlockWorker: Found {Count} expired lock(s).", expiredUsers.Count);

            var statusChanges = new List<AdminUserStatusChangedMessage>();

            foreach (var user in expiredUsers)
            {
                var previousStatus = user.Status;

                user.Status = UserPresenceTracker.IsUserOnline(user.UserId)
                    ? UserStatus.Online
                    : UserStatus.Offline;
                user.LockReason = null;
                user.LockExpiry = null;

                statusChanges.Add(AdminUserStatusChangedMessage.Create(
                    user.UserId,
                    previousStatus,
                    user.Status));

                await emailSender.SendUnlockedNotificationAsync(user.Email, isAutoUnlock: true);
            }

            await context.SaveChangesAsync(stoppingToken);

            foreach (var statusChange in statusChanges)
            {
                await _hubContext.Clients.Group(ClassChatHub.AdminPresenceGroupName)
                    .SendAsync("UserStatusChanged", statusChange, stoppingToken);
            }
        }
    }
}
