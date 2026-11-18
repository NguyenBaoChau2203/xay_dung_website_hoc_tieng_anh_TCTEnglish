using Microsoft.AspNetCore.Http;
using TCTEnglish.ViewModels;
using TCTEnglish.Services.Learning;
using TCTEnglish.Services.Notifications;

namespace TCTEnglish.ViewModels.Account;

public sealed class ProfilePageViewModel
{
    public string FullName { get; set; } = string.Empty;
    public IFormFile? Avatar { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? CurrentAvatarUrl { get; set; }
    public string Role { get; set; } = "Standard";
    public int StreakDays { get; set; }
    public List<GoalsBadgeViewModel> EarnedBadges { get; set; } = new();
    public string AccountCode { get; set; } = string.Empty;
    public string ActiveTab { get; set; } = "profile";
    public string CreatedAtText { get; set; } = "Chưa có dữ liệu ngày tạo";
    public string EmailVerificationText { get; set; } = "Email chưa được xác minh";
    public string PasswordStateText { get; set; } = "Chưa có dữ liệu mật khẩu";
    public string ConnectionSecurityText { get; set; } = "Không xác định giao thức của yêu cầu hiện tại";
    public string TodayGoalText { get; set; } = "Chưa đặt mục tiêu";
    public int? TodayGoalPercent { get; set; }
    public string PlanLabel { get; set; } = "Học viên chuẩn";
    public string PlanExpiryText { get; set; } = "Không suy ra hạn dùng từ vai trò";
    public string? PendingEmail { get; set; }
    public LearningHabitSnapshot Habits { get; set; } = new();
    public NotificationPreferenceForm Preferences { get; set; } = new();
}

public sealed class UpdateProfileInput
{
    public string FullName { get; set; } = string.Empty;
    public IFormFile? Avatar { get; set; }
}
