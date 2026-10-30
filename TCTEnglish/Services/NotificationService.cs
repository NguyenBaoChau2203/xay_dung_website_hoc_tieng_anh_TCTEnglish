using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.Notifications;
using TCTVocabulary.Models;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services;

public class NotificationService : INotificationService
{
    private readonly DbflashcardContext _context;
    private readonly LearningBusinessClock _clock;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        DbflashcardContext context,
        LearningBusinessClock clock,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<List<NotificationViewModel>> GetNotificationsAsync(
        int userId, int page = 1, int pageSize = 20)
    {
        var pageModel = await GetNotificationsPageAsync(userId, page, pageSize, unreadOnly: false);
        return pageModel.Items;
    }

    public async Task<NotificationListViewModel> GetNotificationsPageAsync(
        int userId,
        int page = 1,
        int pageSize = 20,
        bool unreadOnly = false)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var totalCount = await query.CountAsync();
        var unreadCount = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (page > totalPages)
        {
            page = totalPages;
        }

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationViewModel
            {
                Id = n.Id,
                Type = n.Type.ToString(),
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                RelatedUrl = n.RelatedUrl,
                IconClass = n.IconClass,
                CreatedAt = n.CreatedAt,
                TimeAgo = string.Empty
            })
            .ToListAsync();

        foreach (var item in items)
        {
            item.TimeAgo = BuildTimeAgo(item.CreatedAt);
        }

        return new NotificationListViewModel
        {
            Items = items,
            UnreadCount = unreadCount,
            TotalCount = totalCount,
            CurrentPage = page,
            TotalPages = totalPages,
            HasMore = page < totalPages
        };
    }

    public async Task<OperationResult> MarkAsReadAsync(int notificationId, int userId)
    {
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

    public async Task GenerateStreakNotificationsAsync(int userId, StreakUpdateResult streakResult)
    {
        if (!streakResult.DidIncrease)
            return;

        var previousLongest = streakResult.PreviousLongestStreak;
        var currentStreak = streakResult.CurrentStreak;

        if (currentStreak <= previousLongest)
            return;

        var businessDate = _clock.BusinessDate;
        var eventKey = $"streak-record:{userId}:{businessDate:yyyy-MM-dd}:{currentStreak}";

        await TryCreateWithEventKeyAsync(new Notification
        {
            UserId = userId,
            Type = NotificationType.StreakRecord,
            Title = "🔥 Kỷ lục mới!",
            Message = $"Streak {currentStreak} ngày — kỷ lục mới của bạn!",
            IconClass = "fas fa-fire",
            RelatedUrl = "/Goals",
            EventKey = eventKey,
            CreatedAt = _clock.UtcNow
        });
    }

    public async Task GenerateGoalNotificationsAsync(int userId, GoalsActivityRecordResult activityResult)
    {
        if (activityResult.Status != OperationStatus.Success)
            return;

        var activeGoals = await _context.UserGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId && g.IsActive && g.TargetValue > 0)
            .ToListAsync();

        if (activeGoals.Count == 0)
            return;

        var today = _clock.BusinessDate;
        var todayActivity = await _context.UserDailyActivities
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.ActivityDate == today)
            .Select(a => new
            {
                a.CardsReviewed,
                a.SpeakingCompletedCount,
                a.WritingCompletedCount,
                a.ReadingCompletedCount,
                a.ListeningCompletedCount
            })
            .FirstOrDefaultAsync();

        if (todayActivity == null)
            return;

        foreach (var goal in activeGoals)
        {
            var progressValue = goal.GoalArea switch
            {
                GoalArea.Vocabulary => todayActivity.CardsReviewed,
                GoalArea.Speaking => todayActivity.SpeakingCompletedCount,
                GoalArea.Writing => todayActivity.WritingCompletedCount,
                GoalArea.Reading => todayActivity.ReadingCompletedCount,
                GoalArea.Listening => todayActivity.ListeningCompletedCount,
                _ => 0
            };

            var percent = goal.TargetValue <= 0
                ? 0
                : (int)Math.Round(progressValue * 100d / goal.TargetValue);

            if (percent >= 100)
            {
                var eventKey = $"goal:{userId}:{goal.GoalArea}:{today:yyyy-MM-dd}:100";
                await TryCreateWithEventKeyAsync(new Notification
                {
                    UserId = userId,
                    Type = NotificationType.GoalCompleted,
                    Title = "🎯 Hoàn thành mục tiêu!",
                    Message = $"Chúc mừng! Bạn đã hoàn thành mục tiêu {GetGoalAreaLabel(goal.GoalArea)} hôm nay!",
                    IconClass = "fas fa-check-circle",
                    RelatedUrl = "/Goals",
                    EventKey = eventKey,
                    CreatedAt = _clock.UtcNow
                });
                continue;
            }

            if (percent >= 80)
            {
                var eventKey = $"goal:{userId}:{goal.GoalArea}:{today:yyyy-MM-dd}:80";
                await TryCreateWithEventKeyAsync(new Notification
                {
                    UserId = userId,
                    Type = NotificationType.GoalProgress,
                    Title = "📈 Gần đến đích rồi!",
                    Message = $"Bạn đã đạt {percent}% mục tiêu {GetGoalAreaLabel(goal.GoalArea)}, cố thêm chút nữa!",
                    IconClass = "fas fa-chart-line",
                    RelatedUrl = "/Goals",
                    EventKey = eventKey,
                    CreatedAt = _clock.UtcNow
                });
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

        var eventKey = $"badge:{userId}:{badgeId}";
        await TryCreateWithEventKeyAsync(new Notification
        {
            UserId = userId,
            Type = NotificationType.BadgeEarned,
            Title = "🏅 Huy hiệu mới!",
            Message = $"Bạn vừa nhận huy hiệu '{badge.Name}'!",
            IconClass = badge.IconClass,
            RelatedUrl = "/Goals#badges",
            EventKey = eventKey,
            CreatedAt = _clock.UtcNow
        });
    }

    public async Task<OperationResult> CreateAdminAnnouncementAsync(
        string title, string message, int adminUserId)
    {
        if (string.IsNullOrWhiteSpace(title))
            return OperationResult.Invalid("Tiêu đề không được để trống.");

        if (string.IsNullOrWhiteSpace(message))
            return OperationResult.Invalid("Nội dung không được để trống.");

        var activeUserIds = await _context.Users
            .AsNoTracking()
            .Where(u => u.Status != UserStatus.Blocked)
            .Select(u => u.UserId)
            .ToListAsync();

        if (activeUserIds.Count == 0)
            return OperationResult.Success();

        var now = _clock.UtcNow;
        var notifications = activeUserIds.Select(uid => new Notification
        {
            UserId = uid,
            Type = NotificationType.AdminAnnouncement,
            Title = title,
            Message = message,
            IconClass = "fas fa-bullhorn",
            EventKey = null,
            CreatedAt = now
        }).ToList();

        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminUserId} broadcast announcement '{Title}' to {Count} users",
            adminUserId, title, notifications.Count);

        return OperationResult.Success();
    }

    public async Task<int> CleanupOldNotificationsAsync(int daysToKeep = 30)
    {
        var cutoff = _clock.UtcNow.AddDays(-daysToKeep);

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

    private async Task TryCreateWithEventKeyAsync(Notification notification)
    {
        if (string.IsNullOrWhiteSpace(notification.EventKey))
        {
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            return;
        }

        var exists = await _context.Notifications
            .AsNoTracking()
            .AnyAsync(n => n.UserId == notification.UserId && n.EventKey == notification.EventKey);
        if (exists)
            return;

        _context.Notifications.Add(notification);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _context.Entry(notification).State = EntityState.Detached;
            _logger.LogDebug(ex,
                "Duplicate notification event ignored for user {UserId}, key {EventKey}",
                notification.UserId,
                notification.EventKey);
        }
    }

    private static string GetGoalAreaLabel(GoalArea area) => area switch
    {
        GoalArea.Vocabulary => "Từ vựng",
        GoalArea.Speaking => "Nói",
        GoalArea.Writing => "Viết",
        GoalArea.Reading => "Đọc",
        GoalArea.Listening => "Nghe",
        _ => area.ToString()
    };

    private string BuildTimeAgo(DateTime createdAt)
    {
        var diff = _clock.UtcNow - createdAt;

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
