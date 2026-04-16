using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services;

public interface INotificationService
{
    // === CRUD ===
    Task<int> GetUnreadCountAsync(int userId);
    Task<List<NotificationViewModel>> GetNotificationsAsync(int userId, int page = 1, int pageSize = 20);
    Task<OperationResult> MarkAsReadAsync(int notificationId, int userId);
    Task<OperationResult> MarkAllAsReadAsync(int userId);

    // === Auto-generation (gọi bởi các service khác) ===
    Task GenerateStreakNotificationsAsync(int userId, StreakUpdateResult streakResult);
    Task GenerateGoalNotificationsAsync(int userId, GoalsActivityRecordResult activityResult);
    Task GenerateBadgeNotificationAsync(int userId, int badgeId);

    // === Admin broadcast ===
    Task<OperationResult> CreateAdminAnnouncementAsync(string title, string message, int adminUserId);

    // === Cleanup ===
    Task<int> CleanupOldNotificationsAsync(int daysToKeep = 30);
}
