using System.Text.Json;
using TCTEnglish.Services.AI;

namespace TCTEnglish.Services.AI.Rag.Corpus;

public interface IVectorIndexService
{
    Task<VectorIndexOperationResult> DryRunAsync(CancellationToken ct);
    Task<VectorIndexOperationResult> BuildAsync(EmbeddingBudget budget, CancellationToken ct);
    VectorIndexStatus ReadStatus();
}

public sealed class VectorIndexOperationResult
{
    public string Status { get; init; } = "failed";
    public string Message { get; init; } = string.Empty;
    public int ChunkCount { get; init; }
    public int CacheHits { get; init; }
    public int CacheMisses { get; init; }
    public int EstimatedInputTokens { get; init; }
    public int EstimatedRequests { get; init; }
    public int ActualPromptTokens { get; init; }
    public int ApiCalls { get; init; }
    public int SafeInputTokenUpperBound { get; init; }
    public bool PromptUsageComplete { get; init; } = true;
    public long IndexBytes { get; init; }
    public string? LexicalSnapshotId { get; init; }
}

public sealed class VectorIndexStatus
{
    public string State { get; init; } = "missing";
    public string Detail { get; init; } = string.Empty;
    public string? IndexId { get; init; }
    public string? LexicalSnapshotId { get; init; }
    public int ChunkCount { get; init; }
    public long IndexBytes { get; init; }
    public string Model { get; init; } = EmbeddingOptions.ModelName;
    public int Dimensions { get; init; } = EmbeddingOptions.Dimensions;
}

public sealed class VectorIndexService : IVectorIndexService
{
    private readonly RetrievalCorpusService _corpus;
    private readonly ICorpusSnapshotStore _snapshots;
    private readonly IVectorIndexStore _vectors;
    private readonly IOpenAiEmbeddingClient _embeddings;
    private readonly ILogger<VectorIndexService> _logger;
    private readonly IVectorIndexAdmission _admission;
    private readonly TimeSpan _admissionWait;

    public VectorIndexService(
        RetrievalCorpusService corpus,
        ICorpusSnapshotStore snapshots,
        IVectorIndexStore vectors,
        IOpenAiEmbeddingClient embeddings,
        ILogger<VectorIndexService> logger,
        TimeSpan? admissionWait = null,
        IVectorIndexAdmission? admission = null)
    {
        _corpus = corpus;
        _snapshots = snapshots;
        _vectors = vectors;
        _embeddings = embeddings;
        _logger = logger;
        _admissionWait = admissionWait ?? TimeSpan.FromSeconds(5);
        _admission = admission ?? VectorIndexAdmission.Shared;
    }

    public async Task<VectorIndexOperationResult> DryRunAsync(CancellationToken ct)
    {
        var plan = await PlanAsync(ct);
        if (plan.Error is not null)
        {
            return plan.Error;
        }

        return Result("embed-dry-run", "embed-dry-run", plan);
    }

    public async Task<VectorIndexOperationResult> BuildAsync(EmbeddingBudget budget, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(budget);
        if (_vectors.ApprovedRoot is null)
        {
            return new VectorIndexOperationResult { Status = "refused", Message = "unapproved-root" };
        }

        var root = _vectors.ApprovedRoot;
        var acquired = false;
        VectorPlan? plan = null;
        EmbeddingOperationBudget? ledger = null;
        try
        {
            acquired = await _admission.TryAcquireAsync(root, _admissionWait, ct);
            if (!acquired)
            {
                return new VectorIndexOperationResult { Status = "busy", Message = "vector-build-in-progress" };
            }

            plan = await PlanAsync(ct);
            if (plan.Error is not null)
            {
                return plan.Error;
            }

            ledger = new EmbeddingOperationBudget(budget.MaxRequests, budget.MaxInputTokens);
            if (plan.Misses.Any(item => EmbeddingTokenBound.Upper(item.EmbedText) > budget.MaxInputTokens))
            {
                return Result("rejected", "budget-exceeded", plan, ledger);
            }

            var pending = plan.Misses.ToList();
            var offset = 0;
            while (offset < pending.Count)
            {
                ct.ThrowIfCancellationRequested();
                var batch = TakeAffordableBatch(pending, offset, ledger);
                if (batch.Count == 0)
                {
                    // Completed chunks stay cached for the next explicit job.
                    // The current valid index is published only when all chunks exist.
                    return Result(ledger.Attempts == 0 ? "rejected" : "partial",
                        "budget-paused remainingChunks=" + (pending.Count - offset), plan, ledger);
                }

                var drift = await DriftAsync(plan.LexicalSnapshotId, plan.Chunks, ct);
                if (drift is not null)
                {
                    return Result("failed", drift, plan, ledger);
                }

                EmbeddingBatch embedded;
                try
                {
                    embedded = await _embeddings.EmbedAsync(
                        batch.Select(item => item.EmbedText).ToList(),
                        new EmbeddingInvocation
                        {
                            Budget = ledger,
                            ConfirmCurrent = async token => await DriftAsync(plan.LexicalSnapshotId, plan.Chunks, token) is null
                        },
                        ct);
                }
                catch (AiProviderException ex)
                {
                    _logger.LogWarning("Vector rebuild stopped after an embeddings error {Code}.", ex.ErrorCode);
                    var reason = ex.ErrorCode == AiProviderException.ErrorCodeCorpusStale
                        ? "corpus-drift"
                        : "embedding-" + ex.ErrorCode;
                    return Result("failed", reason, plan, ledger);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return Result("cancelled", "cancelled", plan, ledger);
                }

                if (ct.IsCancellationRequested)
                {
                    return Result("cancelled", "cancelled", plan, ledger);
                }

                if (embedded.Vectors.Count != batch.Count
                    || embedded.PromptTokens != embedded.TotalTokens
                    || embedded.PromptTokens < 1)
                {
                    ledger.MarkUsageUnknown();
                    return Result("failed", "embedding-invalid-response", plan, ledger);
                }

                if (ledger.KnownPromptTokens > budget.MaxInputTokens)
                {
                    return Result("failed", "usage-exceeded", plan, ledger);
                }

                for (var index = 0; index < batch.Count; index++)
                {
                    if (!VectorIndexStore.VectorIsUsable(embedded.Vectors[index]))
                    {
                        return Result("failed", "embedding-invalid-response", plan, ledger);
                    }

                    await _vectors.WriteCacheAsync(batch[index].EmbedTextHash, embedded.Vectors[index], ct);
                }

                offset += batch.Count;
            }

            var beforePromotion = await DriftAsync(plan.LexicalSnapshotId, plan.Chunks, ct);
            if (beforePromotion is not null)
            {
                return Result("failed", beforePromotion, plan, ledger);
            }

            var entries = new List<VectorIndexEntry>(plan.Chunks.Count);
            foreach (var chunk in plan.Chunks.OrderBy(item => item.ChunkId, StringComparer.Ordinal))
            {
                if (!_vectors.TryReadCache(chunk.EmbedTextHash, out var vector))
                {
                    return Result("failed", "cache-missing-before-promotion", plan, ledger);
                }

                entries.Add(new VectorIndexEntry
                {
                    ChunkId = chunk.ChunkId,
                    SourceKey = chunk.SourceKey,
                    DocumentContentHash = chunk.DocumentContentHash,
                    EmbedTextHash = chunk.EmbedTextHash,
                    Vector = vector
                });
            }

            var indexFile = new VectorIndexFile
            {
                SchemaVersion = VectorIndexStore.SchemaVersion,
                LexicalSnapshotId = plan.LexicalSnapshotId!,
                Model = EmbeddingOptions.ModelName,
                Dimensions = EmbeddingOptions.Dimensions,
                ChunkerVersion = CorpusVersions.ChunkerVersion,
                Entries = entries
            };
            var commit = await _vectors.CommitAsync(new VectorIndexPayload
            {
                Json = JsonSerializer.Serialize(indexFile, SnapshotJson.Options),
                LexicalSnapshotId = plan.LexicalSnapshotId!,
                ChunkCount = entries.Count
            }, ct);
            _logger.LogInformation(
                "Vector index {Status}: chunks {Chunks}, cache hits {Hits}, misses {Misses}, api calls {Calls}, prompt tokens {Tokens}.",
                commit.Status,
                entries.Count,
                plan.Hits,
                plan.Misses.Count,
                ledger.Attempts,
                ledger.KnownPromptTokens);
            var promoted = Result(commit.Status, commit.Message, plan, ledger);
            return new VectorIndexOperationResult
            {
                Status = promoted.Status,
                Message = promoted.Message,
                ChunkCount = entries.Count,
                CacheHits = plan.Hits,
                CacheMisses = plan.Misses.Count,
                EstimatedInputTokens = plan.EstimatedInputTokens,
                EstimatedRequests = plan.EstimatedRequests,
                SafeInputTokenUpperBound = plan.SafeInputTokenUpperBound,
                ActualPromptTokens = ledger.KnownPromptTokens,
                ApiCalls = ledger.Attempts,
                PromptUsageComplete = !ledger.UsageIncomplete,
                IndexBytes = commit.IndexBytes,
                LexicalSnapshotId = plan.LexicalSnapshotId
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            ledger?.MarkPendingUsageUnknown();
            return plan is null
                ? new VectorIndexOperationResult { Status = "cancelled", Message = "cancelled" }
                : Result("cancelled", "cancelled", plan, ledger);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Data.Common.DbException)
        {
            ledger?.MarkPendingUsageUnknown();
            _logger.LogWarning("Vector maintenance failed with {FailureType}.", ex.GetType().Name);
            return plan is null
                ? new VectorIndexOperationResult { Status = "failed", Message = "index-maintenance-failed" }
                : Result("failed", "index-maintenance-failed", plan, ledger);
        }
        finally
        {
            if (acquired)
            {
                _admission.Release(root);
            }
        }
    }

    public VectorIndexStatus ReadStatus()
    {
        var lexical = _snapshots.LoadCurrent();
        var vectors = _vectors.LoadCurrent(lexical.SnapshotId);
        return new VectorIndexStatus
        {
            State = vectors.Availability.ToString().ToLowerInvariant(),
            Detail = vectors.Detail,
            IndexId = vectors.IndexId,
            LexicalSnapshotId = vectors.LexicalSnapshotId,
            ChunkCount = vectors.Index?.Entries.Count ?? 0,
            IndexBytes = vectors.IndexBytes
        };
    }

    private async Task<VectorPlan> PlanAsync(CancellationToken ct)
    {
        if (_vectors.ApprovedRoot is null)
        {
            return VectorPlan.Fail(new VectorIndexOperationResult { Status = "refused", Message = "unapproved-root" });
        }

        var loaded = _snapshots.LoadCurrent();
        if (loaded.Availability != LexicalAvailability.Ready || string.IsNullOrWhiteSpace(loaded.Json) || string.IsNullOrWhiteSpace(loaded.SnapshotId))
        {
            return VectorPlan.Fail(new VectorIndexOperationResult
            {
                Status = "rejected",
                Message = "lexical-" + loaded.Availability.ToString().ToLowerInvariant(),
                LexicalSnapshotId = loaded.SnapshotId
            });
        }

        if (!SnapshotJson.TryValidate(loaded.Json, out _))
        {
            return VectorPlan.Fail(new VectorIndexOperationResult { Status = "rejected", Message = "lexical-invalid", LexicalSnapshotId = loaded.SnapshotId });
        }

        var snapshot = JsonSerializer.Deserialize<CorpusSnapshotFile>(loaded.Json, SnapshotJson.Options) ?? new CorpusSnapshotFile();
        var current = await _corpus.ListCurrentChunksAsync(ct);
        var indexed = CorpusChunkIdentitySet.FromSnapshot(snapshot);
        if (!CorpusChunkIdentitySet.Matches(current, indexed))
        {
            return VectorPlan.Fail(new VectorIndexOperationResult { Status = "rejected", Message = "corpus-drift", LexicalSnapshotId = loaded.SnapshotId });
        }

        if (indexed.Count == 0)
        {
            return VectorPlan.Fail(new VectorIndexOperationResult { Status = "rejected", Message = "empty-corpus", LexicalSnapshotId = loaded.SnapshotId });
        }

        var misses = new List<CorpusChunkIdentity>();
        var hits = 0;
        foreach (var chunk in indexed)
        {
            if (_vectors.TryReadCache(chunk.EmbedTextHash, out _))
            {
                hits++;
            }
            else
            {
                misses.Add(chunk);
            }
        }

        int estimatedTokens;
        try
        {
            estimatedTokens = misses.Aggregate(0, (sum, chunk) => checked(sum + ApproximateTokenCounter.Count(chunk.EmbedText)));
        }
        catch (OverflowException)
        {
            estimatedTokens = int.MaxValue;
        }

        var estimatedRequests = misses.Count == 0
            ? 0
            : (misses.Count + EmbeddingOptions.MaxInputsPerRequest - 1) / EmbeddingOptions.MaxInputsPerRequest;
        var safeUpper = EmbeddingTokenBound.Sum(misses.Select(chunk => chunk.EmbedText));
        return new VectorPlan(loaded.SnapshotId, indexed, misses, hits, estimatedTokens, estimatedRequests, safeUpper, null);
    }

    private static List<CorpusChunkIdentity> TakeAffordableBatch(
        IReadOnlyList<CorpusChunkIdentity> pending,
        int offset,
        EmbeddingOperationBudget ledger)
    {
        var batch = new List<CorpusChunkIdentity>();
        if (!ledger.CanAttempt)
        {
            return batch;
        }

        var remaining = ledger.AvailableInputTokens;
        var used = 0;
        for (var index = offset; index < pending.Count && batch.Count < EmbeddingOptions.MaxInputsPerRequest; index++)
        {
            var bound = EmbeddingTokenBound.Upper(pending[index].EmbedText);
            if (bound > remaining - used)
            {
                break;
            }

            batch.Add(pending[index]);
            used += bound;
        }

        return batch;
    }

    private async Task<string?> DriftAsync(string? snapshotId, IReadOnlyList<CorpusChunkIdentity> expected, CancellationToken ct)
    {
        var loaded = _snapshots.LoadCurrent();
        var json = loaded.Json;
        if (loaded.Availability != LexicalAvailability.Ready
            || string.IsNullOrWhiteSpace(loaded.SnapshotId)
            || string.IsNullOrWhiteSpace(json)
            || !string.Equals(loaded.SnapshotId, snapshotId, StringComparison.Ordinal)
            || !SnapshotJson.TryValidate(json, out _))
        {
            return "lexical-snapshot-changed";
        }

        var live = await _corpus.ListCurrentChunksAsync(ct);
        return CorpusChunkIdentitySet.Matches(expected, live) ? null : "corpus-drift";
    }

    private static VectorIndexOperationResult Result(string status, string message, VectorPlan plan, EmbeddingOperationBudget? ledger = null)
    {
        return new VectorIndexOperationResult
        {
            Status = status,
            Message = message,
            ChunkCount = plan.Chunks.Count,
            CacheHits = plan.Hits,
            CacheMisses = plan.Misses.Count,
            EstimatedInputTokens = plan.EstimatedInputTokens,
            EstimatedRequests = plan.EstimatedRequests,
            SafeInputTokenUpperBound = plan.SafeInputTokenUpperBound,
            ActualPromptTokens = ledger?.KnownPromptTokens ?? 0,
            ApiCalls = ledger?.Attempts ?? 0,
            PromptUsageComplete = ledger is null || !ledger.UsageIncomplete,
            LexicalSnapshotId = plan.LexicalSnapshotId
        };
    }

    private sealed record VectorPlan(
        string? LexicalSnapshotId,
        IReadOnlyList<CorpusChunkIdentity> Chunks,
        IReadOnlyList<CorpusChunkIdentity> Misses,
        int Hits,
        int EstimatedInputTokens,
        int EstimatedRequests,
        int SafeInputTokenUpperBound,
        VectorIndexOperationResult? Error)
    {
        public static VectorPlan Fail(VectorIndexOperationResult error)
        {
            return new VectorPlan(error.LexicalSnapshotId, [], [], 0, 0, 0, 0, error);
        }
    }
}
