using TCTEnglish.Services.AI.Rag;

namespace TCTEnglish.Services.AI;

/// <summary>
/// Structured chat diagnostics. The formatted line carries identifiers, outcome,
/// retrieval mode, source keys, versions, stage latency and token counts.
/// It never accepts prompt text, source body, secrets or provider payloads.
/// </summary>
public static class AiChatDiagnostics
{
    public static string SafeErrorCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "unclassified";
        }

        var token = code.Trim();
        return token.Length is > 0 and <= 40 && token.All(item => item is >= 'a' and <= 'z' or '_' or >= '0' and <= '9')
            ? token
            : "unclassified";
    }

    public static string SafeRetrievalMode(string? mode)
        => mode switch
        {
            "live" or "hybrid" or "lexical_degraded" or "unavailable" => mode,
            _ => "unspecified"
        };

    public static string FormatCompletion(
        Guid conversationId,
        string? requestId,
        HybridRagOutcome? outcome,
        string? retrievalMode,
        IReadOnlyList<HybridRagEvidence>? evidence,
        long answerMs,
        long projectMs,
        long totalMs,
        int promptTokens,
        int completionTokens,
        int totalTokens,
        string? model)
    {
        var keys = new List<string>();
        var versions = new List<string>();
        if (evidence is not null)
        {
            foreach (var item in evidence)
            {
                if (keys.Count < 12 && IsToken(item.SourceKey, 80))
                {
                    keys.Add(item.SourceKey);
                }

                AddVersion(versions, item.SourceVersion);
                AddVersion(versions, item.CorpusVersion);
                AddVersion(versions, item.SnapshotId);
            }
        }

        return string.Join(' ',
            "AI chat completed.",
            "ConversationId " + conversationId.ToString("D"),
            "RequestId " + (IsToken(requestId, 80) ? requestId : "none"),
            "Outcome " + WireOutcome(outcome),
            "RetrievalMode " + SafeRetrievalMode(retrievalMode),
            "SourceKeys " + (keys.Count == 0 ? "none" : string.Join(',', keys)),
            "SourceVersions " + (versions.Count == 0 ? "none" : string.Join(',', versions)),
            "AnswerMs " + Clamp(answerMs),
            "ProjectMs " + Clamp(projectMs),
            "TotalMs " + Clamp(totalMs),
            "PromptTokens " + Clamp(promptTokens),
            "CompletionTokens " + Clamp(completionTokens),
            "TotalTokens " + Clamp(totalTokens),
            "Model " + SafeModel(model));
    }

    private static void AddVersion(List<string> versions, string? value)
    {
        if (versions.Count >= 12 || !IsToken(value, 80) || versions.Contains(value, StringComparer.Ordinal))
        {
            return;
        }

        versions.Add(value!);
    }

    private static bool IsToken(string? value, int max)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= max
            && value.All(item => char.IsAsciiLetterOrDigit(item) || item is ':' or '_' or '-' or '#');

    private static string SafeModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model) || model.Length > 40)
        {
            return "unspecified";
        }

        return model.All(item => char.IsAsciiLetterOrDigit(item) || item is '_' or '.' or ':' or '-')
            ? model
            : "unspecified";
    }

    private static long Clamp(long value)
        => value < 0 ? 0 : value;

    private static string WireOutcome(HybridRagOutcome? outcome)
        => outcome switch
        {
            HybridRagOutcome.Answered => "answered",
            HybridRagOutcome.NeedsClarification => "needs_clarification",
            HybridRagOutcome.InsufficientEvidence => "insufficient_evidence",
            HybridRagOutcome.OutOfScope => "out_of_scope",
            HybridRagOutcome.AccessDenied => "access_denied",
            HybridRagOutcome.Cancelled => "cancelled",
            HybridRagOutcome.ProviderUnavailable => "provider_unavailable",
            HybridRagOutcome.QuotaExceeded => "quota_exceeded",
            _ => "unknown"
        };
}
