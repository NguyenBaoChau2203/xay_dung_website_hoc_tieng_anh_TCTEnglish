using TCTEnglish.Models;
using TCTEnglish.Services.Notifications;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services;

public interface INotificationService
{
    // === CRUD ===
    Task<int> GetUnreadCountAsync(int userId);
    Task<List<NotificationViewModel>> GetNotificationsAsync(int userId, int page = 1, int pageSize = 20);
    Task<NotificationListViewModel> GetNotificationsPageAsync(
        int userId,
        int page = 1,
        int pageSize = 20,
        bool unreadOnly = false,
        NotificationCategory? category = null);

    /// <summary>
    /// Keyset slice for the bell: items strictly older than the (CreatedAt, Id) cursor, newest first.
    /// A new notification arriving between two reads can neither repeat nor hide an item.
    /// </summary>
    Task<NotificationSliceViewModel> GetNotificationsSliceAsync(
        int userId,
        int take,
        bool unreadOnly,
        DateTime? beforeCreatedUtc,
        int? beforeId,
        NotificationCategory? category = null);

    /// <summary>Unread and total counts per category plus the overall totals (one grouped query).</summary>
    Task<NotificationCategoryCounts> GetCategoryCountsAsync(int userId);

    Task<OperationResult> MarkAsReadAsync(int notificationId, int userId);
    Task<OperationResult> MarkAllAsReadAsync(int userId);

    /// <summary>Marks the owner's unread notifications (optionally of one category) and returns how many changed.</summary>
    Task<OperationResult<int>> MarkAllAsReadAsync(int userId, NotificationCategory? category);

    // === Auto-generation (gọi bởi các service khác) ===
    Task GenerateStreakNotificationsAsync(int userId, StreakUpdateResult streakResult);
    Task GenerateGoalNotificationsAsync(int userId, GoalsActivityRecordResult activityResult);
    Task GenerateBadgeNotificationAsync(int userId, int badgeId);

    /// <summary>
    /// Creates one notification for one user, idempotent by <paramref name="eventKey"/>.
    /// A repeated key is a success that adds nothing; an invalid value or a failed write returns a failure
    /// instead of throwing, so callers (such as billing) can treat it as best effort.
    /// </summary>
    Task<OperationResult> CreateForUserAsync(
        int userId,
        NotificationType type,
        string title,
        string message,
        string? relatedUrl,
        string? iconClass,
        string eventKey);

    // === Admin broadcast ===
    Task<OperationResult> CreateAdminAnnouncementAsync(string title, string message, int adminUserId);

    // === Cleanup ===
    Task<int> CleanupOldNotificationsAsync(int daysToKeep = 30);
}
