namespace TCTEnglish.Models;

public static class OutboundChannels
{
    public const string Push = "Push";
    public const string Email = "Email";
}

public static class OutboundDeliveryStatuses
{
    public const string Pending = "Pending";
    public const string Leased = "Leased";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    public const string Unknown = "Unknown";
    public const string Expired = "Expired";
    public const string Revoked = "Revoked";
    public const string Skipped = "Skipped";
}

public class OutboundDelivery
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string Channel { get; set; } = null!;
    public string PeriodKey { get; set; } = null!;
    public string SubscriptionKey { get; set; } = null!;
    public string Status { get; set; } = OutboundDeliveryStatuses.Pending;
    public int AttemptCount { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public string? LastOutcome { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public virtual TCTVocabulary.Models.User User { get; set; } = null!;
}
