namespace TCTVocabulary.Models;

public class SpeakingImportJob
{
    public long Id { get; set; }

    public Guid PublicId { get; set; }

    public int OwnerUserId { get; set; }

    public string YoutubeId { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;

    /// <summary>queued | running | succeeded | failed | cancelled</summary>
    public string Status { get; set; } = "queued";

    public int AttemptCount { get; set; }

    public int MaxAttempts { get; set; } = 3;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? LeaseUntilUtc { get; set; }

    public string? LeaseOwner { get; set; }

    public string? LastError { get; set; }

    public int? ResultVideoId { get; set; }

    public DateTime? NextAttemptAtUtc { get; set; }

    public string? LeaseToken { get; set; }

    /// <summary>youtube | subtitle. Null means a legacy caption import.</summary>
    public string? SourceKind { get; set; }

    /// <summary>Normalized transcript hash for the source that should be published. Null on legacy rows.</summary>
    public string? SourceContentHash { get; set; }

    public string? SubtitlePayload { get; set; }

    public virtual User OwnerUser { get; set; } = null!;
}
