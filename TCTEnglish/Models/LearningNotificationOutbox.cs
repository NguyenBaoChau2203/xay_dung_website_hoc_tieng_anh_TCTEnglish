namespace TCTVocabulary.Models;

public class LearningNotificationOutbox
{
    public long Id { get; set; }

    public Guid EventId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public int UserId { get; set; }

    public string? ContentKey { get; set; }

    public string? BadgeId { get; set; }

    public DateTime BusinessDate { get; set; }

    public DateTime? OccurredAtUtc { get; set; }

    public string? NotificationEventKey { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public int PayloadVersion { get; set; } = 1;

    public DateTime CreatedAtUtc { get; set; }

    public int AttemptCount { get; set; }

    public DateTime? NextAttemptAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public string? LastError { get; set; }

    public virtual User User { get; set; } = null!;
}
