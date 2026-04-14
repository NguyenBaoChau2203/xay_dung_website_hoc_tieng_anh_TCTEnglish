namespace TCTEnglish.Services.AI.Internal;

public sealed record KnowledgeSnippet(
    string Title,
    string Body,
    string Source,
    string? Route = null,
    int Priority = 0);
