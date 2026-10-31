using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services;
using TCTEnglish.Services.Notifications;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTVocabulary.Workers;

/// <summary>
/// One batch per poll. Phase and LastUserId are the checkpoint. LeaseToken fences late workers.
/// Cap is per process, not per cluster.
/// </summary>
public class NotificationWorker : BackgroundService
{
    public const string DailyJobName = "notification-daily";
    public const string PhaseStreak = "streak";
    public const string PhaseBadge = "badge";
    public const string PhaseCleanup = "cleanup";
    public const string PhaseComplete = "complete";

    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(1);
    private const int MaxUsersPerBatch = 200;
    private const int MaxFailureAttempts = 3;
    private const int CleanupDaysToKeep = 30;
    private static readonly TimeSpan EveningOffsetFromBusinessMidnight = TimeSpan.FromHours(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationWorker> _logger;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private DateTime _lastRenewUtc = DateTime.MinValue;

    public NotificationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Test seam. Invoked after the batch is built and before the checkpoint transaction.</summary>
    public Func<CancellationToken, Task>? BeforeBatchCommitAsync { get; set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationWorker started ({InstanceId}).", _instanceId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainOutboxAsync(stoppingToken);
                await TryRunDueJobsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "NotificationWorker poll failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("NotificationWorker stopped.");
    }

    public async Task DrainOutboxAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxDispatcher = scope.ServiceProvider.GetRequiredService<ILearningOutboxDispatcher>();
        var drained = await outboxDispatcher.DrainAsync(100, stoppingToken);
        if (drained > 0)
        {
            _logger.LogInformation("Drained {Count} learning notification outbox events.", drained);
        }
    }

    public async Task TryRunDueJobsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var clock = scope.ServiceProvider.GetRequiredService<LearningBusinessClock>();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var businessDate = clock.BusinessDate;
        var nowUtc = clock.UtcNow;
        var eveningDueUtc = clock.GetUtcStartOfBusinessDate(businessDate) + EveningOffsetFromBusinessMidnight;
        if (nowUtc < eveningDueUtc)
        {
            return;
        }

        var runId = await EnsureRunAsync(context, businessDate, stoppingToken);
        var token = Guid.NewGuid().ToString("N");
        var claimed = await TryClaimAsync(context, runId, token, nowUtc, stoppingToken);
        if (!claimed)
        {
            return;
        }

        _lastRenewUtc = nowUtc;
        await ProcessOwnedBatchAsync(context, notificationService, clock, runId, token, stoppingToken);
    }

    private async Task<long> EnsureRunAsync(DbflashcardContext context, DateTime businessDate, CancellationToken ct)
    {
        var existingId = await context.NotificationJobRuns.AsNoTracking()
            .Where(r => r.JobName == DailyJobName && r.BusinessDate == businessDate)
            .Select(r => (long?)r.Id)
            .FirstOrDefaultAsync(ct);
        if (existingId is long id)
        {
            return id;
        }

        var run = new NotificationJobRun
        {
            JobName = DailyJobName,
            BusinessDate = businessDate,
            Status = "queued",
            Phase = PhaseStreak,
            AttemptCount = 0
        };
        context.NotificationJobRuns.Add(run);
        try
        {
            await context.SaveChangesAsync(ct);
            context.Entry(run).State = EntityState.Detached;
            return run.Id;
        }
        catch (DbUpdateException ex) when (RelationalUniqueConflict.IsUnique(ex))
        {
            context.Entry(run).State = EntityState.Detached;
            return await context.NotificationJobRuns.AsNoTracking()
                .Where(r => r.JobName == DailyJobName && r.BusinessDate == businessDate)
                .Select(r => r.Id)
                .FirstAsync(ct);
        }
    }

    private async Task<bool> TryClaimAsync(
        DbflashcardContext context,
        long runId,
        string token,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var rows = await context.NotificationJobRuns
            .Where(r => r.Id == runId
                && r.Status != "succeeded"
                && r.Phase != PhaseComplete
                && (r.Status != "failed" || r.AttemptCount < MaxFailureAttempts)
                && (r.Status == "queued"
                    || r.Status == "failed"
                    || r.LeaseUntilUtc == null
                    || r.LeaseUntilUtc <= nowUtc
                    || r.LeaseOwner == _instanceId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, "running")
                .SetProperty(r => r.LeaseOwner, _instanceId)
                .SetProperty(r => r.LeaseToken, token)
                .SetProperty(r => r.LeaseUntilUtc, nowUtc.Add(LeaseDuration))
                .SetProperty(r => r.StartedAtUtc, r => r.StartedAtUtc ?? nowUtc)
                .SetProperty(r => r.Phase, r => r.Phase == null ? PhaseStreak : r.Phase)
                .SetProperty(r => r.AttemptCount, r => r.Status == "queued" || r.Status == "failed"
                    ? r.AttemptCount + 1
                    : r.AttemptCount)
                .SetProperty(r => r.LastError, (string?)null), ct);
        return rows == 1;
    }

    private async Task ProcessOwnedBatchAsync(
        DbflashcardContext context,
        INotificationService notificationService,
        LearningBusinessClock clock,
        long runId,
        string token,
        CancellationToken ct)
    {
        var run = await context.NotificationJobRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId && r.LeaseToken == token && r.Status == "running", ct);
        if (run == null)
        {
            return;
        }

        var phase = string.IsNullOrWhiteSpace(run.Phase) ? PhaseStreak : run.Phase;
        try
        {
            if (phase == PhaseCleanup)
            {
                await RunCleanupAsync(context, notificationService, clock, runId, token, ct);
                return;
            }

            if (phase == PhaseBadge)
            {
                await RunBadgeBatchAsync(context, clock, run, token, ct);
                return;
            }

            await RunStreakBatchAsync(context, clock, run, token, ct);
        }
        catch (DbUpdateException ex) when (!RelationalUniqueConflict.IsUnique(ex))
        {
            _logger.LogWarning(ex, "Notification batch failed without advancing the cursor.");
            DetachAddedNotifications(context);
            await MarkFailedAsync(context, runId, token, ex.Message, ct);
        }
    }

    private async Task RunStreakBatchAsync(
        DbflashcardContext context,
        LearningBusinessClock clock,
        NotificationJobRun run,
        string token,
        CancellationToken ct)
    {
        var today = clock.BusinessDate;
        var yesterday = today.AddDays(-1);
        var cursor = run.LastUserId ?? 0;
        var atRiskUsers = await context.Users.AsNoTracking()
            .Where(u =>
                u.UserId > cursor
                && u.Status != UserStatus.Blocked
                && (u.Streak ?? 0) > 0
                && u.LastStudyDate.HasValue
                && u.LastStudyDate.Value.Date == yesterday)
            .OrderBy(u => u.UserId)
            .Take(MaxUsersPerBatch)
            .Select(u => new { u.UserId, u.Streak })
            .ToListAsync(ct);

        if (atRiskUsers.Count == 0)
        {
            await CommitCheckpointAsync(context, clock, run.Id, token, PhaseBadge, null, Array.Empty<Notification>(), ct);
            return;
        }

        var userIds = atRiskUsers.Select(u => u.UserId).ToList();
        var keys = userIds.Select(id => $"streak-warning:{id}:{today:yyyy-MM-dd}").ToList();
        var existing = await LoadExistingKeysAsync(context, userIds, keys, ct);
        var notifications = new List<Notification>();
        foreach (var user in atRiskUsers)
        {
            var eventKey = $"streak-warning:{user.UserId}:{today:yyyy-MM-dd}";
            if (existing.Contains(eventKey))
            {
                continue;
            }

            notifications.Add(new Notification
            {
                UserId = user.UserId,
                Type = NotificationType.StreakWarning,
                Title = "⚠️ Streak sắp mất!",
                Message = $"Bạn chưa học hôm nay — streak {user.Streak} ngày sắp mất!",
                IconClass = "fas fa-fire-flame-curved",
                RelatedUrl = "/Goals",
                EventKey = eventKey,
                CreatedAt = clock.UtcNow
            });
        }

        var finishedPhase = atRiskUsers.Count < MaxUsersPerBatch;
        await CommitCheckpointAsync(
            context,
            clock,
            run.Id,
            token,
            finishedPhase ? PhaseBadge : PhaseStreak,
            finishedPhase ? null : atRiskUsers[^1].UserId,
            notifications,
            ct);
    }

    private async Task RunBadgeBatchAsync(
        DbflashcardContext context,
        LearningBusinessClock clock,
        NotificationJobRun run,
        string token,
        CancellationToken ct)
    {
        var today = clock.BusinessDate;
        var badges = await context.Badges.AsNoTracking()
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.Id)
            .ToListAsync(ct);
        if (badges.Count == 0)
        {
            await CommitCheckpointAsync(context, clock, run.Id, token, PhaseCleanup, null, Array.Empty<Notification>(), ct);
            return;
        }

        var cursor = run.LastUserId ?? 0;
        var activeUsers = await context.Users.AsNoTracking()
            .Where(u => u.UserId > cursor && u.Status != UserStatus.Blocked)
            .OrderBy(u => u.UserId)
            .Take(MaxUsersPerBatch)
            .Select(u => new { u.UserId, u.Streak, u.LongestStreak })
            .ToListAsync(ct);
        if (activeUsers.Count == 0)
        {
            await CommitCheckpointAsync(context, clock, run.Id, token, PhaseCleanup, null, Array.Empty<Notification>(), ct);
            return;
        }

        var userIds = activeUsers.Select(u => u.UserId).ToList();
        var existingBadges = await context.UserBadges.AsNoTracking()
            .Where(ub => userIds.Contains(ub.UserId))
            .Select(ub => new { ub.UserId, ub.BadgeId })
            .ToListAsync(ct);
        var badgeLookup = existingBadges
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.BadgeId).ToHashSet());
        var activityTotals = await context.UserDailyActivities.AsNoTracking()
            .Where(a => userIds.Contains(a.UserId))
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, TotalDaysActive = g.Count(), TotalXp = g.Sum(a => a.XpEarned) })
            .ToListAsync(ct);
        var activityLookup = activityTotals.ToDictionary(x => x.UserId);

        var notifications = new List<Notification>();
        foreach (var user in activeUsers)
        {
            var owned = badgeLookup.TryGetValue(user.UserId, out var ids) ? ids : new HashSet<int>();
            activityLookup.TryGetValue(user.UserId, out var activity);
            foreach (var badge in badges)
            {
                if (owned.Contains(badge.Id))
                {
                    continue;
                }

                var currentValue = badge.MetricType switch
                {
                    BadgeMetricType.CurrentStreak => user.Streak ?? 0,
                    BadgeMetricType.LongestStreak => user.LongestStreak ?? 0,
                    BadgeMetricType.TotalDaysActive => activity?.TotalDaysActive ?? 0,
                    BadgeMetricType.TotalXp => activity?.TotalXp ?? 0,
                    _ => 0
                };
                var remaining = badge.ThresholdValue - currentValue;
                if (remaining is < 1 or > 2)
                {
                    continue;
                }

                notifications.Add(new Notification
                {
                    UserId = user.UserId,
                    Type = NotificationType.BadgeNear,
                    Title = "🔜 Gần mở khóa huy hiệu!",
                    Message = $"Còn {remaining} bước nữa để mở khóa huy hiệu '{badge.Name}'!",
                    IconClass = badge.IconClass,
                    RelatedUrl = "/Goals#badges",
                    EventKey = $"badge-near:{user.UserId}:{badge.Id}:{today:yyyy-MM-dd}",
                    CreatedAt = clock.UtcNow
                });
                break;
            }
        }

        var finishedPhase = activeUsers.Count < MaxUsersPerBatch;
        await CommitCheckpointAsync(
            context,
            clock,
            run.Id,
            token,
            finishedPhase ? PhaseCleanup : PhaseBadge,
            finishedPhase ? null : activeUsers[^1].UserId,
            notifications,
            ct);
    }

    private async Task RunCleanupAsync(
        DbflashcardContext context,
        INotificationService notificationService,
        LearningBusinessClock clock,
        long runId,
        string token,
        CancellationToken ct)
    {
        if (BeforeBatchCommitAsync != null)
        {
            await BeforeBatchCommitAsync(ct);
        }

        if (!await RenewIfDueAsync(context, clock, runId, token, ct))
        {
            return;
        }

        var owned = await context.NotificationJobRuns.AsNoTracking()
            .AnyAsync(r => r.Id == runId && r.LeaseToken == token && r.Status == "running", ct);
        if (!owned)
        {
            return;
        }

        try
        {
            var deletedCount = await notificationService.CleanupOldNotificationsAsync(CleanupDaysToKeep);
            if (deletedCount > 0)
            {
                _logger.LogInformation("NotificationWorker: cleaned up {Count} old notifications", deletedCount);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await MarkFailedAsync(context, runId, token, ex.Message, ct);
            return;
        }

        var now = clock.UtcNow;
        var rows = await context.NotificationJobRuns
            .Where(r => r.Id == runId && r.LeaseToken == token && r.Status == "running")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, "succeeded")
                .SetProperty(r => r.Phase, PhaseComplete)
                .SetProperty(r => r.LastUserId, (int?)null)
                .SetProperty(r => r.CompletedAtUtc, now)
                .SetProperty(r => r.LastError, (string?)null)
                .SetProperty(r => r.LeaseUntilUtc, (DateTime?)null)
                .SetProperty(r => r.LeaseOwner, (string?)null), ct);
        if (rows != 1)
        {
            _logger.LogWarning("Notification cleanup finished without the lease; job was not marked succeeded.");
        }
    }

    private async Task CommitCheckpointAsync(
        DbflashcardContext context,
        LearningBusinessClock clock,
        long runId,
        string token,
        string nextPhase,
        int? nextUserId,
        IReadOnlyList<Notification> notifications,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (attempt == 0 && BeforeBatchCommitAsync != null)
            {
                await BeforeBatchCommitAsync(ct);
            }

            if (!await RenewIfDueAsync(context, clock, runId, token, ct))
            {
                DetachAddedNotifications(context);
                return;
            }

            await using var tx = await context.Database.BeginTransactionAsync(ct);
            var rows = await context.NotificationJobRuns
                .Where(r => r.Id == runId && r.LeaseToken == token && r.Status == "running")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Phase, nextPhase)
                    .SetProperty(r => r.LastUserId, nextUserId), ct);
            if (rows != 1)
            {
                await tx.RollbackAsync(ct);
                DetachAddedNotifications(context);
                return;
            }

            var pending = notifications;
            if (pending.Count > 0)
            {
                var userIds = pending.Select(n => n.UserId).Distinct().ToList();
                var keys = pending.Select(n => n.EventKey!).ToList();
                var existing = await LoadExistingKeysAsync(context, userIds, keys, ct);
                pending = pending.Where(n => n.EventKey != null && !existing.Contains(n.EventKey)).ToList();
                context.Notifications.AddRange(pending);
            }

            try
            {
                await context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (RelationalUniqueConflict.IsUnique(ex) && attempt == 0)
            {
                _logger.LogWarning(ex, "Notification batch hit a unique conflict; retrying the unsaved rows.");
                await tx.RollbackAsync(ct);
                DetachAddedNotifications(context);
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync(ct);
                DetachAddedNotifications(context);
                await MarkFailedAsync(context, runId, token, ex.Message, ct);
                return;
            }
        }
    }

    private async Task<bool> RenewIfDueAsync(
        DbflashcardContext context,
        LearningBusinessClock clock,
        long runId,
        string token,
        CancellationToken ct)
    {
        var now = clock.UtcNow;
        if (now - _lastRenewUtc < HeartbeatInterval)
        {
            return true;
        }

        var rows = await context.NotificationJobRuns
            .Where(r => r.Id == runId && r.LeaseToken == token && r.Status == "running")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.LeaseUntilUtc, now.Add(LeaseDuration)), ct);
        if (rows == 1)
        {
            _lastRenewUtc = now;
            return true;
        }

        return false;
    }

    private async Task MarkFailedAsync(
        DbflashcardContext context,
        long runId,
        string token,
        string message,
        CancellationToken ct)
    {
        var text = string.IsNullOrWhiteSpace(message) ? "Notification batch failed." : message.Trim();
        if (text.Length > 500)
        {
            text = text[..500];
        }

        await context.NotificationJobRuns
            .Where(r => r.Id == runId && r.LeaseToken == token)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, "failed")
                .SetProperty(r => r.LastError, text)
                .SetProperty(r => r.LeaseUntilUtc, (DateTime?)null)
                .SetProperty(r => r.LeaseOwner, (string?)null), ct);
    }

    private static async Task<HashSet<string>> LoadExistingKeysAsync(
        DbflashcardContext context,
        IReadOnlyCollection<int> userIds,
        IReadOnlyCollection<string> keys,
        CancellationToken ct)
    {
        if (keys.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var existing = await context.Notifications.AsNoTracking()
            .Where(n => userIds.Contains(n.UserId) && n.EventKey != null && keys.Contains(n.EventKey))
            .Select(n => n.EventKey!)
            .ToListAsync(ct);
        return existing.ToHashSet(StringComparer.Ordinal);
    }

    private static void DetachAddedNotifications(DbflashcardContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries<Notification>().Where(e => e.State == EntityState.Added).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
