using System.ComponentModel.DataAnnotations;

namespace TCTEnglish.ViewModels
{
    public class GoalsViewModel
    {
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public string StreakMessage { get; set; } = string.Empty;
        public int DailyGoal { get; set; }
        public int TodayProgressValue { get; set; }
        public int TodayProgressMax { get; set; }
        public int ProgressPercent { get; set; }
        public List<GoalsWeekDayViewModel> WeeklyActivity { get; set; } = new();
        public string WeeklyActivityMessage { get; set; } = string.Empty;
        public List<GoalsBadgeViewModel> Badges { get; set; } = new();
        public UpdateGoalInputViewModel GoalEditor { get; set; } = new();
        public bool ShowGoalEditor { get; set; }

        public bool HasDailyGoal => DailyGoal > 0;
        public bool HasWeeklyActivity => WeeklyActivity.Any(day => day.ActivityCount > 0);
        public bool HasBadges => Badges.Count > 0;
        public bool HasRecentlyUnlockedBadges => Badges.Any(badge => badge.IsRecentlyUnlocked);
        public int RecentlyUnlockedBadgeCount => Badges.Count(badge => badge.IsRecentlyUnlocked);
        public int UnlockedBadgeCount => Badges.Count(badge => badge.IsUnlocked);
        public bool IsCreatingGoal => !HasDailyGoal;
        public string GoalHeaderActionText => IsCreatingGoal ? "Đặt mục tiêu" : "Chỉnh sửa mục tiêu";
        public string GoalEmptyStateActionText => IsCreatingGoal ? "Đặt mục tiêu hôm nay" : "Cập nhật mục tiêu";
        public string GoalEditorTitle => IsCreatingGoal ? "Đặt mục tiêu ngày" : "Cập nhật mục tiêu ngày";
        public string GoalEditorSubmitText => IsCreatingGoal ? "Lưu mục tiêu" : "Lưu thay đổi";
    }

    public class GoalsWeekDayViewModel
    {
        public string DayLabel { get; set; } = string.Empty;
        public string FullLabel { get; set; } = string.Empty;
        public int ActivityCount { get; set; }
        public int HeightPercent { get; set; }
        public bool IsToday { get; set; }
    }

    public class GoalsBadgeViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconClass { get; set; } = "fas fa-award";
        public bool IsUnlocked { get; set; }
        public bool IsRecentlyUnlocked { get; set; }
        public int ProgressValue { get; set; }
        public int TargetValue { get; set; }
        public int ProgressPercent { get; set; }
        public string ProgressLabel { get; set; } = string.Empty;
        public string MetricLabel { get; set; } = string.Empty;
        public DateTime? AwardedAt { get; set; }
        public string StatusText => IsUnlocked ? "Đã mở khóa" : "Đang theo dõi";
    }

    public class UpdateGoalInputViewModel
    {
        public const int MinDailyGoal = 0;
        public const int MaxDailyGoal = 500;

        [Display(Name = "Mục tiêu ngày")]
        [Range(MinDailyGoal, MaxDailyGoal, ErrorMessage = "Mục tiêu ngày phải từ {1} đến {2} thẻ.")]
        public int DailyGoal { get; set; }
    }
}
