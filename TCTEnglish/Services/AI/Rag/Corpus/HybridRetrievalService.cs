using System.Text.Json;
using TCTEnglish.Services.AI;

namespace TCTEnglish.Services.AI.Rag.Corpus;

public static class HybridRetrievalModes
{
    public const string Hybrid = "hybrid";
    public const string LexicalDegraded = "lexical_degraded";
    public const string Unavailable = "unavailable";
}

public sealed record HybridRetrievalRequest(bool AllowQueryEmbedding)
{
    public static HybridRetrievalRequest Deferred { get; } = new(false);

    public Func<CancellationToken, Task<bool>>? ConfirmCurrent { get; init; }

    public int EmbeddingAttempts { get; init; } = 2;

    public Func<int, int, CancellationToken, Task<bool>>? RecordUsage { get; init; }
}

public sealed record HybridEvidenceItem(
    string ChunkId,
    string SourceKey,
    string SourceKind,
    string ContentHash,
    string Text,
    string Title,
    double FusedScore,
    int? LexicalRank,
    int? DenseRank)
{
    public string? SentenceId { get; init; }

    public string? SentenceHash { get; init; }

    public string? Route { get; init; }

    public string? Visibility { get; init; }

    public string? CorpusVersion { get; init; }

    public string? ChunkerVersion { get; init; }

    public string? SourceVersion { get; init; }

    public string? EntityKey { get; init; }

    public string? SnapshotId { get; init; }
}

public sealed record HybridRetrievalResult(
    string Mode,
    string Reason,
    IReadOnlyList<HybridEvidenceItem> Evidence,
    bool RecheckRejected)
{
    public bool AllowsUnconstrainedGeneration => false;

    public bool EmbeddingAttempted { get; init; }

    public int EmbeddingPromptTokens { get; init; }

    public int EmbeddingUnknownTokens { get; init; }

    public bool EmbeddingUsageUnknown { get; init; }
}

public readonly record struct GroundedSourceIdentity(string SourceKey, string DocumentHash)
{
    public string? SnapshotId { get; init; }
}

public interface IHybridRetrievalService
{
    Task<HybridRetrievalResult> RetrieveAsync(int userId, string query, CancellationToken ct);

    Task<HybridRetrievalResult> RetrieveAsync(int userId, string query, HybridRetrievalRequest request, CancellationToken ct);

    Task<bool> SourcesStillCurrentAsync(int userId, IReadOnlyList<GroundedSourceIdentity> sources, CancellationToken ct)
        => Task.FromResult(true);
}

public sealed class HybridRetrievalService : IHybridRetrievalService
{
    private readonly RetrievalCorpusService _corpus;
    private readonly ICorpusSnapshotStore _snapshots;
    private readonly IVectorIndexStore _vectors;
    private readonly IOpenAiEmbeddingClient _embeddings;

    public HybridRetrievalService(
        RetrievalCorpusService corpus,
        ICorpusSnapshotStore snapshots,
        IVectorIndexStore vectors,
        IOpenAiEmbeddingClient embeddings)
    {
        _corpus = corpus;
        _snapshots = snapshots;
        _vectors = vectors;
        _embeddings = embeddings;
    }

    public Task<HybridRetrievalResult> RetrieveAsync(int userId, string query, CancellationToken ct)
    {
        return RetrieveAsync(userId, query, HybridRetrievalRequest.Deferred, ct);
    }

    public async Task<HybridRetrievalResult> RetrieveAsync(int userId, string query, HybridRetrievalRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new HybridRetrievalResult(HybridRetrievalModes.Unavailable, "empty-query", [], false);
        }

        var loaded = _snapshots.LoadCurrent();
        if (loaded.Availability != LexicalAvailability.Ready || string.IsNullOrWhiteSpace(loaded.Json))
        {
            return new HybridRetrievalResult(
                HybridRetrievalModes.Unavailable,
                "lexical-" + loaded.Availability.ToString().ToLowerInvariant(),
                [],
                false);
        }

        if (!SnapshotJson.TryValidate(loaded.Json, out var lexicalDetail))
        {
            return new HybridRetrievalResult(HybridRetrievalModes.Unavailable, "lexical-" + lexicalDetail, [], false);
        }

        var snapshot = JsonSerializer.Deserialize<CorpusSnapshotFile>(loaded.Json, SnapshotJson.Options) ?? new CorpusSnapshotFile();
        var chunks = await LiveChunksAsync(userId, CorpusChunkIdentitySet.FromSnapshot(snapshot).Where(Eligible).ToList(), ct);
        if (chunks.Count == 0)
        {
            return new HybridRetrievalResult(HybridRetrievalModes.LexicalDegraded, "source-revoked", [], true);
        }

        var lexicalRanked = LexicalBranch(snapshot, chunks, query);
        var vectors = _vectors.LoadCurrent(loaded.SnapshotId);
        if (vectors.Availability != VectorIndexAvailability.Ready || vectors.Index is null)
        {
            var lexicalOnly = await MaterializeAsync(userId, snapshot, loaded.SnapshotId, chunks, lexicalRanked.Select(ToLexicalFused).ToList(), ct);
            return new HybridRetrievalResult(HybridRetrievalModes.LexicalDegraded, vectors.Detail, lexicalOnly.Evidence, lexicalOnly.Rejected);
        }

        if (!VectorsMatchChunks(vectors.Index, chunks))
        {
            var lexicalOnly = await MaterializeAsync(userId, snapshot, loaded.SnapshotId, chunks, lexicalRanked.Select(ToLexicalFused).ToList(), ct);
            return new HybridRetrievalResult(HybridRetrievalModes.LexicalDegraded, "vector-chunk-mismatch", lexicalOnly.Evidence, lexicalOnly.Rejected);
        }

        if (!request.AllowQueryEmbedding)
        {
            var lexicalOnly = await MaterializeAsync(userId, snapshot, loaded.SnapshotId, chunks, lexicalRanked.Select(ToLexicalFused).ToList(), ct);
            return new HybridRetrievalResult(HybridRetrievalModes.LexicalDegraded, "query-embedding-deferred", lexicalOnly.Evidence, lexicalOnly.Rejected);
        }

        if (query.Length > new EmbeddingOptions().MaxInputCharacters)
        {
            var lexicalOnly = await MaterializeAsync(userId, snapshot, loaded.SnapshotId, chunks, lexicalRanked.Select(ToLexicalFused).ToList(), ct);
            return new HybridRetrievalResult(HybridRetrievalModes.LexicalDegraded, "query-too-long", lexicalOnly.Evidence, lexicalOnly.Rejected);
        }

        var attempts = Math.Clamp(request.EmbeddingAttempts, 1, 3);
        var perAttempt = EmbeddingTokenBound.Upper(query);
        var ledger = new EmbeddingOperationBudget(attempts, attempts == 1 ? perAttempt : checked(perAttempt * attempts));
        EmbeddingBatch embedded;
        try
        {
            try
            {
                embedded = await _embeddings.EmbedAsync([query], new EmbeddingInvocation
                {
                    Budget = ledger,
                    ConfirmCurrent = request.ConfirmCurrent,
                    RecordUsage = request.RecordUsage
                }, ct);
            }
            finally
            {
                // A usable receipt may already exist when cancellation prevents materialization.
                if (request.RecordUsage is not null)
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await request.RecordUsage(ledger.KnownPromptTokens, ledger.UncertainPromptTokenUpperBound, cleanup.Token);
                }
            }
        }
        catch (AiProviderException ex)
        {
            var lexicalOnly = await MaterializeAsync(userId, snapshot, loaded.SnapshotId, chunks, lexicalRanked.Select(ToLexicalFused).ToList(), ct);
            var sent = ledger.Attempts > 0;
            return UsageResult(
                new HybridRetrievalResult(HybridRetrievalModes.LexicalDegraded, "query-embedding-failed:" + ex.ErrorCode, lexicalOnly.Evidence, lexicalOnly.Rejected),
                sent,
                ledger);
        }

        if (embedded.Vectors.Count != 1)
        {
            var lexicalOnly = await MaterializeAsync(userId, snapshot, loaded.SnapshotId, chunks, lexicalRanked.Select(ToLexicalFused).ToList(), ct);
            return UsageResult(
                new HybridRetrievalResult(HybridRetrievalModes.LexicalDegraded, "query-embedding-failed:invalid_response", lexicalOnly.Evidence, lexicalOnly.Rejected),
                true,
                ledger);
        }

        var byId = vectors.Index.Entries.ToDictionary(entry => entry.ChunkId, StringComparer.Ordinal);
        var denseCandidates = chunks
            .Where(chunk => byId.ContainsKey(chunk.ChunkId))
            .Select(chunk => new DenseCandidate(chunk.ChunkId, chunk.SourceKey, byId[chunk.ChunkId].Vector))
            .ToList();
        if (!CosineRanker.TryTopK(denseCandidates, embedded.Vectors[0], CorpusVersions.MaxCandidateCount, out var denseScores, out _))
        {
            var lexicalOnly = await MaterializeAsync(userId, snapshot, loaded.SnapshotId, chunks, lexicalRanked.Select(ToLexicalFused).ToList(), ct);
            return UsageResult(
                new HybridRetrievalResult(HybridRetrievalModes.LexicalDegraded, "query-vector-invalid", lexicalOnly.Evidence, lexicalOnly.Rejected),
                true,
                ledger);
        }

        var denseRanked = denseScores
            .Where(score => score.Score > 0d)
            .Select(score => new RankedChunk(score.ChunkId, score.SourceKey))
            .ToList();
        var fused = ReciprocalRankFusion.DedupSources(ReciprocalRankFusion.Fuse(lexicalRanked, denseRanked));
        var hybrid = await MaterializeAsync(userId, snapshot, loaded.SnapshotId, chunks, fused, ct);
        return UsageResult(
            new HybridRetrievalResult(HybridRetrievalModes.Hybrid, "rrf-k" + ReciprocalRankFusion.K, hybrid.Evidence, hybrid.Rejected),
            true,
            ledger);
    }

    public async Task<bool> SourcesStillCurrentAsync(int userId, IReadOnlyList<GroundedSourceIdentity> sources, CancellationToken ct)
    {
        var expectedSnapshots = sources.Select(source => source.SnapshotId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (expectedSnapshots.Any(id => id != _snapshots.LoadCurrent().SnapshotId)) return false;
        foreach (var source in sources.Where(item => !string.IsNullOrWhiteSpace(item.SourceKey) && !string.IsNullOrWhiteSpace(item.DocumentHash))
                     .DistinctBy(item => item.SourceKey + "\n" + item.DocumentHash))
        {
            var recheck = await _corpus.RecheckSourceAsync(userId, source.SourceKey, source.DocumentHash, ct);
            if (!recheck.Eligible)
            {
                return false;
            }
        }

        return expectedSnapshots.All(id => id == _snapshots.LoadCurrent().SnapshotId);
    }

    private static HybridRetrievalResult UsageResult(HybridRetrievalResult result, bool attempted, EmbeddingOperationBudget ledger)
    {
        var known = Math.Max(0, ledger.KnownPromptTokens);
        var unknown = Math.Max(0, ledger.UncertainPromptTokenUpperBound);
        return result with
        {
            EmbeddingAttempted = attempted,
            EmbeddingPromptTokens = known,
            EmbeddingUnknownTokens = unknown,
            EmbeddingUsageUnknown = unknown > 0 || (attempted && known == 0 && ledger.UsageIncomplete)
        };
    }

    private async Task<List<CorpusChunkIdentity>> LiveChunksAsync(int userId, IReadOnlyList<CorpusChunkIdentity> chunks, CancellationToken ct)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in chunks.GroupBy(chunk => chunk.SourceKey + "\n" + chunk.DocumentContentHash, StringComparer.Ordinal))
        {
            var sample = group.First();
            var recheck = await _corpus.RecheckSourceAsync(userId, sample.SourceKey, sample.DocumentContentHash, ct);
            if (recheck.Eligible)
            {
                allowed.Add(group.Key);
            }
        }

        return chunks.Where(chunk => allowed.Contains(chunk.SourceKey + "\n" + chunk.DocumentContentHash)).ToList();
    }

    private static bool Eligible(CorpusChunkIdentity chunk)
    {
        return CorpusVersions.AllowedSourceKinds.Contains(chunk.SourceKind)
            && chunk.Visibility is "public" or "authenticated"
            && !CorpusVersions.ForbiddenKeyPrefixes.Any(prefix => chunk.SourceKey.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool VectorsMatchChunks(VectorIndexFile index, IReadOnlyList<CorpusChunkIdentity> chunks)
    {
        var entries = index.Entries.ToDictionary(entry => entry.ChunkId, StringComparer.Ordinal);
        if (chunks.Count == 0)
        {
            return false;
        }

        foreach (var chunk in chunks)
        {
            if (!entries.TryGetValue(chunk.ChunkId, out var entry)
                || !string.Equals(entry.SourceKey, chunk.SourceKey, StringComparison.Ordinal)
                || !string.Equals(entry.DocumentContentHash, chunk.DocumentContentHash, StringComparison.Ordinal)
                || !string.Equals(entry.EmbedTextHash, chunk.EmbedTextHash, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<RankedChunk> LexicalBranch(CorpusSnapshotFile snapshot, IReadOnlyList<CorpusChunkIdentity> eligible, string query)
    {
        var allowed = eligible.Select(chunk => chunk.ChunkId).ToHashSet(StringComparer.Ordinal);
        var documents = new List<Bm25FieldDocument>();
        foreach (var entry in snapshot.Documents ?? [])
        {
            var titleTokens = LexicalNormalizer.Unigrams(entry.Title);
            var keywordTokens = LexicalNormalizer.KeywordTokens(entry.Keywords ?? []);
            var sentences = (entry.Sentences ?? []).ToDictionary(sentence => sentence.Number);
            foreach (var chunk in (entry.Chunks ?? []).OrderBy(item => item.Number))
            {
                if (!allowed.Contains(chunk.ChunkId))
                {
                    continue;
                }

                var bodyTokens = new List<string>();
                foreach (var number in chunk.SentenceNumbers ?? [])
                {
                    if (sentences.TryGetValue(number, out var sentence))
                    {
                        bodyTokens.AddRange(LexicalNormalizer.Unigrams(sentence.Text));
                    }
                }

                documents.Add(new Bm25FieldDocument(chunk.ChunkId, entry.SourceKey, titleTokens, keywordTokens, bodyTokens));
            }
        }

        var phrases = documents
            .SelectMany(document => document.KeywordTokens)
            .Where(token => token.StartsWith(LexicalNormalizer.PhrasePrefix, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal);
        var queryTokens = LexicalNormalizer.QueryTokens(query, phrases);
        return Bm25Ranker.Rank(documents, queryTokens)
            .Select(score => new RankedChunk(score.ChunkId, score.SourceKey))
            .ToList();
    }

    private static FusedChunk ToLexicalFused(RankedChunk chunk, int index)
    {
        var rank = index + 1;
        return new FusedChunk(chunk.ChunkId, chunk.SourceKey, 1d / (ReciprocalRankFusion.K + rank), rank, null);
    }

    private async Task<(IReadOnlyList<HybridEvidenceItem> Evidence, bool Rejected)> MaterializeAsync(
        int userId,
        CorpusSnapshotFile snapshot,
        string? snapshotId,
        IReadOnlyList<CorpusChunkIdentity> chunks,
        IReadOnlyList<FusedChunk> fused,
        CancellationToken ct)
    {
        var byId = chunks.ToDictionary(chunk => chunk.ChunkId, StringComparer.Ordinal);
        var documents = (snapshot.Documents ?? []).ToDictionary(
            document => document.SourceKey + "\n" + document.ContentHash,
            StringComparer.Ordinal);
        var evidence = new List<HybridEvidenceItem>();
        var rejected = false;
        foreach (var item in fused)
        {
            ct.ThrowIfCancellationRequested();
            if (!byId.TryGetValue(item.ChunkId, out var chunk))
            {
                rejected = true;
                continue;
            }

            var recheck = await _corpus.RecheckSourceAsync(userId, chunk.SourceKey, chunk.DocumentContentHash, ct);
            if (!recheck.Eligible || recheck.Document is null
                || !documents.TryGetValue(chunk.SourceKey + "\n" + chunk.DocumentContentHash, out var indexed))
            {
                rejected = true;
                continue;
            }

            var snapChunk = (indexed.Chunks ?? []).FirstOrDefault(candidate => candidate.ChunkId == chunk.ChunkId);
            var sentences = new List<CorpusSnapshotSentence>();
            foreach (var number in snapChunk?.SentenceNumbers ?? [])
            {
                var sentence = (indexed.Sentences ?? []).FirstOrDefault(candidate => candidate.Number == number);
                if (sentence is null || !string.Equals(CorpusHash.Sha256(sentence.Text), sentence.ContentHash, StringComparison.Ordinal))
                {
                    rejected = true;
                    continue;
                }

                sentences.Add(sentence);
            }

            if (sentences.Count == 0)
            {
                rejected = true;
                continue;
            }

            foreach (var sentence in sentences)
            {
                evidence.Add(new HybridEvidenceItem(
                    chunk.ChunkId,
                    recheck.Document.SourceKey,
                    recheck.Document.SourceKind,
                    recheck.Document.ContentHash,
                    sentence.Text,
                    recheck.Document.Title,
                    item.Score,
                    item.LexicalRank,
                    item.DenseRank)
                {
                    SentenceId = sentence.SentenceId,
                    SentenceHash = sentence.ContentHash,
                    Route = string.IsNullOrWhiteSpace(recheck.Document.Route) ? null : recheck.Document.Route,
                    Visibility = recheck.Document.Visibility,
                    CorpusVersion = snapshot.CorpusVersion,
                    ChunkerVersion = snapshot.ChunkerVersion,
                    SourceVersion = recheck.Document.SourceVersion,
                    EntityKey = recheck.Document.EntityKey,
                    SnapshotId = snapshotId
                });
                if (evidence.Count == CorpusVersions.MaxResultCount)
                {
                    return (evidence, rejected && evidence.Count == 0);
                }
            }
        }

        return (evidence, rejected && evidence.Count == 0);
    }
}
