namespace TCTEnglish.Services.AI.Internal;

public interface IAnswerComposer
{
    Task<string> ComposeAsync(
        UserIntent intent,
        string userMessage,
        IReadOnlyList<KnowledgeSnippet> snippets,
        CancellationToken ct);
}
