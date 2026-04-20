namespace TCTEnglish.Services.AI.Internal;

public interface IKnowledgeRetriever
{
    bool CanHandle(UserIntent intent);

    Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(
        int userId,
        string userMessage,
        CancellationToken ct);
}
