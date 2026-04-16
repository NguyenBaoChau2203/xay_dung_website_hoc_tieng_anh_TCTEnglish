using System.ComponentModel.DataAnnotations;
using TCTVocabulary.Models;

namespace TCTEnglish.Models;

public enum NotificationType
{
    StreakWarning    = 1,   // "Bạn chưa học hôm nay — streak X ngày sắp mất!"
    StreakRecord     = 2,   // "Streak X ngày — kỷ lục mới!"
    GoalProgress     = 3,   // "Bạn đã đạt 80% mục tiêu hôm nay!"
    GoalCompleted    = 4,   // "Chúc mừng! Bạn đã hoàn thành mục tiêu hôm nay!"
    BadgeEarned      = 5,   // "Bạn vừa nhận huy hiệu 'Bền bỉ'!"
    BadgeNear        = 6,   // "Còn 2 bài nữa để mở khóa huy hiệu..."
    AdminAnnouncement = 7   // Admin gửi thông báo toàn hệ thống
}

public class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public NotificationType Type { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;

    [MaxLength(500)]
    public string? RelatedUrl { get; set; }   // VD: "/Goals", "/Goals#badges"

    [MaxLength(100)]
    public string? IconClass { get; set; }    // VD: "fas fa-fire", "fas fa-trophy"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; } = null!;
}
