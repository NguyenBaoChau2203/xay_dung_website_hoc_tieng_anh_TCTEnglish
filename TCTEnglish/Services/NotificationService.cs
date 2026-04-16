using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services;

public class NotificationService : INotificationService
{
    private readonly DbflashcardContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        DbflashcardContext context,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // =========================================================================
    // CRUD
    // =========================================================================

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<List<NotificationViewModel>> GetNotificationsAsync(
        int userId, int page = 1, int pageSize = 20)
    {
        var skip = (Math.Max(1, page) - 1) * pageSize;

        return await _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(n => new NotificationViewModel
            {
                Id         = n.Id,
                Type       = n.Type.ToString(),
                Title      = n.Title,
                Message    = n.Message,
                IsRead     = n.IsRead,
                RelatedUrl = n.RelatedUrl,
                IconClass  = n.IconClass,
                CreatedAt  = n.CreatedAt,
                TimeAgo    = BuildTimeAgo(n.CreatedAt)
            })
            .ToListAsync();
    }

    public async Task<OperationResult> MarkAsReadAsync(int notificationId, int userId)
    {
        // Anti-IDOR: kết hợp notificationId + userId
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
            return OperationResult.NotFound("Không tìm thấy thông báo.");

        if (notification.IsRead)
            return OperationResult.Success();

        notification.IsRead = true;
        await _context.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult> MarkAllAsReadAsync(int userId)
    {
        var updated = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(n => n.IsRead, true));

        _logger.LogDebug(
            "MarkAllAsRead: user {UserId}, updated {Count} notifications",
            userId, updated);

        return OperationResult.Success();
    }

    // =========================================================================
    // Auto-generation
    // =========================================================================

    public async Task GenerateStreakNotificationsAsync(int userId, StreakUpdateResult streakResult)
    {
        // Chỉ tạo StreakRecord khi streak tăng VÀ vượt longestStreak trước đó
        if (!streakResult.DidIncrease)
            return;

        // Lấy longestStreak lưu trong DB trước khi streak tăng
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new { u.LongestStreak })
            .FirstOrDefaultAsync();

        if (user == null)
            return;

        var previousLongest = user.LongestStreak ?? 0;
        var currentStreak   = streakResult.CurrentStreak;

        // Chỉ tạo khi streak hiện tại > longestStreak cũ (kỷ lục mới)
        if (currentStreak <= previousLongest)
            return;

        // Tránh duplicate: check đã có StreakRecord hôm nay chưa
        if (await HasNotificationTodayAsync(userId, NotificationType.StreakRecord))
            return;

        await CreateNotificationAsync(new Notification
        {
            UserId    = userId,
            Type      = NotificationType.StreakRecord,
            Title     = "🔥 Kỷ lục mới!",
            Message   = $"Streak {currentStreak} ngày — kỷ lục mới của bạn!",
            IconClass = "fas fa-fire",
            RelatedUrl = "/Goals"
        });

        _logger.LogInformation(
            "StreakRecord notification created for user {UserId}, streak {Streak}",
            userId, currentStreak);
    }

    public async Task GenerateGoalNotificationsAsync(int userId, GoalsActivityRecordResult activityResult)
    {
        if (activityResult.Status != OperationStatus.Success)
            return;

        // Lấy goals active của user
        var activeGoals = await _context.UserGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId && g.IsActive && g.TargetValue > 0)
            .ToListAsync();

        if (activeGoals.Count == 0)
            return;

        // Lấy activity hôm nay
        var today = DateTime.UtcNow.Date;
        var todayActivity = await _context.UserDailyActivities
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.ActivityDate == today)
            .Select(a => new
            {
                a.CardsReviewed,
                a.SpeakingCompletedCount,
                a.WritingCompletedCount,
                a.ReadingCompletedCount,
                a.ListeningCompletedCount,
                a.VocabularyCompletedCount
            })
            .FirstOrDefaultAsync();

        if (todayActivity == null)
            return;

        foreach (var goal in activeGoals)
        {
            var progressValue = goal.GoalArea switch
            {
                GoalArea.Vocabulary => todayActivity.CardsReviewed,
                GoalArea.Speaking   => todayActivity.SpeakingCompletedCount,
                GoalArea.Writing    => todayActivity.WritingCompletedCount,
                GoalArea.Reading    => todayActivity.ReadingCompletedCount,
                GoalArea.Listening  => todayActivity.ListeningCompletedCount,
                _                   => 0
            };

            var percent = goal.TargetValue <= 0
                ? 0
                : (int)Math.Round(progressValue * 100d / goal.TargetValue);

            // 100% — GoalCompleted (ưu tiên hơn GoalProgress)
            if (percent >= 100)
            {
                if (!await HasNotificationTodayAsync(userId, NotificationType.GoalCompleted))
                {
                    await CreateNotificationAsync(new Notification
                    {
                        UserId     = userId,
                        Type       = NotificationType.GoalCompleted,
                        Title      = "🎯 Hoàn thành mục tiêu!",
                        Message    = $"Chúc mừng! Bạn đã hoàn thành mục tiêu {GetGoalAreaLabel(goal.GoalArea)} hôm nay!",
                        IconClass  = "fas fa-check-circle",
                        RelatedUrl = "/Goals"
                    });

                    _logger.LogDebug(
                        "GoalCompleted notification created for user {UserId}, area {Area}",
                        userId, goal.GoalArea);
                }
                continue; // Không tạo thêm GoalProgress nếu đã completed
            }

            // >= 80% nhưng < 100% — GoalProgress
            if (percent >= 80)
            {
                if (!await HasNotificationTodayAsync(userId, NotificationType.GoalProgress))
                {
                    await CreateNotificationAsync(new Notification
                    {
                        UserId     = userId,
                        Type       = NotificationType.GoalProgress,
                        Title      = "📈 Gần đến đích rồi!",
                        Message    = $"Bạn đã đạt {percent}% mục tiêu {GetGoalAreaLabel(goal.GoalArea)}, cố thêm chút nữa!",
                        IconClass  = "fas fa-chart-line",
                        RelatedUrl = "/Goals"
                    });

                    _logger.LogDebug(
                        "GoalProgress notification created for user {UserId}, area {Area}, percent {Percent}",
                        userId, goal.GoalArea, percent);
                }
            }
        }
    }

    public async Task GenerateBadgeNotificationAsync(int userId, int badgeId)
    {
        var badge = await _context.Badges
            .AsNoTracking()
            .Where(b => b.Id == badgeId)
            .FirstOrDefaultAsync();

        if (badge == null)
        {
            _logger.LogWarning(
                "GenerateBadgeNotification: badge {BadgeId} not found for user {UserId}",
                badgeId, userId);
            return;
        }

        // Tránh duplicate: check đã có BadgeEarned cho badge này hôm nay chưa
        if (await HasNotificationTodayAsync(userId, NotificationType.BadgeEarned))
        {
            // Kiểm tra cụ thể badge trong message (tên badge)
            var alreadyExists = await _context.Notifications
                .AsNoTracking()
                .AnyAsync(n =>
                    n.UserId == userId
                    && n.Type == NotificationType.BadgeEarned
                    && n.CreatedAt.Date == DateTime.UtcNow.Date
                    && n.Message.Contains(badge.Name));

            if (alreadyExists)
                return;
        }

        await CreateNotificationAsync(new Notification
        {
            UserId     = userId,
            Type       = NotificationType.BadgeEarned,
            Title      = "🏅 Huy hiệu mới!",
            Message    = $"Bạn vừa nhận huy hiệu '{badge.Name}'!",
            IconClass  = badge.IconClass,
            RelatedUrl = "/Goals#badges"
        });

        _logger.LogInformation(
            "BadgeEarned notification created for user {UserId}, badge {BadgeName}",
            userId, badge.Name);
    }

    // =========================================================================
    // Admin broadcast
    // =========================================================================

    public async Task<OperationResult> CreateAdminAnnouncementAsync(
        string title, string message, int adminUserId)
    {
        if (string.IsNullOrWhiteSpace(title))
            return OperationResult.Invalid("Tiêu đề không được để trống.");

        if (string.IsNullOrWhiteSpace(message))
            return OperationResult.Invalid("Nội dung không được để trống.");

        // Lấy tất cả userId active (Status != Blocked)
        var activeUserIds = await _context.Users
            .AsNoTracking()
            .Where(u => u.Status != UserStatus.Blocked)
            .Select(u => u.UserId)
            .ToListAsync();

        if (activeUserIds.Count == 0)
            return OperationResult.Success();

        var now = DateTime.UtcNow;
        var notifications = activeUserIds.Select(uid => new Notification
        {
            UserId     = uid,
            Type       = NotificationType.AdminAnnouncement,
            Title      = title,
            Message    = message,
            IconClass  = "fas fa-bullhorn",
            CreatedAt  = now
        }).ToList();

        // Bulk insert — một lần AddRange + SaveChanges (không loop)
        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminUserId} broadcast announcement '{Title}' to {Count} users",
            adminUserId, title, notifications.Count);

        return OperationResult.Success();
    }

    // =========================================================================
    // Cleanup
    // =========================================================================

    public async Task<int> CleanupOldNotificationsAsync(int daysToKeep = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);

        var deleted = await _context.Notifications
            .Where(n => n.IsRead && n.CreatedAt < cutoff)
            .ExecuteDeleteAsync();

        if (deleted > 0)
        {
            _logger.LogInformation(
                "CleanupOldNotifications: deleted {Count} read notifications older than {DaysToKeep} days",
                deleted, daysToKeep);
        }

        return deleted;
    }

    // =========================================================================
    // Helpers (private)
    // =========================================================================

    /// <summary>Tạo và lưu một notification mới.</summary>
    private async Task CreateNotificationAsync(Notification notification)
    {
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Kiểm tra user đã có notification cùng <paramref name="type"/> trong ngày hôm nay chưa.
    /// Dùng để tránh duplicate.
    /// </summary>
    private async Task<bool> HasNotificationTodayAsync(int userId, NotificationType type)
    {
        var todayUtc = DateTime.UtcNow.Date;
        return await _context.Notifications
            .AsNoTracking()
            .AnyAsync(n =>
                n.UserId == userId
                && n.Type == type
                && n.CreatedAt >= todayUtc
                && n.CreatedAt < todayUtc.AddDays(1));
    }

    /// <summary>Trả về nhãn tiếng Việt cho một <see cref="GoalArea"/>.</summary>
    private static string GetGoalAreaLabel(GoalArea area) => area switch
    {
        GoalArea.Vocabulary => "Từ vựng",
        GoalArea.Speaking   => "Nói",
        GoalArea.Writing    => "Viết",
        GoalArea.Reading    => "Đọc",
        GoalArea.Listening  => "Nghe",
        _                   => area.ToString()
    };

    /// <summary>
    /// Tính khoảng thời gian từ <paramref name="createdAt"/> đến hiện tại theo tiếng Việt.
    /// Ví dụ: "5 phút trước", "2 giờ trước", "3 ngày trước".
    /// </summary>
    private static string BuildTimeAgo(DateTime createdAt)
    {
        var diff = DateTime.UtcNow - createdAt;

        if (diff.TotalMinutes < 1)
            return "vừa xong";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} phút trước";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} giờ trước";
        if (diff.TotalDays < 30)
            return $"{(int)diff.TotalDays} ngày trước";
        if (diff.TotalDays < 365)
            return $"{(int)(diff.TotalDays / 30)} tháng trước";

        return $"{(int)(diff.TotalDays / 365)} năm trước";
    }
}
