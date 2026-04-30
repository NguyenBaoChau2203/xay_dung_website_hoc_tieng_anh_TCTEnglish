using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace TCTEnglish.Services.AI.Internal.Retrievers;

public sealed class WebsiteGuideRetriever : IKnowledgeRetriever
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IReadOnlyList<WebsiteGuideEntry> _guides;

    public WebsiteGuideRetriever(IWebHostEnvironment environment)
        : this(Path.Combine(environment.ContentRootPath, "wwwroot", "data", "ai", "website-guides.json"))
    {
    }

    public WebsiteGuideRetriever(string guideFilePath)
    {
        _guides = LoadGuides(guideFilePath);
    }

    public bool CanHandle(UserIntent intent) => intent == UserIntent.WebsiteGuide;

    public Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var normalizedMessage = AiTextNormalizer.Normalize(userMessage);
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return Task.FromResult<IReadOnlyList<KnowledgeSnippet>>([]);
        }

        var queryTokens = AiTextNormalizer.Tokenize(normalizedMessage);
        WebsiteGuideEntry? bestGuide = null;
        var bestScore = 0;

        foreach (var guide in _guides)
        {
            var score = ScoreGuide(guide, normalizedMessage, queryTokens);
            if (score > bestScore)
            {
                bestScore = score;
                bestGuide = guide;
            }
        }

        if (bestGuide is null || bestScore < 3)
        {
            return Task.FromResult<IReadOnlyList<KnowledgeSnippet>>([]);
        }

        return Task.FromResult<IReadOnlyList<KnowledgeSnippet>>([
            new KnowledgeSnippet(
                bestGuide.Title,
                bestGuide.Body,
                KnowledgeSnippetSources.WebsiteGuide,
                bestGuide.Route,
                bestScore)
        ]);
    }

    private static int ScoreGuide(WebsiteGuideEntry guide, string normalizedMessage, HashSet<string> queryTokens)
    {
        var score = 0;
        var normalizedTitle = AiTextNormalizer.Normalize(guide.Title);
        if (normalizedMessage.Contains(normalizedTitle, StringComparison.Ordinal))
        {
            score += 8;
        }

        score += ScoreKeywordMatches(guide.Keywords, normalizedMessage);

        var guideTokens = AiTextNormalizer.Tokenize(guide.Title);
        guideTokens.UnionWith(AiTextNormalizer.Tokenize(guide.Topic));
        foreach (var keyword in guide.Keywords)
        {
            guideTokens.UnionWith(AiTextNormalizer.Tokenize(keyword));
        }

        var normalizedTopic = AiTextNormalizer.Normalize(guide.Topic);
        if (!string.IsNullOrWhiteSpace(normalizedTopic)
            && queryTokens.Contains(normalizedTopic))
        {
            score += 4;
        }

        score += queryTokens.Count(guideTokens.Contains);
        return score;
    }

    private static int ScoreKeywordMatches(IEnumerable<string> keywords, string normalizedMessage)
    {
        var score = 0;
        foreach (var keyword in keywords)
        {
            var normalizedKeyword = AiTextNormalizer.Normalize(keyword);
            if (string.IsNullOrWhiteSpace(normalizedKeyword)
                || !$" {normalizedMessage} ".Contains($" {normalizedKeyword} ", StringComparison.Ordinal))
            {
                continue;
            }

            var keywordTokenCount = AiTextNormalizer.Tokenize(keyword).Count;
            score += 6 + Math.Min(keywordTokenCount, 4);
        }

        return score;
    }

    private static IReadOnlyList<WebsiteGuideEntry> LoadGuides(string guideFilePath)
    {
        if (!File.Exists(guideFilePath))
        {
            return [];
        }

        using var stream = File.OpenRead(guideFilePath);
        var document = JsonSerializer.Deserialize<WebsiteGuideDocument>(stream, JsonOptions);
        return document?.Guides ?? [];
    }

    private sealed class WebsiteGuideDocument
    {
        public List<WebsiteGuideEntry> Guides { get; set; } = [];
    }

    private sealed class WebsiteGuideEntry
    {
        public string Title { get; set; } = string.Empty;

        public List<string> Keywords { get; set; } = [];

        public string Body { get; set; } = string.Empty;

        public string? Route { get; set; }

        public string Topic { get; set; } = string.Empty;
    }
}
