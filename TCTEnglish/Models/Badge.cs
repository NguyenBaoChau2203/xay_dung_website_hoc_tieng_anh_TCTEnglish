using System.ComponentModel.DataAnnotations;

namespace TCTVocabulary.Models;

public enum BadgeMetricType
{
    CurrentStreak = 1,
    LongestStreak = 2,
    TotalDaysActive = 3,
    TotalXp = 4,
    SpeakingVideosCompleted = 5,
    VocabularyCompletions = 6,
    WritingExercisesCompleted = 7,
    DistinctWritingExercisesCompleted = 8,
    ConfirmedLongestStreak = 9
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
    [MaxLength(30)]
    public string? DifficultyKey { get; set; }
    public bool IsSecret { get; set; }

    public virtual ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
}

public static class BadgeSeedData
{
    public static Badge[] CreateBadges()
    {
        Badge[] legacy =
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
            },
            new()
            {
                Id = 7,
                Code = "speaking-first-video",
                Name = "Khởi động Speaking",
                Description = "Hoàn thành video Speaking đầu tiên.",
                IconClass = "fas fa-microphone",
                MetricType = BadgeMetricType.SpeakingVideosCompleted,
                ThresholdValue = 1,
                SortOrder = 7
            },
            new()
            {
                Id = 8,
                Code = "speaking-five-videos",
                Name = "Nói trôi chảy",
                Description = "Hoàn thành 5 video Speaking để duy trì luyện tập.",
                IconClass = "fas fa-comments",
                MetricType = BadgeMetricType.SpeakingVideosCompleted,
                ThresholdValue = 5,
                SortOrder = 8
            },
            new()
            {
                Id = 9,
                Code = "vocabulary-first-mastered",
                Name = "Mở khóa Từ vựng",
                Description = "Hoàn thành 1 thẻ Vocabulary ở trạng thái Mastered.",
                IconClass = "fas fa-book-open",
                MetricType = BadgeMetricType.VocabularyCompletions,
                ThresholdValue = 1,
                SortOrder = 9
            },
            new()
            {
                Id = 10,
                Code = "vocabulary-ten-mastered",
                Name = "Nhịp từ vựng",
                Description = "Hoàn thành 10 thẻ Vocabulary ở trạng thái Mastered.",
                IconClass = "fas fa-spell-check",
                MetricType = BadgeMetricType.VocabularyCompletions,
                ThresholdValue = 10,
                SortOrder = 10
            },
            new()
            {
                Id = 11,
                Code = "writing-first-exercise",
                Name = "Mở khóa Writing",
                Description = "Hoàn thành bài Writing đầu tiên.",
                IconClass = "fas fa-pen",
                MetricType = BadgeMetricType.WritingExercisesCompleted,
                ThresholdValue = 1,
                SortOrder = 11
            },
            new()
            {
                Id = 12,
                Code = "writing-five-exercises",
                Name = "Viết chắc tay",
                Description = "Hoàn thành 5 bài Writing để duy trì thói quen luyện viết.",
                IconClass = "fas fa-pen-fancy",
                MetricType = BadgeMetricType.WritingExercisesCompleted,
                ThresholdValue = 5,
                SortOrder = 12
            }
        ];
        foreach (var badge in legacy)
            badge.DifficultyKey = badge.Id is 1 or 5 or 7 or 9 or 11 ? "chill" : "cuon";
        return legacy.Concat(new[]
        {
            Create(13, "writing-mood", "Bút mới lên mood", "chill", BadgeMetricType.DistinctWritingExercisesCompleted, 3, "Hoàn thành lần đầu 3 bài Writing khác nhau."),
            Create(14, "study-checkin", "Check-in bằng bài học", "chill", BadgeMetricType.TotalDaysActive, 3, "Có 3 ngày học đã xác nhận."),
            Create(15, "writing-deadline", "Deadline cũng phải nể", "cuon", BadgeMetricType.DistinctWritingExercisesCompleted, 10, "Hoàn thành lần đầu 10 bài Writing khác nhau."),
            Create(16, "xp-mood", "XP lên mood", "cuon", BadgeMetricType.TotalXp, 500, "Tích lũy 500 XP từ hoạt động học tập."),
            Create(17, "keyboard-form", "Bàn phím vào form", "chien-than", BadgeMetricType.DistinctWritingExercisesCompleted, 25, "Hoàn thành lần đầu 25 bài Writing khác nhau."),
            Create(18, "steady-flame", "Giữ lửa không seen", "chien-than", BadgeMetricType.ConfirmedLongestStreak, 14, "Có chuỗi 14 ngày học liên tiếp đã xác nhận."),
            Create(19, "keyboard-boss", "Trùm cuối bàn phím", "trum-cuoi", BadgeMetricType.DistinctWritingExercisesCompleted, 50, "Hoàn thành lần đầu 50 bài Writing khác nhau."),
            Create(20, "monthly-flame", "Giữ lửa xuyên tháng", "trum-cuoi", BadgeMetricType.ConfirmedLongestStreak, 30, "Có chuỗi 30 ngày học liên tiếp đã xác nhận."),
            Create(21, "secret-plot-twist", "Plot twist thứ 13", "chien-than", BadgeMetricType.DistinctWritingExercisesCompleted, 13, "Hoàn thành lần đầu 13 bài Writing khác nhau.", true),
            Create(22, "secret-quiet-effort", "Hệ chăm ngầm", "chien-than", BadgeMetricType.TotalDaysActive, 21, "Có 21 ngày học đã xác nhận.", true)
        }).ToArray();
    }

    private static Badge Create(int id, string code, string name, string tier, BadgeMetricType metric,
        int threshold, string description, bool secret = false) => new()
    {
        Id = id, Code = code, Name = name, DifficultyKey = tier, MetricType = metric,
        ThresholdValue = threshold, Description = description, IsSecret = secret,
        SortOrder = id, IconClass = "fas fa-award"
    };
}
