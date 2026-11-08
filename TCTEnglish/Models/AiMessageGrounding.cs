namespace TCTEnglish.Models;

/// <summary>
/// Read-time projection. Not a persisted entity and not a stored route list.
/// </summary>
public sealed class AiMessageGrounding
{
    public int SchemaVersion { get; init; }

    public string? Outcome { get; init; }

    public string? Scope { get; init; }

    public DateTime? CapturedAtUtc { get; init; }

    public string Status { get; init; } = "unverified";

    public string? Notice { get; init; }

    public IReadOnlyList<AiMessageGroundingSource> Sources { get; init; } = [];
}

public sealed class AiMessageGroundingSource
{
    public string SourceKey { get; init; } = string.Empty;

    public string SourceType { get; init; } = string.Empty;

    public string SentenceId { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string? Route { get; init; }

    public string Freshness { get; init; } = "current";
}
