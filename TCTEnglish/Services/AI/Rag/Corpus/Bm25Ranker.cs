namespace TCTEnglish.Services.AI.Rag.Corpus;

public sealed record Bm25FieldDocument(
    string ChunkId,
    string SourceKey,
    IReadOnlyList<string> TitleTokens,
    IReadOnlyList<string> KeywordTokens,
    IReadOnlyList<string> BodyTokens);

public sealed record Bm25Score(string ChunkId, string SourceKey, double Score);

/// <summary>
/// Okapi BM25 over title, keyword and body fields.
/// score(q, d) = sum_f weight_f * sum_t IDF_f(t) * ((tf*(k1+1)) / (tf + k1*(1-b+b*|d_f|/avgdl_f))).
/// IDF_f(t) = ln(1 + (N - df_f(t) + 0.5) / (df_f(t) + 0.5)).
/// A zero-length field contributes nothing. Ties break by source key, then chunk id.
/// </summary>
public static class Bm25Ranker
{
    public static IReadOnlyList<Bm25Score> Rank(
        IReadOnlyList<Bm25FieldDocument> documents,
        IReadOnlyList<string> queryTokens,
        int limit = CorpusVersions.MaxCandidateCount)
    {
        if (documents.Count == 0 || queryTokens.Count == 0 || limit <= 0)
        {
            return [];
        }

        var fields = new[]
        {
            BuildField(documents, static document => document.TitleTokens, CorpusVersions.TitleWeight),
            BuildField(documents, static document => document.KeywordTokens, CorpusVersions.KeywordWeight),
            BuildField(documents, static document => document.BodyTokens, CorpusVersions.BodyWeight)
        };

        var scores = new List<Bm25Score>(documents.Count);
        for (var index = 0; index < documents.Count; index++)
        {
            var score = 0d;
            foreach (var field in fields)
            {
                score += ScoreField(field, index, queryTokens);
            }

            scores.Add(new Bm25Score(documents[index].ChunkId, documents[index].SourceKey, score));
        }

        return scores
            .Where(score => score.Score > 0d)
            .OrderByDescending(score => score.Score)
            .ThenBy(score => score.SourceKey, StringComparer.Ordinal)
            .ThenBy(score => score.ChunkId, StringComparer.Ordinal)
            .Take(limit)
            .ToList();
    }

    public static bool ScoresTie(double left, double right)
    {
        return Math.Abs(left - right) <= 1e-9;
    }

    private static FieldIndex BuildField(
        IReadOnlyList<Bm25FieldDocument> documents,
        Func<Bm25FieldDocument, IReadOnlyList<string>> selector,
        double weight)
    {
        var documentFrequency = new Dictionary<string, int>(StringComparer.Ordinal);
        var frequencies = new Dictionary<string, int>[documents.Count];
        var lengths = new int[documents.Count];
        var totalLength = 0;
        for (var index = 0; index < documents.Count; index++)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var token in selector(documents[index]))
            {
                counts[token] = counts.GetValueOrDefault(token) + 1;
            }

            frequencies[index] = counts;
            lengths[index] = selector(documents[index]).Count;
            totalLength += lengths[index];
            foreach (var token in counts.Keys)
            {
                documentFrequency[token] = documentFrequency.GetValueOrDefault(token) + 1;
            }
        }

        return new FieldIndex(
            weight,
            documents.Count,
            documents.Count == 0 ? 0d : totalLength / (double)documents.Count,
            documentFrequency,
            frequencies,
            lengths);
    }

    private static double ScoreField(FieldIndex field, int documentIndex, IReadOnlyList<string> queryTokens)
    {
        if (field.AverageLength <= 0d || field.Lengths[documentIndex] == 0)
        {
            return 0d;
        }

        var frequencies = field.Frequencies[documentIndex];
        var score = 0d;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in queryTokens)
        {
            if (!seen.Add(token) || !frequencies.TryGetValue(token, out var termFrequency))
            {
                continue;
            }

            var documentFrequency = field.DocumentFrequency.GetValueOrDefault(token);
            if (documentFrequency == 0)
            {
                continue;
            }

            var idf = Math.Log(1d + ((field.DocumentCount - documentFrequency + 0.5d) / (documentFrequency + 0.5d)));
            var normalization = 1d - CorpusVersions.Bm25B + (CorpusVersions.Bm25B * field.Lengths[documentIndex] / field.AverageLength);
            var numerator = termFrequency * (CorpusVersions.Bm25K1 + 1d);
            var denominator = termFrequency + (CorpusVersions.Bm25K1 * normalization);
            score += field.Weight * idf * (numerator / denominator);
        }

        return score;
    }

    private sealed record FieldIndex(
        double Weight,
        int DocumentCount,
        double AverageLength,
        Dictionary<string, int> DocumentFrequency,
        Dictionary<string, int>[] Frequencies,
        int[] Lengths);
}
