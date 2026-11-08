namespace TCTEnglish.Services.AI.Rag;

public sealed record HybridRagEvidence(
    string SourceKey,
    string SourceType,
    string SentenceId,
    string ContentHash,
    string? CorpusVersion,
    string? ChunkerVersion,
    string? EmbeddingModel,
    string? AuthorizedRoute,
    string Visibility,
    int? OwnerUserId,
    DateTime CapturedAtUtc,
    string Text)
{
    public string? DocumentContentHash { get; init; }
    public string? SnapshotId { get; init; }
    public string? SourceVersion { get; init; }
    public string? EntityKey { get; init; }
}
