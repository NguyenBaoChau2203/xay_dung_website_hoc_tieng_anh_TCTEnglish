using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TCTEnglish.Services.AI.Internal;
using TCTEnglish.Services.AI.Internal.Retrievers;
using Xunit;
using Xunit.Abstractions;

namespace TCTEnglish.Tests;

/// <summary>
/// Validates the frozen synthetic Hybrid RAG fixture and records the current
/// non-paid classifier/guide retriever behavior. It does not score answer quality.
/// </summary>
public sealed class HybridRagEvaluationBaselineTests
{
    private const string CanonicalFixtureSha256 = "1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f";
    private const string RevisionOneRawSha256 = "0abf1e31fc781c0ba7b87d5916cda23e69564033b19efdebfdfd9e7ef2bb4116";

    private static readonly string[] Categories =
    [
        "navigation",
        "lesson_card",
        "personal_facts",
        "outside_scope",
        "injection_access",
        "ambiguous_mixed",
        "stale_error_cancel"
    ];

    private static readonly Dictionary<string, (int Development, int HeldOut)> ExpectedCounts = new()
    {
        ["navigation"] = (17, 8),
        ["lesson_card"] = (17, 8),
        ["personal_facts"] = (13, 7),
        ["outside_scope"] = (10, 5),
        ["injection_access"] = (10, 5),
        ["ambiguous_mixed"] = (6, 4),
        ["stale_error_cancel"] = (7, 3)
    };

    private static readonly HashSet<string> Outcomes =
    [
        "answered",
        "needs_clarification",
        "insufficient_evidence",
        "out_of_scope",
        "access_denied",
        "cancelled",
        "provider_unavailable",
        "quota_exceeded"
    ];

    private readonly ITestOutputHelper _output;

    public HybridRagEvaluationBaselineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Fixture_IsFrozenAndInternallyConsistent()
    {
        var path = GetFixturePath();
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(CanonicalFixtureSha256, HybridRagFixtureValidator.CanonicalSha256(bytes));

        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        var errors = HybridRagFixtureValidator.Validate(root);
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
        Assert.Equal("hybrid-rag-eval-1", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(2, root.GetProperty("fixtureRevision").GetInt32());
        Assert.Equal("2026-11-06", root.GetProperty("frozenOn").GetString());
        Assert.Equal(RevisionOneRawSha256, root.GetProperty("previousRevision").GetProperty("rawSha256").GetString());
        Assert.NotEqual(CanonicalFixtureSha256, RevisionOneRawSha256);

        var actors = Index(root.GetProperty("actors"));
        var sources = Index(root.GetProperty("sources"));
        Assert.Equal(3, actors.Count);
        Assert.Equal(51, sources.Count);
        Assert.Contains("src-card-zephyr-window", sources.Keys);
        Assert.Equal(501, sources["src-card-zephyr-window"].GetProperty("index").GetInt32());
        Assert.True(sources["src-set-lan-large"].GetProperty("cardCount").GetInt32() > 500);

        var revoked = sources["src-class-revoked"].GetProperty("members").EnumerateArray().Single();
        Assert.Equal("revoked", revoked.GetProperty("status").GetString());

        var cases = root.GetProperty("cases").EnumerateArray().ToList();
        Assert.Equal(120, cases.Count);
        Assert.Equal(80, cases.Count(item => item.GetProperty("split").GetString() == "development"));
        Assert.Equal(40, cases.Count(item => item.GetProperty("split").GetString() == "held_out"));

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var questions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in cases)
        {
            var id = Required(item, "id");
            var question = Required(item, "question");
            Assert.True(ids.Add(id), $"Duplicate case id {id}.");
            Assert.True(questions.Add(question), $"Duplicate question on {id}.");
            Assert.Contains(item.GetProperty("category").GetString(), Categories);
            Assert.Contains(item.GetProperty("expectedOutcome").GetString(), Outcomes);
            var wording = Required(item, "wordingConstraint");
            Assert.Contains(wording, new[] { "required-facts-only", "verbatim-facts-plus-derived-values" });
        }

        var byId = cases.ToDictionary(item => Required(item, "id"), StringComparer.Ordinal);
        Assert.Equal("Quiz mode opens at /Home/QuizMode/{setId}.", byId["HR-NAV-D14"].GetProperty("requiredFacts")[0].GetString());
        Assert.Equal("src-guide-quiz", byId["HR-NAV-D14"].GetProperty("requiredSourceIds")[0].GetString());
        Assert.Contains("pair each term with its definition", byId["HR-NAV-D15"].GetProperty("requiredFacts")[0].GetString(), StringComparison.Ordinal);
        Assert.Equal("src-guide-matching", byId["HR-NAV-D15"].GetProperty("requiredSourceIds")[0].GetString());
        Assert.DoesNotContain("folder", byId["HR-NAV-D14"].GetProperty("requiredFacts")[0].GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("folder", byId["HR-NAV-D15"].GetProperty("requiredFacts")[0].GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("insufficient_evidence", byId["HR-NAV-D09"].GetProperty("expectedOutcome").GetString());
        Assert.Empty(byId["HR-NAV-D09"].GetProperty("requiredFacts").EnumerateArray());
        Assert.Contains("/Home/Grammar", byId["HR-NAV-D07"].GetProperty("requiredFacts")[0].GetString(), StringComparison.Ordinal);
        Assert.Contains(byId["HR-LES-D09"].GetProperty("requiredFacts").EnumerateArray(), fact => fact.GetString() == "harbor means a sheltered port.");
        Assert.Equal(6, byId["HR-PER-H03"].GetProperty("expectedValues")[0].GetProperty("value").GetInt32());
        Assert.Equal(10, byId["HR-PER-H03"].GetProperty("expectedValues")[0].GetProperty("operands").GetProperty("targetCards").GetInt32());
        Assert.Equal(4, byId["HR-PER-H03"].GetProperty("expectedValues")[0].GetProperty("operands").GetProperty("reviewedCards").GetInt32());
        Assert.Equal("weather", byId["HR-MIX-H02"].GetProperty("unansweredParts")[0].GetProperty("facet").GetString());
        Assert.Equal(6, byId["HR-MIX-H02"].GetProperty("expectedValues")[0].GetProperty("value").GetInt32());
        Assert.Equal("deleted", byId["HR-OPS-D04"].GetProperty("scenario").GetProperty("current").GetProperty("state").GetString());
        Assert.Equal(15, byId["HR-OPS-D05"].GetProperty("scenario").GetProperty("questionLimit").GetInt32());
        Assert.Equal(15, byId["HR-OPS-D05"].GetProperty("scenario").GetProperty("questionsUsed").GetInt32());
        Assert.True(byId["HR-OPS-D05"].GetProperty("scenario").GetProperty("tokensUsed").GetInt32() < byId["HR-OPS-D05"].GetProperty("scenario").GetProperty("tokenBudget").GetInt32());
        Assert.NotEqual(
            byId["HR-OPS-D06"].GetProperty("scenario").GetProperty("indexedContentHash").GetString(),
            byId["HR-OPS-D06"].GetProperty("scenario").GetProperty("currentContentHash").GetString());
        Assert.Equal(503, byId["HR-OPS-H03"].GetProperty("scenario").GetProperty("statusCode").GetInt32());
        Assert.Equal("stop-stream", byId["HR-OPS-D01"].GetProperty("scenario").GetProperty("action").GetString());
        Assert.False(string.IsNullOrWhiteSpace(byId["HR-OPS-D01"].GetProperty("scenario").GetProperty("streamId").GetString()));
        Assert.Equal("revoked", byId["HR-SEC-D01"].GetProperty("scenario").GetProperty("currentMembership").GetString());

        foreach (var category in Categories)
        {
            var (development, heldOut) = ExpectedCounts[category];
            Assert.Equal(development, cases.Count(item => item.GetProperty("category").GetString() == category && item.GetProperty("split").GetString() == "development"));
            Assert.Equal(heldOut, cases.Count(item => item.GetProperty("category").GetString() == category && item.GetProperty("split").GetString() == "held_out"));
        }

        Assert.Contains(cases, item => item.GetProperty("id").GetString() == "HR-NAV-D04" && item.GetProperty("question").GetString() == "How do I change my password?");
        Assert.Contains(cases, item => item.GetProperty("riskTags").EnumerateArray().Any(tag => tag.GetString() == "card-window-500"));
        Assert.Contains(cases, item => item.GetProperty("riskTags").EnumerateArray().Any(tag => tag.GetString() == "revoked-access"));
        Assert.Contains(cases, item => item.GetProperty("riskTags").EnumerateArray().Any(tag => tag.GetString() == "malicious-source"));
        Assert.Contains(cases, item => item.GetProperty("riskTags").EnumerateArray().Any(tag => tag.GetString() == "accent"));
        Assert.Contains(cases, item => item.GetProperty("riskTags").EnumerateArray().Any(tag => tag.GetString() == "no-accent"));
        Assert.Contains(cases, item => item.GetProperty("riskTags").EnumerateArray().Any(tag => tag.GetString() == "english"));
    }

    [Fact]
    public async Task Baseline_ObservesCurrentClassifierAndGuideRetrieverWithoutScoringQuality()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(GetFixturePath()));
        var classifier = new DeterministicIntentClassifier();
        var retriever = new WebsiteGuideRetriever(GetGuideFilePath());
        var intentCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var guideHits = 0;

        foreach (var item in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var question = Required(item, "question");
            var classification = classifier.Classify(question);
            Assert.True(Enum.IsDefined(classification.Intent));
            Assert.InRange(classification.Confidence, 0f, 1f);

            var intentName = classification.Intent.ToString();
            intentCounts[intentName] = intentCounts.GetValueOrDefault(intentName) + 1;

            var snippets = await retriever.RetrieveAsync(0, question, CancellationToken.None);
            Assert.True(snippets.Count is 0 or 1);
            if (snippets.Count == 1)
            {
                guideHits++;
                Assert.Equal(KnowledgeSnippetSources.WebsiteGuide, snippets[0].Source);
                Assert.False(string.IsNullOrWhiteSpace(snippets[0].Title));
            }
        }

        var summary = new StringBuilder();
        summary.Append("baseline-observation cases=120 guideHits=").Append(guideHits).Append(" intents=");
        summary.Append(string.Join(",", intentCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}:{pair.Value}")));
        _output.WriteLine(summary.ToString());
        Console.WriteLine(summary.ToString());

        Assert.Equal(120, intentCounts.Values.Sum());
        Assert.InRange(guideHits, 0, 120);
    }

    [Fact]
    public void CanonicalHash_TreatsLfAndCrlfAsTheSameFreeze()
    {
        var bytes = File.ReadAllBytes(GetFixturePath());
        var lf = Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        var crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);
        var lfBytes = Encoding.UTF8.GetBytes(lf);
        var crlfBytes = Encoding.UTF8.GetBytes(crlf);

        Assert.Equal(CanonicalFixtureSha256, HybridRagFixtureValidator.CanonicalSha256(lfBytes));
        Assert.Equal(CanonicalFixtureSha256, HybridRagFixtureValidator.CanonicalSha256(crlfBytes));
        Assert.NotEqual(
            CanonicalFixtureSha256,
            Convert.ToHexString(SHA256.HashData(crlfBytes)).ToLowerInvariant());
    }

    [Theory]
    [InlineData("fact")]
    [InlineData("id")]
    [InlineData("outcome")]
    [InlineData("split")]
    public void CanonicalHash_RejectsChangedFactIdOutcomeOrSplit(string kind)
    {
        var text = Encoding.UTF8.GetString(File.ReadAllBytes(GetFixturePath()));
        var mutated = MutateNavigationCase(text, kind);
        Assert.NotEqual(text, mutated);
        Assert.NotEqual(
            CanonicalFixtureSha256,
            HybridRagFixtureValidator.CanonicalSha256(Encoding.UTF8.GetBytes(mutated)));
    }

    [Theory]
    [InlineData("missing-scenario", "missing scenario")]
    [InlineData("dangling-source", "dangling source")]
    [InlineData("dangling-actor", "dangling actor")]
    [InlineData("negative-quota", "negative quota")]
    [InlineData("equal-stale-hash", "stale hash matches current")]
    [InlineData("tombstone-still-required", "tombstone source is still required")]
    [InlineData("http-503-without-status", "http-503 requires status 503")]
    [InlineData("stop-without-stream", "stop-stream requires streamId")]
    public void Validator_RejectsMalformedScenarioClones(string defect, string token)
    {
        var root = JsonNode.Parse(File.ReadAllText(GetFixturePath()))!.AsObject();
        switch (defect)
        {
            case "missing-scenario":
                FindCase(root, "HR-NAV-D01").Remove("scenario");
                break;
            case "dangling-source":
                FindCase(root, "HR-NAV-D01")["sourceIds"]!.AsArray().Add("src-missing");
                break;
            case "dangling-actor":
                FindCase(root, "HR-NAV-D01")["actorId"] = "actor-missing";
                break;
            case "negative-quota":
                FindCase(root, "HR-OPS-D05")["scenario"]!["questionsUsed"] = -1;
                break;
            case "equal-stale-hash":
                var stale = FindCase(root, "HR-OPS-D06")["scenario"]!.AsObject();
                stale["indexedContentHash"] = stale["currentContentHash"]!.GetValue<string>();
                break;
            case "tombstone-still-required":
                FindCase(root, "HR-OPS-D04")["requiredSourceIds"]!.AsArray().Add("src-card-zephyr-window");
                break;
            case "http-503-without-status":
                FindCase(root, "HR-OPS-H03")["scenario"]!.AsObject().Remove("statusCode");
                break;
            case "stop-without-stream":
                FindCase(root, "HR-OPS-D01")["scenario"]!.AsObject().Remove("streamId");
                break;
        }

        using var document = JsonDocument.Parse(root.ToJsonString());
        var errors = string.Join(Environment.NewLine, HybridRagFixtureValidator.Validate(document.RootElement));
        Assert.Contains(token, errors, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("quota-not-exhausted", "HR-OPS-D05 quota setup does not reach a limit")]
    [InlineData("provider-fault-replaced-with-none", "HR-OPS-H03 missing provider fault")]
    [InlineData("cancel-replaced-with-none", "HR-OPS-D01 missing cancel action")]
    [InlineData("tombstone-wrong-declared-source", "HR-OPS-D04 scenario source is outside the declared scope")]
    public void Validator_RejectsOutcomeAndSourceScopeMismatches(string defect, string token)
    {
        var root = JsonNode.Parse(File.ReadAllText(GetFixturePath()))!.AsObject();
        switch (defect)
        {
            case "quota-not-exhausted":
                var quota = FindCase(root, "HR-OPS-D05")["scenario"]!.AsObject();
                quota["questionsUsed"] = 0;
                quota["tokensUsed"] = 0;
                break;
            case "provider-fault-replaced-with-none":
                FindCase(root, "HR-OPS-H03")["scenario"] = new JsonObject { ["type"] = "none" };
                break;
            case "cancel-replaced-with-none":
                FindCase(root, "HR-OPS-D01")["scenario"] = new JsonObject { ["type"] = "none" };
                break;
            case "tombstone-wrong-declared-source":
                var tombstone = FindCase(root, "HR-OPS-D04")["scenario"]!.AsObject();
                var invoiceText = SourceText(root, "src-card-invoice");
                tombstone["sourceId"] = "src-card-invoice";
                tombstone["previous"]!["contentHash"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(invoiceText))).ToLowerInvariant();
                break;
        }

        using var document = JsonDocument.Parse(root.ToJsonString());
        var errors = string.Join(Environment.NewLine, HybridRagFixtureValidator.Validate(document.RootElement));
        Assert.Contains(token, errors, StringComparison.Ordinal);
    }

    private static string MutateNavigationCase(string text, string kind)
    {
        const string marker = "\"id\": \"HR-NAV-D14\"";
        var idAt = text.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(idAt >= 0);
        if (kind == "id")
        {
            return text.Replace(marker, "\"id\": \"HR-NAV-X14\"", StringComparison.Ordinal);
        }

        if (kind == "fact")
        {
            return text.Replace(
                "Quiz mode opens at /Home/QuizMode/{setId}.",
                "Quiz mode opens at /Home/QuizMode/{setId}. MUTATED",
                StringComparison.Ordinal);
        }

        var key = kind == "outcome" ? "\"expectedOutcome\"" : "\"split\"";
        var keyAt = kind == "outcome"
            ? text.LastIndexOf(key, idAt, StringComparison.Ordinal)
            : text.IndexOf(key, idAt, StringComparison.Ordinal);
        var valueStart = text.IndexOf('"', keyAt + key.Length);
        var valueEnd = text.IndexOf('"', valueStart + 1);
        var replacement = kind == "outcome" ? "refused" : "held_out";
        return string.Concat(text.AsSpan(0, valueStart + 1), replacement, text.AsSpan(valueEnd));
    }

    private static JsonObject FindCase(JsonObject root, string id)
    {
        foreach (var item in root["cases"]!.AsArray())
        {
            if (item!["id"]!.GetValue<string>() == id)
            {
                return item.AsObject();
            }
        }

        throw new InvalidOperationException(id);
    }

    private static string SourceText(JsonObject root, string id)
    {
        foreach (var item in root["sources"]!.AsArray())
        {
            if (item!["id"]!.GetValue<string>() == id)
            {
                return item["canonicalText"]!.GetValue<string>();
            }
        }

        throw new InvalidOperationException(id);
    }

    private static Dictionary<string, JsonElement> Index(JsonElement array)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var item in array.EnumerateArray())
        {
            result.Add(Required(item, "id"), item);
        }

        return result;
    }

    private static string Required(JsonElement item, string name)
        => item.GetProperty(name).GetString() ?? throw new InvalidOperationException($"Missing {name}.");

    private static string GetFixturePath()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "AI", "hybrid-rag-evaluation.json"));

    private static string GetGuideFilePath()
        => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "TCTEnglish", "wwwroot", "data", "ai", "website-guides.json"));
}
