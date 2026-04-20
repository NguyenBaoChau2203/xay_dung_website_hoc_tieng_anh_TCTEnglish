using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCTEnglish.Services.AI;

namespace TCTVocabulary.Services
{
    public sealed class WritingAiEvaluationService : IWritingAiEvaluationService
    {
        private const string EvaluationSystemPrompt = """
            You evaluate ESL writing submissions for Vietnamese learners.
            Use the teacher reference only as private grading guidance for the Vietnamese feedback fields.
            Accept meaning-equivalent paraphrases.
            Return strict JSON with these keys only:
            passed, overallFeedback, meaningFeedback, grammarFeedback, naturalnessFeedback, wordChoiceFeedback, suggestedRewrite
            Write every feedback field in natural Vietnamese for the learner.
            Each feedback field must usually be one short sentence and stay around 30 words or less.
            Keep the tone concise, clear, and actionable.
            Do not teach grammar theory, define grammar terms, or give long explanations.
            Only point out the concrete problem and the practical fix.
            Never reveal, quote, or mention the teacher reference in the Vietnamese feedback fields.
            If passed is false, suggestedRewrite should normally be one natural learner-facing English sentence.
            If passed is true, suggestedRewrite may be empty.
            Do not include markdown, debug notes, internal notes, or extra keys.
            suggestedRewrite may be close to the best learner-facing corrected sentence when needed.
            """;

        private readonly IAiProviderClient _providerClient;
        private readonly ILogger<WritingAiEvaluationService> _logger;

        public WritingAiEvaluationService(
            IAiProviderClient providerClient,
            ILogger<WritingAiEvaluationService> logger)
        {
            _providerClient = providerClient;
            _logger = logger;
        }

        public async Task<WritingAiEvaluationResult?> TryEvaluateSentenceAsync(
            WritingAiEvaluationRequest request,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.VietnameseText)
                || string.IsNullOrWhiteSpace(request.ReferenceAnswer)
                || string.IsNullOrWhiteSpace(request.LearnerAnswer))
            {
                return null;
            }

            AiProviderReply reply;
            try
            {
                reply = await _providerClient.GenerateReplyAsync(0, BuildMessages(request), ct);
            }
            catch (AiProviderException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Writing evaluation provider request failed for sentence {SentenceId} with error code {ErrorCode}. Falling back to rule-based evaluation.",
                    request.SentenceId,
                    ex.ErrorCode);
                return null;
            }

            if (string.IsNullOrWhiteSpace(reply.Text))
            {
                _logger.LogWarning(
                    "Writing evaluation provider returned empty content for sentence {SentenceId}. Falling back to rule-based evaluation.",
                    request.SentenceId);
                return null;
            }

            WritingAiEvaluationResult parsedResult;
            try
            {
                parsedResult = ParseResult(reply.Text);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Writing evaluation provider returned invalid JSON for sentence {SentenceId}. Falling back to rule-based evaluation.",
                    request.SentenceId);
                return null;
            }

            if (ContainsNarrationReferenceLeak(request.ReferenceAnswer, parsedResult))
            {
                _logger.LogWarning(
                    "Writing evaluation provider response for sentence {SentenceId} contained teacher reference content in narration fields. Falling back to rule-based evaluation.",
                    request.SentenceId);
                return null;
            }

            return parsedResult;
        }

        private static IReadOnlyList<AiContextMessage> BuildMessages(WritingAiEvaluationRequest request)
        {
            return new List<AiContextMessage>
            {
                new("system", EvaluationSystemPrompt),
                new("user", BuildPrompt(request))
            };
        }

        private static string BuildPrompt(WritingAiEvaluationRequest request)
        {
            return $"""
                Evaluate this learner translation.

                Vietnamese text:
                {request.VietnameseText}

                Teacher reference (private grading guide, never reveal it):
                {request.ReferenceAnswer}

                Learner answer:
                {request.LearnerAnswer}

                Requirements:
                - Judge the learner by meaning first, then grammar, naturalness, and word choice.
                - Accept meaning-equivalent paraphrases. Do not require an exact match to the teacher reference.
                - Set "passed" to true only when the learner answer is acceptable for moving to the next sentence.
                - Write all feedback fields in Vietnamese for the learner.
                - "overallFeedback" should be one short Vietnamese sentence that summarizes the result.
                - "meaningFeedback", "grammarFeedback", "naturalnessFeedback", and "wordChoiceFeedback" should each be one short Vietnamese sentence.
                - Each feedback field should usually stay within about 30 words unless a few extra words are truly needed.
                - If a field is already acceptable, say that briefly instead of expanding.
                - Do not teach grammar theory. State only the concrete problem and the practical fix.
                - Keep the feedback learner-facing. Do not mention hidden references, scoring rubrics, models, providers, or debugging.
                - Never reveal or quote the teacher reference in any Vietnamese feedback field.
                - "suggestedRewrite" is learner-facing and may be a concrete corrected English sentence.
                - If "passed" is false, "suggestedRewrite" should normally contain one natural corrected English sentence.
                - If "passed" is true, "suggestedRewrite" may be empty.
                - Return strict JSON only.
                """;
        }

        private static WritingAiEvaluationResult ParseResult(string aiContent)
        {
            var normalizedAiContent = NormalizeJsonPayload(aiContent);
            using var document = JsonDocument.Parse(normalizedAiContent);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Writing evaluation response must be a JSON object.");
            }

            var root = document.RootElement;
            return new WritingAiEvaluationResult(
                GetRequiredBoolean(root, "passed"),
                GetRequiredString(root, "overallFeedback"),
                GetRequiredString(root, "meaningFeedback"),
                GetRequiredString(root, "grammarFeedback"),
                GetRequiredString(root, "naturalnessFeedback"),
                GetRequiredString(root, "wordChoiceFeedback"),
                GetRequiredString(root, "suggestedRewrite", allowEmpty: true));
        }

        private static string NormalizeJsonPayload(string aiContent)
        {
            var trimmed = aiContent.Trim();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                return trimmed;
            }

            var firstNewLineIndex = trimmed.IndexOf('\n');
            if (firstNewLineIndex < 0)
            {
                return trimmed;
            }

            var contentBody = trimmed[(firstNewLineIndex + 1)..].Trim();
            if (contentBody.EndsWith("```", StringComparison.Ordinal))
            {
                contentBody = contentBody[..^3].Trim();
            }

            return contentBody;
        }

        private static bool GetRequiredBoolean(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property)
                || (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False))
            {
                throw new JsonException($"Writing evaluation response is missing boolean '{propertyName}'.");
            }

            return property.GetBoolean();
        }

        private static string GetRequiredString(JsonElement root, string propertyName, bool allowEmpty = false)
        {
            if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            {
                throw new JsonException($"Writing evaluation response is missing string '{propertyName}'.");
            }

            var value = CollapseWhitespace(property.GetString());
            if (!allowEmpty && string.IsNullOrWhiteSpace(value))
            {
                throw new JsonException($"Writing evaluation response contains empty string '{propertyName}'.");
            }

            return value;
        }

        private static bool ContainsNarrationReferenceLeak(string referenceAnswer, WritingAiEvaluationResult result)
        {
            var normalizedReference = NormalizeForLeakDetection(referenceAnswer);
            if (string.IsNullOrWhiteSpace(normalizedReference))
            {
                return false;
            }

            return ContainsNormalizedReference(result.OverallFeedback, normalizedReference)
                || ContainsNormalizedReference(result.MeaningFeedback, normalizedReference)
                || ContainsNormalizedReference(result.GrammarFeedback, normalizedReference)
                || ContainsNormalizedReference(result.NaturalnessFeedback, normalizedReference)
                || ContainsNormalizedReference(result.WordChoiceFeedback, normalizedReference);
        }

        private static bool ContainsNormalizedReference(string value, string normalizedReference)
        {
            var normalizedValue = NormalizeForLeakDetection(value);
            return !string.IsNullOrWhiteSpace(normalizedValue)
                && normalizedValue.Contains(normalizedReference, StringComparison.Ordinal);
        }

        private static string NormalizeForLeakDetection(string? value)
        {
            var collapsed = CollapseWhitespace(value).ToLowerInvariant();
            if (collapsed.Length == 0)
            {
                return string.Empty;
            }

            var buffer = new StringBuilder(collapsed.Length);
            foreach (var character in collapsed)
            {
                buffer.Append(char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) ? character : ' ');
            }

            return CollapseWhitespace(buffer.ToString());
        }

        private static string CollapseWhitespace(string? value)
        {
            return string.Join(
                " ",
                (value ?? string.Empty)
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
