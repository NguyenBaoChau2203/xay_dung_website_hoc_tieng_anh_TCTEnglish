using System.Text;
using System.Text.Json;
using TCTEnglish.Services.AI;

namespace TCTEnglish.Services.AI.Rag.Corpus;

public enum VectorIndexAvailability
{
    Ready,
    Missing,
    Corrupt,
    EmbeddingSpaceMismatch,
    SnapshotMismatch
}

public interface IVectorWriteProbe
{
    void BeforeManifestCommit(string manifestPath);
}

public interface IVectorIndexStore
{
    string? ApprovedRoot { get; }
    VectorIndexLoad LoadCurrent(string? lexicalSnapshotId);
    Task<VectorIndexCommit> CommitAsync(VectorIndexPayload payload, CancellationToken ct);
    bool TryReadCache(string embedTextHash, out float[] vector);
    Task WriteCacheAsync(string embedTextHash, float[] vector, CancellationToken ct);
}

public sealed class VectorIndexPayload
{
    public string Json { get; init; } = string.Empty;
    public string LexicalSnapshotId { get; init; } = string.Empty;
    public int ChunkCount { get; init; }
}

public sealed class VectorIndexCommit
{
    public string Status { get; init; } = "failed";
    public string? IndexId { get; init; }
    public string? ChecksumSha256 { get; init; }
    public long IndexBytes { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class VectorIndexLoad
{
    public VectorIndexAvailability Availability { get; init; } = VectorIndexAvailability.Missing;
    public string? IndexId { get; init; }
    public string? LexicalSnapshotId { get; init; }
    public string? Json { get; init; }
    public VectorIndexFile? Index { get; init; }
    public long IndexBytes { get; init; }
    public string Detail { get; init; } = string.Empty;
}

public sealed class VectorManifestFile
{
    public int SchemaVersion { get; set; }
    public string IndexId { get; set; } = string.Empty;
    public string LexicalSnapshotId { get; set; } = string.Empty;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public string ChunkerVersion { get; set; } = string.Empty;
    public string PromotedAtUtc { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public long IndexBytes { get; set; }
}

public sealed class VectorIndexFile
{
    public int SchemaVersion { get; set; }
    public string LexicalSnapshotId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public string ChunkerVersion { get; set; } = string.Empty;
    public List<VectorIndexEntry> Entries { get; set; } = [];
}

public sealed class VectorIndexEntry
{
    public string ChunkId { get; set; } = string.Empty;
    public string SourceKey { get; set; } = string.Empty;
    public string DocumentContentHash { get; set; } = string.Empty;
    public string EmbedTextHash { get; set; } = string.Empty;
    public float[] Vector { get; set; } = [];
}

public sealed class EmbeddingCacheFile
{
    public int SchemaVersion { get; set; }
    public string Model { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public string ChunkerVersion { get; set; } = string.Empty;
    public string EmbedTextHash { get; set; } = string.Empty;
    public float[] Vector { get; set; } = [];
}

public sealed class VectorIndexStore : IVectorIndexStore
{
    public const int SchemaVersion = 1;
    private readonly string? _root;
    private readonly IVectorWriteProbe? _probe;
    private readonly SemaphoreSlim _commitLock = new(1, 1);

    public VectorIndexStore(string contentRoot, IVectorWriteProbe? probe = null)
    {
        _probe = probe;
        if (CorpusPathGuard.TryResolve(contentRoot, out var root, out _))
        {
            _root = root;
        }
    }

    public string? ApprovedRoot => _root;

    public VectorIndexLoad LoadCurrent(string? lexicalSnapshotId)
    {
        if (_root is null || !File.Exists(ManifestPath()))
        {
            return new VectorIndexLoad { Availability = VectorIndexAvailability.Missing, Detail = "manifest-missing" };
        }

        VectorManifestFile? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<VectorManifestFile>(File.ReadAllText(ManifestPath()), SnapshotJson.Options);
        }
        catch (Exception)
        {
            return new VectorIndexLoad { Availability = VectorIndexAvailability.Corrupt, Detail = "manifest-corrupt" };
        }

        if (manifest is null || !IsIndexId(manifest.IndexId))
        {
            return new VectorIndexLoad { Availability = VectorIndexAvailability.Corrupt, Detail = "manifest-identity" };
        }

        if (!string.IsNullOrWhiteSpace(lexicalSnapshotId)
            && !string.Equals(manifest.LexicalSnapshotId, lexicalSnapshotId, StringComparison.Ordinal))
        {
            return new VectorIndexLoad
            {
                Availability = VectorIndexAvailability.SnapshotMismatch,
                IndexId = manifest.IndexId,
                LexicalSnapshotId = manifest.LexicalSnapshotId,
                Detail = "vector-snapshot-mismatch"
            };
        }

        var directory = PublishedDirectory(manifest.IndexId);
        if (!CorpusPathGuard.IsContained(_root, directory))
        {
            return new VectorIndexLoad { Availability = VectorIndexAvailability.Corrupt, Detail = "snapshot-escape", IndexId = manifest.IndexId };
        }

        var indexPath = Path.Combine(directory, "vector-index.json");
        var checksumPath = Path.Combine(directory, "vector-index.sha256");
        if (!File.Exists(indexPath) || !File.Exists(checksumPath))
        {
            return new VectorIndexLoad { Availability = VectorIndexAvailability.Corrupt, Detail = "snapshot-truncated", IndexId = manifest.IndexId };
        }

        byte[] bytes;
        string recorded;
        try
        {
            bytes = File.ReadAllBytes(indexPath);
            recorded = File.ReadAllText(checksumPath).Trim();
        }
        catch (Exception)
        {
            return new VectorIndexLoad { Availability = VectorIndexAvailability.Corrupt, Detail = "snapshot-unreadable", IndexId = manifest.IndexId };
        }

        var actual = CorpusHash.Sha256(bytes);
        if (!actual.Equals(recorded, StringComparison.Ordinal) || !actual.Equals(manifest.ChecksumSha256, StringComparison.Ordinal))
        {
            return new VectorIndexLoad { Availability = VectorIndexAvailability.Corrupt, Detail = "checksum-mismatch", IndexId = manifest.IndexId };
        }

        VectorIndexFile? index;
        try
        {
            index = JsonSerializer.Deserialize<VectorIndexFile>(Encoding.UTF8.GetString(bytes), SnapshotJson.Options);
        }
        catch (JsonException)
        {
            return new VectorIndexLoad { Availability = VectorIndexAvailability.Corrupt, Detail = "truncated-json", IndexId = manifest.IndexId };
        }

        if (index is null)
        {
            return new VectorIndexLoad { Availability = VectorIndexAvailability.Corrupt, Detail = "empty-index", IndexId = manifest.IndexId };
        }

        var space = DescribeSpace(index, manifest);
        if (space is not null)
        {
            return new VectorIndexLoad
            {
                Availability = VectorIndexAvailability.EmbeddingSpaceMismatch,
                IndexId = manifest.IndexId,
                LexicalSnapshotId = index.LexicalSnapshotId,
                Detail = space
            };
        }

        if (!VectorsAreUsable(index))
        {
            return new VectorIndexLoad { Availability = VectorIndexAvailability.Corrupt, Detail = "vector-invalid", IndexId = manifest.IndexId };
        }

        return new VectorIndexLoad
        {
            Availability = VectorIndexAvailability.Ready,
            IndexId = manifest.IndexId,
            LexicalSnapshotId = index.LexicalSnapshotId,
            Json = Encoding.UTF8.GetString(bytes),
            Index = index,
            IndexBytes = bytes.Length,
            Detail = string.Empty
        };
    }

    public async Task<VectorIndexCommit> CommitAsync(VectorIndexPayload payload, CancellationToken ct)
    {
        if (_root is null)
        {
            return Failed("refused", "unapproved-root");
        }

        if (!await _commitLock.WaitAsync(TimeSpan.FromSeconds(5), ct))
        {
            return Failed("busy", "vector-lock-held");
        }

        var indexId = "v" + Guid.NewGuid().ToString("N");
        var staging = Path.Combine(_root, "vectors", "staging", indexId);
        try
        {
            ct.ThrowIfCancellationRequested();
            var loaded = ValidatePayload(payload.Json, payload.LexicalSnapshotId, payload.ChunkCount);
            if (loaded is null)
            {
                return Failed("refused", "vector-validation");
            }

            if (!CorpusPathGuard.IsContained(_root, staging))
            {
                return Failed("refused", "staging-escape");
            }

            Directory.CreateDirectory(staging);
            var bytes = Encoding.UTF8.GetBytes(payload.Json);
            var checksum = CorpusHash.Sha256(bytes);
            await WriteBytesAsync(Path.Combine(staging, "vector-index.json"), bytes, ct);
            await WriteBytesAsync(Path.Combine(staging, "vector-index.sha256"), Encoding.UTF8.GetBytes(checksum + "\n"), ct);
            var readBack = await File.ReadAllBytesAsync(Path.Combine(staging, "vector-index.json"), ct);
            if (!CorpusHash.Sha256(readBack).Equals(checksum, StringComparison.Ordinal))
            {
                DeleteContained(staging);
                return Failed("failed", "checksum-mismatch-before-promotion");
            }

            var destination = PublishedDirectory(indexId);
            if (!CorpusPathGuard.IsContained(_root, destination))
            {
                DeleteContained(staging);
                return Failed("refused", "snapshot-escape");
            }

            Directory.CreateDirectory(Path.Combine(_root, "vectors", "published"));
            await CorpusFilePromotion.RunAsync(() => Directory.Move(staging, destination), ct, directoryMove: true);
            var manifest = new VectorManifestFile
            {
                SchemaVersion = SchemaVersion,
                IndexId = indexId,
                LexicalSnapshotId = payload.LexicalSnapshotId,
                ChecksumSha256 = checksum,
                Model = EmbeddingOptions.ModelName,
                Dimensions = EmbeddingOptions.Dimensions,
                ChunkerVersion = CorpusVersions.ChunkerVersion,
                PromotedAtUtc = DateTime.UtcNow.ToString("O"),
                ChunkCount = payload.ChunkCount,
                IndexBytes = bytes.Length
            };
            var manifestJson = JsonSerializer.Serialize(manifest, SnapshotJson.Options);
            _probe?.BeforeManifestCommit(ManifestPath());
            await CommitManifestAsync(manifestJson, ct);
            return new VectorIndexCommit
            {
                Status = "promoted",
                IndexId = indexId,
                ChecksumSha256 = checksum,
                IndexBytes = bytes.Length,
                Message = "promoted"
            };
        }
        catch (OperationCanceledException)
        {
            DeleteContained(staging);
            DeletePublished(indexId);
            return Failed("cancelled", "cancelled-before-promotion");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            DeleteContained(staging);
            DeletePublished(indexId);
            return Failed("failed", "write-failed:" + ex.GetType().Name + ":" + (ex.HResult & 0xffff));
        }
        finally
        {
            _commitLock.Release();
        }
    }

    public bool TryReadCache(string embedTextHash, out float[] vector)
    {
        vector = [];
        if (_root is null || string.IsNullOrWhiteSpace(embedTextHash))
        {
            return false;
        }

        var path = CachePath(embedTextHash);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var cached = JsonSerializer.Deserialize<EmbeddingCacheFile>(File.ReadAllText(path), SnapshotJson.Options);
            if (cached is null
                || cached.SchemaVersion != SchemaVersion
                || cached.Model != EmbeddingOptions.ModelName
                || cached.Dimensions != EmbeddingOptions.Dimensions
                || cached.ChunkerVersion != CorpusVersions.ChunkerVersion
                || !string.Equals(cached.EmbedTextHash, embedTextHash, StringComparison.Ordinal)
                || !VectorIsUsable(cached.Vector))
            {
                return false;
            }

            vector = cached.Vector;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task WriteCacheAsync(string embedTextHash, float[] vector, CancellationToken ct)
    {
        if (_root is null || !VectorIsUsable(vector) || string.IsNullOrWhiteSpace(embedTextHash))
        {
            return;
        }

        var path = CachePath(embedTextHash);
        if (!CorpusPathGuard.IsContained(_root, path))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(new EmbeddingCacheFile
        {
            SchemaVersion = SchemaVersion,
            Model = EmbeddingOptions.ModelName,
            Dimensions = EmbeddingOptions.Dimensions,
            ChunkerVersion = CorpusVersions.ChunkerVersion,
            EmbedTextHash = embedTextHash,
            Vector = vector
        }, SnapshotJson.Options);
        await WriteBytesAsync(path, Encoding.UTF8.GetBytes(json), ct);
    }

    public static bool VectorIsUsable(float[]? vector)
    {
        if (vector is null || vector.Length != EmbeddingOptions.Dimensions)
        {
            return false;
        }

        double sum = 0;
        foreach (var value in vector)
        {
            if (!float.IsFinite(value))
            {
                return false;
            }

            sum += value * (double)value;
        }

        return double.IsFinite(sum) && sum > 0d;
    }

    private static string? DescribeSpace(VectorIndexFile index, VectorManifestFile manifest)
    {
        if (index.SchemaVersion != SchemaVersion || manifest.SchemaVersion != SchemaVersion)
        {
            return "unsupported-schema";
        }

        if (index.Model != EmbeddingOptions.ModelName || manifest.Model != EmbeddingOptions.ModelName)
        {
            return "vector-model-mismatch";
        }

        if (index.Dimensions != EmbeddingOptions.Dimensions || manifest.Dimensions != EmbeddingOptions.Dimensions)
        {
            return "vector-dimension-mismatch";
        }

        if (index.ChunkerVersion != CorpusVersions.ChunkerVersion || manifest.ChunkerVersion != CorpusVersions.ChunkerVersion)
        {
            return "vector-chunker-mismatch";
        }

        if (!string.Equals(index.LexicalSnapshotId, manifest.LexicalSnapshotId, StringComparison.Ordinal))
        {
            return "vector-snapshot-mismatch";
        }

        return null;
    }

    private static bool VectorsAreUsable(VectorIndexFile index)
    {
        if (index.Entries is null || index.Entries.Count == 0)
        {
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in index.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.ChunkId)
                || string.IsNullOrWhiteSpace(entry.SourceKey)
                || string.IsNullOrWhiteSpace(entry.DocumentContentHash)
                || string.IsNullOrWhiteSpace(entry.EmbedTextHash)
                || !seen.Add(entry.ChunkId)
                || CorpusVersions.ForbiddenKeyPrefixes.Any(prefix => entry.SourceKey.StartsWith(prefix, StringComparison.Ordinal))
                || !VectorIsUsable(entry.Vector))
            {
                return false;
            }
        }

        return true;
    }

    private static VectorIndexFile? ValidatePayload(string json, string lexicalSnapshotId, int chunkCount)
    {
        VectorIndexFile? index;
        try
        {
            index = JsonSerializer.Deserialize<VectorIndexFile>(json, SnapshotJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }

        if (index is null
            || index.SchemaVersion != SchemaVersion
            || index.Model != EmbeddingOptions.ModelName
            || index.Dimensions != EmbeddingOptions.Dimensions
            || index.ChunkerVersion != CorpusVersions.ChunkerVersion
            || !string.Equals(index.LexicalSnapshotId, lexicalSnapshotId, StringComparison.Ordinal)
            || index.Entries is null
            || index.Entries.Count != chunkCount
            || !VectorsAreUsable(index))
        {
            return null;
        }

        return index;
    }

    private string ManifestPath()
    {
        return Path.Combine(_root ?? string.Empty, "vector-manifest.json");
    }

    private string PublishedDirectory(string indexId)
    {
        return Path.Combine(_root ?? string.Empty, "vectors", "published", indexId);
    }

    private string CachePath(string embedTextHash)
    {
        var key = CorpusHash.Sha256(string.Join('\n', EmbeddingOptions.ModelName, EmbeddingOptions.Dimensions.ToString(), CorpusVersions.ChunkerVersion, embedTextHash));
        return Path.Combine(_root ?? string.Empty, "embedding-cache", key + ".json");
    }

    private async Task CommitManifestAsync(string json, CancellationToken ct)
    {
        var root = _root ?? throw new InvalidOperationException("unapproved-root");
        var finalPath = ManifestPath();
        var tempPath = finalPath + ".writing";
        await WriteBytesAsync(tempPath, Encoding.UTF8.GetBytes(json), ct);
        await CorpusFilePromotion.RunAsync(() =>
        {
            if (File.Exists(finalPath)) File.Replace(tempPath, finalPath, finalPath + ".bak", ignoreMetadataErrors: true);
            else File.Move(tempPath, finalPath);
        }, ct);
    }

    private async Task WriteBytesAsync(string path, byte[] bytes, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var partial = path + ".partial";
        await using (var stream = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await stream.WriteAsync(bytes, ct);
            await stream.FlushAsync(ct);
        }

        await CorpusFilePromotion.RunAsync(() =>
        {
            if (File.Exists(path)) File.Replace(partial, path, null);
            else File.Move(partial, path);
        }, ct);
    }

    private void DeleteContained(string path)
    {
        if (_root is null || !CorpusPathGuard.IsContained(Path.Combine(_root, "vectors", "staging"), path) || !Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(path, recursive: true);
    }

    private void DeletePublished(string indexId)
    {
        if (_root is null || !IsIndexId(indexId))
        {
            return;
        }

        var path = PublishedDirectory(indexId);
        if (CorpusPathGuard.IsContained(Path.Combine(_root, "vectors", "published"), path) && Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static bool IsIndexId(string? indexId)
    {
        return indexId is not null
            && indexId.Length == 33
            && indexId[0] == 'v'
            && indexId.Skip(1).All(Uri.IsHexDigit);
    }

    private static VectorIndexCommit Failed(string status, string message)
    {
        return new VectorIndexCommit { Status = status, Message = message };
    }
}
