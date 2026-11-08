using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TCTEnglish.Services.AI.Rag.Corpus;

namespace TCTEnglish.Services.AI.Rag;

public sealed record GroundedSentence(
    string SentenceId,
    string SourceKey,
    string SourceType,
    string Text,
    string? Route,
    string? EntityKey)
{
    public string? SentenceHash { get; init; }

    public string? DocumentHash { get; init; }

    public string? Visibility { get; init; }

    public string? CorpusVersion { get; init; }

    public string? ChunkerVersion { get; init; }

    public string? SourceVersion { get; init; }

    public string? SnapshotId { get; init; }
}

public sealed record GroundedHistoryTurn(string Role, string Content);

public enum GroundedSelectorStatus
{
    NotRequired,
    Completed,
    Unavailable,
    Invalid
}

public sealed record GroundedComposeRequest(
    string Message,
    IReadOnlyList<GroundedHistoryTurn> History,
    IReadOnlyList<GroundedSentence> Sentences,
    string? ModelJson,
    GroundedSelectorStatus SelectorStatus,
    bool RecheckRejected);

public sealed record GroundedComposition(
    HybridRagOutcome Outcome,
    string? Scope,
    string Text,
    IReadOnlyList<GroundedSentence> Sentences,
    bool PersistReply);

/// <summary>
/// Copies supplied source sentences. Model JSON may only name those ids.
/// Assistant history and model prose are not evidence.
/// </summary>
public static class GroundedAnswerComposer
{
    public const string GreetingText =
        "Chào bạn. Mình có thể giúp các câu hỏi về bài học và dữ liệu bạn được xem trên TCT English.";

    public const string OutOfScopeText =
        "Mình chỉ hỗ trợ dữ liệu nội bộ của TCT English. Bạn vui lòng hỏi về tính năng hoặc dữ liệu trong hệ thống.";

    public const string UnavailableText =
        "Dịch vụ soạn câu tạm thời không phản hồi. Mình không bịa nội dung thay cho nguồn.";

    public const string RejectedSelectionText =
        "Mình không chấp nhận câu chọn không nằm trong nguồn đã cung cấp, nên không trả lời phần đó.";

    public const string ClarificationText =
        "Mình thấy nhiều nguồn cùng khớp. Bạn hãy nêu rõ nguồn hoặc câu muốn hỏi.";

    public const string UnansweredPrefix = "Mình chưa trả lời các phần không có nguồn: ";

    public static bool ShouldRetrieve(string? message, IReadOnlyList<GroundedHistoryTurn> history)
    {
        if (IsGreeting(message))
        {
            return false;
        }

        if (LexicalAnswerAdmission.IsExplicitSourceRequest(message ?? string.Empty))
        {
            return true;
        }

        return IsAnaphora(message) && history.Any(turn =>
            string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(turn.Content)
            && !IsAnaphora(turn.Content));
    }

    public static string RetrievalQuery(string? message, IReadOnlyList<GroundedHistoryTurn> history)
    {
        var resolved = ResolveQuestion(message?.Trim() ?? string.Empty, history ?? [], out var ambiguous);
        return ambiguous ? message?.Trim() ?? string.Empty : resolved;
    }

    public static int CountCovering(string question, IReadOnlyList<GroundedSentence> sentences)
        => sentences.Count(sentence => Covers(question, sentence));

    public static GroundedComposition Compose(GroundedComposeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var message = request.Message?.Trim() ?? string.Empty;
        var history = request.History ?? [];
        var catalog = request.Sentences ?? [];
        if (IsGreeting(message))
        {
            return Done(HybridRagOutcome.Answered, "full", GreetingText, []);
        }

        if (request.SelectorStatus == GroundedSelectorStatus.Unavailable)
        {
            return Done(HybridRagOutcome.ProviderUnavailable, null, UnavailableText, []);
        }

        var question = ResolveQuestion(message, history, out var ambiguous);
        if (ambiguous)
        {
            return Done(HybridRagOutcome.NeedsClarification, null, ClarificationText, []);
        }

        if (request.SelectorStatus == GroundedSelectorStatus.Invalid || !TrySelect(request.ModelJson, request.SelectorStatus, catalog, out var selected))
        {
            return Done(HybridRagOutcome.InsufficientEvidence, null, RejectedSelectionText, []);
        }

        var parts = SplitParts(question);
        var unsupported = new List<string>();
        var sourceParts = new List<string>();
        foreach (var part in parts)
        {
            if (IsUnsupportedPart(part))
            {
                unsupported.Add(part.Trim());
            }
            else
            {
                sourceParts.Add(part.Trim());
            }
        }

        if (sourceParts.Count == 0)
        {
            return Done(HybridRagOutcome.OutOfScope, null, OutOfScopeText, []);
        }

        if (request.RecheckRejected && catalog.Count == 0)
        {
            return Done(HybridRagOutcome.InsufficientEvidence, null, LexicalBoundaryMessages.SourceRevoked, []);
        }

        var copied = new List<GroundedSentence>();
        foreach (var part in sourceParts)
        {
            var pool = selected.Count > 0 ? selected : catalog;
            var covers = pool.Where(sentence => Covers(part, sentence)).DistinctBy(sentence => sentence.SentenceId).ToList();
            if (covers.Count > 1)
            {
                return Done(HybridRagOutcome.NeedsClarification, null, ClarificationText, covers);
            }

            if (covers.Count == 1)
            {
                copied.Add(covers[0]);
                continue;
            }

            var catalogCovers = catalog.Where(sentence => Covers(part, sentence)).DistinctBy(sentence => sentence.SentenceId).ToList();
            if (selected.Count == 0 && catalogCovers.Count == 1)
            {
                copied.Add(catalogCovers[0]);
                continue;
            }

            if (selected.Count == 0 && catalogCovers.Count > 1)
            {
                return Done(HybridRagOutcome.NeedsClarification, null, ClarificationText, catalogCovers);
            }

            unsupported.Add(part);
        }

        if (copied.Count == 0)
        {
            var text = request.RecheckRejected
                ? LexicalBoundaryMessages.SourceRevoked
                : LexicalBoundaryMessages.Unsupported;
            return Done(HybridRagOutcome.InsufficientEvidence, null, text, []);
        }

        var body = string.Join(Environment.NewLine, copied.Select(sentence => sentence.Text).Distinct());
        if (unsupported.Count > 0)
        {
            body = body + Environment.NewLine + Environment.NewLine + UnansweredPrefix + string.Join("; ", unsupported);
        }

        foreach (var route in copied.Select(sentence => AllowRoute(sentence.Route)).Where(route => route is not null).Distinct())
        {
            body = string.Join(Environment.NewLine, body, string.Empty, $"Bạn có thể truy cập ngay tại: {route}");
        }

        var scope = unsupported.Count > 0 ? "partial" : "full";
        return Done(HybridRagOutcome.Answered, scope, body, copied);
    }

    private static bool TrySelect(
        string? modelJson,
        GroundedSelectorStatus status,
        IReadOnlyList<GroundedSentence> catalog,
        out List<GroundedSentence> selected)
    {
        selected = [];
        if (status == GroundedSelectorStatus.NotRequired)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(modelJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(modelJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("sentenceIds", out var ids)
                || ids.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            if (status == GroundedSelectorStatus.Completed && ids.GetArrayLength() == 0)
            {
                return false;
            }

            var allowed = catalog.ToDictionary(sentence => sentence.SentenceId, StringComparer.Ordinal);
            foreach (var id in ids.EnumerateArray())
            {
                if (id.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                var value = id.GetString();
                if (string.IsNullOrWhiteSpace(value) || !allowed.TryGetValue(value, out var sentence))
                {
                    return false;
                }

                selected.Add(sentence);
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ResolveQuestion(string message, IReadOnlyList<GroundedHistoryTurn> history, out bool ambiguous)
    {
        ambiguous = false;
        if (!IsAnaphora(message))
        {
            return message;
        }

        var previous = history
            .Where(turn => string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase))
            .Select(turn => turn.Content?.Trim() ?? string.Empty)
            .Where(content => content.Length > 0 && !IsAnaphora(content))
            .ToList();
        if (previous.Count == 0)
        {
            ambiguous = true;
            return message;
        }

        if (IsChoiceAnaphora(message) && previous.Count > 1)
        {
            ambiguous = true;
            return message;
        }

        return previous[^1];
    }

    private static bool Covers(string part, GroundedSentence sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence.Text))
        {
            return false;
        }

        var hit = new LexicalHit(
            sentence.SourceKey,
            sentence.SourceType,
            sentence.SentenceId,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sentence.Text))).ToLowerInvariant(),
            string.Empty,
            string.Empty,
            sentence.Route,
            "public",
            sentence.Text,
            sentence.SourceKey,
            0);
        return LexicalAnswerAdmission.TryAdmit(part, [hit], out var admitted, out _) && admitted.Count == 1;
    }

    private static IReadOnlyList<string> SplitParts(string question)
    {
        var parts = question.Split([" and ", " và "], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? [question] : parts;
    }

    private static bool IsUnsupportedPart(string part)
    {
        var normalized = LexicalNormalizer.Normalize(part);
        return normalized.Contains("translate", StringComparison.Ordinal)
            || normalized.Contains("dich", StringComparison.Ordinal)
            || normalized.Contains("grammar", StringComparison.Ordinal)
            || normalized.Contains("ngu phap", StringComparison.Ordinal)
            || normalized.Contains("example", StringComparison.Ordinal)
            || normalized.Contains("vi du", StringComparison.Ordinal)
            || normalized.Contains("define", StringComparison.Ordinal)
            || normalized.Contains("dinh nghia", StringComparison.Ordinal)
            || normalized.Contains("in french", StringComparison.Ordinal)
            || normalized.Contains("sang tieng", StringComparison.Ordinal);
    }

    public static bool IsGreeting(string? message)
    {
        var normalized = LexicalNormalizer.Normalize(message);
        return normalized is "hello" or "hi" or "xin chao" or "chao" or "chao ban";
    }

    private static bool IsAnaphora(string? message)
    {
        var normalized = LexicalNormalizer.Normalize(message);
        return normalized is "it" or "that" or "this" or "that one" or "the same" or "the same one"
                or "is that true" or "is that correct" or "cai do" or "cau do" or "nguon do" or "dieu do"
                or "co dung khong" or "which one" or "cai nao"
            || normalized.StartsWith("is that ", StringComparison.Ordinal)
            || normalized.StartsWith("con cai ", StringComparison.Ordinal);
    }

    private static bool IsChoiceAnaphora(string? message)
    {
        var normalized = LexicalNormalizer.Normalize(message);
        return normalized is "which one" or "cai nao";
    }

    private static string? AllowRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route)
            || route.Contains("://", StringComparison.Ordinal)
            || route.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase)
            || !route.StartsWith('/'))
        {
            return null;
        }

        return route;
    }

    private static GroundedComposition Done(
        HybridRagOutcome outcome,
        string? scope,
        string text,
        IReadOnlyList<GroundedSentence> sentences)
        => new(outcome, scope, text, sentences, true);
}
