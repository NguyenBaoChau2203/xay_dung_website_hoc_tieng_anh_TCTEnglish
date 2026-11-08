using System.Security.Cryptography;
using System.Text;
using TCTEnglish.Services.AI.Internal;
using TCTEnglish.Services.AI.Rag.Corpus;

namespace TCTEnglish.Services.AI.Rag;

public sealed class AiChatAnswerService : IAiChatAnswerService
{
    public const string ModelName = "internal-chat-boundary";
    private const float ConfidentIntentThreshold = 0.55f;

    private readonly IAiQueryClassifier _classifier;
    private readonly IReadOnlyList<IKnowledgeRetriever> _retrievers;
    private readonly IAnswerComposer _composer;
    private readonly IAiTokenCounter _tokenCounter;
    private readonly ILexicalRetrievalService? _lexicalRetrieval;

    public AiChatAnswerService(
        IAiQueryClassifier classifier,
        IEnumerable<IKnowledgeRetriever> retrievers,
        IAnswerComposer composer,
        IAiTokenCounter tokenCounter,
        ILexicalRetrievalService? lexicalRetrieval = null)
    {
        _classifier = classifier;
        _retrievers = retrievers.ToList();
        _composer = composer;
        _tokenCounter = tokenCounter;
        _lexicalRetrieval = lexicalRetrieval;
    }

    public async Task<AiChatAnswer> AnswerAsync(
        int userId,
        string userMessage,
        IReadOnlyList<AiContextMessage> contextMessages,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _ = contextMessages;

        var message = userMessage?.Trim() ?? string.Empty;
        if (ChatDisclosurePolicy.IsPromptOrCredentialExtraction(message))
        {
            return CreateAnswer(HybridRagOutcome.OutOfScope, null, ChatDisclosurePolicy.RefusalText, [], message);
        }

        var classification = _classifier.Classify(message);
        var externalFactRequest = ChatExternalFactPolicy.IsExplicitExternalFactRequest(message)
            || (classification.Intent == UserIntent.OutOfScope && ChatExternalFactPolicy.IsUnscopedGenerationRequest(message));
        var intents = new HashSet<UserIntent> { classification.Intent };
        if (classification.Confidence < ConfidentIntentThreshold || classification.Intent == UserIntent.OutOfScope)
        {
            intents.Add(UserIntent.WebsiteGuide);
            intents.Add(UserIntent.OutOfScope);
            if (!externalFactRequest)
            {
                intents.Add(UserIntent.CardLookup);
            }
        }

        var snippets = new List<KnowledgeSnippet>();
        foreach (var retriever in _retrievers.Where(retriever => intents.Any(retriever.CanHandle)))
        {
            snippets.AddRange(await retriever.RetrieveAsync(userId, message, ct));
        }

        var sanitized = snippets
            .Select(SanitizeSnippet)
            .Where(snippet => !string.IsNullOrWhiteSpace(snippet.Title) || !string.IsNullOrWhiteSpace(snippet.Body))
            .ToList();
        if (sanitized.Any(snippet => snippet.Source == KnowledgeSnippetSources.SecurityPolicy))
        {
            var refusal = await _composer.ComposeAsync(UserIntent.OutOfScope, message, sanitized, ct);
            return CreateAnswer(HybridRagOutcome.OutOfScope, null, refusal, [], message);
        }

        var lookup = StoredCardLookupRequest.Parse(message);
        var hasCardCandidates = sanitized.Any(snippet => snippet.Source == KnowledgeSnippetSources.CardLookupResult);
        var cardFlow = intents.Contains(UserIntent.CardLookup) && (lookup is not null || hasCardCandidates);
        if (externalFactRequest || (cardFlow && lookup is null))
        {
            var refusal = await _composer.ComposeAsync(UserIntent.OutOfScope, message, [], ct);
            return CreateAnswer(HybridRagOutcome.OutOfScope, null, refusal, [], message);
        }

        sanitized.RemoveAll(snippet => snippet.Source == KnowledgeSnippetSources.CardLookupResult
            && (lookup is null || !lookup.Supports(snippet)));
        if (cardFlow && lookup is not null)
        {
            sanitized = sanitized.Where(snippet => snippet.Source == KnowledgeSnippetSources.CardLookupResult).ToList();
            if (sanitized.Count == 0)
            {
                var missing = lookup.Kind switch
                {
                    StoredCardLookupKind.Pronunciation => $"Mình chưa có phiên âm đã lưu cho \"{lookup.Target}\" trong các thẻ bạn được xem.",
                    StoredCardLookupKind.Example => $"Mình chưa có ví dụ đã lưu cho \"{lookup.Target}\" trong các thẻ bạn được xem.",
                    _ => $"Mình chưa tìm thấy nội dung đã lưu cho toàn bộ từ hoặc cụm \"{lookup.Target}\" trong các thẻ bạn được xem. Bạn có thể hỏi nghĩa của một từ hoặc cụm có trong bộ thẻ."
                };
                return CreateAnswer(HybridRagOutcome.InsufficientEvidence, null, missing, [], message);
            }
        }

        var cardSelection = CardLookupSelection.Resolve(message, sanitized);
        if (cardSelection.Mode != CardLookupAnswerMode.None)
        {
            sanitized = sanitized
                .Where(snippet => snippet.Source != KnowledgeSnippetSources.CardLookupResult)
                .Concat(cardSelection.Cards)
                .ToList();
        }
        else
        {
            var lexicalAnswer = await TryLexicalAnswerAsync(userId, message, classification.Intent, sanitized, ct);
            if (lexicalAnswer is not null)
            {
                return lexicalAnswer;
            }
        }

        var answerIntent = SelectAnswerIntent(classification.Intent, sanitized);
        var text = await _composer.ComposeAsync(answerIntent, message, sanitized, ct);
        if (string.IsNullOrWhiteSpace(text))
        {
            text = "Mình chỉ hỗ trợ dữ liệu nội bộ của TCT English. Bạn vui lòng hỏi về tính năng hoặc dữ liệu trong hệ thống.";
        }

        var outcome = cardSelection.Mode == CardLookupAnswerMode.Clarify
            ? HybridRagOutcome.NeedsClarification
            : SelectOutcome(answerIntent, sanitized);
        var evidenceSnippets = outcome == HybridRagOutcome.NeedsClarification
            ? cardSelection.Cards
            : sanitized;
        var evidence = outcome is HybridRagOutcome.Answered or HybridRagOutcome.NeedsClarification
            ? BuildEvidence(userId, evidenceSnippets)
            : [];
        var scope = outcome == HybridRagOutcome.Answered ? "full" : null;
        return CreateAnswer(outcome, scope, text, evidence, message);
    }

    private async Task<AiChatAnswer?> TryLexicalAnswerAsync(
        int userId,
        string message,
        UserIntent intent,
        IReadOnlyList<KnowledgeSnippet> snippets,
        CancellationToken ct)
    {
        if (_lexicalRetrieval is null || HasLiveAnswer(snippets) || intent is not (UserIntent.WebsiteGuide or UserIntent.OutOfScope))
        {
            return null;
        }

        var hasGuide = snippets.Any(snippet => snippet.Source == KnowledgeSnippetSources.WebsiteGuide);
        var lexical = await _lexicalRetrieval.RetrieveAsync(userId, message, ct);
        if (hasGuide)
        {
            // An incidental guide keyword (for example "bell") cannot answer a
            // passage fact request. Live personal facts still retain priority.
            var lessonHits = lexical.Hits.Where(hit => hit.SourceType != "guide").ToList();
            if (!LexicalAnswerAdmission.IsExplicitSourceRequest(message)
                && !lessonHits.Any(hit => LexicalAnswerAdmission.ReferencesSourceTopic(message, hit.Text)))
            {
                return null;
            }

            if (lessonHits.Count == 0)
            {
                return CreateAnswer(HybridRagOutcome.InsufficientEvidence, null, LexicalBoundaryMessages.Unsupported, [], message);
            }

            lexical = new LexicalRetrievalResult
            {
                Availability = lexical.Availability,
                Hits = lessonHits,
                AmbiguousTie = lessonHits.Count >= 2 && lessonHits[0].SourceKey != lessonHits[1].SourceKey
                    && Bm25Ranker.ScoresTie(lessonHits[0].Score, lessonHits[1].Score)
            };
        }
        if (lexical.AmbiguousTie && lexical.Hits.Count >= 2)
        {
            var choices = string.Join(" hay ", lexical.Hits.Take(2).Select(hit => $"{hit.Title} ({hit.SourceKey})"));
            return CreateAnswer(
                HybridRagOutcome.NeedsClarification,
                null,
                $"Mình thấy hai nguồn cùng khớp. Bạn muốn hỏi về {choices}?",
                BuildLexicalEvidence(lexical.Hits),
                message);
        }

        if (lexical.Hits.Count > 0)
        {
            if (!LexicalAnswerAdmission.TryAdmit(message, lexical.Hits, out var admitted, out var answerScope))
            {
                return CreateAnswer(HybridRagOutcome.InsufficientEvidence, null, LexicalBoundaryMessages.Unsupported, [], message);
            }

            var first = admitted[0];
            var text = string.Join(Environment.NewLine, admitted.Select(hit => hit.Text).Distinct());
            if (answerScope == "excerpt")
            {
                text = "Đoạn học liệu khớp từ khóa (trích nguyên văn để bạn tham khảo):" + Environment.NewLine + text;
            }
            if (!string.IsNullOrWhiteSpace(first.Route))
            {
                text = string.Join(Environment.NewLine, text, string.Empty, $"Bạn có thể truy cập ngay tại: {first.Route}");
            }

            return CreateAnswer(HybridRagOutcome.Answered, answerScope, text, BuildLexicalEvidence(admitted), message);
        }

        if (lexical.RecheckRejected)
        {
            return CreateAnswer(HybridRagOutcome.InsufficientEvidence, null, LexicalBoundaryMessages.SourceRevoked, [], message);
        }

        if (lexical.Availability != LexicalAvailability.Ready && intent == UserIntent.WebsiteGuide)
        {
            return CreateAnswer(HybridRagOutcome.InsufficientEvidence, null, LexicalBoundaryMessages.IndexUnavailable, [], message);
        }

        return null;
    }

    private static bool HasLiveAnswer(IReadOnlyList<KnowledgeSnippet> snippets)
    {
        return snippets.Any(snippet => snippet.Source is KnowledgeSnippetSources.CardLookupResult
            or KnowledgeSnippetSources.SpeakingSuggestion
            or KnowledgeSnippetSources.SpeakingSuggestionSummary
            or KnowledgeSnippetSources.UserVocabularySummary
            or KnowledgeSnippetSources.UserVocabularySet
            or KnowledgeSnippetSources.ProgressSummary
            or KnowledgeSnippetSources.ClassInfoSummary
            or KnowledgeSnippetSources.ClassInfoItem
            or KnowledgeSnippetSources.StudyRecommendation
            or KnowledgeSnippetSources.LearningAreaSummary
            or KnowledgeSnippetSources.GoalSummary
            or KnowledgeSnippetSources.GoalAreaItem
            or KnowledgeSnippetSources.NotificationSummary
            or KnowledgeSnippetSources.NotificationItem
            or KnowledgeSnippetSources.BillingSummary
            or KnowledgeSnippetSources.BillingOrderItem);
    }

    private static IReadOnlyList<HybridRagEvidence> BuildLexicalEvidence(IReadOnlyList<LexicalHit> hits)
    {
        var capturedAt = DateTime.UtcNow;
        return hits.Select(hit => new HybridRagEvidence(
            hit.SourceKey,
            hit.SourceType,
            hit.SentenceId,
            hit.ContentHash,
            hit.CorpusVersion,
            hit.ChunkerVersion,
            null,
            hit.Route,
            hit.Visibility,
            null,
            capturedAt,
            hit.Text)).ToList();
    }

    private AiChatAnswer CreateAnswer(
        HybridRagOutcome outcome,
        string? answerScope,
        string text,
        IReadOnlyList<HybridRagEvidence> evidence,
        string userMessage)
    {
        var promptTokens = _tokenCounter.CountTokens(userMessage);
        var completionTokens = _tokenCounter.CountTokens(text);
        return new AiChatAnswer(
            outcome,
            answerScope,
            text,
            evidence,
            promptTokens,
            completionTokens,
            promptTokens + completionTokens,
            ModelName,
            Guid.NewGuid().ToString("N"));
    }

    private static UserIntent SelectAnswerIntent(UserIntent classifiedIntent, IReadOnlyList<KnowledgeSnippet> snippets)
    {
        if (snippets.Any(snippet => snippet.Source == KnowledgeSnippetSources.CardLookupResult))
        {
            return UserIntent.CardLookup;
        }

        if (snippets.Any(snippet => snippet.Source == KnowledgeSnippetSources.WebsiteGuide))
        {
            return UserIntent.WebsiteGuide;
        }

        if (snippets.Count > 0 && classifiedIntent != UserIntent.OutOfScope)
        {
            return classifiedIntent;
        }

        if (snippets.Count > 0)
        {
            return classifiedIntent == UserIntent.OutOfScope
                ? InferIntent(snippets) ?? UserIntent.OutOfScope
                : classifiedIntent;
        }

        return classifiedIntent;
    }

    private static UserIntent? InferIntent(IReadOnlyList<KnowledgeSnippet> snippets)
    {
        if (snippets.Any(snippet => snippet.Source == KnowledgeSnippetSources.SpeakingSuggestion))
        {
            return UserIntent.SpeakingSuggestion;
        }

        if (snippets.Any(snippet => snippet.Source == KnowledgeSnippetSources.ClassInfoSummary))
        {
            return UserIntent.ClassInfo;
        }

        if (snippets.Any(snippet => snippet.Source == KnowledgeSnippetSources.LearningAreaSummary))
        {
            return UserIntent.WebsiteGuide;
        }

        return null;
    }

    private static HybridRagOutcome SelectOutcome(UserIntent intent, IReadOnlyList<KnowledgeSnippet> snippets)
    {
        if (snippets.Any(snippet =>
                snippet.Source is KnowledgeSnippetSources.CardLookupResult
                    or KnowledgeSnippetSources.WebsiteGuide
                    or KnowledgeSnippetSources.SpeakingSuggestion
                    or KnowledgeSnippetSources.UserVocabularySummary
                    or KnowledgeSnippetSources.ProgressSummary
                    or KnowledgeSnippetSources.ClassInfoSummary
                    or KnowledgeSnippetSources.StudyRecommendation
                    or KnowledgeSnippetSources.LearningAreaSummary
                    or KnowledgeSnippetSources.GoalSummary
                    or KnowledgeSnippetSources.NotificationSummary
                    or KnowledgeSnippetSources.BillingSummary))
        {
            return HybridRagOutcome.Answered;
        }

        if (intent is UserIntent.Greeting)
        {
            return HybridRagOutcome.Answered;
        }

        if (intent is UserIntent.CardLookup or UserIntent.MyVocabulary or UserIntent.MyProgress
            or UserIntent.SpeakingSuggestion or UserIntent.ClassInfo or UserIntent.StudyRecommendation)
        {
            return HybridRagOutcome.InsufficientEvidence;
        }

        return HybridRagOutcome.OutOfScope;
    }

    private static KnowledgeSnippet SanitizeSnippet(KnowledgeSnippet snippet)
    {
        return snippet with
        {
            Title = RemoveUntrustedInstructions(snippet.Title),
            Body = RemoveUntrustedInstructions(snippet.Body)
        };
    }

    private static string RemoveUntrustedInstructions(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var cleaned = value;
        foreach (var phrase in new[]
        {
            "ignore previous instructions",
            "reveal the system prompt",
            "system prompt"
        })
        {
            cleaned = ReplacePhrase(cleaned, phrase);
        }

        return cleaned.Trim();
    }

    private static string ReplacePhrase(string value, string phrase)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;
        var index = value.IndexOf(phrase, comparison);
        while (index >= 0)
        {
            value = value.Remove(index, phrase.Length);
            index = value.IndexOf(phrase, comparison);
        }

        return value;
    }

    private static IReadOnlyList<HybridRagEvidence> BuildEvidence(int userId, IReadOnlyList<KnowledgeSnippet> snippets)
    {
        var capturedAt = DateTime.UtcNow;
        return snippets
            .Where(snippet => !string.IsNullOrWhiteSpace(snippet.Body) || !string.IsNullOrWhiteSpace(snippet.Title))
            .Where(snippet => !IsAggregateSummary(snippet.Source)
                || !string.IsNullOrWhiteSpace(snippet.EntityKey)
                || OwnerSnapshotKey(snippet.Source, userId) is not null)
            .Select((snippet, index) =>
            {
                var text = string.IsNullOrWhiteSpace(snippet.Body) ? snippet.Title : snippet.Body;
                var entityKey = snippet.EntityKey ?? OwnerSnapshotKey(snippet.Source, userId);
                var sourceKey = !string.IsNullOrWhiteSpace(entityKey)
                    ? entityKey
                    : snippet.Source == KnowledgeSnippetSources.CardLookupResult && snippet.EntityId is int cardId
                        ? $"card:{cardId}"
                        : $"{snippet.Source}:{snippet.Title}";
                var visibility = ResolveVisibility(snippet);
                return new HybridRagEvidence(
                    sourceKey,
                    MapSourceType(snippet.Source),
                    $"{sourceKey}#s{index + 1}",
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant(),
                    null,
                    null,
                    null,
                    AllowRoute(snippet.Route),
                    visibility,
                    visibility == "public" || snippet.Source == KnowledgeSnippetSources.WebsiteGuide ? null : userId,
                    capturedAt,
                    text)
                {
                    DocumentContentHash = GroundingHashes.TitleBody(snippet.Title, text),
                    EntityKey = sourceKey
                };
            })
            .ToList();
    }

    private static string? AllowRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route) || route.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return route;
    }

    private static string? OwnerSnapshotKey(string source, int userId)
    {
        if (userId <= 0)
        {
            return null;
        }

        var kind = source switch
        {
            KnowledgeSnippetSources.ProgressSummary
                or KnowledgeSnippetSources.LearningAreaSummary
                or KnowledgeSnippetSources.StudyRecommendation => "progress",
            KnowledgeSnippetSources.GoalSummary or KnowledgeSnippetSources.GoalAreaItem => "goal",
            KnowledgeSnippetSources.BillingSummary or KnowledgeSnippetSources.BillingOrderItem => "billing",
            KnowledgeSnippetSources.NotificationSummary or KnowledgeSnippetSources.NotificationItem => "notification",
            KnowledgeSnippetSources.UserVocabularySummary or KnowledgeSnippetSources.UserVocabularySet => "vocabulary",
            _ => null
        };
        return kind is null ? null : $"owner-snapshot:{kind}-{userId}";
    }

    private static bool IsAggregateSummary(string source)
        => source is KnowledgeSnippetSources.SpeakingSuggestionSummary
            or KnowledgeSnippetSources.ClassInfoSummary
            or KnowledgeSnippetSources.UserVocabularySummary
            or KnowledgeSnippetSources.ProgressSummary
            or KnowledgeSnippetSources.LearningAreaSummary
            or KnowledgeSnippetSources.GoalSummary
            or KnowledgeSnippetSources.NotificationSummary
            or KnowledgeSnippetSources.BillingSummary
            or KnowledgeSnippetSources.StudyRecommendation;

    private static string MapSourceType(string source)
    {
        return source switch
        {
            KnowledgeSnippetSources.WebsiteGuide => "guide",
            KnowledgeSnippetSources.CardLookupResult => "card",
            KnowledgeSnippetSources.SpeakingSuggestion => "speaking-sentence",
            KnowledgeSnippetSources.ClassInfoSummary or KnowledgeSnippetSources.ClassInfoItem => "class",
            KnowledgeSnippetSources.NotificationSummary or KnowledgeSnippetSources.NotificationItem => "notification",
            KnowledgeSnippetSources.BillingSummary or KnowledgeSnippetSources.BillingOrderItem => "billing",
            KnowledgeSnippetSources.GoalSummary or KnowledgeSnippetSources.GoalAreaItem => "goal",
            KnowledgeSnippetSources.ProgressSummary or KnowledgeSnippetSources.LearningAreaSummary => "progress",
            _ => "guide"
        };
    }

    private static string ResolveVisibility(KnowledgeSnippet snippet)
    {
        var marked = ReadField(snippet.Body, "visibility");
        if (marked is "public" or "authenticated" or "owner" or "class-member" or "owner-premium")
        {
            return marked;
        }

        return MapVisibility(snippet.Source);
    }

    private static string? ReadField(string? body, string key)
    {
        if (string.IsNullOrEmpty(body))
        {
            return null;
        }

        foreach (var part in body.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = part.Split('=', 2);
            if (split.Length == 2 && string.Equals(split[0], key, StringComparison.Ordinal))
            {
                return split[1];
            }
        }

        return null;
    }

    private static string MapVisibility(string source)
    {
        return source switch
        {
            KnowledgeSnippetSources.WebsiteGuide => "authenticated",
            KnowledgeSnippetSources.SpeakingSuggestion => "public",
            KnowledgeSnippetSources.CardLookupResult => "owner",
            KnowledgeSnippetSources.ClassInfoItem or KnowledgeSnippetSources.ClassInfoSummary => "class-member",
            _ => "owner"
        };
    }
}
