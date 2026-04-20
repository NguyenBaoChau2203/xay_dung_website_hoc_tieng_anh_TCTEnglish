using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;

namespace TCTVocabulary.Services;

public partial class WritingService
{
    private const string PremiumUserSourceType = "premium-user-ai";
    private const string WritingGenerationRequestType = "create-from-ai";
    private const string LevelFallback = "intermediate";
    private const string ContentTypeFallback = "articles";
    private const string TopicFallback = "General";
    private const int PremiumDailyGenerationLimit = 5;
    private const int MinWritingGenerationSourceTextLength = 20;
    private const int MaxWritingGenerationSourceTextLength = 4000;
    private const int MaxWritingGenerationSentenceCount = 80;
    private const int MaxWritingGenerationSentenceTextLength = 2000;
    private const int MaxWritingGenerationTitleLength = 255;
    private const int MaxWritingGenerationTopicLength = 100;
    private const int MaxWritingGenerationPreviewTextLength = 1000;
    private const int WritingGenerationRequestTokenBudget = 12000;
    private const int WritingGenerationMaxOutputTokens = 2400;
    private const int WritingGenerationTimeoutSeconds = 90;
    private static readonly TimeSpan WritingGenerationReplayWindow = TimeSpan.FromMinutes(5);
    private static readonly string[] AllowedWritingGenerationLevels = ["beginner", "intermediate", "advanced"];
    private static readonly string[] AllowedWritingGenerationContentTypes = ["emails", "diaries", "essays", "articles", "stories", "reports"];
    private static readonly ConcurrentDictionary<string, CachedWritingGenerationEntry> WritingGenerationReplayCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> WritingGenerationUserLocks = new();
    private static readonly JsonSerializerOptions WritingGenerationJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string WritingGenerationSystemPrompt = """
        You create bilingual writing exercises for Vietnamese learners of English.
        Return strict JSON only and do not wrap the JSON in markdown.
        Detect whether the source text is Vietnamese ("vi") or English ("en").
        Produce metadata plus a sentence-by-sentence bilingual alignment.
        Keep the original meaning faithful and natural in both languages.
        Use only these enum values for suggestedLevel: beginner, intermediate, advanced.
        Use only these enum values for suggestedContentType: emails, diaries, essays, articles, stories, reports.
        Keep the sentence count reasonable for the source text and never omit a meaningful sentence.
        Set breakAfter to true only when the sentence ends a paragraph.
        Return exactly this JSON shape:
        {
          "detectedSourceLanguage": "vi",
          "suggestedTitle": "string",
          "suggestedTopic": "string",
          "suggestedLevel": "beginner|intermediate|advanced",
          "suggestedContentType": "emails|diaries|essays|articles|stories|reports",
          "previewText": "string",
          "sentences": [
            {
              "vietnameseText": "string",
              "englishMeaning": "string",
              "breakAfter": false
            }
          ]
        }
        """;

    private readonly IAiProviderClient _aiProviderClient;
    private readonly IAiTokenCounter _aiTokenCounter;
    private readonly AiOptions _aiOptions;
    private readonly ILogger<WritingService> _logger;

    public async Task<WritingCreateFromAiResultViewModel> CreateFromAiAsync(
        WritingCreateFromAiRequestViewModel request,
        int userId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedSourceText = NormalizeWritingGenerationSourceText(request.SourceText);
        var validationFailure = ValidateWritingGenerationRequest(normalizedSourceText);
        if (validationFailure is not null)
        {
            await TryPersistWritingGenerationLogAsync(userId, false, validationFailure.ErrorCode);
            return validationFailure;
        }

        CleanupExpiredWritingGenerationEntries();

        var fingerprint = BuildWritingGenerationFingerprint(userId, normalizedSourceText, request.IdempotencyKey);
        var newEntry = new CachedWritingGenerationEntry(
            () => CreateFromAiCoreAsync(userId, normalizedSourceText, ct),
            DateTime.UtcNow.Add(WritingGenerationReplayWindow));
        var cachedEntry = WritingGenerationReplayCache.GetOrAdd(fingerprint, newEntry);
        var ownsEntry = ReferenceEquals(cachedEntry, newEntry);

        try
        {
            var result = await cachedEntry.Operation.Value.WaitAsync(ct);

            if (ownsEntry)
            {
                if (result.Outcome == WritingCreateFromAiOutcome.Success)
                {
                    cachedEntry.ExpiresAtUtc = DateTime.UtcNow.Add(WritingGenerationReplayWindow);
                }
                else
                {
                    WritingGenerationReplayCache.TryRemove(new KeyValuePair<string, CachedWritingGenerationEntry>(fingerprint, cachedEntry));
                }

                return result;
            }

            return result.Outcome == WritingCreateFromAiOutcome.Success
                ? CloneWritingGenerationResult(result, isReplay: true)
                : result;
        }
        catch
        {
            if (ownsEntry)
            {
                WritingGenerationReplayCache.TryRemove(new KeyValuePair<string, CachedWritingGenerationEntry>(fingerprint, cachedEntry));
            }

            throw;
        }
    }

    private async Task<WritingCreateFromAiResultViewModel> CreateFromAiCoreAsync(
        int userId,
        string normalizedSourceText,
        CancellationToken ct)
    {
        var userLock = WritingGenerationUserLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await userLock.WaitAsync(ct);

        try
        {
            var normalizedRole = await GetNormalizedWritingGenerationRoleAsync(userId, ct);
            if (normalizedRole != Roles.Admin && normalizedRole != Roles.Premium)
            {
                await TryPersistWritingGenerationLogAsync(userId, false, "premium_required");
                return BuildForbiddenWritingGenerationResult(
                    "Vui long nang cap len goi Premium de tao bai viet bang AI.",
                    "premium_required");
            }

            if (normalizedRole != Roles.Admin)
            {
                var successfulRequestsToday = await CountSuccessfulWritingGenerationsTodayAsync(userId, ct);
                if (successfulRequestsToday >= PremiumDailyGenerationLimit)
                {
                    var retryAfterSeconds = GetSecondsUntilNextUtcDay();
                    await TryPersistWritingGenerationLogAsync(userId, false, "daily_quota_exceeded");
                    return BuildQuotaExceededWritingGenerationResult(
                        "Ban da dung het gioi han tao bai viet bang AI trong ngay hom nay.",
                        "daily_quota_exceeded",
                        retryAfterSeconds);
                }
            }

            var messages = BuildWritingGenerationMessages(normalizedSourceText);
            var plannedTokens = messages.Sum(message => _aiTokenCounter.CountTokens(message.Content));
            if (plannedTokens > WritingGenerationRequestTokenBudget)
            {
                await TryPersistWritingGenerationLogAsync(userId, false, "request_token_budget_exceeded");
                return BuildInvalidWritingGenerationResult(
                    "Noi dung qua dai cho mot lan tao bai viet. Vui long rut gon bai viet va thu lai.",
                    "request_token_budget_exceeded");
            }

            AiProviderReply aiReply;
            try
            {
                aiReply = await _aiProviderClient.GenerateReplyAsync(
                    userId,
                    messages,
                    ct,
                    new AiProviderRequestOptions
                    {
                        MaxOutputTokens = WritingGenerationMaxOutputTokens,
                        RequestTimeoutSeconds = WritingGenerationTimeoutSeconds,
                        Temperature = 0.2
                    });
            }
            catch (AiProviderException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Writing generation provider request failed for user {UserId} with error code {ErrorCode}.",
                    userId,
                    ex.ErrorCode);
                await TryPersistWritingGenerationLogAsync(userId, false, ex.ErrorCode);
                return BuildFailedWritingGenerationResult(
                    ResolveWritingGenerationProviderErrorMessage(ex.ErrorCode),
                    ex.ErrorCode);
            }

            RawWritingGenerationPayload rawPayload;
            try
            {
                rawPayload = ParseWritingGenerationPayload(aiReply.Text);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Writing generation provider returned invalid JSON for user {UserId}.",
                    userId);
                await TryPersistWritingGenerationLogAsync(userId, false, "invalid_ai_json");
                return BuildFailedWritingGenerationResult(
                    "AI tra ve du lieu khong hop le. Vui long thu lai sau.",
                    "invalid_ai_json");
            }

            var normalizedPayload = NormalizeWritingGenerationPayload(rawPayload, normalizedSourceText);
            var payloadValidationError = ValidateWritingGenerationPayload(normalizedPayload);
            if (payloadValidationError is not null)
            {
                await TryPersistWritingGenerationLogAsync(userId, false, payloadValidationError);
                return BuildFailedWritingGenerationResult(
                    "AI tra ve noi dung chua du de tao bai viet. Vui long thu lai sau.",
                    payloadValidationError);
            }

            try
            {
                var persistedExercise = await PersistGeneratedWritingExerciseAsync(userId, normalizedPayload, ct);

                _logger.LogInformation(
                    "Writing generation created exercise {ExerciseId} for user {UserId}. SentenceCount {SentenceCount}. Model {Model}. PromptTokens {PromptTokens}. CompletionTokens {CompletionTokens}. TotalTokens {TotalTokens}.",
                    persistedExercise.ExerciseId,
                    userId,
                    persistedExercise.SentenceCount,
                    aiReply.Model,
                    aiReply.PromptTokens,
                    aiReply.CompletionTokens,
                    aiReply.TotalTokens);

                return new WritingCreateFromAiResultViewModel
                {
                    Outcome = WritingCreateFromAiOutcome.Success,
                    ExerciseId = persistedExercise.ExerciseId,
                    SentenceCount = persistedExercise.SentenceCount,
                    Level = normalizedPayload.Level,
                    ContentType = normalizedPayload.ContentType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Writing generation failed to persist data for user {UserId}.",
                    userId);
                _context.ChangeTracker.Clear();
                await TryPersistWritingGenerationLogAsync(userId, false, "persistence_failed");
                return BuildFailedWritingGenerationResult(
                    "Khong the luu bai viet luc nay. Vui long thu lai sau.",
                    "persistence_failed");
            }
        }
        finally
        {
            userLock.Release();
        }
    }

    private async Task<PersistedWritingExercise> PersistGeneratedWritingExerciseAsync(
        int userId,
        NormalizedWritingGenerationPayload payload,
        CancellationToken ct)
    {
        var createdAtUtc = DateTime.UtcNow;
        var createdExerciseId = 0;
        var sentenceCount = payload.Sentences.Count;

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            var exercise = new WritingExercise
            {
                UserId = userId,
                Title = payload.Title,
                Level = payload.Level,
                ContentType = payload.ContentType,
                Topic = payload.Topic,
                SourceType = PremiumUserSourceType,
                PreviewText = payload.PreviewText,
                IsPublished = false,
                CreatedAt = createdAtUtc
            };

            _context.WritingExercises.Add(exercise);
            await _context.SaveChangesAsync(ct);

            var sentences = payload.Sentences
                .Select((sentence, index) => new WritingExerciseSentence
                {
                    WritingExerciseId = exercise.Id,
                    SortOrder = index + 1,
                    VietnameseText = sentence.VietnameseText,
                    EnglishMeaning = sentence.EnglishMeaning,
                    BreakAfter = sentence.BreakAfter
                })
                .ToList();

            _context.WritingExerciseSentences.AddRange(sentences);
            _context.WritingGenerationLogs.Add(new WritingGenerationLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RequestType = WritingGenerationRequestType,
                IsSuccess = true,
                RequestedAtUtc = createdAtUtc
            });

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            createdExerciseId = exercise.Id;
        });

        return new PersistedWritingExercise(createdExerciseId, sentenceCount);
    }

    private async Task<string> GetNormalizedWritingGenerationRoleAsync(int userId, CancellationToken ct)
    {
        var role = await _context.Users
            .AsNoTracking()
            .Where(user => user.UserId == userId)
            .Select(user => user.Role)
            .FirstOrDefaultAsync(ct);

        return Roles.Normalize(role);
    }

    private async Task<int> CountSuccessfulWritingGenerationsTodayAsync(int userId, CancellationToken ct)
    {
        return await _context.WritingGenerationLogs
            .AsNoTracking()
            .CountAsync(log => log.UserId == userId
                && log.RequestType == WritingGenerationRequestType
                && log.IsSuccess
                && log.RequestedAtUtc >= DateTime.UtcNow.Date, ct);
    }

    private async Task TryPersistWritingGenerationLogAsync(int userId, bool isSuccess, string? errorCode)
    {
        try
        {
            _context.WritingGenerationLogs.Add(new WritingGenerationLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RequestType = WritingGenerationRequestType,
                IsSuccess = isSuccess,
                ErrorCode = string.IsNullOrWhiteSpace(errorCode)
                    ? null
                    : TruncateText(errorCode, 100),
                RequestedAtUtc = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _context.ChangeTracker.Clear();
            _logger.LogWarning(
                ex,
                "Unable to persist writing generation log for user {UserId} with error code {ErrorCode}.",
                userId,
                errorCode);
        }
    }

    private static WritingCreateFromAiResultViewModel? ValidateWritingGenerationRequest(string normalizedSourceText)
    {
        if (string.IsNullOrWhiteSpace(normalizedSourceText))
        {
            return BuildInvalidWritingGenerationResult(
                "Vui long nhap noi dung bai viet truoc khi tao bang AI.",
                "source_text_required");
        }

        if (normalizedSourceText.Length < MinWritingGenerationSourceTextLength)
        {
            return BuildInvalidWritingGenerationResult(
                "Noi dung qua ngan de tao bai viet. Vui long nhap day du hon.",
                "source_text_too_short");
        }

        if (normalizedSourceText.Length > MaxWritingGenerationSourceTextLength)
        {
            return BuildInvalidWritingGenerationResult(
                $"Noi dung vuot qua gioi han {MaxWritingGenerationSourceTextLength} ky tu.",
                "source_text_too_long");
        }

        if (!normalizedSourceText.Any(char.IsLetterOrDigit))
        {
            return BuildInvalidWritingGenerationResult(
                "Noi dung bai viet khong hop le. Vui long nhap van ban co y nghia.",
                "source_text_invalid");
        }

        return null;
    }

    private static IReadOnlyList<AiContextMessage> BuildWritingGenerationMessages(string sourceText)
    {
        return
        [
            new AiContextMessage("system", WritingGenerationSystemPrompt),
            new AiContextMessage("user", BuildWritingGenerationUserPrompt(sourceText))
        ];
    }

    private static string BuildWritingGenerationUserPrompt(string sourceText)
    {
        return $"""
            Create a bilingual writing exercise from this source text.

            Source text:
            {sourceText}

            Requirements:
            - Detect whether the source text is Vietnamese ("vi") or English ("en").
            - If the source text is Vietnamese, keep each sentence in "vietnameseText" and translate it into natural English in "englishMeaning".
            - If the source text is English, translate each sentence into Vietnamese in "vietnameseText" and keep a natural English sentence in "englishMeaning".
            - Preserve sentence order and keep the alignment faithful.
            - Suggest a concise title, a topic, a level, a content type, and a short preview.
            - Keep sentence count reasonable and do not exceed 80 sentences.
            - Set breakAfter to true only for the final sentence in a paragraph.
            - Return strict JSON only with no markdown and no commentary.
            """;
    }

    private static RawWritingGenerationPayload ParseWritingGenerationPayload(string aiContent)
    {
        var normalizedJson = NormalizeWritingGenerationJson(aiContent);
        var payload = JsonSerializer.Deserialize<RawWritingGenerationPayload>(normalizedJson, WritingGenerationJsonOptions)
            ?? throw new JsonException("Writing generation payload is empty.");

        if (payload.Sentences == null)
        {
            throw new JsonException("Writing generation payload is missing sentences.");
        }

        return payload;
    }

    private static NormalizedWritingGenerationPayload NormalizeWritingGenerationPayload(
        RawWritingGenerationPayload rawPayload,
        string normalizedSourceText)
    {
        var normalizedSentences = rawPayload.Sentences!
            .Select(sentence => new NormalizedWritingGenerationSentence(
                CollapseWhitespace(sentence.VietnameseText),
                CollapseWhitespace(sentence.EnglishMeaning),
                sentence.BreakAfter ?? false))
            .ToList();

        var previewFallback = normalizedSentences
            .Select(sentence => sentence.VietnameseText)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
            ?? CollapseWhitespace(normalizedSourceText);

        var titleFallback = BuildWritingGenerationTitleFallback(normalizedSentences, normalizedSourceText);

        return new NormalizedWritingGenerationPayload(
            Title: NormalizeWritingGenerationMetadata(rawPayload.SuggestedTitle, titleFallback, MaxWritingGenerationTitleLength),
            Topic: NormalizeWritingGenerationMetadata(rawPayload.SuggestedTopic, TopicFallback, MaxWritingGenerationTopicLength),
            Level: NormalizeWritingGenerationEnum(rawPayload.SuggestedLevel, AllowedWritingGenerationLevels, LevelFallback),
            ContentType: NormalizeWritingGenerationEnum(rawPayload.SuggestedContentType, AllowedWritingGenerationContentTypes, ContentTypeFallback),
            PreviewText: NormalizeWritingGenerationMetadata(rawPayload.PreviewText, previewFallback, MaxWritingGenerationPreviewTextLength),
            Sentences: normalizedSentences);
    }

    private static string? ValidateWritingGenerationPayload(NormalizedWritingGenerationPayload payload)
    {
        if (payload.Sentences.Count == 0)
        {
            return "invalid_ai_payload";
        }

        if (payload.Sentences.Count > MaxWritingGenerationSentenceCount)
        {
            return "ai_sentence_limit_exceeded";
        }

        foreach (var sentence in payload.Sentences)
        {
            if (string.IsNullOrWhiteSpace(sentence.VietnameseText)
                || string.IsNullOrWhiteSpace(sentence.EnglishMeaning))
            {
                return "invalid_ai_payload";
            }

            if (sentence.VietnameseText.Length > MaxWritingGenerationSentenceTextLength
                || sentence.EnglishMeaning.Length > MaxWritingGenerationSentenceTextLength)
            {
                return "ai_sentence_too_long";
            }
        }

        return null;
    }

    private static string NormalizeWritingGenerationSourceText(string? sourceText)
    {
        return (sourceText ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();
    }

    private static string NormalizeWritingGenerationJson(string aiContent)
    {
        var trimmedContent = aiContent?.Trim() ?? string.Empty;
        if (!trimmedContent.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmedContent;
        }

        var firstLineBreakIndex = trimmedContent.IndexOf('\n');
        if (firstLineBreakIndex < 0)
        {
            return trimmedContent;
        }

        var contentBody = trimmedContent[(firstLineBreakIndex + 1)..].Trim();
        if (contentBody.EndsWith("```", StringComparison.Ordinal))
        {
            contentBody = contentBody[..^3].Trim();
        }

        return contentBody;
    }

    private static string NormalizeWritingGenerationMetadata(string? value, string fallback, int maxLength)
    {
        var normalizedValue = CollapseWhitespace(value);
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            normalizedValue = CollapseWhitespace(fallback);
        }

        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            normalizedValue = "Writing exercise";
        }

        return TruncateText(normalizedValue, maxLength);
    }

    private static string NormalizeWritingGenerationEnum(
        string? value,
        IEnumerable<string> allowedValues,
        string fallback)
    {
        var normalizedValue = CollapseWhitespace(value).ToLowerInvariant();
        return allowedValues.Contains(normalizedValue, StringComparer.OrdinalIgnoreCase)
            ? normalizedValue
            : fallback;
    }

    private static string BuildWritingGenerationTitleFallback(
        IEnumerable<NormalizedWritingGenerationSentence> sentences,
        string normalizedSourceText)
    {
        var firstSentence = sentences
            .Select(sentence => sentence.EnglishMeaning)
            .Concat(sentences.Select(sentence => sentence.VietnameseText))
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

        var fallback = string.IsNullOrWhiteSpace(firstSentence)
            ? CollapseWhitespace(normalizedSourceText)
            : firstSentence;

        fallback = CollapseWhitespace(fallback);
        return fallback.Length <= 80 ? fallback : fallback[..80].Trim();
    }

    private static WritingCreateFromAiResultViewModel BuildInvalidWritingGenerationResult(string errorMessage, string errorCode)
    {
        return new WritingCreateFromAiResultViewModel
        {
            Outcome = WritingCreateFromAiOutcome.Invalid,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode
        };
    }

    private static WritingCreateFromAiResultViewModel BuildForbiddenWritingGenerationResult(string errorMessage, string errorCode)
    {
        return new WritingCreateFromAiResultViewModel
        {
            Outcome = WritingCreateFromAiOutcome.Forbidden,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode
        };
    }

    private static WritingCreateFromAiResultViewModel BuildQuotaExceededWritingGenerationResult(
        string errorMessage,
        string errorCode,
        int retryAfterSeconds)
    {
        return new WritingCreateFromAiResultViewModel
        {
            Outcome = WritingCreateFromAiOutcome.QuotaExceeded,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            RetryAfterSeconds = retryAfterSeconds
        };
    }

    private static WritingCreateFromAiResultViewModel BuildFailedWritingGenerationResult(string errorMessage, string errorCode)
    {
        return new WritingCreateFromAiResultViewModel
        {
            Outcome = WritingCreateFromAiOutcome.Failed,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode
        };
    }

    private static WritingCreateFromAiResultViewModel CloneWritingGenerationResult(
        WritingCreateFromAiResultViewModel source,
        bool isReplay)
    {
        return new WritingCreateFromAiResultViewModel
        {
            Outcome = source.Outcome,
            ExerciseId = source.ExerciseId,
            SentenceCount = source.SentenceCount,
            Level = source.Level,
            ContentType = source.ContentType,
            ErrorCode = source.ErrorCode,
            ErrorMessage = source.ErrorMessage,
            RetryAfterSeconds = source.RetryAfterSeconds,
            IsReplay = isReplay
        };
    }

    private static string BuildWritingGenerationFingerprint(int userId, string normalizedSourceText, string? idempotencyKey)
    {
        var normalizedIdempotencyKey = CollapseWhitespace(idempotencyKey);
        var rawFingerprint = string.IsNullOrWhiteSpace(normalizedIdempotencyKey)
            ? $"{userId}\n{normalizedSourceText}"
            : $"{userId}\n{normalizedIdempotencyKey}\n{normalizedSourceText}";

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawFingerprint)));
    }

    private static void CleanupExpiredWritingGenerationEntries()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in WritingGenerationReplayCache)
        {
            if (entry.Value.ExpiresAtUtc <= now)
            {
                WritingGenerationReplayCache.TryRemove(entry.Key, out _);
            }
        }
    }

    private static int GetSecondsUntilNextUtcDay()
    {
        var utcNow = DateTime.UtcNow;
        var nextUtcDay = utcNow.Date.AddDays(1);
        return Math.Max(1, (int)Math.Ceiling((nextUtcDay - utcNow).TotalSeconds));
    }

    private static string ResolveWritingGenerationProviderErrorMessage(string errorCode)
    {
        return errorCode switch
        {
            AiProviderException.ErrorCodeTimeout => "AI dang mat nhieu thoi gian hon du kien. Vui long thu lai sau.",
            AiProviderException.ErrorCodeRateLimited => "AI dang ban. Vui long cho mot chut roi thu lai.",
            AiProviderException.ErrorCodeProviderUnavailable => "AI tam thoi khong san sang. Vui long thu lai sau.",
            AiProviderException.ErrorCodeAuthentication => "He thong AI dang gap loi cau hinh. Vui long lien he quan tri vien.",
            _ => "Khong the tao bai viet bang AI luc nay. Vui long thu lai sau."
        };
    }

    private static string TruncateText(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed class CachedWritingGenerationEntry
    {
        public CachedWritingGenerationEntry(
            Func<Task<WritingCreateFromAiResultViewModel>> operationFactory,
            DateTime expiresAtUtc)
        {
            Operation = new Lazy<Task<WritingCreateFromAiResultViewModel>>(
                operationFactory,
                LazyThreadSafetyMode.ExecutionAndPublication);
            ExpiresAtUtc = expiresAtUtc;
        }

        public Lazy<Task<WritingCreateFromAiResultViewModel>> Operation { get; }

        public DateTime ExpiresAtUtc { get; set; }
    }

    private sealed record PersistedWritingExercise(int ExerciseId, int SentenceCount);

    private sealed record NormalizedWritingGenerationPayload(
        string Title,
        string Topic,
        string Level,
        string ContentType,
        string PreviewText,
        List<NormalizedWritingGenerationSentence> Sentences);

    private sealed record NormalizedWritingGenerationSentence(
        string VietnameseText,
        string EnglishMeaning,
        bool BreakAfter);

    private sealed class RawWritingGenerationPayload
    {
        public string? SuggestedTitle { get; set; }

        public string? SuggestedTopic { get; set; }

        public string? SuggestedLevel { get; set; }

        public string? SuggestedContentType { get; set; }

        public string? PreviewText { get; set; }

        public List<RawWritingGenerationSentencePayload>? Sentences { get; set; }
    }

    private sealed class RawWritingGenerationSentencePayload
    {
        public string? VietnameseText { get; set; }

        public string? EnglishMeaning { get; set; }

        public bool? BreakAfter { get; set; }
    }
}
