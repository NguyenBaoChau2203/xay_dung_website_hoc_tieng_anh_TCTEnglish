using TCTEnglish.ViewModels;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Learning;

/// <summary>The only learner badge projection. Locked discoveries contain no catalog identity or numeric rule.</summary>
public static class BadgeProjection
{
    public static bool HasSupportedRule(Badge badge)
        => badge.ThresholdValue > 0 && Enum.IsDefined(badge.MetricType);

    public static string TierLabel(string? key) => key switch
    {
        "chill" => "Nhập môn chill",
        "cuon" => "Bắt đầu cuốn",
        "chien-than" => "Hệ chiến thần",
        "trum-cuoi" => "Trùm cuối server",
        _ => "Thành tích"
    };

    public static GoalsBadgeViewModel Build(Badge badge, int progress, DateTime? awardedAt,
        bool recentlyUnlocked, bool retroactive)
    {
        if (badge.IsSecret && !awardedAt.HasValue)
            return new GoalsBadgeViewModel
            {
                Name = "Ủa, bí mật nè!", Description = "Một điều bất ngờ đang chờ bạn khám phá.",
                IconClass = "fas fa-lock", IsSecret = true, IsRedacted = true,
                DisplayGroupKey = "bi-an", DisplayGroupLabel = "Ủa, bí mật nè!",
                ProgressLabel = "Chưa khám phá"
            };

        var supported = HasSupportedRule(badge);
        var unlocked = awardedAt.HasValue;
        var tier = badge.DifficultyKey is "chill" or "cuon" or "chien-than" or "trum-cuoi"
            ? badge.DifficultyKey : "other";
        var value = unlocked ? badge.ThresholdValue : Math.Clamp(progress, 0, Math.Max(0, badge.ThresholdValue));
        var unit = badge.MetricType switch
        {
            BadgeMetricType.TotalXp => "XP",
            BadgeMetricType.SpeakingVideosCompleted => "video",
            BadgeMetricType.VocabularyCompletions => "thẻ",
            BadgeMetricType.WritingExercisesCompleted or BadgeMetricType.DistinctWritingExercisesCompleted => "bài",
            _ => "ngày"
        };
        return new GoalsBadgeViewModel
        {
            Code = badge.Code, Name = badge.Name, Description = badge.Description,
            IconClass = string.IsNullOrWhiteSpace(badge.IconClass) ? "fas fa-award" : badge.IconClass,
            IsSecret = badge.IsSecret, IsUnlocked = unlocked, AwardedAt = awardedAt,
            IsRecentlyUnlocked = unlocked && recentlyUnlocked, IsRetroactive = retroactive,
            DifficultyKey = tier, DifficultyLabel = TierLabel(tier),
            DisplayGroupKey = badge.IsSecret ? "bi-an" : tier,
            DisplayGroupLabel = badge.IsSecret ? "Ủa, bí mật nè!" : TierLabel(tier),
            IsAvailable = supported,
            ProgressValue = supported || unlocked ? value : null,
            TargetValue = supported || unlocked ? badge.ThresholdValue : null,
            ProgressPercent = unlocked ? 100 : supported ? (int)Math.Round(value * 100d / badge.ThresholdValue) : null,
            MetricLabel = supported ? unit : string.Empty,
            ProgressLabel = unlocked ? "Đã mở khóa" : supported ? $"{value}/{badge.ThresholdValue} {unit}" : "Chưa có quy tắc hỗ trợ"
        };
    }
}
