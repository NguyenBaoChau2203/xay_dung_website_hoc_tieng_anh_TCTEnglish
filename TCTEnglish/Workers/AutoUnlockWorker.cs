using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TCTEnglish.Hubs;
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
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AutoUnlockWorker encountered an error.");
                }

                await Task.Delay(Interval, stoppingToken);
            }

            _logger.LogInformation("AutoUnlockWorker stopped.");
        }

        public async Task<int> ProcessExpiredLocksAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IAppEmailSender>();

            var now = DateTime.UtcNow;

            var expiredUsers = await context.Users.AsNoTracking()
                .Where(u => u.Status == UserStatus.Blocked
                         && u.LockExpiry.HasValue
                         && u.LockExpiry.Value <= now
                         && u.LockExpiry.Value < DateTime.MaxValue)
                .ToListAsync(stoppingToken);

            if (expiredUsers.Count == 0)
            {
                return 0;
            }

            _logger.LogInformation("AutoUnlockWorker: Found {Count} expired lock(s).", expiredUsers.Count);

            var unlocked = 0;

            foreach (var user in expiredUsers)
            {
                stoppingToken.ThrowIfCancellationRequested();
                var currentStatus = UserPresenceTracker.IsUserOnline(user.UserId)
                    ? UserStatus.Online
                    : UserStatus.Offline;
                // Only the worker that wins the conditional transition may send a notice.
                // Commit first; never hold a DB transaction across SMTP.
                var affected = await context.Users
                    .Where(u => u.UserId == user.UserId && u.Status == UserStatus.Blocked && u.LockExpiry == user.LockExpiry)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, currentStatus)
                        .SetProperty(u => u.LockReason, (string?)null).SetProperty(u => u.LockExpiry, (DateTime?)null), stoppingToken);
                if (affected == 0) continue;
                unlocked++;
                try
                {
                    await _hubContext.Clients.Group(ClassChatHub.AdminPresenceGroupName)
                        .SendAsync("UserStatusChanged", AdminUserStatusChangedMessage.Create(user.UserId, UserStatus.Blocked, currentStatus), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning("Unlock committed; notification failed for user {UserId} ({Category}).", user.UserId, ex.GetType().Name);
                }
                try
                {
                    var delivered = await emailSender.TrySendUnlockedNotificationAsync(user.Email, true, stoppingToken);
                    if (!delivered) _logger.LogWarning("Unlock committed; email delivery failed for user {UserId}.", user.UserId);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning("Unlock committed; email failed for user {UserId} ({Category}).", user.UserId, ex.GetType().Name);
                }
            }
            return unlocked;
        }
    }
}

