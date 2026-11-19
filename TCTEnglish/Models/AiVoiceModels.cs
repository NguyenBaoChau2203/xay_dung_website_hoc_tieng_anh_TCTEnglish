namespace TCTEnglish.Models;

public sealed class AiVoiceCatalogTopic
{
    public int Id { get; set; }
    public string StableKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Scenario { get; set; } = "";
    public string AiRole { get; set; } = "";
    public string LearnerRole { get; set; } = "";
    public string Level { get; set; } = "A2";
    public bool IsPublished { get; set; }
    public int SortOrder { get; set; }
}

public sealed class AiVoiceCustomTopic
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "";
    public string Scenario { get; set; } = "";
    public string AiRole { get; set; } = "";
    public string LearnerRole { get; set; } = "";
    public string Level { get; set; } = "A2";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class AiVoiceSession
{
    public Guid Id { get; set; }
    public int? UserId { get; set; }
    public string TopicKind { get; set; } = "catalog";
    public int TopicId { get; set; }
    public string TopicTitle { get; set; } = "";
    public string TopicSnapshot { get; set; } = "";
    public string Model { get; set; } = "";
    public string State { get; set; } = "creating";
    public string AgentState { get; set; } = "listening";
    public string? ProviderCallId { get; set; }
    public string AccountingState { get; set; } = "reserved";
    public DateTime QuotaDayUtc { get; set; }
    public int ReservedSeconds { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime LastActivityAtUtc { get; set; }
    public DateTime HeartbeatAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? CloseReason { get; set; }
    public bool ProviderDispatched { get; set; }
    public bool SetupFinished { get; set; }
    public bool CancelRequested { get; set; }
    public bool HistoryDeleted { get; set; }
    public bool NeedsHangup { get; set; }
    public int HangupAttempts { get; set; }
    public DateTime NextCleanupAtUtc { get; set; }
    public string? CleanupClaim { get; set; }
    public DateTime? CleanupClaimUntilUtc { get; set; }
    public string? PlayingResponseId { get; set; }
    public long Revision { get; set; }
}

public sealed class AiVoiceTurn
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public string ItemId { get; set; } = "";
    public string? ResponseId { get; set; }
    public string Speaker { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Interrupted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AiVoiceUsageDay
{
    public int UserId { get; set; }
    public DateTime UtcDate { get; set; }
    public int ReservedSeconds { get; set; }
    public int ConsumedSeconds { get; set; }
    public long InputAudioTokens { get; set; }
    public long OutputAudioTokens { get; set; }
    public long InputTextTokens { get; set; }
    public long OutputTextTokens { get; set; }
}

public sealed class AiVoiceUserLease
{
    public int UserId { get; set; }
    public Guid SessionId { get; set; }
}

public sealed class AiVoiceReceipt
{
    public Guid SessionId { get; set; }
    public string Kind { get; set; } = "";
    public string EventKey { get; set; } = "";
}

public sealed class AiVoiceCapacity
{
    public int Id { get; set; }
    public int Revision { get; set; }
}
