namespace TCTEnglish.Services.AI.Rag.Corpus;

public sealed record DenseCandidate(string ChunkId, string SourceKey, float[] Vector);

public sealed record DenseScore(string ChunkId, string SourceKey, double Score);

/// <summary>
/// Cosine similarity over one immutable vector list. Ties break by source key, then chunk id.
/// A zero-norm or non-finite query produces no ranking. Synthetic vectors prove ordering only.
/// </summary>
public static class CosineRanker
{
    public static bool TryTopK(IReadOnlyList<DenseCandidate> candidates, float[] query, int limit, out IReadOnlyList<DenseScore> ranked, out string detail)
    {
        ranked = [];
        if (query is null || query.Length < 1 || !IsUnitCapable(query))
        {
            detail = "query-vector-invalid";
            return false;
        }

        if (limit <= 0)
        {
            detail = "limit";
            return false;
        }

        var scores = new List<DenseScore>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (candidate.Vector is null
                || candidate.Vector.Length != query.Length
                || !IsUnitCapable(candidate.Vector))
            {
                detail = "candidate-vector-invalid";
                return false;
            }

            var score = Dot(candidate.Vector, query) / (Norm(candidate.Vector) * Norm(query));
            if (!double.IsFinite(score))
            {
                detail = "candidate-vector-invalid";
                return false;
            }

            scores.Add(new DenseScore(candidate.ChunkId, candidate.SourceKey, score));
        }

        ranked = scores
            .OrderByDescending(score => score.Score)
            .ThenBy(score => score.SourceKey, StringComparer.Ordinal)
            .ThenBy(score => score.ChunkId, StringComparer.Ordinal)
            .Take(limit)
            .ToList();
        detail = string.Empty;
        return true;
    }

    public static bool ScoresTie(double left, double right)
    {
        return Math.Abs(left - right) <= 1e-12;
    }

    private static bool IsUnitCapable(float[] vector)
    {
        return vector.Length > 0 && vector.All(float.IsFinite) && Norm(vector) > 0d;
    }

    private static double Dot(float[] left, float[] right)
    {
        double sum = 0;
        for (var index = 0; index < left.Length; index++)
        {
            sum += left[index] * (double)right[index];
        }

        return sum;
    }

    private static double Norm(float[] vector)
    {
        double sum = 0;
        foreach (var value in vector)
        {
            sum += value * (double)value;
        }

        return Math.Sqrt(sum);
    }
}
