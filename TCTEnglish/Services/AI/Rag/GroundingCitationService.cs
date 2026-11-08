using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.AI.Internal.Retrievers;
using TCTVocabulary.Models;
using TCTEnglish.Services.AI.Rag.Corpus;

namespace TCTEnglish.Services.AI.Rag;

public enum GroundingProjectionMode
{
    Display,
    ProviderContext
}

public interface IGroundingCitationService
{
    string Capture(AiChatAnswer answer);

    Task ProjectAsync(int userId, IList<AiMessage> messages, GroundingProjectionMode mode, CancellationToken ct);
}

public static class GroundingNotices
{
    public const string Unavailable = "Nguồn dùng cho câu trả lời này không còn xem được.";

    public const string StaleContext = "Câu trả lời trước dùng nguồn đã thay đổi sau thời điểm ghi nhận. Không dùng nội dung cũ làm dữ liệu hiện tại.";

    public const string UnverifiedContext = "Không xác minh được nguồn của câu trả lời này. Không dùng nội dung cũ làm dữ liệu hiện tại.";

    public static string Changed(DateTime? capturedUtc)
        => capturedUtc.HasValue
            ? $"Nguồn đã thay đổi sau {capturedUtc.Value:yyyy-MM-dd HH:mm} UTC. Câu trả lời dưới đây là nội dung đã ghi tại thời điểm đó, không phải tiến độ hiện tại."
            : "Nguồn đã thay đổi sau thời điểm ghi nhận. Câu trả lời dưới đây là nội dung đã ghi lúc đó, không phải tiến độ hiện tại.";

    public static string Snapshot(DateTime? capturedUtc)
        => capturedUtc.HasValue
            ? $"Số liệu này được ghi tại {capturedUtc.Value:yyyy-MM-dd HH:mm} UTC. Đây không phải tiến độ hay trạng thái hiện tại."
            : "Số liệu này là bản ghi tại thời điểm trả lời. Đây không phải tiến độ hay trạng thái hiện tại.";
}

public static class GroundingHashes
{
    public static string Sha256(string? text)
        => CorpusHash.Sha256(text ?? string.Empty);

    public static string TitleBody(string? title, string? body)
        => Sha256((title ?? string.Empty) + "\n" + (body ?? string.Empty));
}

public sealed class GroundingCaptureException : InvalidOperationException
{
    public GroundingCaptureException(string message)
        : base(message)
    {
    }
}

public sealed class GroundingCitationService : IGroundingCitationService
{
    public const int SchemaVersion = 1;
    public const int MaxEnvelopeChars = 12_000;
    public const int MaxSources = 12;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 8,
        ReadCommentHandling = JsonCommentHandling.Disallow
    };

    private static readonly string[] KnownPrefixes =
    [
        "speaking-sentence:",
        "owner-snapshot:",
        "guide:",
        "reading:",
        "writing:",
        "listening:",
        "speaking:",
        "card:",
        "class:"
    ];

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

    private static readonly HashSet<string> Scopes = new(StringComparer.Ordinal)
    {
        "full",
        "partial",
        "excerpt"
    };

    private readonly DbflashcardContext _context;
    private readonly RetrievalCorpusService _corpus;
    private readonly ILearnerSourceDisplayCache _cache;

    public GroundingCitationService(
        DbflashcardContext context,
        RetrievalCorpusService corpus,
        ILearnerSourceDisplayCache? cache = null)
    {
        _context = context;
        _corpus = corpus;
        _cache = cache ?? LearnerSourceDisplayCache.Disabled;
    }

    public string Capture(AiChatAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var evidenceItems = answer.Evidence ?? [];
        var stored = new List<StoredSource>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var evidence in evidenceItems)
        {
            if (string.IsNullOrWhiteSpace(evidence.SourceKey))
            {
                continue;
            }

            var key = evidence.SourceKey.Trim();
            if (!KnownPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
            {
                continue;
            }

            if (!TryDescribe(key, out var sourceType, out _))
            {
                throw new GroundingCaptureException("Grounding source identity is not valid.");
            }

            var sentenceId = string.IsNullOrWhiteSpace(evidence.SentenceId) ? key + "#s1" : evidence.SentenceId.Trim();
            if (!IsToken(sentenceId, 200))
            {
                throw new GroundingCaptureException("Grounding sentence identity is not valid.");
            }

            if (!seen.Add(key + "\n" + sentenceId))
            {
                continue;
            }

            if (stored.Count == MaxSources)
            {
                throw new GroundingCaptureException("Grounding source list exceeds the supported size.");
            }

            stored.Add(new StoredSource
            {
                SourceKey = key,
                SourceType = sourceType,
                SentenceId = sentenceId,
                ContentHash = HexOrNull(evidence.ContentHash),
                DocumentHash = HexOrNull(evidence.DocumentContentHash),
                SourceVersion = TokenOrNull(evidence.SourceVersion, 80),
                SnapshotId = TokenOrNull(evidence.SnapshotId, 80),
                CorpusVersion = TokenOrNull(evidence.CorpusVersion, 40),
                ChunkerVersion = TokenOrNull(evidence.ChunkerVersion, 40),
                Visibility = TokenOrNull(evidence.Visibility, 40),
                EntityKey = key
            });
        }

        var outcome = ToWire(answer.Outcome);
        var scope = string.IsNullOrWhiteSpace(answer.AnswerScope) ? null : answer.AnswerScope.Trim();
        if (scope is not null && !Scopes.Contains(scope))
        {
            throw new GroundingCaptureException("Grounding scope is not valid.");
        }

        var capturedAt = evidenceItems.Count == 0
            ? DateTime.UtcNow
            : evidenceItems.Min(item => item.CapturedAtUtc == default ? DateTime.UtcNow : item.CapturedAtUtc);
        var envelope = new StoredEnvelope
        {
            SchemaVersion = SchemaVersion,
            Outcome = outcome,
            Scope = scope,
            CapturedAtUtc = DateTime.SpecifyKind(capturedAt, DateTimeKind.Utc),
            Sources = stored
        };
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        if (json.Length > MaxEnvelopeChars)
        {
            throw new GroundingCaptureException("Grounding metadata exceeds the supported size.");
        }

        return json;
    }

    public async Task ProjectAsync(int userId, IList<AiMessage> messages, GroundingProjectionMode mode, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ct.ThrowIfCancellationRequested();
        var accessScope = await DescribeAccessScopeAsync(userId, ct);
        var cache = new Dictionary<string, LiveSource?>(StringComparer.Ordinal);
        foreach (var message in messages)
        {
            ct.ThrowIfCancellationRequested();
            if (message.Role != AiMessageRole.Assistant || string.IsNullOrWhiteSpace(message.GroundingMetadata))
            {
                message.Grounding = null;
                continue;
            }

            if (!TryRead(message.GroundingMetadata, out var envelope, out var sources))
            {
                ApplyUnverified(message, mode);
                continue;
            }

            var live = new List<(StoredSource Source, LiveSource Live)>();
            var revoked = false;
            foreach (var group in sources.GroupBy(source => source.SourceKey, StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                var rows = group.ToList();
                var cacheKey = string.Join(
                    "\n",
                    rows.Select(source => source.SourceKey + "|" + source.SentenceId + "|" + source.DocumentHash + "|" + source.ContentHash));
                if (!cache.TryGetValue(cacheKey, out var inspected))
                {
                    inspected = await InspectGroupAsync(userId, rows, accessScope, ct);
                    cache[cacheKey] = inspected;
                }

                if (inspected is null)
                {
                    revoked = true;
                    break;
                }

                live.Add((rows[0], inspected));
            }

            if (revoked)
            {
                message.Content = GroundingNotices.Unavailable;
                message.Grounding = new AiMessageGrounding
                {
                    SchemaVersion = SchemaVersion,
                    Outcome = envelope.Outcome,
                    Status = "unavailable",
                    Notice = GroundingNotices.Unavailable,
                    CapturedAtUtc = envelope.CapturedAtUtc,
                    Sources = []
                };
                continue;
            }

            var anyChanged = live.Any(item => item.Live.Freshness == "changed");
            var anySnapshot = live.Any(item => item.Live.Freshness == "snapshot");
            if (anyChanged || anySnapshot)
            {
                var notice = anyChanged
                    ? GroundingNotices.Changed(envelope.CapturedAtUtc)
                    : GroundingNotices.Snapshot(envelope.CapturedAtUtc);
                if (mode == GroundingProjectionMode.ProviderContext)
                {
                    message.Content = GroundingNotices.StaleContext;
                    message.Grounding = null;
                }
                else
                {
                    message.Grounding = Build(envelope, anyChanged ? "changed" : "snapshot", notice, live);
                }

                continue;
            }

            if (mode == GroundingProjectionMode.ProviderContext)
            {
                message.Grounding = null;
                continue;
            }

            message.Grounding = Build(envelope, "current", null, live);
        }
    }

    private static AiMessageGrounding Build(
        StoredEnvelope envelope,
        string status,
        string? notice,
        IReadOnlyList<(StoredSource Source, LiveSource Live)> live)
    {
        return new AiMessageGrounding
        {
            SchemaVersion = SchemaVersion,
            Outcome = envelope.Outcome,
            Scope = envelope.Scope,
            CapturedAtUtc = envelope.CapturedAtUtc,
            Status = status,
            Notice = notice,
            Sources = live.Select(item => new AiMessageGroundingSource
            {
                SourceKey = item.Source.SourceKey!,
                SourceType = item.Live.SourceType,
                SentenceId = item.Source.SentenceId ?? string.Empty,
                Title = item.Live.Title,
                Route = item.Live.Route,
                Freshness = item.Live.Freshness
            }).ToList()
        };
    }

    private static void ApplyUnverified(AiMessage message, GroundingProjectionMode mode)
    {
        if (mode == GroundingProjectionMode.ProviderContext)
        {
            message.Content = GroundingNotices.UnverifiedContext;
            message.Grounding = null;
            return;
        }

        message.Grounding = Unverified();
    }

    private async Task<LiveSource?> InspectGroupAsync(int userId, IReadOnlyList<StoredSource> group, string accessScope, CancellationToken ct)
    {
        if (group.Count == 0)
        {
            return null;
        }

        var key = group[0].SourceKey ?? string.Empty;
        var live = await InspectGroupCoreAsync(userId, group, accessScope, ct);
        if (live is null && key.Length > 0)
        {
            _cache.Forget(userId, key);
        }

        return live;
    }

    private async Task<LiveSource?> InspectGroupCoreAsync(int userId, IReadOnlyList<StoredSource> group, string accessScope, CancellationToken ct)
    {
        var key = group[0].SourceKey ?? string.Empty;
        if (!TryDescribe(key, out var sourceType, out var rest))
        {
            return null;
        }

        if (key.StartsWith("owner-snapshot:", StringComparison.Ordinal))
        {
            return await InspectAsync(userId, group[0], accessScope, ct);
        }

        if (key.StartsWith("card:", StringComparison.Ordinal)
            || key.StartsWith("class:", StringComparison.Ordinal)
            || (key.StartsWith("speaking:", StringComparison.Ordinal)
                && !key.StartsWith("speaking-sentence:", StringComparison.Ordinal)))
        {
            LiveSource? selected = null;
            foreach (var source in group)
            {
                var inspected = await InspectAsync(userId, source, accessScope, ct);
                if (inspected is null)
                {
                    return null;
                }

                if (selected is null || inspected.Freshness == "changed")
                {
                    selected = inspected;
                }
            }

            return selected;
        }

        var document = await _corpus.ReadVisibleDocumentAsync(userId, key, ct);
        if (document is null)
        {
            return null;
        }

        var freshness = CorpusGroupFreshness(document, group, ct);
        return Remember(
            userId,
            accessScope,
            key,
            GroundingHashes.TitleBody(document.Title, document.Body),
            sourceType,
            TrimTitle(document.Title),
            document.Route,
            freshness);
    }

    private async Task<LiveSource?> InspectAsync(int userId, StoredSource source, string accessScope, CancellationToken ct)
    {
        var key = source.SourceKey ?? string.Empty;
        if (!TryDescribe(key, out var sourceType, out var rest))
        {
            return null;
        }

        if (key.StartsWith("owner-snapshot:", StringComparison.Ordinal))
        {
            var split = rest.Split('-', 2);
            if (split.Length != 2 || !int.TryParse(split[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ownerId) || ownerId != userId)
            {
                return null;
            }

            return Remember(
                userId,
                accessScope,
                key,
                GroundingHashes.Sha256(split[0] + ":" + ownerId.ToString(CultureInfo.InvariantCulture)),
                sourceType,
                SnapshotTitle(split[0]),
                null,
                "snapshot");
        }

        if (key.StartsWith("card:", StringComparison.Ordinal))
        {
            return await InspectCardAsync(userId, int.Parse(rest, CultureInfo.InvariantCulture), source, accessScope, ct);
        }

        if (key.StartsWith("class:", StringComparison.Ordinal))
        {
            return await InspectClassAsync(userId, int.Parse(rest, CultureInfo.InvariantCulture), source, accessScope, ct);
        }

        if (key.StartsWith("speaking:", StringComparison.Ordinal))
        {
            return await InspectSpeakingVideoAsync(userId, int.Parse(rest, CultureInfo.InvariantCulture), source, accessScope, ct);
        }

        return null;
    }

    private async Task<LiveSource?> InspectCardAsync(int userId, int cardId, StoredSource source, string accessScope, CancellationToken ct)
    {
        var card = await LearnerCardAccess.Authorized(_context, userId)
            .Where(item => item.CardId == cardId)
            .Select(item => new
            {
                item.Term,
                item.Definition,
                item.Phonetic,
                item.Example,
                item.SetId,
                SetName = item.Set.SetName
            })
            .FirstOrDefaultAsync(ct);
        if (card is null)
        {
            return null;
        }

        var body = string.Join('|',
            "setName=" + Clean(card.SetName),
            "definition=" + Clean(card.Definition),
            "phonetic=" + Clean(card.Phonetic),
            "example=" + Clean(card.Example));
        var freshness = Matches(source, card.Term, body) ? "current" : "changed";
        return Remember(
            userId,
            accessScope,
            source.SourceKey ?? string.Empty,
            GroundingHashes.TitleBody(card.Term, body),
            "card",
            TrimTitle(card.Term),
            $"/Home/Study/{card.SetId}",
            freshness);
    }

    private async Task<LiveSource?> InspectClassAsync(int userId, int classId, StoredSource source, string accessScope, CancellationToken ct)
    {
        var row = await _context.Classes.AsNoTracking()
            .Where(item => item.ClassId == classId
                && (item.OwnerId == userId || item.ClassMembers.Any(member => member.UserId == userId)))
            .Select(item => new
            {
                item.ClassName,
                IsOwner = item.OwnerId == userId,
                MemberCount = item.ClassMembers.Count() + 1
            })
            .FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        var body = $"role={(row.IsOwner ? "owner" : "member")}|memberCount={row.MemberCount}";
        var freshness = Matches(source, row.ClassName, body) ? "current" : "changed";
        return Remember(
            userId,
            accessScope,
            source.SourceKey ?? string.Empty,
            GroundingHashes.TitleBody(row.ClassName, body),
            "class",
            TrimTitle(row.ClassName),
            $"/Home/ClassDetail/{classId}",
            freshness);
    }

    private async Task<LiveSource?> InspectSpeakingVideoAsync(int userId, int videoId, StoredSource source, string accessScope, CancellationToken ct)
    {
        var video = await _context.SpeakingVideos.AsNoTracking()
            .Where(item => item.Id == videoId)
            .Select(item => new { item.Title, item.Level, item.Topic, item.OwnerUserId })
            .FirstOrDefaultAsync(ct);
        if (video is null)
        {
            return null;
        }

        var allowed = video.OwnerUserId is null
            || (video.OwnerUserId == userId && await AiLearnerAccess.CanUseOwnPrivateMediaAsync(_context, userId, ct));
        if (!allowed)
        {
            return null;
        }

        var visibility = video.OwnerUserId is null ? "public" : "owner-premium";
        var body = $"level={video.Level}|topic={video.Topic}|visibility={visibility}";
        var freshness = Matches(source, video.Title, body) ? "current" : "changed";
        return Remember(
            userId,
            accessScope,
            source.SourceKey ?? string.Empty,
            GroundingHashes.TitleBody(video.Title, body),
            "speaking",
            TrimTitle(video.Title),
            $"/Speaking/Practice/{videoId}",
            freshness);
    }

    private static string CorpusGroupFreshness(CorpusDocument document, IReadOnlyList<StoredSource> rows, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        PreparedCorpusDocument prepared;
        try
        {
            prepared = CorpusChunker.Prepare(document);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return "changed";
        }

        var titleBody = GroundingHashes.TitleBody(document.Title, document.Body);
        var bodyHash = GroundingHashes.Sha256(document.Body);
        var hasDocumentHash = rows.Any(row => !string.IsNullOrEmpty(row.DocumentHash));
        var documentMatched = rows.Any(row =>
            !string.IsNullOrEmpty(row.DocumentHash)
            && (row.DocumentHash == document.ContentHash || row.DocumentHash == titleBody));
        if (hasDocumentHash && !documentMatched)
        {
            return "changed";
        }

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(row.SentenceId) || string.IsNullOrEmpty(row.ContentHash))
            {
                if (!documentMatched)
                {
                    return "changed";
                }

                continue;
            }

            if (row.ContentHash == document.ContentHash || row.ContentHash == titleBody || row.ContentHash == bodyHash)
            {
                continue;
            }

            var sentence = prepared.Sentences.FirstOrDefault(item => item.SentenceId == row.SentenceId);
            if (sentence is null || !string.Equals(sentence.ContentHash, row.ContentHash, StringComparison.Ordinal))
            {
                return "changed";
            }
        }

        if (!documentMatched)
        {
            var storedIds = rows.Select(row => row.SentenceId).Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal);
            if (prepared.Sentences.Count == 0 || prepared.Sentences.Any(sentence => !storedIds.Contains(sentence.SentenceId)))
            {
                return "changed";
            }
        }

        return "current";
    }

    private static bool Matches(StoredSource source, string? title, string body)
    {
        if (!string.IsNullOrEmpty(source.DocumentHash))
        {
            return source.DocumentHash == GroundingHashes.TitleBody(title, body);
        }

        return !string.IsNullOrEmpty(source.ContentHash) && source.ContentHash == GroundingHashes.Sha256(body);
    }

    private static bool TryRead(string metadata, out StoredEnvelope envelope, out List<StoredSource> sources)
    {
        envelope = new StoredEnvelope();
        sources = [];
        if (metadata.Length > MaxEnvelopeChars)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(metadata, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            JsonElement sourceElements = default;
            var foundSources = false;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals("sources", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (foundSources)
                {
                    return false;
                }

                foundSources = true;
                sourceElements = property.Value;
            }

            if (!foundSources
                || sourceElements.ValueKind != JsonValueKind.Array
                || sourceElements.GetArrayLength() > MaxSources)
            {
                return false;
            }

            foreach (var item in sourceElements.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }
            }

            var parsed = JsonSerializer.Deserialize<StoredEnvelope>(metadata, JsonOptions);
            if (parsed is null || parsed.SchemaVersion != SchemaVersion || !Outcomes.Contains(parsed.Outcome ?? string.Empty))
            {
                return false;
            }

            if (parsed.Scope is not null && !Scopes.Contains(parsed.Scope))
            {
                return false;
            }

            if (parsed.Sources is null)
            {
                return false;
            }

            var list = parsed.Sources;
            if (list.Count > MaxSources)
            {
                return false;
            }

            foreach (var source in list)
            {
                if (source is null || string.IsNullOrWhiteSpace(source.SourceKey) || !TryDescribe(source.SourceKey, out _, out _))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(source.ContentHash) && !IsHex(source.ContentHash))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(source.DocumentHash) && !IsHex(source.DocumentHash))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(source.SentenceId) && !IsToken(source.SentenceId, 200))
                {
                    return false;
                }
            }

            envelope = parsed;
            sources = list;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryDescribe(string key, out string sourceType, out string rest)
    {
        sourceType = string.Empty;
        rest = string.Empty;
        foreach (var prefix in KnownPrefixes)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            rest = key[prefix.Length..];
            sourceType = prefix switch
            {
                "guide:" => "guide",
                "reading:" => "reading-passage",
                "writing:" => "writing-exercise",
                "listening:" => "listening-lesson",
                "speaking-sentence:" => "speaking-sentence",
                "speaking:" => "speaking",
                "card:" => "card",
                "class:" => "class",
                "owner-snapshot:" => "progress",
                _ => string.Empty
            };
            if (prefix == "owner-snapshot:")
            {
                var split = rest.Split('-', 2);
                if (split.Length != 2
                    || split[0] is not ("progress" or "goal" or "billing" or "notification" or "vocabulary")
                    || !int.TryParse(split[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ownerId)
                    || ownerId <= 0)
                {
                    return false;
                }

                sourceType = split[0];
                return true;
            }

            if (prefix == "guide:")
            {
                return rest.Length is > 0 and <= 80 && rest.All(item => char.IsAsciiLetterOrDigit(item) || item == '-');
            }

            return rest.Length is > 0 and <= 12
                && int.TryParse(rest, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                && id > 0
                && id.ToString(CultureInfo.InvariantCulture) == rest;
        }

        return false;
    }

    private static AiMessageGrounding Unverified()
        => new()
        {
            SchemaVersion = 0,
            Status = "unverified",
            Sources = []
        };

    private static string ToWire(HybridRagOutcome outcome)
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
            _ => throw new GroundingCaptureException("Grounding outcome is not valid.")
        };

    private static string? HexOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var hex = value.Trim().ToLowerInvariant();
        if (!IsHex(hex))
        {
            throw new GroundingCaptureException("Grounding hash is not valid.");
        }

        return hex;
    }

    private static string? TokenOrNull(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var token = value.Trim();
        return IsToken(token, max) ? token : null;
    }

    private static bool IsHex(string value)
        => value.Length == 64 && value.All(item => item is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsToken(string value, int max)
        => value.Length is > 0 && value.Length <= max && value.All(item => char.IsAsciiLetterOrDigit(item) || item is ':' or '_' or '-' or '#');

    private static string Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace('|', '/').Replace('=', ':').Trim();

    private static string? TrimTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var trimmed = title.Trim();
        return trimmed.Length <= 180 ? trimmed : trimmed[..180];
    }

    private static string SnapshotTitle(string kind)
        => kind switch
        {
            "goal" => "Mục tiêu đã ghi",
            "billing" => "Gói dịch vụ đã ghi",
            "notification" => "Thông báo đã ghi",
            "vocabulary" => "Bộ từ đã ghi",
            _ => "Tiến độ đã ghi"
        };

    internal static bool IsSafeRoute(string? route)
        => LearnerSourceLink.IsAllowed(route);

    private LiveSource Remember(
        int userId,
        string accessScope,
        string sourceKey,
        string version,
        string sourceType,
        string? title,
        string? route,
        string freshness)
    {
        var safeRoute = IsSafeRoute(route) ? route : null;
        var cached = _cache.TryReuse(userId, accessScope, sourceKey, version, currentlyAuthorized: true);
        if (cached is not null)
        {
            return new LiveSource(
                freshness,
                string.IsNullOrEmpty(cached.SourceType) ? sourceType : cached.SourceType,
                cached.Title,
                cached.Route);
        }

        _cache.Store(userId, accessScope, sourceKey, version, new LearnerSourceDisplay(title, safeRoute, sourceType));
        return new LiveSource(freshness, sourceType, title, safeRoute);
    }

    private async Task<string> DescribeAccessScopeAsync(int userId, CancellationToken ct)
    {
        var role = Roles.Normalize(await _context.Users.AsNoTracking()
            .Where(user => user.UserId == userId)
            .Select(user => user.Role)
            .FirstOrDefaultAsync(ct));
        var memberIds = await _context.ClassMembers.AsNoTracking()
            .Where(member => member.UserId == userId)
            .Select(member => member.ClassId)
            .ToListAsync(ct);
        var ownedIds = await _context.Classes.AsNoTracking()
            .Where(item => item.OwnerId == userId)
            .Select(item => item.ClassId)
            .ToListAsync(ct);
        var snapshot = "none";
        try
        {
            var status = _corpus.ReadStatus();
            snapshot = (status.SnapshotId ?? "none") + ":" + status.Availability;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            snapshot = "unread";
        }

        return string.Join('|',
            role,
            role is Roles.Admin or Roles.Premium ? "private-media" : "shared-media",
            GroundingHashes.Sha256(string.Join(',', memberIds.Order())),
            GroundingHashes.Sha256(string.Join(',', ownedIds.Order())),
            snapshot);
    }

    private sealed class StoredEnvelope
    {
        public int SchemaVersion { get; set; }

        public string? Outcome { get; set; }

        public string? Scope { get; set; }

        public DateTime? CapturedAtUtc { get; set; }

        public List<StoredSource>? Sources { get; set; }
    }

    private sealed class StoredSource
    {
        public string? SourceKey { get; set; }

        public string? SourceType { get; set; }

        public string? SentenceId { get; set; }

        public string? ContentHash { get; set; }

        public string? DocumentHash { get; set; }

        public string? SourceVersion { get; set; }

        public string? SnapshotId { get; set; }

        public string? CorpusVersion { get; set; }

        public string? ChunkerVersion { get; set; }

        public string? Visibility { get; set; }

        public string? EntityKey { get; set; }
    }

    private sealed record LiveSource(string Freshness, string SourceType, string? Title, string? Route);
}
