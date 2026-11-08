namespace TCTEnglish.Services.AI;

public sealed record AiProviderReply(
    string Text,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    string Model,
    string? RequestId)
{
    // Already included in CompletionTokens; diagnostics must not add it a second time.
    public int ReasoningTokens { get; init; }

    /// <summary>Earlier chat attempts whose response was lost after the request was sent.</summary>
    public int UncertainTokenUpper { get; init; }
}

