namespace TCTEnglish.Services.AI.Rag;

public sealed record ChatSentenceSelectionRequest(
    int UserId,
    string Question,
    IReadOnlyList<GroundedHistoryTurn> History,
    IReadOnlyList<GroundedSentence> Sentences,
    int MaxOutputTokens)
{
    public int MaxInputUpper { get; init; }

    public int GenerationAttempts { get; init; } = 1;

    public Func<CancellationToken, Task<bool>>? ConfirmCurrent { get; init; }

    public Func<int, int, CancellationToken, Task<bool>>? RecordUsage { get; init; }
}

public sealed record ChatSentenceSelection(
    string Json,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    bool UsageKnown)
{
    public bool OutputAccepted { get; init; } = true;

    public int UnknownTokens { get; init; }
}

public interface IChatSentenceSelector
{
    int MeasureSelection(ChatSentenceSelectionRequest request) => 0;

    Task<ChatSentenceSelection> SelectAsync(ChatSentenceSelectionRequest request, CancellationToken ct);
}
