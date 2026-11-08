using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TCTEnglish.Tests;

/// <summary>
/// Checks the synthetic Hybrid RAG fixture structure. It does not score answer quality.
/// </summary>
public static class HybridRagFixtureValidator
{
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

    private static readonly Dictionary<string, (int Development, int HeldOut)> ExpectedCounts = new(StringComparer.Ordinal)
    {
        ["navigation"] = (17, 8),
        ["lesson_card"] = (17, 8),
        ["personal_facts"] = (13, 7),
        ["outside_scope"] = (10, 5),
        ["injection_access"] = (10, 5),
        ["ambiguous_mixed"] = (6, 4),
        ["stale_error_cancel"] = (7, 3)
    };

    private static readonly HashSet<string> Outcomes = new(StringComparer.Ordinal)
    {
        "answered",
        "needs_clarification",
        "insufficient_evidence",
        "out_of_scope",
        "access_denied",
        "cancelled",
        "provider_unavailable",
        "quota_exceeded"
    };

    public static string CanonicalSha256(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var canonical = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static IReadOnlyList<string> Validate(JsonElement root)
    {
        var errors = new List<string>();
        if (root.GetProperty("schemaVersion").GetString() != "hybrid-rag-eval-1")
        {
            errors.Add("schemaVersion");
        }

        if (root.GetProperty("fixtureRevision").GetInt32() != 2)
        {
            errors.Add("fixtureRevision");
        }

        var actors = Index(root.GetProperty("actors"), errors, "actor");
        var sources = Index(root.GetProperty("sources"), errors, "source");
        var cases = root.GetProperty("cases").EnumerateArray().ToList();
        if (cases.Count != 120)
        {
            errors.Add("case count");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var questions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in cases)
        {
            ValidateCase(item, actors, sources, ids, questions, errors);
        }

        if (cases.Count(item => item.GetProperty("split").GetString() == "development") != 80
            || cases.Count(item => item.GetProperty("split").GetString() == "held_out") != 40)
        {
            errors.Add("80/40 split");
        }

        foreach (var category in Categories)
        {
            var (development, heldOut) = ExpectedCounts[category];
            var actualDevelopment = cases.Count(item => item.GetProperty("category").GetString() == category && item.GetProperty("split").GetString() == "development");
            var actualHeldOut = cases.Count(item => item.GetProperty("category").GetString() == category && item.GetProperty("split").GetString() == "held_out");
            if (actualDevelopment != development || actualHeldOut != heldOut)
            {
                errors.Add($"split {category}");
            }
        }

        return errors;
    }

    private static void ValidateCase(
        JsonElement item,
        Dictionary<string, JsonElement> actors,
        Dictionary<string, JsonElement> sources,
        HashSet<string> ids,
        HashSet<string> questions,
        List<string> errors)
    {
        var id = Text(item, "id");
        if (!ids.Add(id))
        {
            errors.Add($"duplicate id {id}");
        }

        if (!questions.Add(Text(item, "question")))
        {
            errors.Add($"{id} duplicate question");
        }

        if (!Categories.Contains(Text(item, "category"), StringComparer.Ordinal))
        {
            errors.Add($"{id} category");
        }

        var outcome = Text(item, "expectedOutcome");
        if (!Outcomes.Contains(outcome))
        {
            errors.Add($"{id} outcome");
        }

        var actorId = Text(item, "actorId");
        if (!actors.TryGetValue(actorId, out var actor))
        {
            errors.Add($"{id} dangling actor {actorId}");
        }
        else if (!(actor.GetProperty("email").GetString() ?? string.Empty).EndsWith("@example.test", StringComparison.Ordinal))
        {
            errors.Add($"{id} actor email");
        }

        var sourceIds = Texts(item, "sourceIds");
        var requiredSourceIds = Texts(item, "requiredSourceIds");
        foreach (var sourceId in sourceIds)
        {
            if (!sources.ContainsKey(sourceId))
            {
                errors.Add($"{id} dangling source {sourceId}");
            }
        }

        foreach (var sourceId in requiredSourceIds)
        {
            if (!sourceIds.Contains(sourceId, StringComparer.Ordinal))
            {
                errors.Add($"{id} required source missing from sourceIds");
            }
        }

        var facts = Texts(item, "requiredFacts");
        var scope = Text(item, "answerScope");
        var unanswered = item.TryGetProperty("unansweredParts", out var unansweredElement)
            ? unansweredElement.EnumerateArray().ToList()
            : [];
        if (scope == "partial")
        {
            if (outcome != "answered" || unanswered.Count == 0)
            {
                errors.Add($"{id} partial answer missing unanswered facet");
            }
        }
        else if (scope == "full")
        {
            if (outcome != "answered" || unanswered.Count != 0)
            {
                errors.Add($"{id} full answer leaves a facet unanswered");
            }
        }
        else if (scope == "none")
        {
            if (outcome == "answered" || facts.Count != 0 || requiredSourceIds.Count != 0 || unanswered.Count != 0)
            {
                errors.Add($"{id} none scope");
            }
        }
        else
        {
            errors.Add($"{id} answerScope");
        }

        foreach (var part in unanswered)
        {
            if (string.IsNullOrWhiteSpace(Text(part, "facet")))
            {
                errors.Add($"{id} unanswered facet");
            }

            var partOutcome = Text(part, "outcome");
            if (partOutcome == "answered" || !Outcomes.Contains(partOutcome))
            {
                errors.Add($"{id} unanswered outcome");
            }
        }

        if (outcome == "answered")
        {
            if (facts.Count == 0 || requiredSourceIds.Count == 0)
            {
                errors.Add($"{id} answered without facts");
            }

            var corpus = string.Join("\n", requiredSourceIds.Where(sources.ContainsKey).Select(sourceId => sources[sourceId].GetProperty("canonicalText").GetString()));
            foreach (var fact in facts)
            {
                if (!corpus.Contains(fact, StringComparison.Ordinal))
                {
                    errors.Add($"{id} fact is not in the required source");
                }
            }

            foreach (var sourceId in requiredSourceIds.Where(sources.ContainsKey))
            {
                if (!sources[sourceId].TryGetProperty("untrustedSentences", out var untrusted))
                {
                    continue;
                }

                foreach (var sentence in untrusted.EnumerateArray())
                {
                    var value = sentence.GetString() ?? string.Empty;
                    if (facts.Any(fact => fact.Contains(value, StringComparison.Ordinal)))
                    {
                        errors.Add($"{id} required fact copies an untrusted sentence");
                    }
                }
            }

            ValidateExpectedValues(item, id, corpus, errors);
        }
        else if (item.TryGetProperty("expectedValues", out _))
        {
            errors.Add($"{id} expected value on a non-answer");
        }

        var wording = Text(item, "wordingConstraint");
        var hasDerived = item.TryGetProperty("expectedValues", out var expectedValues) && expectedValues.GetArrayLength() > 0;
        if (hasDerived && wording != "verbatim-facts-plus-derived-values")
        {
            errors.Add($"{id} wordingConstraint");
        }
        else if (!hasDerived && wording != "required-facts-only")
        {
            errors.Add($"{id} wordingConstraint");
        }

        if (!item.TryGetProperty("scenario", out var scenario) || scenario.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{id} missing scenario");
            return;
        }

        ValidateScenario(id, actorId, outcome, sourceIds, scenario, sources, actors, requiredSourceIds, errors);
    }

    private static void ValidateExpectedValues(JsonElement item, string id, string corpus, List<string> errors)
    {
        if (!item.TryGetProperty("expectedValues", out var values))
        {
            return;
        }

        foreach (var value in values.EnumerateArray())
        {
            if (Text(value, "kind") != "derived" || Text(value, "derivation") != "targetCards - reviewedCards")
            {
                errors.Add($"{id} unsupported derivation");
                continue;
            }

            var operands = value.GetProperty("operands");
            var target = operands.GetProperty("targetCards").GetInt32();
            var reviewed = operands.GetProperty("reviewedCards").GetInt32();
            if (value.GetProperty("value").GetInt32() != target - reviewed)
            {
                errors.Add($"{id} derived value does not match operands");
            }

            if (string.IsNullOrWhiteSpace(Text(value, "unit")) || string.IsNullOrWhiteSpace(Text(value, "id")))
            {
                errors.Add($"{id} derived value missing unit");
            }

            if (!ContainsWholeNumber(corpus, target) || !ContainsWholeNumber(corpus, reviewed))
            {
                errors.Add($"{id} derived operand is not in the source");
            }
        }
    }

    private static void ValidateScenario(
        string id,
        string caseActorId,
        string outcome,
        List<string> declaredSourceIds,
        JsonElement scenario,
        Dictionary<string, JsonElement> sources,
        Dictionary<string, JsonElement> actors,
        List<string> requiredSourceIds,
        List<string> errors)
    {
        var type = Text(scenario, "type");
        switch (type)
        {
            case "none":
                RequireFields(scenario, id, ["type"], [], errors);
                break;
            case "user-action":
                RequireFields(scenario, id, ["type", "action", "streamId"], [], errors);
                if (Text(scenario, "action") != "stop-stream" || string.IsNullOrWhiteSpace(Text(scenario, "streamId")))
                {
                    errors.Add($"{id} stop-stream requires streamId");
                }

                break;
            case "provider-fault":
                ValidateProviderFault(id, scenario, errors);
                break;
            case "quota":
                ValidateQuota(id, caseActorId, scenario, actors, errors);
                break;
            case "source-tombstone":
                ValidateTombstone(id, scenario, sources, requiredSourceIds, errors);
                break;
            case "stale-index":
                ValidateStaleIndex(id, scenario, sources, errors);
                break;
            case "access-transition":
                ValidateAccessTransition(id, caseActorId, scenario, sources, actors, errors);
                break;
            case "authorization":
                ValidateAuthorization(id, caseActorId, scenario, sources, actors, errors);
                break;
            default:
                errors.Add($"{id} unknown scenario");
                break;
        }

        // Compare the scenario already supplied by the case. Do not create fault,
        // quota, cancel, or source fields from the expected outcome, question, or id.
        CompareSuppliedSetupWithOutcome(id, outcome, scenario, errors);
        RequireScenarioSourceInDeclaredScope(id, scenario, declaredSourceIds, errors);
    }

    private static void CompareSuppliedSetupWithOutcome(string id, string outcome, JsonElement scenario, List<string> errors)
    {
        switch (outcome)
        {
            case "quota_exceeded" when !SuppliedQuotaReachesLimit(scenario):
                errors.Add($"{id} quota setup does not reach a limit");
                break;
            case "provider_unavailable" when !HasExplicitProviderFault(scenario):
                errors.Add(Text(scenario, "type") == "none"
                    ? $"{id} missing provider fault"
                    : $"{id} incompatible provider fault");
                break;
            case "cancelled" when !HasExplicitCancelAction(scenario):
                errors.Add(Text(scenario, "type") == "none"
                    ? $"{id} missing cancel action"
                    : $"{id} incompatible cancel action");
                break;
        }
    }

    private static bool SuppliedQuotaReachesLimit(JsonElement scenario)
    {
        return Text(scenario, "type") == "quota"
            && TryInt(scenario, "questionLimit", out var questionLimit)
            && TryInt(scenario, "questionsUsed", out var questionsUsed)
            && TryInt(scenario, "tokenBudget", out var tokenBudget)
            && TryInt(scenario, "tokensUsed", out var tokensUsed)
            && questionLimit > 0
            && tokenBudget > 0
            && (questionsUsed >= questionLimit || tokensUsed >= tokenBudget);
    }

    private static bool HasExplicitProviderFault(JsonElement scenario)
        => Text(scenario, "type") == "provider-fault"
            && Text(scenario, "fault") is "timeout" or "network" or "http-503";

    private static bool HasExplicitCancelAction(JsonElement scenario)
        => Text(scenario, "type") == "user-action"
            && Text(scenario, "action") == "stop-stream";

    private static void RequireScenarioSourceInDeclaredScope(string id, JsonElement scenario, List<string> declaredSourceIds, List<string> errors)
    {
        if (!scenario.TryGetProperty("sourceId", out var sourceId) || sourceId.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var value = sourceId.GetString() ?? string.Empty;
        if (!declaredSourceIds.Contains(value, StringComparer.Ordinal))
        {
            errors.Add($"{id} scenario source is outside the declared scope");
        }
    }

    private static bool TryInt(JsonElement scenario, string name, out int number)
    {
        number = 0;
        return scenario.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out number);
    }

    private static void ValidateProviderFault(string id, JsonElement scenario, List<string> errors)
    {
        var fault = Text(scenario, "fault");
        if (fault == "http-503")
        {
            RequireFields(scenario, id, ["type", "fault", "statusCode"], [], errors);
            if (!scenario.TryGetProperty("statusCode", out var status) || status.ValueKind != JsonValueKind.Number || status.GetInt32() != 503)
            {
                errors.Add($"{id} http-503 requires status 503");
            }
        }
        else if (fault is "timeout" or "network")
        {
            RequireFields(scenario, id, ["type", "fault"], [], errors);
        }
        else
        {
            errors.Add($"{id} unknown provider fault");
        }
    }

    private static void ValidateQuota(
        string id,
        string caseActorId,
        JsonElement scenario,
        Dictionary<string, JsonElement> actors,
        List<string> errors)
    {
        RequireFields(scenario, id, ["type", "actorId", "dayUtc", "questionLimit", "questionsUsed", "tokenBudget", "tokensUsed"], [], errors);
        var actorId = Text(scenario, "actorId");
        if (actorId != caseActorId || !actors.ContainsKey(actorId))
        {
            errors.Add($"{id} dangling actor {actorId}");
        }

        if (!Regex.IsMatch(Text(scenario, "dayUtc"), @"^\d{4}-\d{2}-\d{2}$"))
        {
            errors.Add($"{id} quota day");
        }

        RequireNonNegative(id, scenario, "questionsUsed", errors);
        RequireNonNegative(id, scenario, "tokensUsed", errors);
        RequirePositive(id, scenario, "questionLimit", errors);
        RequirePositive(id, scenario, "tokenBudget", errors);
    }

    private static void ValidateTombstone(
        string id,
        JsonElement scenario,
        Dictionary<string, JsonElement> sources,
        List<string> requiredSourceIds,
        List<string> errors)
    {
        RequireFields(scenario, id, ["type", "sourceId", "previous", "current"], [], errors);
        var sourceId = Text(scenario, "sourceId");
        if (!sources.TryGetValue(sourceId, out var source))
        {
            errors.Add($"{id} dangling source {sourceId}");
            return;
        }

        if (requiredSourceIds.Contains(sourceId, StringComparer.Ordinal))
        {
            errors.Add($"{id} tombstone source is still required");
        }

        var previous = scenario.GetProperty("previous");
        var current = scenario.GetProperty("current");
        var previousHash = Text(previous, "contentHash");
        if (!IsSha256(previousHash) || !string.Equals(previousHash, Sha256(source.GetProperty("canonicalText").GetString() ?? string.Empty), StringComparison.Ordinal))
        {
            errors.Add($"{id} tombstone previous hash");
        }

        if (current.GetProperty("contentHash").ValueKind != JsonValueKind.Null)
        {
            errors.Add($"{id} tombstone current hash");
        }

        var state = Text(current, "state");
        if (state is not ("deleted" or "unpublished")
            || current.GetProperty("published").ValueKind != JsonValueKind.False)
        {
            errors.Add($"{id} tombstone state");
        }

        if (state == "unpublished")
        {
            if (!source.TryGetProperty("published", out var published) || published.GetBoolean() || previous.GetProperty("published").GetBoolean() != true)
            {
                errors.Add($"{id} tombstone current contradicts source");
            }
        }
        else if (source.TryGetProperty("published", out var published) && published.GetBoolean())
        {
            errors.Add($"{id} tombstone current contradicts source");
        }
    }

    private static void ValidateStaleIndex(string id, JsonElement scenario, Dictionary<string, JsonElement> sources, List<string> errors)
    {
        RequireFields(scenario, id, ["type", "sourceId", "indexVersion", "sourceVersion", "indexedContentHash", "currentContentHash"], [], errors);
        var sourceId = Text(scenario, "sourceId");
        if (!sources.TryGetValue(sourceId, out var source))
        {
            errors.Add($"{id} dangling source {sourceId}");
            return;
        }

        var indexed = Text(scenario, "indexedContentHash");
        var current = Text(scenario, "currentContentHash");
        if (!IsSha256(indexed) || !IsSha256(current))
        {
            errors.Add($"{id} stale hash");
        }

        if (string.Equals(indexed, current, StringComparison.Ordinal))
        {
            errors.Add($"{id} stale hash matches current");
        }

        if (!string.Equals(current, Sha256(source.GetProperty("canonicalText").GetString() ?? string.Empty), StringComparison.Ordinal))
        {
            errors.Add($"{id} stale current hash mismatch");
        }

        if (string.IsNullOrWhiteSpace(Text(scenario, "indexVersion"))
            || string.Equals(Text(scenario, "indexVersion"), Text(scenario, "sourceVersion"), StringComparison.Ordinal))
        {
            errors.Add($"{id} stale version");
        }
    }

    private static void ValidateAccessTransition(
        string id,
        string caseActorId,
        JsonElement scenario,
        Dictionary<string, JsonElement> sources,
        Dictionary<string, JsonElement> actors,
        List<string> errors)
    {
        RequireFields(scenario, id, ["type", "actorId", "sourceId", "previousMembership", "currentMembership"], [], errors);
        var actorId = Text(scenario, "actorId");
        var sourceId = Text(scenario, "sourceId");
        if (actorId != caseActorId || !actors.ContainsKey(actorId))
        {
            errors.Add($"{id} dangling actor {actorId}");
        }

        if (!sources.TryGetValue(sourceId, out var source))
        {
            errors.Add($"{id} dangling source {sourceId}");
            return;
        }

        var previous = Text(scenario, "previousMembership");
        var current = Text(scenario, "currentMembership");
        if (string.IsNullOrWhiteSpace(previous) || string.IsNullOrWhiteSpace(current) || previous == current)
        {
            errors.Add($"{id} access transition");
        }

        var matched = source.GetProperty("members").EnumerateArray().Any(member =>
            member.GetProperty("actorId").GetString() == actorId
            && member.GetProperty("status").GetString() == current);
        if (!matched)
        {
            errors.Add($"{id} access transition contradicts source");
        }
    }

    private static void ValidateAuthorization(
        string id,
        string caseActorId,
        JsonElement scenario,
        Dictionary<string, JsonElement> sources,
        Dictionary<string, JsonElement> actors,
        List<string> errors)
    {
        RequireFields(
            scenario,
            id,
            ["type", "actorId", "allowed", "reason"],
            ["sourceId", "targetActorId", "secretClass", "instructionClass", "metric"],
            errors);
        var actorId = Text(scenario, "actorId");
        if (actorId != caseActorId || !actors.ContainsKey(actorId))
        {
            errors.Add($"{id} dangling actor {actorId}");
        }

        if (scenario.TryGetProperty("allowed", out var allowed) && (allowed.ValueKind != JsonValueKind.False))
        {
            errors.Add($"{id} authorization allowed must be false");
        }

        var reason = Text(scenario, "reason");
        var sourceId = scenario.TryGetProperty("sourceId", out var sourceProperty) ? sourceProperty.GetString() : null;
        JsonElement source = default;
        var hasSource = sourceId is not null && sources.TryGetValue(sourceId, out source);
        if (sourceId is not null && !hasSource)
        {
            errors.Add($"{id} dangling source {sourceId}");
        }

        switch (reason)
        {
            case "not-owner":
                if (!hasSource
                    || !source.TryGetProperty("ownerActorId", out var owner)
                    || owner.GetString() == actorId)
                {
                    errors.Add($"{id} not-owner contradicts source");
                }

                break;
            case "unpublished":
                if (!hasSource || !source.TryGetProperty("published", out var published) || published.GetBoolean())
                {
                    errors.Add($"{id} unpublished contradicts source");
                }

                break;
            case "secret":
                if (string.IsNullOrWhiteSpace(Text(scenario, "secretClass")))
                {
                    errors.Add($"{id} secret class");
                }

                break;
            case "prompt-injection":
                if (string.IsNullOrWhiteSpace(Text(scenario, "instructionClass")))
                {
                    errors.Add($"{id} instruction class");
                }

                break;
            case "other-user-metric":
                var target = Text(scenario, "targetActorId");
                if (!actors.ContainsKey(target) || target == actorId || string.IsNullOrWhiteSpace(Text(scenario, "metric")))
                {
                    errors.Add($"{id} dangling actor {target}");
                }

                break;
            case "admin-route":
            case "privilege-escalation":
                break;
            default:
                errors.Add($"{id} authorization reason");
                break;
        }

        if (scenario.TryGetProperty("targetActorId", out var targetProperty))
        {
            var target = targetProperty.GetString() ?? string.Empty;
            if (!actors.ContainsKey(target) || target == actorId)
            {
                errors.Add($"{id} dangling actor {target}");
            }
        }
    }

    private static void RequireNonNegative(string id, JsonElement scenario, string name, List<string> errors)
    {
        if (!scenario.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || value.GetInt32() < 0)
        {
            errors.Add($"{id} negative quota");
        }
    }

    private static void RequirePositive(string id, JsonElement scenario, string name, List<string> errors)
    {
        if (!scenario.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || value.GetInt32() <= 0)
        {
            errors.Add($"{id} quota limit must be positive");
        }
    }

    private static void RequireFields(JsonElement element, string id, string[] required, string[] optional, List<string> errors)
    {
        var names = element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var name in required)
        {
            if (!names.Contains(name))
            {
                errors.Add($"{id} missing {name}");
            }
        }

        var allowed = new HashSet<string>(required, StringComparer.Ordinal);
        allowed.UnionWith(optional);
        foreach (var name in names)
        {
            if (!allowed.Contains(name))
            {
                errors.Add($"{id} unexpected scenario field {name}");
            }
        }
    }

    private static Dictionary<string, JsonElement> Index(JsonElement array, List<string> errors, string label)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var item in array.EnumerateArray())
        {
            var id = Text(item, "id");
            if (!result.TryAdd(id, item))
            {
                errors.Add($"duplicate {label} {id}");
            }
        }

        return result;
    }

    private static string Text(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static List<string> Texts(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(element => element.GetString() ?? string.Empty).ToList()
            : [];

    private static bool ContainsWholeNumber(string corpus, int number)
        => Regex.IsMatch(corpus, $@"(?<!\d){number}(?!\d)");

    private static bool IsSha256(string value)
        => Regex.IsMatch(value, "^[0-9a-f]{64}$");

    private static string Sha256(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
