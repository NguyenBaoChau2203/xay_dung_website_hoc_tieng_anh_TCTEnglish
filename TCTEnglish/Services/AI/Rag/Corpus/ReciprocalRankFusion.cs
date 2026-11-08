namespace TCTEnglish.Services.AI.Rag.Corpus;

public sealed record RankedChunk(string ChunkId, string SourceKey);

public sealed record FusedChunk(string ChunkId, string SourceKey, double Score, int? LexicalRank, int? DenseRank);

/// <summary>
/// Reciprocal rank fusion with equal weights. score = sum 1 / (k + rank), rank is 1-based.
/// Initial k is 60. BM25 and cosine values are not added, and the score is not a probability.
/// </summary>
public static class ReciprocalRankFusion
{
    public const int K = 60;

    public static IReadOnlyList<FusedChunk> Fuse(IReadOnlyList<RankedChunk> lexical, IReadOnlyList<RankedChunk> dense, int k = K)
    {
        if (k < 1)
        {
            return [];
        }

        var scores = new Dictionary<string, Accumulator>(StringComparer.Ordinal);
        Add(scores, lexical, k, lexicalBranch: true);
        Add(scores, dense, k, lexicalBranch: false);
        return scores.Values
            .Select(item => new FusedChunk(item.ChunkId, item.SourceKey, item.Score, item.LexicalRank, item.DenseRank))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.SourceKey, StringComparer.Ordinal)
            .ThenBy(item => item.ChunkId, StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<FusedChunk> DedupSources(IReadOnlyList<FusedChunk> fused)
    {
        var kept = new List<FusedChunk>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in fused)
        {
            if (seen.Add(item.SourceKey))
            {
                kept.Add(item);
            }
        }

        return kept;
    }

    private static void Add(Dictionary<string, Accumulator> scores, IReadOnlyList<RankedChunk> branch, int k, bool lexicalBranch)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rank = 0;
        foreach (var item in branch)
        {
            if (string.IsNullOrWhiteSpace(item.ChunkId) || !seen.Add(item.ChunkId))
            {
                continue;
            }

            rank++;
            if (!scores.TryGetValue(item.ChunkId, out var accumulated))
            {
                accumulated = new Accumulator(item.ChunkId, item.SourceKey);
                scores.Add(item.ChunkId, accumulated);
            }

            accumulated.Score += 1d / (k + rank);
            if (lexicalBranch)
            {
                accumulated.LexicalRank = rank;
            }
            else
            {
                accumulated.DenseRank = rank;
            }
        }
    }

    private sealed class Accumulator(string chunkId, string sourceKey)
    {
        public string ChunkId { get; } = chunkId;

        public string SourceKey { get; } = sourceKey;

        public double Score { get; set; }

        public int? LexicalRank { get; set; }

        public int? DenseRank { get; set; }
    }
}
