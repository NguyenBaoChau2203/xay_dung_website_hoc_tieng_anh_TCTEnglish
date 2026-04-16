using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTVocabulary.Workers;

/// <summary>
/// Background worker chạy mỗi ngày:
///   1. StreakWarning — nhắc users chưa học hôm nay (streak sắp mất)
///   2. BadgeNear    — nhắc users gần đạt badge mới
///   3. Cleanup      — xóa notifications đã đọc cũ hơn 30 ngày
///
/// Lên lịch chạy lúc 20:00 giờ VN (UTC+7 → 13:00 UTC).
/// Dùng IServiceScopeFactory để tránh circular dependency.
/// </summary>
public class NotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationWorker> _logger;

    // 20:00 giờ VN = 13:00 UTC
    private static readonly TimeSpan TargetTimeUtc = new(13, 0, 0);
    private const int CleanupDaysToKeep = 30;

    public NotificationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delayUntilNext = CalculateDelayUntilNext();
            _logger.LogDebug(
                "NotificationWorker: next run in {Delay}", delayUntilNext);

            try
            {
                await Task.Delay(delayUntilNext, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            await RunAllJobsAsync(stoppingToken);
        }

        _logger.LogInformation("NotificationWorker stopped.");
    }

    private async Task RunAllJobsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            await GenerateStreakWarningsAsync(context, stoppingToken);
            await GenerateBadgeNearNotificationsAsync(context, stoppingToken);

            var deletedCount = await notificationService.CleanupOldNotificationsAsync(CleanupDaysToKeep);
            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "NotificationWorker: cleaned up {Count} old notifications", deletedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotificationWorker encountered an error during daily run.");
        }
    }

    // =========================================================================
    // 1. Streak Warning
    // =========================================================================

    /// <summary>
    /// Tìm users có streak > 0, LastStudyDate = yesterday (chưa học hôm nay),
    /// và chưa nhận StreakWarning hôm nay → tạo thông báo nhắc nhở.
    /// </summary>
    private async Task GenerateStreakWarningsAsync(
        DbflashcardContext context, CancellationToken stoppingToken)
    {
        var today = BusinessDateHelper.Today;
        var yesterday = today.AddDays(-1);
        var todayUtc = DateTime.UtcNow.Date;

        // Users có streak > 0 VÀ LastStudyDate = yesterday (chưa học hôm nay)
        var atRiskUsers = await context.Users
            .AsNoTracking()
            .Where(u =>
                u.Status != UserStatus.Blocked
                && (u.Streak ?? 0) > 0
                && u.LastStudyDate.HasValue
                && u.LastStudyDate.Value.Date == yesterday)
            .Select(u => new { u.UserId, u.Streak })
            .ToListAsync(stoppingToken);

        if (atRiskUsers.Count == 0) return;

        // Lấy danh sách userIds đã nhận StreakWarning hôm nay
        var alreadyNotifiedIds = await context.Set<Notification>()
            .AsNoTracking()
            .Where(n =>
                n.Type == NotificationType.StreakWarning
                && n.CreatedAt >= todayUtc
                && n.CreatedAt < todayUtc.AddDays(1))
            .Select(n => n.UserId)
            .Distinct()
            .ToListAsync(stoppingToken);

        var alreadyNotifiedSet = alreadyNotifiedIds.ToHashSet();
        var newNotifications = new List<Notification>();

        foreach (var user in atRiskUsers)
        {
            if (alreadyNotifiedSet.Contains(user.UserId)) continue;

            newNotifications.Add(new Notification
            {
                UserId     = user.UserId,
                Type       = NotificationType.StreakWarning,
                Title      = "⚠️ Streak sắp mất!",
                Message    = $"Bạn chưa học hôm nay — streak {user.Streak} ngày sắp mất!",
                IconClass  = "fas fa-fire-flame-curved",
                RelatedUrl = "/Goals",
                CreatedAt  = DateTime.UtcNow
            });
        }

        if (newNotifications.Count > 0)
        {
            context.Set<Notification>().AddRange(newNotifications);
            await context.SaveChangesAsync(stoppingToken);

            _logger.LogInformation(
                "NotificationWorker: sent {Count} StreakWarning notifications",
                newNotifications.Count);
        }
    }

    // =========================================================================
    // 2. Badge Near
    // =========================================================================

    /// <summary>
    /// Tìm users gần đạt badge (thiếu ≤ 2 unit) và chưa nhận BadgeNear hôm nay.
    /// </summary>
    private async Task GenerateBadgeNearNotificationsAsync(
        DbflashcardContext context, CancellationToken stoppingToken)
    {
        var todayUtc = DateTime.UtcNow.Date;

        var badges = await context.Badges
            .AsNoTracking()
            .ToListAsync(stoppingToken);

        if (badges.Count == 0) return;

        // Lấy users active (không blocked)
        var activeUsers = await context.Users
            .AsNoTracking()
            .Where(u => u.Status != UserStatus.Blocked)
            .Select(u => new
            {
                u.UserId,
                u.Streak,
                u.LongestStreak,
                u.LastStudyDate
            })
            .ToListAsync(stoppingToken);

        if (activeUsers.Count == 0) return;

        // Lấy all existing UserBadges
        var existingUserBadges = await context.UserBadges
            .AsNoTracking()
            .Select(ub => new { ub.UserId, ub.BadgeId })
            .ToListAsync(stoppingToken);

        var existingBadgeLookup = existingUserBadges
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.BadgeId).ToHashSet());

        // Lấy daily activity totals per user (simplifed — tổng active days + xp)
        var userActivityTotals = await context.UserDailyActivities
            .AsNoTracking()
            .GroupBy(a => a.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalDaysActive = g.Count(),
                TotalXp = g.Sum(a => a.XpEarned)
            })
            .ToListAsync(stoppingToken);

        var activityLookup = userActivityTotals.ToDictionary(x => x.UserId);

        // Already-notified today
        var alreadyNotifiedIds = await context.Set<Notification>()
            .AsNoTracking()
            .Where(n =>
                n.Type == NotificationType.BadgeNear
                && n.CreatedAt >= todayUtc
                && n.CreatedAt < todayUtc.AddDays(1))
            .Select(n => n.UserId)
            .Distinct()
            .ToListAsync(stoppingToken);

        var alreadyNotifiedSet = alreadyNotifiedIds.ToHashSet();
        var newNotifications = new List<Notification>();

        foreach (var user in activeUsers)
        {
            if (alreadyNotifiedSet.Contains(user.UserId)) continue;

            var userBadgeIds = existingBadgeLookup.TryGetValue(user.UserId, out var ids)
                ? ids
                : new HashSet<int>();

            activityLookup.TryGetValue(user.UserId, out var activity);

            foreach (var badge in badges)
            {
                // Skip badges user already has
                if (userBadgeIds.Contains(badge.Id)) continue;

                var currentValue = GetMetricValueForUser(
                    badge.MetricType, user.Streak, user.LongestStreak,
                    activity?.TotalDaysActive ?? 0, activity?.TotalXp ?? 0);

                var remaining = badge.ThresholdValue - currentValue;

                // "Gần đạt" = thiếu từ 1–2 đơn vị
                if (remaining >= 1 && remaining <= 2)
                {
                    newNotifications.Add(new Notification
                    {
                        UserId     = user.UserId,
                        Type       = NotificationType.BadgeNear,
                        Title      = "🔜 Gần mở khóa huy hiệu!",
                        Message    = $"Còn {remaining} bước nữa để mở khóa huy hiệu '{badge.Name}'!",
                        IconClass  = badge.IconClass,
                        RelatedUrl = "/Goals#badges",
                        CreatedAt  = DateTime.UtcNow
                    });
                    break; // Chỉ 1 BadgeNear notification per user per day
                }
            }
        }

        if (newNotifications.Count > 0)
        {
            context.Set<Notification>().AddRange(newNotifications);
            await context.SaveChangesAsync(stoppingToken);

            _logger.LogInformation(
                "NotificationWorker: sent {Count} BadgeNear notifications",
                newNotifications.Count);
        }
    }

    private static int GetMetricValueForUser(
        BadgeMetricType metricType,
        int? currentStreak, int? longestStreak,
        int totalDaysActive, int totalXp)
    {
        return metricType switch
        {
            BadgeMetricType.CurrentStreak         => currentStreak ?? 0,
            BadgeMetricType.LongestStreak          => longestStreak ?? 0,
            BadgeMetricType.TotalDaysActive         => totalDaysActive,
            BadgeMetricType.TotalXp                => totalXp,
            // SpeakingVideosCompleted, VocabularyCompletions, WritingExercisesCompleted
            // — omitted in this simplified worker (would need additional queries)
            _                                      => 0
        };
    }

    // =========================================================================
    // Scheduling helper
    // =========================================================================

    private static TimeSpan CalculateDelayUntilNext()
    {
        var now = DateTime.UtcNow;
        var nextRun = now.Date + TargetTimeUtc;

        if (nextRun <= now)
            nextRun = nextRun.AddDays(1);

        return nextRun - now;
    }
}
