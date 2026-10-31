namespace TCTEnglish.Services.Writing;

public sealed class LocalGrammarFinding
{
    public int Offset { get; init; }
    public int Length { get; init; }
    public string Message { get; init; } = string.Empty;
    public string RuleId { get; init; } = string.Empty;
}

public sealed class LocalGrammarCheckResult
{
    public bool Available { get; init; }
    public string? UnavailableReason { get; init; }
    public IReadOnlyList<LocalGrammarFinding> Findings { get; init; } = Array.Empty<LocalGrammarFinding>();
}

public interface ILocalGrammarChecker
{
    Task<LocalGrammarCheckResult> CheckAsync(string text, CancellationToken cancellationToken);
}
