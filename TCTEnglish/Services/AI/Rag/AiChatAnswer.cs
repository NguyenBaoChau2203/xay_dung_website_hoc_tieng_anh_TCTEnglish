namespace TCTEnglish.Services.AI.Rag;

public sealed record AiChatAnswer(
    HybridRagOutcome Outcome,
    string? AnswerScope,
    string Text,
    IReadOnlyList<HybridRagEvidence> Evidence,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    string Model,
    string? RequestId)
{
    public string RetrievalMode { get; init; } = "live";
}
