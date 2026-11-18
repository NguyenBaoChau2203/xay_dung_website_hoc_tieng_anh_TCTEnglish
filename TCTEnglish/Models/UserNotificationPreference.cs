namespace TCTEnglish.Models;

public class UserNotificationPreference
{
    public int UserId { get; set; }
    public bool DailyReminderEnabled { get; set; }
    public int ReminderMinutes { get; set; } = 20 * 60;
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public bool PushOptIn { get; set; }
    public bool WeeklyEmailOptIn { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public virtual TCTVocabulary.Models.User User { get; set; } = null!;
}
