namespace TCTEnglish.Services.AI.Rag.Corpus;

public sealed record CorpusChunkIdentity(
    string ChunkId,
    string SourceKey,
    string SourceKind,
    string DocumentContentHash,
    string EmbedText,
    string EmbedTextHash,
    string Visibility);

public static class CorpusChunkIdentitySet
{
    public static IReadOnlyList<CorpusChunkIdentity> FromPrepared(IReadOnlyList<PreparedCorpusDocument> prepared)
    {
        var chunks = new List<CorpusChunkIdentity>();
        foreach (var item in prepared)
        {
            var sentences = item.Sentences.ToDictionary(sentence => sentence.Number);
            foreach (var chunk in item.Chunks.OrderBy(chunk => chunk.Number))
            {
                var text = Join(chunk.SentenceNumbers.Select(number => sentences.TryGetValue(number, out var sentence) ? sentence.Text : string.Empty));
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                chunks.Add(new CorpusChunkIdentity(
                    chunk.ChunkId,
                    item.Document.SourceKey,
                    item.Document.SourceKind,
                    item.Document.ContentHash,
                    text,
                    CorpusHash.Sha256(text),
                    item.Document.Visibility));
            }
        }

        return chunks;
    }

    public static IReadOnlyList<CorpusChunkIdentity> FromSnapshot(CorpusSnapshotFile snapshot)
    {
        var chunks = new List<CorpusChunkIdentity>();
        foreach (var entry in snapshot.Documents ?? [])
        {
            var sentences = (entry.Sentences ?? []).ToDictionary(sentence => sentence.Number);
            foreach (var chunk in (entry.Chunks ?? []).OrderBy(item => item.Number))
            {
                var parts = new List<string>();
                var complete = true;
                foreach (var number in chunk.SentenceNumbers ?? [])
                {
                    if (!sentences.TryGetValue(number, out var sentence))
                    {
                        complete = false;
                        break;
                    }

                    parts.Add(sentence.Text);
                }

                var text = complete ? Join(parts) : string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                chunks.Add(new CorpusChunkIdentity(
                    chunk.ChunkId,
                    entry.SourceKey,
                    entry.SourceKind,
                    entry.ContentHash,
                    text,
                    CorpusHash.Sha256(text),
                    entry.Visibility));
            }
        }

        return chunks;
    }

    public static bool Matches(IReadOnlyList<CorpusChunkIdentity> left, IReadOnlyList<CorpusChunkIdentity> right)
    {
        if (!TryMap(left, out var map) || !TryMap(right, out var other) || map.Count != other.Count)
        {
            return false;
        }

        foreach (var (chunkId, item) in map)
        {
            if (!other.TryGetValue(chunkId, out var candidate)
                || !string.Equals(item.SourceKey, candidate.SourceKey, StringComparison.Ordinal)
                || !string.Equals(item.SourceKind, candidate.SourceKind, StringComparison.Ordinal)
                || !string.Equals(item.DocumentContentHash, candidate.DocumentContentHash, StringComparison.Ordinal)
                || !string.Equals(item.EmbedTextHash, candidate.EmbedTextHash, StringComparison.Ordinal)
                || !string.Equals(item.Visibility, candidate.Visibility, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryMap(IReadOnlyList<CorpusChunkIdentity> chunks, out Dictionary<string, CorpusChunkIdentity> map)
    {
        map = new Dictionary<string, CorpusChunkIdentity>(StringComparer.Ordinal);
        foreach (var chunk in chunks)
        {
            if (string.IsNullOrWhiteSpace(chunk.ChunkId) || !map.TryAdd(chunk.ChunkId, chunk))
            {
                map.Clear();
                return false;
            }
        }

        return true;
    }

    private static string Join(IEnumerable<string> parts)
    {
        return string.Join('\n', parts);
    }
}
