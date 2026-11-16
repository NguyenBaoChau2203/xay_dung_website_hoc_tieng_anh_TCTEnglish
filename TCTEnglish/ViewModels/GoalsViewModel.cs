using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using TCTVocabulary.Models;

namespace TCTEnglish.ViewModels
{
    public class GoalsViewModel
    {
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public string StreakMessage { get; set; } = string.Empty;
        public List<GoalCardViewModel> GoalCards { get; set; } = new();
        public int DailyGoal { get; set; }
        public int TodayProgressValue { get; set; }
        public int TodayProgressMax { get; set; }
        public int ProgressPercent { get; set; }
        public List<GoalsWeekDayViewModel> WeeklyActivity { get; set; } = new();
        public DateTime BusinessDate { get; set; }
        public List<GoalsStudyDayViewModel> StudyDays { get; set; } = new();
        public string WeeklyActivityMessage { get; set; } = string.Empty;
        public List<GoalsBadgeViewModel> Badges { get; set; } = new();
        public UpdateGoalInputViewModel GoalEditor { get; set; } = new();
        public List<SelectListItem> GoalAreaOptions { get; set; } = new();
        public List<string> DeferredGoalAreaLabels { get; set; } = new();
        public bool ShowGoalEditor { get; set; }

        public bool HasGoalCards => GoalCards.Count > 0;
        public bool HasDailyGoal => DailyGoal > 0;
        public bool HasWeeklyActivity => WeeklyActivity.Any(day => day.ActivityCount > 0);
        public bool HasBadges => Badges.Count > 0;
        public bool HasDeferredGoalAreas => DeferredGoalAreaLabels.Count > 0;
        public bool HasRecentlyUnlockedBadges => Badges.Any(badge => badge.IsRecentlyUnlocked);
        public int RecentlyUnlockedBadgeCount => Badges.Count(badge => badge.IsRecentlyUnlocked);
        public int UnlockedBadgeCount => Badges.Count(badge => badge.IsUnlocked);
        public bool IsCreatingGoal => !HasGoalCards;
        public string GoalHeaderActionText => IsCreatingGoal ? "Thêm mục tiêu" : "Thêm / cập nhật mục tiêu";
        public string GoalEmptyStateActionText => IsCreatingGoal ? "Tạo mục tiêu đầu tiên" : "Cập nhật mục tiêu";
        public string GoalEditorTitle => IsCreatingGoal ? "Tạo mục tiêu mới" : "Cập nhật mục tiêu theo kỹ năng";
        public string GoalEditorSubmitText => IsCreatingGoal ? "Lưu mục tiêu" : "Lưu thay đổi";
    }

    public class GoalCardViewModel
    {
        public GoalArea GoalArea { get; set; }
        public string AreaLabel { get; set; } = string.Empty;
        public string UnitLabel { get; set; } = string.Empty;
        public int TargetValue { get; set; }
        public int TodayProgressValue { get; set; }
        public int ProgressPercent { get; set; }
        public string ProgressLabel => $"Hôm nay: {TodayProgressValue}/{TargetValue} {UnitLabel}";
        public string TargetLabel => $"Mục tiêu: {TargetValue} {UnitLabel}/ngày";
    }

    public class GoalsWeekDayViewModel
    {
        public string DayLabel { get; set; } = string.Empty;
        public string FullLabel { get; set; } = string.Empty;
        public int ActivityCount { get; set; }
        public int HeightPercent { get; set; }
        public bool IsToday { get; set; }
    }

    public sealed class GoalsStudyDayViewModel
    {
        public DateTime BusinessDate { get; init; }
        public string DayLabel { get; init; } = string.Empty;
        public string FullLabel => BusinessDate.ToString("dd/MM/yyyy");
        public bool IsToday { get; init; }
        public bool HasConfirmedStudy { get; init; }
        public int? ActiveGoalCount { get; init; }
        public int? MetGoalCount { get; init; }
        // null: unknown history or no positive target; false: known unmet targets.
        public bool? AllGoalsMet => ActiveGoalCount is > 0 && MetGoalCount.HasValue
            ? MetGoalCount == ActiveGoalCount : null;
        public string StudyLabel => HasConfirmedStudy ? "Có ngày học đã xác nhận" : "Chưa ghi nhận ngày học";
        public string GoalLabel => AllGoalsMet == true ? "Đã đạt mọi mục tiêu"
            : AllGoalsMet == false ? "Chưa đạt mọi mục tiêu"
            : ActiveGoalCount == 0 ? "Không có mục tiêu đang chạy" : "Chưa đủ lịch sử mục tiêu";
    }

    public class GoalsBadgeViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconClass { get; set; } = "fas fa-award";
        public bool IsUnlocked { get; set; }
        public bool IsRecentlyUnlocked { get; set; }
        public int? ProgressValue { get; set; }
        public int? TargetValue { get; set; }
        public int? ProgressPercent { get; set; }
        public string ProgressLabel { get; set; } = string.Empty;
        public string MetricLabel { get; set; } = string.Empty;
        public DateTime? AwardedAt { get; set; }
        public string? DifficultyKey { get; set; }
        public string DifficultyLabel { get; set; } = string.Empty;
        public string DisplayGroupKey { get; set; } = string.Empty;
        public string DisplayGroupLabel { get; set; } = string.Empty;
        public bool IsSecret { get; set; }
        public bool IsRedacted { get; set; }
        public bool HasKnownProgress => ProgressValue.HasValue && TargetValue.HasValue;
        public bool IsAvailable { get; set; }
        public bool CanRecommend => !IsUnlocked && !IsRedacted && IsAvailable;
        public bool IsRetroactive { get; set; }
        public string StatusText => IsUnlocked ? (IsRetroactive ? "Đã ghi nhận thành tích trước đây" : "Đã mở khóa")
            : IsRedacted ? "Chưa khám phá" : IsAvailable ? "Đang theo dõi" : "Chưa có quy tắc hỗ trợ";
    }

    public class UpdateGoalInputViewModel
    {
        public const int MinTargetValue = 0;
        public const int MaxTargetValue = 500;

        [Display(Name = "Kỹ năng")]
        [Required(ErrorMessage = "Vui lòng chọn kỹ năng cho mục tiêu.")]
        public GoalArea GoalArea { get; set; } = GoalArea.Vocabulary;

        [Display(Name = "Mục tiêu mỗi ngày")]
        [Range(MinTargetValue, MaxTargetValue, ErrorMessage = "Mục tiêu mỗi ngày phải từ {1} đến {2}.")]
        public int TargetValue { get; set; }
    }
}
