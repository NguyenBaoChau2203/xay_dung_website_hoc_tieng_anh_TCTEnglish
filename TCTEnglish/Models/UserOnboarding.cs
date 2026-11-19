namespace TCTEnglish.Models;

/// <summary>Status values of <see cref="UserOnboarding.Status"/>.</summary>
public static class OnboardingStatus
{
    public const string Pending = "Pending";
    public const string Completed = "Completed";
    public const string Skipped = "Skipped";
    public const string NotEligible = "NotEligible";
}

/// <summary>
/// First-run onboarding state of one user (one row per user). A missing row means "not decided yet".
/// The JSON columns hold small lists written and read only by <c>OnboardingService</c>; nothing here is shared
/// with other users and nothing here changes the system vocabulary.
/// </summary>
public sealed class UserOnboarding
{
    public int UserId { get; set; }

    /// <summary>Pending, Completed, Skipped or NotEligible (see <see cref="OnboardingStatus"/>).</summary>
    public string Status { get; set; } = OnboardingStatus.Pending;

    public int PromptCount { get; set; }
    public DateTime? LastPromptedAt { get; set; }

    /// <summary>Chosen goal keys, e.g. ["travel","communication"].</summary>
    public string? GoalsJson { get; set; }

    /// <summary>Chosen topic keys (A01..C08).</summary>
    public string? TopicKeysJson { get; set; }

    /// <summary>A1, A2, B1 or B2.</summary>
    public string? Level { get; set; }

    /// <summary>5, 10, 15 or 20 new words a day.</summary>
    public int? DailyNewWords { get; set; }

    /// <summary>Lower-case terms the user marked "Đã biết": excluded from decks, never marked as mastered.</summary>
    public string? KnownTermsJson { get; set; }

    /// <summary>The decks created from the topics: [{setId, goal, topicKeys}].</summary>
    public string? DecksJson { get; set; }

    /// <summary>The folder "Lộ trình của tôi" (no foreign key: the user may delete it).</summary>
    public int? FolderId { get; set; }

    /// <summary>Idempotency of the last Complete/Refill call.</summary>
    public string? LastRequestId { get; set; }

    /// <summary>SHA-256 (hex) of the payload of that call, so the same id with another payload is a conflict.</summary>
    public string? LastRequestHash { get; set; }

    /// <summary>The result of that call, replayed when the same request is sent again.</summary>
    public string? LastResultJson { get; set; }

    public DateTime? BannerDismissedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
