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

        score += AiTextNormalizer.CountMatches(normalizedMessage, guide.Keywords) * 6;

        var guideTokens = AiTextNormalizer.Tokenize(guide.Title);
        foreach (var keyword in guide.Keywords)
        {
            guideTokens.UnionWith(AiTextNormalizer.Tokenize(keyword));
        }

        score += queryTokens.Count(guideTokens.Contains);
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
    }
}
