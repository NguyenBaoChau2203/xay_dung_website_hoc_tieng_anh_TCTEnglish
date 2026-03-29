namespace TCTEnglish.Services.AI;

public sealed record AiProviderReply(
    string Text,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    string Model,
    string? RequestId);

