using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Notifications;

public sealed class NotificationPreferenceForm
{
    public bool DailyReminderEnabled { get; set; }
    public int ReminderHour { get; set; } = 20;
    public int ReminderMinute { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public bool PushOptIn { get; set; }
    public bool WeeklyEmailOptIn { get; set; }
    public bool Saved { get; set; }
    public string PushState { get; set; } = "Chưa bật";
    public string EmailState { get; set; } = "Chưa bật";
    public int ActivePushDevices { get; set; }
    public bool PushConfigured { get; set; }
    public string ReturnSurface { get; set; } = "settings";
}

public interface INotificationPreferenceService
{
    Task<NotificationPreferenceForm> ReadAsync(int userId, bool pushConfigured, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> SaveAsync(int userId, NotificationPreferenceForm input, CancellationToken cancellationToken = default);
}

public sealed class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly DbflashcardContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly Microsoft.Extensions.Options.IOptions<OutboundDeliveryOptions>? _outbound;
    private readonly IConfiguration? _configuration;

    public NotificationPreferenceService(DbflashcardContext context, TimeProvider timeProvider,
        Microsoft.Extensions.Options.IOptions<OutboundDeliveryOptions>? outbound = null, IConfiguration? configuration = null)
    {
        _context = context;
        _timeProvider = timeProvider;
        _outbound = outbound;
        _configuration = configuration;
    }

    public async Task<NotificationPreferenceForm> ReadAsync(int userId, bool pushConfigured, CancellationToken cancellationToken = default)
    {
        var row = await _context.UserNotificationPreferences.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var devices = await _context.WebPushSubscriptions.CountAsync(
            item => item.UserId == userId && item.RevokedAtUtc == null,
            cancellationToken);
        if (row == null)
        {
            return new NotificationPreferenceForm
            {
                PushConfigured = pushConfigured,
                PushState = pushConfigured ? "Chưa đăng ký thiết bị" : "Máy chủ chưa có khóa VAPID",
                EmailState = "Chưa bật. 20:00 chỉ là gợi ý, chưa phải lịch đang chạy."
            };
        }

        return new NotificationPreferenceForm
        {
            DailyReminderEnabled = row.DailyReminderEnabled,
            ReminderHour = row.ReminderMinutes / 60,
            ReminderMinute = row.ReminderMinutes % 60,
            TimeZoneId = row.TimeZoneId,
            PushOptIn = row.PushOptIn,
            WeeklyEmailOptIn = row.WeeklyEmailOptIn,
            Saved = true,
            PushConfigured = pushConfigured,
            ActivePushDevices = devices,
            PushState = DescribePush(row.PushOptIn, pushConfigured, devices),
            EmailState = row.WeeklyEmailOptIn
                ? _outbound?.Value.PublicSiteConfigured == true
                    && !string.IsNullOrWhiteSpace(_configuration?["SmtpSettings:Host"])
                    && !string.IsNullOrWhiteSpace(_configuration?["SmtpSettings:SenderEmail"])
                    && !string.IsNullOrWhiteSpace(_configuration?["SmtpSettings:Password"])
                    ? "Đã lưu yêu cầu email tuần. Thư chỉ gửi khi địa chỉ đã xác minh."
                    : "Đã lưu yêu cầu email tuần; máy chủ cần đủ cấu hình SMTP và địa chỉ web công khai."
                : "Đã lưu: không gửi email tuần."
        };
    }

    public Task<(bool Succeeded, string? Error)> SaveAsync(int userId, NotificationPreferenceForm input, CancellationToken cancellationToken = default)
        => TCTEnglish.Services.Accounts.UserMutationScope.ExecuteAsync(_context, userId,
            () => SaveCoreAsync(userId, input, cancellationToken), result => result.Succeeded, cancellationToken);

    private async Task<(bool Succeeded, string? Error)> SaveCoreAsync(int userId, NotificationPreferenceForm input, CancellationToken cancellationToken)
    {
        if (!await _context.Users.AnyAsync(row => row.UserId == userId && row.AnonymizedAt == null, cancellationToken))
            return (false, "Không tìm thấy tài khoản.");
        if (input.ReminderHour is < 0 or > 23 || input.ReminderMinute is < 0 or > 59)
        {
            return (false, "Giờ nhắc không hợp lệ.");
        }

        if (!ReminderTimeZone.TryResolve(input.TimeZoneId, out _))
        {
            return (false, "Múi giờ nhắc không được hỗ trợ.");
        }

        var row = await _context.UserNotificationPreferences.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (row == null)
        {
            row = new UserNotificationPreference { UserId = userId };
            _context.UserNotificationPreferences.Add(row);
        }

        row.DailyReminderEnabled = input.DailyReminderEnabled;
        row.ReminderMinutes = input.ReminderHour * 60 + input.ReminderMinute;
        row.TimeZoneId = input.TimeZoneId.Trim();
        row.PushOptIn = input.PushOptIn;
        row.WeeklyEmailOptIn = input.WeeklyEmailOptIn;
        row.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    private static string DescribePush(bool optIn, bool configured, int devices)
    {
        if (!optIn)
        {
            return "Đã lưu: không gửi push.";
        }

        if (!configured)
        {
            return "Đã lưu yêu cầu push, nhưng máy chủ chưa có khóa VAPID nên chưa gửi được.";
        }

        return devices == 0
            ? "Đã lưu yêu cầu push. Trình duyệt chưa cấp đăng ký thiết bị."
            : $"Đã đăng ký {devices} thiết bị. Quyền thông báo của trình duyệt là trạng thái riêng.";
    }
}
