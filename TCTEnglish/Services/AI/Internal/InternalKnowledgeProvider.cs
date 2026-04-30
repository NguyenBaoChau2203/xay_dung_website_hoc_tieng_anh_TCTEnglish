using TCTEnglish.Services.AI;

namespace TCTEnglish.Services.AI.Internal;

public sealed class InternalKnowledgeProvider : IAiProviderClient
{
    private const float DefaultConfidenceThreshold = 0.55f;

    private readonly IAiQueryClassifier _classifier;
    private readonly IReadOnlyList<IKnowledgeRetriever> _retrievers;
    private readonly IAnswerComposer _composer;

    public InternalKnowledgeProvider(
        IAiQueryClassifier classifier,
        IEnumerable<IKnowledgeRetriever> retrievers,
        IAnswerComposer composer)
    {
        _classifier = classifier;
        _retrievers = retrievers.ToList();
        _composer = composer;
    }

    public async Task<AiProviderReply> GenerateReplyAsync(
        int userId,
        IReadOnlyList<AiContextMessage> messages,
        CancellationToken ct,
        AiProviderRequestOptions? requestOptions = null)
    {
        var userMessage = messages
            .LastOrDefault(x => string.Equals(x.Role, "user", StringComparison.OrdinalIgnoreCase))
            ?.Content?
            .Trim();

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            var fallback = await _composer.ComposeAsync(UserIntent.OutOfScope, string.Empty, [], ct);
            return new AiProviderReply(fallback, 0, 0, 0, "internal-keyword", Guid.NewGuid().ToString("N"));
        }

        var rawClassification = _classifier.Classify(userMessage);
        var classification = rawClassification.Confidence < DefaultConfidenceThreshold
            ? rawClassification with { Intent = UserIntent.OutOfScope }
            : rawClassification;

        var snippets = new List<KnowledgeSnippet>();
        foreach (var retriever in _retrievers.Where(x => x.CanHandle(classification.Intent)))
        {
            var retrievedSnippets = await retriever.RetrieveAsync(userId, userMessage, ct);
            snippets.AddRange(retrievedSnippets);
        }

        var answer = await _composer.ComposeAsync(classification.Intent, userMessage, snippets, ct);
        if (string.IsNullOrWhiteSpace(answer))
        {
            answer = "Mình chỉ hỗ trợ dữ liệu nội bộ của TCT English. Bạn vui lòng hỏi về tính năng hoặc dữ liệu trong hệ thống.";
        }

        var normalizedClassifierName = string.IsNullOrWhiteSpace(classification.ClassifierName)
            ? "keyword"
            : classification.ClassifierName.Trim().ToLowerInvariant();

        return new AiProviderReply(
            answer,
            0,
            0,
            0,
            $"internal-{normalizedClassifierName}",
            Guid.NewGuid().ToString("N"));
    }
}
