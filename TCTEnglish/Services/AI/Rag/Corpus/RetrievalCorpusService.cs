using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Rag.Corpus;

public interface IRetrievalIndexService
{
    Task<RetrievalIndexOperationResult> DryRunAsync(CancellationToken ct);
    Task<RetrievalIndexOperationResult> BuildAsync(string? requestedRoot, CancellationToken ct);
    RetrievalIndexStatus ReadStatus();
}

public interface ILexicalRetrievalService
{
    Task<LexicalRetrievalResult> RetrieveAsync(int userId, string query, CancellationToken ct);
}

public sealed class RetrievalCorpusService : IRetrievalIndexService, ILexicalRetrievalService
{
    private readonly GuideCorpusAdapter _guides;
    private readonly LessonCorpusAdapter _lessons;
    private readonly ICorpusSnapshotStore _store;
    private readonly DbflashcardContext _context;

    public RetrievalCorpusService(string guideFilePath, DbflashcardContext context, ICorpusSnapshotStore store)
    {
        _guides = new GuideCorpusAdapter(guideFilePath);
        _lessons = new LessonCorpusAdapter(context);
        _store = store;
        _context = context;
    }

    public async Task<RetrievalIndexOperationResult> DryRunAsync(CancellationToken ct)
    {
        var collected = await CollectAsync(ct);
        var prepared = collected.Documents.Select(CorpusChunker.Prepare).ToList();
        return new RetrievalIndexOperationResult
        {
            Status = "dry-run",
            DocumentCount = prepared.Count,
            ChunkCount = prepared.Sum(item => item.Chunks.Count),
            ExclusionCount = collected.Exclusions.Count,
            Truncated = collected.Truncated,
            Message = "dry-run",
            Exclusions = collected.Exclusions,
            SourceHashes = prepared.Select(item => item.Document.SourceKey + "=" + item.Document.ContentHash).ToList()
        };
    }

    public async Task<RetrievalIndexOperationResult> BuildAsync(string? requestedRoot, CancellationToken ct)
    {
        if (CorpusPathGuard.IsForeignRequest(requestedRoot))
        {
            return new RetrievalIndexOperationResult { Status = "refused", Message = "foreign-path" };
        }

        if (_store.ApprovedRoot is null)
        {
            return new RetrievalIndexOperationResult { Status = "refused", Message = "unapproved-root" };
        }

        var previous = _store.LoadCurrent();
        try
        {
            var collected = await CollectAsync(ct);
            var prepared = collected.Documents.Select(CorpusChunker.Prepare).ToList();
            var snapshot = CreateSnapshot(prepared);
            var json = JsonSerializer.Serialize(snapshot, SnapshotJson.Options);
            if (!SnapshotJson.TryValidate(json, out var detail))
            {
                return Preserve(previous, "failed", "validation:" + detail, collected);
            }

            var commit = await _store.CommitAsync(new CorpusSnapshotPayload
            {
                Json = json,
                DocumentCount = prepared.Count,
                ChunkCount = prepared.Sum(item => item.Chunks.Count)
            }, ct);
            if (commit.Status != "promoted")
            {
                return Preserve(previous, commit.Status, commit.Message, collected);
            }

            return new RetrievalIndexOperationResult
            {
                Status = "promoted",
                DocumentCount = prepared.Count,
                ChunkCount = prepared.Sum(item => item.Chunks.Count),
                ExclusionCount = collected.Exclusions.Count,
                Truncated = collected.Truncated,
                SnapshotId = commit.SnapshotId,
                ChecksumSha256 = commit.ChecksumSha256,
                SnapshotBytes = commit.SnapshotBytes,
                Message = "promoted",
                Exclusions = collected.Exclusions,
                SourceHashes = prepared.Select(item => item.Document.SourceKey + "=" + item.Document.ContentHash).ToList()
            };
        }
        catch (OperationCanceledException)
        {
            return Preserve(previous, "cancelled", "cancelled-before-promotion", new CorpusCollection());
        }
    }

    public async Task<IReadOnlyList<CorpusChunkIdentity>> ListCurrentChunksAsync(CancellationToken ct)
    {
        var collected = await CollectAsync(ct);
        var prepared = collected.Documents.Select(CorpusChunker.Prepare).ToList();
        return CorpusChunkIdentitySet.FromPrepared(prepared);
    }

    public async Task<CorpusDocument?> ReadVisibleDocumentAsync(int userId, string sourceKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceKey)
            || CorpusVersions.ForbiddenKeyPrefixes.Any(prefix => sourceKey.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return null;
        }

        var live = await ReadLiveAsync(sourceKey, ct);
        if (live is null || !CorpusVersions.AllowedSourceKinds.Contains(live.SourceKind))
        {
            return null;
        }

        return await AllowsAsync(live.Visibility, userId, ct) ? live : null;
    }

    public async Task<SourceRecheck> RecheckSourceAsync(int userId, string sourceKey, string expectedContentHash, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceKey)
            || CorpusVersions.ForbiddenKeyPrefixes.Any(prefix => sourceKey.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return new SourceRecheck(false, null);
        }

        var live = await ReadLiveAsync(sourceKey, ct);
        if (live is null
            || !string.Equals(live.ContentHash, expectedContentHash, StringComparison.Ordinal)
            || !CorpusVersions.AllowedSourceKinds.Contains(live.SourceKind)
            || live.Visibility is not ("public" or "authenticated"))
        {
            return new SourceRecheck(false, null);
        }

        var allowed = await AllowsAsync(live.Visibility, userId, ct);
        return new SourceRecheck(allowed, allowed ? live : null);
    }

    public RetrievalIndexStatus ReadStatus()
    {
        var loaded = _store.LoadCurrent();
        return new RetrievalIndexStatus
        {
            Availability = loaded.Availability,
            SnapshotId = loaded.SnapshotId,
            DocumentCount = loaded.DocumentCount,
            ChunkCount = loaded.ChunkCount,
            PromotedAtUtc = loaded.PromotedAtUtc,
            SnapshotBytes = loaded.SnapshotBytes,
            Detail = loaded.Detail
        };
    }

    public async Task<LexicalRetrievalResult> RetrieveAsync(int userId, string query, CancellationToken ct)
    {
        var loaded = _store.LoadCurrent();
        if (loaded.Availability != LexicalAvailability.Ready || loaded.Json is null)
        {
            return new LexicalRetrievalResult { Availability = loaded.Availability, Detail = loaded.Detail };
        }

        if (!SnapshotJson.TryValidate(loaded.Json, out var detail))
        {
            var availability = detail is "unsupported-schema" or "mixed-version" or "embedding-present" ? LexicalAvailability.Unsupported : LexicalAvailability.Corrupt;
            return new LexicalRetrievalResult { Availability = availability, Detail = detail };
        }

        var snapshot = JsonSerializer.Deserialize<CorpusSnapshotFile>(loaded.Json, SnapshotJson.Options) ?? new CorpusSnapshotFile();
        var indexed = Index(snapshot);
        var phrases = indexed.SelectMany(item => item.Document.KeywordTokens).Where(token => token.StartsWith(LexicalNormalizer.PhrasePrefix, StringComparison.Ordinal)).Distinct();
        var queryTokens = LexicalNormalizer.QueryTokens(query, phrases);
        var ranked = Bm25Ranker.Rank(indexed.Select(item => item.Document).ToList(), queryTokens);
        if (ranked.Count == 0)
        {
            return new LexicalRetrievalResult { Availability = LexicalAvailability.Ready };
        }

        var accepted = new List<LexicalHit>();
        var rejected = false;
        string? tieSource = null;
        double? tieScore = null;
        foreach (var score in ranked)
        {
            ct.ThrowIfCancellationRequested();
            var match = indexed.First(item => item.Document.ChunkId == score.ChunkId);
            var live = await ReadLiveAsync(match.Entry.SourceKey, ct);
            if (live is null || !live.ContentHash.Equals(match.Entry.ContentHash, StringComparison.Ordinal) || !await AllowsAsync(live.Visibility, userId, ct))
            {
                rejected = true;
                continue;
            }

            var hit = ToHit(match, live, score.Score, queryTokens);
            if (hit is null)
            {
                rejected = true;
                continue;
            }

            if (accepted.Count == 0)
            {
                tieSource = hit.SourceKey;
                tieScore = hit.Score;
            }
            else if (tieScore is not null && Bm25Ranker.ScoresTie(tieScore.Value, hit.Score) && !string.Equals(tieSource, hit.SourceKey, StringComparison.Ordinal))
            {
                accepted.Add(hit);
                return new LexicalRetrievalResult
                {
                    Availability = LexicalAvailability.Ready,
                    Hits = accepted,
                    AmbiguousTie = true
                };
            }

            accepted.Add(hit);
            if (accepted.Count == CorpusVersions.MaxResultCount)
            {
                break;
            }
        }

        return new LexicalRetrievalResult
        {
            Availability = LexicalAvailability.Ready,
            Hits = accepted,
            RecheckRejected = accepted.Count == 0 && rejected
        };
    }

    private async Task<CorpusCollection> CollectAsync(CancellationToken ct)
    {
        var guides = await _guides.CollectAsync(ct);
        var lessons = await _lessons.CollectAsync(ct);
        var combined = new CorpusCollection { Truncated = guides.Truncated || lessons.Truncated };
        combined.Documents.AddRange(guides.Documents);
        combined.Documents.AddRange(lessons.Documents);
        combined.Exclusions.AddRange(guides.Exclusions);
        combined.Exclusions.AddRange(lessons.Exclusions);
        if (combined.Documents.Count > CorpusVersions.MaxSourceRows)
        {
            combined.Truncated = true;
            combined.Documents.RemoveRange(CorpusVersions.MaxSourceRows, combined.Documents.Count - CorpusVersions.MaxSourceRows);
        }

        return combined;
    }

    private static CorpusSnapshotFile CreateSnapshot(IReadOnlyList<PreparedCorpusDocument> prepared)
    {
        return new CorpusSnapshotFile
        {
            SchemaVersion = CorpusVersions.SchemaVersion,
            CorpusVersion = CorpusVersions.CorpusVersion,
            ChunkerVersion = CorpusVersions.ChunkerVersion,
            NormalizerVersion = CorpusVersions.NormalizerVersion,
            EmbeddingModel = null,
            EmbeddingDimension = null,
            Bm25K1 = CorpusVersions.Bm25K1,
            Bm25B = CorpusVersions.Bm25B,
            Documents = prepared.Select(item => new CorpusSnapshotEntry
            {
                SourceKind = item.Document.SourceKind,
                SourceKey = item.Document.SourceKey,
                EntityKey = item.Document.EntityKey,
                Title = item.Document.Title,
                Keywords = item.Document.Keywords.ToList(),
                Body = item.Document.Body,
                Route = item.Document.Route,
                Visibility = item.Document.Visibility,
                ContentHash = item.Document.ContentHash,
                SourceVersion = item.Document.SourceVersion,
                Sentences = item.Sentences.Select(sentence => new CorpusSnapshotSentence
                {
                    SentenceId = sentence.SentenceId,
                    Number = sentence.Number,
                    Text = sentence.Text,
                    ContentHash = sentence.ContentHash
                }).ToList(),
                Chunks = item.Chunks.Select(chunk => new CorpusSnapshotChunk
                {
                    ChunkId = chunk.ChunkId,
                    Number = chunk.Number,
                    SentenceNumbers = chunk.SentenceNumbers.ToList()
                }).ToList()
            }).ToList()
        };
    }

    private static List<IndexedChunk> Index(CorpusSnapshotFile snapshot)
    {
        var indexed = new List<IndexedChunk>();
        foreach (var entry in snapshot.Documents)
        {
            var titleTokens = LexicalNormalizer.Unigrams(entry.Title);
            var keywordTokens = LexicalNormalizer.KeywordTokens(entry.Keywords);
            var sentences = entry.Sentences.ToDictionary(sentence => sentence.Number);
            foreach (var chunk in entry.Chunks.OrderBy(chunk => chunk.Number))
            {
                var bodyTokens = new List<string>();
                foreach (var number in chunk.SentenceNumbers)
                {
                    if (sentences.TryGetValue(number, out var sentence))
                    {
                        bodyTokens.AddRange(LexicalNormalizer.Unigrams(sentence.Text));
                    }
                }

                indexed.Add(new IndexedChunk(
                    new Bm25FieldDocument(chunk.ChunkId, entry.SourceKey, titleTokens, keywordTokens, bodyTokens),
                    entry,
                    chunk));
            }
        }

        return indexed;
    }

    private Task<CorpusDocument?> ReadLiveAsync(string sourceKey, CancellationToken ct)
    {
        if (sourceKey.StartsWith("guide:", StringComparison.Ordinal))
        {
            return _guides.ReadAsync(sourceKey, ct);
        }

        return _lessons.ReadAsync(sourceKey, ct);
    }

    private async Task<bool> AllowsAsync(string visibility, int userId, CancellationToken ct)
    {
        if (visibility == "public")
        {
            return true;
        }

        if (visibility != "authenticated" || userId <= 0)
        {
            return false;
        }

        return await _context.Users.AsNoTracking().AnyAsync(user => user.UserId == userId, ct);
    }

    private static LexicalHit? ToHit(IndexedChunk match, CorpusDocument live, double score, IReadOnlyList<string> queryTokens)
    {
        var valid = match.Entry.Sentences
            .Where(item => match.Chunk.SentenceNumbers.Contains(item.Number) && CorpusHash.Sha256(item.Text) == item.ContentHash)
            .Select(item => (Sentence: item, Overlap: QueryOverlap(item.Text, queryTokens)))
            .OrderByDescending(item => item.Overlap)
            .ThenBy(item => item.Sentence.Number)
            .ToList();
        var matched = valid.FirstOrDefault(item => item.Overlap > 0).Sentence;
        if (matched is null && (QueryOverlap(match.Entry.Title, queryTokens) > 0 || match.Entry.Keywords.Any(keyword => QueryOverlap(keyword, queryTokens) > 0)))
        {
            matched = valid.Count == 0 ? null : valid[0].Sentence;
        }

        var text = matched?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return new LexicalHit(
            live.SourceKey,
            MapSourceType(live.SourceKind),
            matched!.SentenceId,
            matched.ContentHash,
            CorpusVersions.CorpusVersion,
            CorpusVersions.ChunkerVersion,
            string.IsNullOrWhiteSpace(live.Route) ? null : live.Route,
            live.Visibility,
            text,
            live.Title,
            score);
    }

    private static int QueryOverlap(string? text, IReadOnlyList<string> queryTokens)
    {
        if (queryTokens.Count == 0 || string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var tokens = LexicalNormalizer.Unigrams(text).ToHashSet(StringComparer.Ordinal);
        var content = 0;
        var framing = 0;
        foreach (var token in queryTokens
            .Where(token => !token.StartsWith(LexicalNormalizer.PhrasePrefix, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal))
        {
            if (!tokens.Contains(token))
            {
                continue;
            }

            if (FramingTokens.Contains(token))
            {
                framing++;
            }
            else
            {
                content++;
            }
        }

        if (content == 0 && framing == 0)
        {
            return 0;
        }

        // One subject token outranks every framing or function token in the question.
        return content * 100 + framing;
    }

    private static readonly HashSet<string> FramingTokens = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "and", "or", "of", "to", "in", "on", "at", "for", "from", "with", "by",
        "is", "are", "was", "were", "be", "do", "does", "did", "can", "could", "would", "should",
        "i", "you", "we", "they", "me", "my", "your", "this", "that", "these", "those", "here", "there",
        "where", "when", "what", "who", "which", "how", "find", "lesson", "example", "examples",
        "please", "tell", "show", "about"
    };

    private static string MapSourceType(string kind)
    {
        return kind switch
        {
            "guide" => "guide",
            "reading" => "reading-passage",
            "writing" => "writing-exercise",
            "listening" => "listening-lesson",
            "speaking" => "speaking-sentence",
            _ => "guide"
        };
    }

    private static RetrievalIndexOperationResult Preserve(CorpusSnapshotLoad previous, string status, string message, CorpusCollection collected)
    {
        return new RetrievalIndexOperationResult
        {
            Status = status,
            Message = message,
            SnapshotId = previous.Availability == LexicalAvailability.Ready ? previous.SnapshotId : null,
            DocumentCount = collected.Documents.Count,
            ExclusionCount = collected.Exclusions.Count,
            Exclusions = collected.Exclusions
        };
    }

    private sealed record IndexedChunk(Bm25FieldDocument Document, CorpusSnapshotEntry Entry, CorpusSnapshotChunk Chunk);
}
