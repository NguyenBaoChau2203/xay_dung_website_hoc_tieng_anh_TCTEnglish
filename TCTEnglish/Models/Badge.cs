using System.ComponentModel.DataAnnotations;

namespace TCTVocabulary.Models;

public enum BadgeMetricType
{
    CurrentStreak = 1,
    LongestStreak = 2,
    TotalDaysActive = 3,
    TotalXp = 4
}

public class Badge
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Code { get; set; } = null!;

    [MaxLength(150)]
    public string Name { get; set; } = null!;

    [MaxLength(500)]
    public string Description { get; set; } = null!;

    [MaxLength(100)]
    public string IconClass { get; set; } = "fas fa-award";

    public BadgeMetricType MetricType { get; set; }
    public int ThresholdValue { get; set; }
    public int SortOrder { get; set; }

    public virtual ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
}

public static class BadgeSeedData
{
    public static Badge[] CreateBadges()
    {
        return
        [
            new()
            {
                Id = 1,
                Code = "first-session",
                Name = "Khởi động",
                Description = "Hoàn thành ngày học đầu tiên để bắt đầu hành trình.",
                IconClass = "fas fa-seedling",
                MetricType = BadgeMetricType.TotalDaysActive,
                ThresholdValue = 1,
                SortOrder = 1
            },
            new()
            {
                Id = 2,
                Code = "three-day-streak",
                Name = "Giữ nhịp",
                Description = "Duy trì streak học tập trong 3 ngày liên tiếp.",
                IconClass = "fas fa-fire",
                MetricType = BadgeMetricType.CurrentStreak,
                ThresholdValue = 3,
                SortOrder = 2
            },
            new()
            {
                Id = 3,
                Code = "seven-day-peak",
                Name = "Bền bỉ",
                Description = "Chạm mốc streak dài nhất 7 ngày.",
                IconClass = "fas fa-bolt",
                MetricType = BadgeMetricType.LongestStreak,
                ThresholdValue = 7,
                SortOrder = 3
            },
            new()
            {
                Id = 4,
                Code = "active-week",
                Name = "Cả tuần chăm chỉ",
                Description = "Có hoạt động học tập trong 7 ngày khác nhau.",
                IconClass = "fas fa-calendar-check",
                MetricType = BadgeMetricType.TotalDaysActive,
                ThresholdValue = 7,
                SortOrder = 4
            },
            new()
            {
                Id = 5,
                Code = "xp-collector",
                Name = "Tích điểm",
                Description = "Tích lũy đủ 50 XP từ các hoạt động học tập.",
                IconClass = "fas fa-star",
                MetricType = BadgeMetricType.TotalXp,
                ThresholdValue = 50,
                SortOrder = 5
            },
            new()
            {
                Id = 6,
                Code = "xp-champion",
                Name = "Bứt phá",
                Description = "Đạt 200 XP để mở khóa cột mốc cao hơn.",
                IconClass = "fas fa-trophy",
                MetricType = BadgeMetricType.TotalXp,
                ThresholdValue = 200,
                SortOrder = 6
            }
        ];
    }
}
