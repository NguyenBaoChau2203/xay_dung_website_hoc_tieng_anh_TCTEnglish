using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.Services.Learning;
using TCTEnglish.Services.Notifications;
using NotificationsClock = TCTEnglish.Services.Notifications.LearningBusinessClock;
using TCTEnglish.ViewModels.Account;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Accounts;

public interface IAccountProfileService
{
    Task<ProfilePageViewModel?> BuildAsync(int userId, bool requestIsHttps, string? activeTab, string? postedName, CancellationToken cancellationToken = default, int windowDays = 7);

    Task<AccountActionResult> UpdateAsync(int userId, string? fullName, IFormFile? avatar, CancellationToken cancellationToken = default);
}

public sealed class AccountProfileService : IAccountProfileService
{
    private readonly DbflashcardContext _context;
    private readonly IAvatarUploadService _avatars;
    private readonly IGoalsService _goals;
    private readonly IPremiumAccessService _premium;
    private readonly ILearningHabitService _habits;
    private readonly INotificationPreferenceService _preferences;
    private readonly Microsoft.Extensions.Options.IOptions<OutboundDeliveryOptions> _outbound;
    private readonly NotificationsClock _clock;

    public AccountProfileService(
        DbflashcardContext context,
        IAvatarUploadService avatars,
        IGoalsService goals,
        IPremiumAccessService premium,
        ILearningHabitService habits,
        INotificationPreferenceService preferences,
        Microsoft.Extensions.Options.IOptions<OutboundDeliveryOptions> outbound,
        NotificationsClock clock)
    {
        _context = context;
        _avatars = avatars;
        _goals = goals;
        _premium = premium;
        _habits = habits;
        _preferences = preferences;
        _outbound = outbound;
        _clock = clock;
    }

    public async Task<ProfilePageViewModel?> BuildAsync(
        int userId,
        bool requestIsHttps,
        string? activeTab,
        string? postedName,
        CancellationToken cancellationToken = default, int windowDays = 7)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken);
        if (user == null || user.AnonymizedAt != null)
        {
            return null;
        }

        var badges = await _goals.GetEarnedBadgesForProfileAsync(userId);
        var access = await _premium.GetAccessSnapshotAsync(userId);
        var habits = await _habits.BuildAsync(userId, windowDays, cancellationToken);
        var preferences = await _preferences.ReadAsync(userId, _outbound.Value.PushConfigured, cancellationToken);
        var today = _clock.BusinessDate;
        var goal = await _context.UserGoals.AsNoTracking()
            .Where(row => row.UserId == userId && row.GoalArea == GoalArea.Vocabulary && row.IsActive)
            .Select(row => new { row.TargetValue })
            .FirstOrDefaultAsync(cancellationToken);
        var reviewed = await _context.UserDailyActivities.AsNoTracking()
            .Where(row => row.UserId == userId && row.ActivityDate == today)
            .Select(row => row.CardsReviewed)
            .FirstOrDefaultAsync(cancellationToken);
        var pending = await _context.AccountSecurityChallenges.AsNoTracking()
            .Where(row => row.UserId == userId && row.Purpose == AccountProofPurpose.EmailChange && row.ConsumedAtUtc == null && row.ExpiresAtUtc > DateTime.UtcNow)
            .OrderByDescending(row => row.Id)
            .Select(row => row.NewEmail)
            .FirstOrDefaultAsync(cancellationToken);

        string goalText;
        int? percent = null;
        if (goal == null)
        {
            goalText = "Chưa đặt mục tiêu từ vựng";
        }
        else if (goal.TargetValue <= 0)
        {
            goalText = "Mục tiêu từ vựng đang tạm dừng";
        }
        else
        {
            percent = (int)Math.Clamp(Math.Round(100d * reviewed / goal.TargetValue), 0, 100);
            goalText = $"{reviewed}/{goal.TargetValue} từ đã ôn hôm nay";
        }

        return new ProfilePageViewModel
        {
            FullName = postedName ?? user.FullName ?? string.Empty,
            Email = user.Email,
            CurrentAvatarUrl = user.AvatarUrl,
            Role = user.Role ?? Roles.Standard,
            StreakDays = user.Streak ?? 0,
            EarnedBadges = badges.ToList(),
            AccountCode = AccountDisplayCode.Format(user.UserId),
            ActiveTab = activeTab is "security" or "subscription" ? activeTab : "profile",
            CreatedAtText = user.CreatedAt.HasValue
                ? user.CreatedAt.Value.ToString("dd/MM/yyyy")
                : "Chưa có dữ liệu ngày tạo",
            EmailVerificationText = user.EmailVerifiedAt.HasValue
                ? "Email đã xác minh"
                : "Email chưa được xác minh",
            PasswordStateText = DescribePassword(user.PasswordHash),
            ConnectionSecurityText = requestIsHttps
                ? "Yêu cầu hiện tại đang dùng HTTPS"
                : "Yêu cầu hiện tại không dùng HTTPS",
            TodayGoalText = goalText,
            TodayGoalPercent = percent,
            PlanLabel = access.IsAdmin ? "Quản trị viên" : access.IsPremium ? "Học viên Premium" : "Học viên chuẩn",
            PlanExpiryText = access.PremiumEndsAtUtc.HasValue
                ? "Hạn gói: " + access.PremiumEndsAtUtc.Value.ToString("dd/MM/yyyy")
                : "Không có hạn gói đã ghi nhận",
            PendingEmail = pending,
            Habits = habits,
            Preferences = preferences
        };
    }

    public async Task<AccountActionResult> UpdateAsync(int userId, string? fullName, IFormFile? avatar, CancellationToken cancellationToken = default)
    {
        var name = (fullName ?? string.Empty).Trim();
        if (name.Length is < 1 or > 100)
        {
            return AccountActionResult.Fail("Họ tên cần từ 1 đến 100 ký tự.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(row => row.UserId == userId && row.AnonymizedAt == null, cancellationToken);
        if (user == null)
        {
            return AccountActionResult.Fail("Không tìm thấy tài khoản.");
        }

        if (avatar == null || avatar.Length == 0)
        {
            var changed = await _context.Users.Where(row => row.UserId == userId && row.AnonymizedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.FullName, name), cancellationToken);
            if (changed == 0) return AccountActionResult.Fail("Không tìm thấy tài khoản.");
            await _context.Entry(user).ReloadAsync(cancellationToken);
            return AccountActionResult.Ok("Cập nhật thông tin cá nhân thành công.", reissueSession: true);
        }

        string staged;
        try
        {
            staged = await _avatars.StageAvatarAsync(avatar, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return AccountActionResult.Fail(ex.Message);
        }

        var previous = user.AvatarUrl;
        int updated;
        try
        {
            updated = await _context.Users
                .Where(row => row.UserId == userId && row.AnonymizedAt == null && row.AvatarUrl == previous)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.AvatarUrl, staged)
                    .SetProperty(row => row.FullName, name), cancellationToken);
        }
        catch (DbUpdateException)
        {
            _avatars.DeleteOwnedAvatar(staged);
            return AccountActionResult.Fail("Chưa lưu được hồ sơ. Ảnh cũ vẫn được giữ.");
        }

        if (updated == 0)
        {
            _avatars.DeleteOwnedAvatar(staged);
            return AccountActionResult.Fail("Ảnh vừa được cập nhật ở một phiên khác. Hãy tải lại trang.");
        }

        if (!string.Equals(previous, staged, StringComparison.Ordinal))
        {
            _avatars.DeleteOwnedAvatar(previous);
        }

        await _context.Entry(user).ReloadAsync(cancellationToken);
        return AccountActionResult.Ok("Cập nhật thông tin cá nhân thành công.", reissueSession: true);
    }

    private static string DescribePassword(string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return "Chưa có dữ liệu mật khẩu";
        }

        if (hash.StartsWith("SOCIAL_LOGIN_", StringComparison.Ordinal))
        {
            return "Có dấu đăng nhập xã hội, chưa phải mật khẩu cục bộ đã kiểm chứng";
        }

        return hash.StartsWith("$2", StringComparison.Ordinal)
            ? "Đã có mật khẩu cục bộ"
            : "Dữ liệu mật khẩu không ở định dạng đã kiểm chứng";
    }
}
