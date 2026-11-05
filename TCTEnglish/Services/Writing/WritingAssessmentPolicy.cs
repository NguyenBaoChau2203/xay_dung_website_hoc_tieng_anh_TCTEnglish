using System.Security.Cryptography;
using System.Text;

namespace TCTEnglish.Services.Writing;

public enum WritingAssessmentStatus
{
    Accepted = 1,
    NeedsRevision = 2,
    NeedsReview = 3
}

public sealed class WritingAssessmentDecision
{
    public required WritingAssessmentStatus Status { get; init; }
    public required string Source { get; init; }
    public required string SummaryTitle { get; init; }
    public required string SummaryText { get; init; }
    public required string MeaningFeedback { get; init; }
    public required string GrammarFeedback { get; init; }
    public required string NaturalnessFeedback { get; init; }
    public required string WordChoiceFeedback { get; init; }
    public string SuggestedRewrite { get; init; } = string.Empty;

    public bool Passed => Status == WritingAssessmentStatus.Accepted;
    public bool CanAutoAdvance => Status == WritingAssessmentStatus.Accepted;
    public bool ReviewRequired => Status == WritingAssessmentStatus.NeedsReview;
}

/// <summary>
/// Pure local writing assessment. No I/O. Exact/normalized match is Accepted;
/// empty/symbol failures are NeedsRevision; other non-exact answers are NeedsReview
/// (never auto-pass via keyword coverage).
/// </summary>
public static class WritingAssessmentPolicy
{
    /// <summary>
    /// Map persisted EvaluationSource/Passed to status without reading SummaryTitle text.
    /// </summary>
    public static WritingAssessmentStatus ResolveHistoricalStatus(bool passed, string? evaluationSource)
    {
        if (passed)
        {
            return WritingAssessmentStatus.Accepted;
        }

        // local-review: local NeedsReview decision.
        // local+ai-feedback: legacy rows written when optional cloud overwrote EvaluationSource;
        // treat as NeedsReview when !passed so reload keeps the local decision.
        if (string.Equals(evaluationSource, "local-review", StringComparison.OrdinalIgnoreCase)
            || string.Equals(evaluationSource, "local+ai-feedback", StringComparison.OrdinalIgnoreCase))
        {
            return WritingAssessmentStatus.NeedsReview;
        }

        return WritingAssessmentStatus.NeedsRevision;
    }

    /// <summary>
    /// Short history-chip label for Practice/Data recentAttempts (not the full summary title).
    /// </summary>
    public static string ResolveHistoricalStatusLabel(bool passed, string? evaluationSource)
        => ResolveHistoricalStatus(passed, evaluationSource) switch
        {
            WritingAssessmentStatus.Accepted => "Đạt",
            WritingAssessmentStatus.NeedsReview => "Cần xem lại",
            _ => "Cần sửa"
        };

    public const int MaxAcceptedVariantsPerSentence = 5;
    public const int MaxAcceptedAnswerLength = 1000;

    /// <summary>
    /// Content-bound optimistic concurrency token for Admin accepted-answer forms.
    /// Binds exercise/sentence identity, Vietnamese/English meaning, and ordered variants.
    /// </summary>
    public static string BuildAcceptedAnswersStateToken(
        int exerciseId,
        int sentenceId,
        string? vietnameseText,
        string? englishMeaning,
        IEnumerable<(int Id, string NormalizedHash, long ApprovedAtUtcTicks)> variants)
    {
        var variantsPayload = string.Join(
            '|',
            variants
                .OrderBy(variant => variant.Id)
                .Select(variant => $"{variant.Id}:{variant.NormalizedHash}:{variant.ApprovedAtUtcTicks}"));

        var payload =
            $"ex={exerciseId}|sen={sentenceId}|vi={CollapseWhitespace(vietnameseText)}|en={CollapseWhitespace(englishMeaning)}|vars={variantsPayload}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string BuildAcceptedAnswersStateToken(
        int exerciseId,
        int sentenceId,
        string? vietnameseText,
        string? englishMeaning,
        IEnumerable<TCTVocabulary.Models.WritingAcceptedAnswer> answers)
    {
        return BuildAcceptedAnswersStateToken(
            exerciseId,
            sentenceId,
            vietnameseText,
            englishMeaning,
            answers.Select(answer => (answer.Id, answer.NormalizedHash, answer.ApprovedAtUtc.Ticks)));
    }

    public static WritingAssessmentDecision Assess(
        string? referenceAnswer,
        string? userAnswer,
        IEnumerable<string>? approvedVariants = null)
    {
        var collapsedAnswer = CollapseWhitespace(userAnswer);
        if (string.IsNullOrWhiteSpace(collapsedAnswer))
        {
            return HardRevision(
                "Cần nhập câu",
                "Hãy nhập một câu tiếng Anh trước khi yêu cầu hệ thống chấm.",
                "Hiện chưa có câu tiếng Anh để đối chiếu.",
                "Hãy bắt đầu bằng một câu tiếng Anh hoàn chỉnh.",
                "Hãy giữ cách diễn đạt ngắn gọn và rõ ràng.",
                "Hãy dùng các ý chính từ câu tiếng Việt.");
        }

        var answerTokens = ExtractTokens(collapsedAnswer);
        if (answerTokens.Count == 0)
        {
            return HardRevision(
                "Cần có từ tiếng Anh",
                "Hãy dùng ít nhất một từ tiếng Anh để hệ thống có thể chấm.",
                "Câu trả lời hiện tại chưa có đủ nội dung tiếng Anh để kiểm tra ý nghĩa.",
                "Hãy viết một câu hoàn chỉnh thay vì chỉ nhập ký hiệu hoặc để trống.",
                "Hãy hướng tới một câu tiếng Anh tự nhiên.",
                "Hãy chọn từ vựng sát với ý gốc.");
        }

        var normalizedAnswer = NormalizeForComparison(collapsedAnswer);
        var normalizedReference = NormalizeForComparison(referenceAnswer);

        if (!string.IsNullOrEmpty(normalizedReference)
            && string.Equals(normalizedAnswer, normalizedReference, StringComparison.Ordinal))
        {
            return new WritingAssessmentDecision
            {
                Status = WritingAssessmentStatus.Accepted,
                Source = "local-exact",
                SummaryTitle = "Câu đạt yêu cầu",
                SummaryText = "Câu của bạn khớp đáp án tham chiếu sau khi chuẩn hóa. Bạn có thể sang câu tiếp theo.",
                MeaningFeedback = "Ý nghĩa khớp đáp án được hệ thống kiểm soát.",
                GrammarFeedback = "Không phát hiện lệch so với đáp án tham chiếu.",
                NaturalnessFeedback = "Câu được chấp nhận theo khớp chính xác đã chuẩn hóa.",
                WordChoiceFeedback = "Từ vựng khớp đáp án tham chiếu.",
                SuggestedRewrite = string.Empty
            };
        }

        if (approvedVariants != null)
        {
            foreach (var variant in approvedVariants)
            {
                var normalizedVariant = NormalizeForComparison(variant);
                if (!string.IsNullOrEmpty(normalizedVariant)
                    && string.Equals(normalizedAnswer, normalizedVariant, StringComparison.Ordinal))
                {
                    return new WritingAssessmentDecision
                    {
                        Status = WritingAssessmentStatus.Accepted,
                        Source = "local-approved-variant",
                        SummaryTitle = "Câu đạt yêu cầu",
                        SummaryText = "Câu của bạn khớp một đáp án đã được Admin duyệt. Bạn có thể sang câu tiếp theo.",
                        MeaningFeedback = "Ý nghĩa khớp đáp án biến thể đã duyệt.",
                        GrammarFeedback = "Không phát hiện lệch so với đáp án biến thể đã duyệt.",
                        NaturalnessFeedback = "Câu được chấp nhận theo khớp chính xác với đáp án đã duyệt.",
                        WordChoiceFeedback = "Từ vựng khớp đáp án biến thể đã duyệt.",
                        SuggestedRewrite = string.Empty
                    };
                }
            }
        }

        // Non-exact answers are never auto-accepted by keyword overlap.
        return new WritingAssessmentDecision
        {
            Status = WritingAssessmentStatus.NeedsReview,
            Source = "local-review",
            SummaryTitle = "Cần xem lại câu này",
            SummaryText = "Câu chưa khớp đáp án đã duyệt. Hệ thống không tự khẳng định sai nghĩa — hãy so sánh gợi ý và chỉnh lại nếu cần.",
            MeaningFeedback = "Câu có thể đúng theo cách diễn đạt khác, nhưng chưa có đáp án biến thể được duyệt để chấp nhận tự động.",
            GrammarFeedback = "Hãy tự kiểm tra ngữ pháp; đánh giá ngữ pháp tự động không quyết định đạt/không đạt.",
            NaturalnessFeedback = "Hãy giữ câu tiếng Anh tự nhiên và bám sát ý gốc.",
            WordChoiceFeedback = "So sánh với gợi ý/đáp án tham chiếu nếu có, rồi gửi lại khi chắc hơn.",
            SuggestedRewrite = string.Empty
        };
    }

    public static string ComputeNormalizedHash(string? answer)
    {
        var normalized = NormalizeForComparison(answer);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool IsExactNormalizedMatch(string? left, string? right)
    {
        var normalizedLeft = NormalizeForComparison(left);
        var normalizedRight = NormalizeForComparison(right);
        return !string.IsNullOrEmpty(normalizedLeft)
            && string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal);
    }

    private static WritingAssessmentDecision HardRevision(
        string title,
        string summary,
        string meaning,
        string grammar,
        string naturalness,
        string wordChoice)
    {
        return new WritingAssessmentDecision
        {
            Status = WritingAssessmentStatus.NeedsRevision,
            Source = "local-hard-fail",
            SummaryTitle = title,
            SummaryText = summary,
            MeaningFeedback = meaning,
            GrammarFeedback = grammar,
            NaturalnessFeedback = naturalness,
            WordChoiceFeedback = wordChoice,
            SuggestedRewrite = string.Empty
        };
    }

    public static string CollapseWhitespace(string? value)
    {
        return string.Join(
            ' ',
            (value ?? string.Empty)
                .Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
    }

    public static string NormalizeForComparison(string? value)
    {
        var collapsed = CollapseWhitespace(value).ToLowerInvariant();
        var buffer = new StringBuilder(collapsed.Length);

        for (var index = 0; index < collapsed.Length; index++)
        {
            var character = collapsed[index];
            if (char.IsLetterOrDigit(character) || char.IsWhiteSpace(character))
            {
                buffer.Append(character);
                continue;
            }

            if (character is '−' or '–')
            {
                buffer.Append('-');
                continue;
            }

            if (IsMeaningBearingSymbol(character))
            {
                buffer.Append(character);
                continue;
            }

            // Keep numeric separators between digits WITHOUT collapsing locale forms.
            // English 1,000 must not equal 1.000; unapproved 1,5 vs 1.5 stays distinct.
            if ((character == '.' || character == ',')
                && index > 0
                && index + 1 < collapsed.Length
                && char.IsDigit(collapsed[index - 1])
                && char.IsDigit(collapsed[index + 1]))
            {
                buffer.Append(character);
                continue;
            }

            // Decorative punctuation only — never strip currency, signs, compare ops, or percent.
            buffer.Append(' ');
        }

        return CollapseWhitespace(buffer.ToString());
    }

    private static bool IsMeaningBearingSymbol(char character)
    {
        return character is '$' or '€' or '£' or '¥' or '%' or '+' or '-' or '−' or '±'
            or '<' or '>' or '=' or '≤' or '≥' or '≠' or '/' or ':' or '*'
            || char.GetUnicodeCategory(character) == System.Globalization.UnicodeCategory.CurrencySymbol
            || char.GetUnicodeCategory(character) == System.Globalization.UnicodeCategory.MathSymbol;
    }

    private static List<string> ExtractTokens(string value)
    {
        return NormalizeForComparison(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }
}
