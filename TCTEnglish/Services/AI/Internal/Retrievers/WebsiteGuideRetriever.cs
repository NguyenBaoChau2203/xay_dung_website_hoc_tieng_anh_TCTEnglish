using Microsoft.AspNetCore.Hosting;

namespace TCTEnglish.Services.AI.Internal.Retrievers;

public sealed class WebsiteGuideRetriever : IKnowledgeRetriever
{
    private readonly string _guideFilePath;
    private readonly object _catalogGate = new();
    private Task<IReadOnlyList<WebsiteGuideEntry>>? _catalog;

    public WebsiteGuideRetriever(IWebHostEnvironment environment)
        : this(Path.Combine(environment.ContentRootPath, "wwwroot", "data", "ai", "website-guides.json"))
    {
    }

    public WebsiteGuideRetriever(string guideFilePath)
    {
        _guideFilePath = guideFilePath;
    }

    public bool CanHandle(UserIntent intent) => intent is UserIntent.WebsiteGuide or UserIntent.StudyRecommendation;

    public async Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var normalizedMessage = AiTextNormalizer.Normalize(userMessage);
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return [];
        }

        var guides = await GetGuidesAsync(ct);
        var reviewed = MatchReviewedGuide(normalizedMessage, guides);
        if (reviewed is not null) return [ToSnippet(reviewed, 1_000)];

        // These are general choices, never fabricated personal progress. The
        // composer still prefers a real recommendation supplied by live retrievers.
        if (DeterministicIntentClassifier.IsStudyRecommendation(normalizedMessage))
        {
            string[] starterIds = ["starter-vocabulary", "starter-speaking", "starter-writing"];
            return starterIds
                .Select(id => guides.FirstOrDefault(guide => guide.Id == id))
                .Where(guide => guide is not null)
                .Select(guide => ToSnippet(guide!, 3))
                .ToList();
        }

        var queryTokens = AiTextNormalizer.Tokenize(normalizedMessage);
        WebsiteGuideEntry? bestGuide = null;
        var bestScore = 0;

        foreach (var guide in guides)
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
            return [];
        }

        return [ToSnippet(bestGuide, bestScore)];
    }

    private static KnowledgeSnippet ToSnippet(WebsiteGuideEntry guide, int priority)
        => new(guide.Title, guide.Body, KnowledgeSnippetSources.WebsiteGuide,
            guide.Route, priority, EntityKey: string.IsNullOrWhiteSpace(guide.Id) ? null : "guide:" + guide.Id.Trim());

    internal async Task<KnowledgeSnippet?> MatchReviewedQuestionAsync(string userMessage, CancellationToken ct)
    {
        var normalized = AiTextNormalizer.Normalize(userMessage);
        if (normalized.Length == 0) return null;
        var guide = MatchReviewedGuide(normalized, await GetGuidesAsync(ct));
        return guide is null ? null : ToSnippet(guide, 1_000);
    }

    private static WebsiteGuideEntry? MatchReviewedGuide(string normalized, IReadOnlyList<WebsiteGuideEntry> guides)
    {
        var exact = guides.FirstOrDefault(candidate => candidate.Questions.Any(question => AiTextNormalizer.Normalize(question) == normalized));
        if (exact is not null) return exact;
        // Explicit authored phrases help stale classifiers without turning a
        // question about live personal facts into a static FAQ response.
        if (AiTextNormalizer.ContainsAny(normalized, "tien do cua toi", "tien do cua minh", "toi da hoc", "minh da hoc",
                "streak cua toi", "xp cua toi", "bo tu cua toi", "lop hoc cua toi", "goi cua toi")) return null;
        var roadmap = ResolveRoadmap(normalized);
        if (roadmap is not null) return guides.FirstOrDefault(guide => guide.Id == roadmap);
        var matches = guides.Select(guide => (Guide: guide, Score: Math.Max(ScoreMatchRules(guide.MatchRules, normalized),
                ScoreKeywordMatches(guide.MatchPhrases, normalized))))
            .Where(item => item.Score > 0).OrderByDescending(item => item.Score).ToList();
        return matches.Count > 0 && (matches.Count == 1 || matches[0].Score > matches[1].Score) ? matches[0].Guide : null;
    }

    private static int ScoreMatchRules(IEnumerable<List<string>> rules, string normalized)
        => rules.Where(rule => rule.All(term => AiTextNormalizer.ContainsAny(normalized, AiTextNormalizer.Normalize(term))))
            .Select(rule => 50 + rule.Count * 4 + Math.Min(24, rule.Sum(term => AiTextNormalizer.Tokenize(term).Count)))
            .DefaultIfEmpty(0).Max();

    private static string? ResolveRoadmap(string normalized)
    {
        var toeic = AiTextNormalizer.ContainsAny(normalized, "toeic", "toiec");
        var ielts = AiTextNormalizer.ContainsAny(normalized, "ielts", "ielst");
        var cefr = AiTextNormalizer.ContainsAny(normalized, "a1", "a2", "b1", "b2", "c1", "c2", "cefr");
        var asksForPath = AiTextNormalizer.ContainsAny(normalized, "lo trinh", "ke hoach hoc", "study plan", "learning path",
            "roadmap", "work toward", "from scratch", "mat goc", "tung buoc", "huong den", "cai thien trinh do",
            "tu 0 len", "can dat", "muon dat", "bat dau tu dau", "dich", "nham", "di tu", "giai doan", "chang", "khoi dong");
        if (!asksForPath) return null;
        if (!toeic && !ielts && !cefr && !AiTextNormalizer.ContainsAny(normalized,
                "lo trinh", "ke hoach hoc tieng anh", "ke hoach tong quat", "study plan", "learning path", "roadmap", "cai thien trinh do")) return null;
        if (AiTextNormalizer.ContainsAny(normalized, "bao lau", "may thang", "bao nhieu ngay", "cam ket", "chac chan")) return "roadmap-time-expectations";
        if ((toeic ? 1 : 0) + (ielts ? 1 : 0) + (cefr ? 1 : 0) > 1) return "roadmap-overview";
        if (toeic) return AiTextNormalizer.ContainsAny(normalized, "450") ? "roadmap-toeic-450"
            : AiTextNormalizer.ContainsAny(normalized, "650") ? "roadmap-toeic-650" : "roadmap-overview";
        if (ielts) return AiTextNormalizer.ContainsAny(normalized, "4 5", "4 5 ielts") ? "roadmap-ielts-4-5"
            : AiTextNormalizer.ContainsAny(normalized, "6 5") ? "roadmap-ielts-6-5" : "roadmap-overview";
        if (cefr)
        {
            if (AiTextNormalizer.ContainsAny(normalized, "a1") && AiTextNormalizer.ContainsAny(normalized, "a2") && !AiTextNormalizer.ContainsAny(normalized, "b1", "b2", "c1", "c2")) return "roadmap-a1-a2";
            if (AiTextNormalizer.ContainsAny(normalized, "a2") && AiTextNormalizer.ContainsAny(normalized, "b1") && !AiTextNormalizer.ContainsAny(normalized, "a1", "b2", "c1", "c2")) return "roadmap-a2-b1";
            if (AiTextNormalizer.ContainsAny(normalized, "b1") && AiTextNormalizer.ContainsAny(normalized, "b2") && !AiTextNormalizer.ContainsAny(normalized, "a1", "a2", "c1", "c2")) return "roadmap-b1-b2";
            if (AiTextNormalizer.ContainsAny(normalized, "b2") && AiTextNormalizer.ContainsAny(normalized, "c1") && !AiTextNormalizer.ContainsAny(normalized, "a1", "a2", "b1", "c2")) return "roadmap-b2-c1";
            return "roadmap-cefr";
        }
        return "roadmap-overview";
    }

    private static int ScoreGuide(WebsiteGuideEntry guide, string normalizedMessage, HashSet<string> queryTokens)
    {
        if (guide.Questions.Any(question => AiTextNormalizer.Normalize(question) == normalizedMessage))
        {
            return 1_000;
        }

        var score = 0;
        var normalizedTitle = AiTextNormalizer.Normalize(guide.Title);
        if (normalizedMessage.Contains(normalizedTitle, StringComparison.Ordinal))
        {
            score += 8;
        }

        score += ScoreKeywordMatches(guide.Keywords, normalizedMessage);

        // Full question variants help natural phrasing without adding their
        // generic words ("where", "how", "can") to every guide's token score.
        score += ScoreKeywordMatches(guide.Questions, normalizedMessage);

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

    private Task<IReadOnlyList<WebsiteGuideEntry>> GetGuidesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_catalogGate)
        {
            if (_catalog is null || _catalog.IsFaulted || _catalog.IsCanceled)
                _catalog = ReadGuidesAsync();
            return _catalog.WaitAsync(ct);
        }
    }

    private async Task<IReadOnlyList<WebsiteGuideEntry>> ReadGuidesAsync()
        => (await WebsiteGuideCatalog.ReadAsync(_guideFilePath, CancellationToken.None))
            .Select(item => item.Guide)
            .Where(guide => guide.Id is not ("study-read" or "ai-chatbox-usage"))
            .ToList();
}
