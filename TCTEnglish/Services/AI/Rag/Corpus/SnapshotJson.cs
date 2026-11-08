using System.Text.Json;
using System.Text.Json.Serialization;

namespace TCTEnglish.Services.AI.Rag.Corpus;

public static class SnapshotJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    public static bool TryValidate(string json, out string detail)
    {
        CorpusSnapshotFile? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<CorpusSnapshotFile>(json, Options);
        }
        catch (JsonException)
        {
            detail = "truncated-json";
            return false;
        }

        if (snapshot is null)
        {
            detail = "empty-snapshot";
            return false;
        }

        if (snapshot.SchemaVersion != CorpusVersions.SchemaVersion)
        {
            detail = "unsupported-schema";
            return false;
        }

        if (snapshot.CorpusVersion != CorpusVersions.CorpusVersion
            || snapshot.ChunkerVersion != CorpusVersions.ChunkerVersion
            || snapshot.NormalizerVersion != CorpusVersions.NormalizerVersion)
        {
            detail = "mixed-version";
            return false;
        }

        if (snapshot.EmbeddingModel is not null || snapshot.EmbeddingDimension is not null)
        {
            detail = "embedding-present";
            return false;
        }

        foreach (var document in snapshot.Documents ?? [])
        {
            if (!CorpusVersions.AllowedSourceKinds.Contains(document.SourceKind))
            {
                detail = "source-kind";
                return false;
            }

            if (string.IsNullOrWhiteSpace(document.SourceKey)
                || CorpusVersions.ForbiddenKeyPrefixes.Any(prefix => document.SourceKey.StartsWith(prefix, StringComparison.Ordinal)))
            {
                detail = "forbidden-source";
                return false;
            }

            if (document.Visibility is not ("public" or "authenticated"))
            {
                detail = "visibility";
                return false;
            }

            if (document.Route.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
            {
                detail = "admin-route";
                return false;
            }

            var canonical = CorpusHash.Canonical(document.SourceKey, document.Visibility, document.Route, document.Title, document.Keywords ?? [], document.Body);
            if (!CorpusHash.Sha256(canonical).Equals(document.ContentHash, StringComparison.Ordinal))
            {
                detail = "content-hash";
                return false;
            }

            if (!SentencesFit(document))
            {
                detail = "chunk-limit";
                return false;
            }
        }

        detail = string.Empty;
        return true;
    }

    private static bool SentencesFit(CorpusSnapshotEntry document)
    {
        var numbers = new HashSet<int>();
        foreach (var sentence in document.Sentences ?? [])
        {
            if (sentence.Number <= 0
                || !numbers.Add(sentence.Number)
                || string.IsNullOrWhiteSpace(sentence.Text)
                || !CorpusHash.Sha256(sentence.Text).Equals(sentence.ContentHash, StringComparison.Ordinal)
                || ApproximateTokenCounter.Count(sentence.Text) > CorpusVersions.MaxChunkTokens)
            {
                return false;
            }
        }

        foreach (var chunk in document.Chunks ?? [])
        {
            var total = 0;
            var index = 0;
            foreach (var number in chunk.SentenceNumbers ?? [])
            {
                var sentence = document.Sentences?.FirstOrDefault(item => item.Number == number);
                if (sentence is null)
                {
                    return false;
                }

                total += (index == 0 ? 0 : 1) + ApproximateTokenCounter.Count(sentence.Text);
                index++;
            }

            if (total > CorpusVersions.MaxChunkTokens)
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class CorpusManifestFile
{
    public int SchemaVersion { get; set; }
    public string SnapshotId { get; set; } = string.Empty;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public string CorpusVersion { get; set; } = string.Empty;
    public string ChunkerVersion { get; set; } = string.Empty;
    public string NormalizerVersion { get; set; } = string.Empty;
    public string? EmbeddingModel { get; set; }
    public int? EmbeddingDimension { get; set; }
    public string PromotedAtUtc { get; set; } = string.Empty;
    public int DocumentCount { get; set; }
    public int ChunkCount { get; set; }
    public long SnapshotBytes { get; set; }
}

public sealed class CorpusSnapshotFile
{
    public int SchemaVersion { get; set; }
    public string CorpusVersion { get; set; } = string.Empty;
    public string ChunkerVersion { get; set; } = string.Empty;
    public string NormalizerVersion { get; set; } = string.Empty;
    public string? EmbeddingModel { get; set; }
    public int? EmbeddingDimension { get; set; }
    public double Bm25K1 { get; set; }
    public double Bm25B { get; set; }
    public List<CorpusSnapshotEntry> Documents { get; set; } = [];
}

public sealed class CorpusSnapshotEntry
{
    public string SourceKind { get; set; } = string.Empty;
    public string SourceKey { get; set; } = string.Empty;
    public string EntityKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = [];
    public string Body { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string SourceVersion { get; set; } = string.Empty;
    public List<CorpusSnapshotSentence> Sentences { get; set; } = [];
    public List<CorpusSnapshotChunk> Chunks { get; set; } = [];
}

public sealed class CorpusSnapshotSentence
{
    public string SentenceId { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Text { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
}

public sealed class CorpusSnapshotChunk
{
    public string ChunkId { get; set; } = string.Empty;
    public int Number { get; set; }
    public List<int> SentenceNumbers { get; set; } = [];
}
