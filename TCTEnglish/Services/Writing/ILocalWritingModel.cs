namespace TCTEnglish.Services.Writing;

public sealed class LocalWritingModelResult
{
    public bool Available { get; init; }
    public string? UnavailableReason { get; init; }
    public string FeedbackJson { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<string> Suggestions { get; init; } = Array.Empty<string>();
}

public interface ILocalWritingModel
{
    Task<LocalWritingModelResult> GetFeedbackAsync(string promptPayload, CancellationToken cancellationToken);
}
