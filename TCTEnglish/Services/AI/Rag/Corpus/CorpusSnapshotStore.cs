namespace TCTEnglish.Services.AI.Rag.Corpus;

public static class CorpusPathGuard
{
    public static bool TryResolve(string? contentRoot, out string root, out string error)
    {
        root = string.Empty;
        if (string.IsNullOrWhiteSpace(contentRoot) || contentRoot.Contains("..", StringComparison.Ordinal))
        {
            error = "traversal";
            return false;
        }

        string fullContent;
        try
        {
            fullContent = Path.GetFullPath(contentRoot);
        }
        catch (Exception)
        {
            error = "invalid-content-root";
            return false;
        }

        if (ContainsSegment(fullContent, "wwwroot"))
        {
            error = "wwwroot";
            return false;
        }

        root = Path.GetFullPath(Path.Combine(fullContent, ".appdata", "local", "ai-rag"));
        var prefix = fullContent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            error = "escape";
            root = string.Empty;
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool IsForeignRequest(string? requestedRoot)
    {
        return !string.IsNullOrWhiteSpace(requestedRoot);
    }

    public static bool IsContained(string approvedRoot, string candidate)
    {
        if (string.IsNullOrWhiteSpace(approvedRoot) || string.IsNullOrWhiteSpace(candidate) || candidate.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        string fullRoot;
        string fullCandidate;
        try
        {
            fullRoot = Path.GetFullPath(approvedRoot);
            fullCandidate = Path.GetFullPath(candidate);
        }
        catch (Exception)
        {
            return false;
        }

        if (fullCandidate.Equals(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsSegment(string path, string segment)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => part.Equals(segment, StringComparison.OrdinalIgnoreCase));
    }
}

public interface ICorpusWriteProbe
{
    void BeforeWrite(string fullPath);
}

public interface ICorpusSnapshotStore
{
    string? ApprovedRoot { get; }
    Task<CorpusSnapshotCommit> CommitAsync(CorpusSnapshotPayload payload, CancellationToken ct);
    CorpusSnapshotLoad LoadCurrent();
    CorpusSnapshotLoad LoadSnapshot(string snapshotId);
}

public sealed class CorpusSnapshotPayload
{
    public string Json { get; init; } = string.Empty;
    public int DocumentCount { get; init; }
    public int ChunkCount { get; init; }
}

public sealed class CorpusSnapshotCommit
{
    public string Status { get; init; } = "failed";
    public string? SnapshotId { get; init; }
    public string? ChecksumSha256 { get; init; }
    public long SnapshotBytes { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class CorpusSnapshotLoad
{
    public LexicalAvailability Availability { get; init; } = LexicalAvailability.Missing;
    public string? SnapshotId { get; init; }
    public string? Json { get; init; }
    public string? ChecksumSha256 { get; init; }
    public long SnapshotBytes { get; init; }
    public DateTime? PromotedAtUtc { get; init; }
    public int DocumentCount { get; init; }
    public int ChunkCount { get; init; }
    public string? Detail { get; init; }
}

public sealed class CorpusSnapshotStore : ICorpusSnapshotStore
{
    private readonly string? _root;
    private readonly string? _rootError;
    private readonly ICorpusWriteProbe? _probe;
    private readonly SemaphoreSlim _buildLock = new(1, 1);
    private readonly TimeSpan _lockWait;

    public CorpusSnapshotStore(string contentRoot, ICorpusWriteProbe? probe = null, TimeSpan? lockWait = null)
    {
        _probe = probe;
        _lockWait = lockWait ?? TimeSpan.FromSeconds(5);
        if (!CorpusPathGuard.TryResolve(contentRoot, out var root, out var error))
        {
            _rootError = error;
            return;
        }

        _root = root;
    }

    public string? ApprovedRoot => _root;

    public async Task<CorpusSnapshotCommit> CommitAsync(CorpusSnapshotPayload payload, CancellationToken ct)
    {
        if (_root is null)
        {
            return Failed("refused", _rootError ?? "unapproved-root");
        }

        try
        {
            if (!await _buildLock.WaitAsync(_lockWait, ct))
            {
                return Failed("busy", "build-lock-held");
            }
        }
        catch (OperationCanceledException)
        {
            return Failed("cancelled", "cancelled-before-promotion");
        }

        var snapshotId = "s" + Guid.NewGuid().ToString("N");
        var staging = Path.Combine(_root, "staging", snapshotId);
        try
        {
            ct.ThrowIfCancellationRequested();
            if (!CorpusPathGuard.IsContained(_root, staging))
            {
                return Failed("refused", "staging-escape");
            }

            Directory.CreateDirectory(staging);
            var snapshotPath = Path.Combine(staging, "corpus.json");
            var checksumPath = Path.Combine(staging, "corpus.sha256");
            var bytes = System.Text.Encoding.UTF8.GetBytes(payload.Json);
            await WriteBytesAsync(snapshotPath, bytes, ct);
            var checksum = CorpusHash.Sha256(bytes);
            await WriteBytesAsync(checksumPath, System.Text.Encoding.UTF8.GetBytes(checksum + "\n"), ct);
            ct.ThrowIfCancellationRequested();

            var readBack = await File.ReadAllBytesAsync(snapshotPath, ct);
            if (!CorpusHash.Sha256(readBack).Equals(checksum, StringComparison.Ordinal) || readBack.Length != bytes.Length)
            {
                DeleteContainedDirectory(staging);
                return Failed("failed", "checksum-mismatch-before-promotion");
            }

            var destination = Path.Combine(_root, "snapshots", snapshotId);
            if (!CorpusPathGuard.IsContained(_root, destination))
            {
                DeleteContainedDirectory(staging);
                return Failed("refused", "snapshot-escape");
            }

            Directory.CreateDirectory(Path.Combine(_root, "snapshots"));
            await CorpusFilePromotion.RunAsync(() => Directory.Move(staging, destination), ct, directoryMove: true);
            var manifest = new CorpusManifestFile
            {
                SchemaVersion = CorpusVersions.SchemaVersion,
                SnapshotId = snapshotId,
                ChecksumSha256 = checksum,
                CorpusVersion = CorpusVersions.CorpusVersion,
                ChunkerVersion = CorpusVersions.ChunkerVersion,
                NormalizerVersion = CorpusVersions.NormalizerVersion,
                EmbeddingModel = null,
                EmbeddingDimension = null,
                PromotedAtUtc = DateTime.UtcNow.ToString("O"),
                DocumentCount = payload.DocumentCount,
                ChunkCount = payload.ChunkCount,
                SnapshotBytes = bytes.Length
            };
            var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest, SnapshotJson.Options);
            await CommitManifestAsync(manifestJson, ct);
            return new CorpusSnapshotCommit
            {
                Status = "promoted",
                SnapshotId = snapshotId,
                ChecksumSha256 = checksum,
                SnapshotBytes = bytes.Length,
                Message = "promoted"
            };
        }
        catch (OperationCanceledException)
        {
            DeleteContainedDirectory(staging);
            return Failed("cancelled", "cancelled-before-promotion");
        }
        catch (IOException ex)
        {
            DeleteContainedDirectory(staging);
            return Failed("failed", "write-failed:" + ex.GetType().Name + ":" + (ex.HResult & 0xffff));
        }
        catch (UnauthorizedAccessException ex)
        {
            DeleteContainedDirectory(staging);
            return Failed("failed", "write-failed:" + ex.GetType().Name);
        }
        finally
        {
            _buildLock.Release();
        }
    }

    public CorpusSnapshotLoad LoadCurrent()
    {
        if (_root is null || !File.Exists(Path.Combine(_root, "manifest.json")))
        {
            return new CorpusSnapshotLoad { Availability = LexicalAvailability.Missing, Detail = _rootError ?? "manifest-missing" };
        }

        CorpusManifestFile? manifest;
        try
        {
            manifest = System.Text.Json.JsonSerializer.Deserialize<CorpusManifestFile>(File.ReadAllText(Path.Combine(_root, "manifest.json")), SnapshotJson.Options);
        }
        catch (Exception)
        {
            return new CorpusSnapshotLoad { Availability = LexicalAvailability.Corrupt, Detail = "manifest-corrupt" };
        }

        if (manifest is null || !IsSnapshotId(manifest.SnapshotId))
        {
            return new CorpusSnapshotLoad { Availability = LexicalAvailability.Corrupt, Detail = "manifest-identity" };
        }

        if (manifest.SchemaVersion != CorpusVersions.SchemaVersion
            || manifest.CorpusVersion != CorpusVersions.CorpusVersion
            || manifest.ChunkerVersion != CorpusVersions.ChunkerVersion
            || manifest.NormalizerVersion != CorpusVersions.NormalizerVersion
            || manifest.EmbeddingModel is not null
            || manifest.EmbeddingDimension is not null)
        {
            return new CorpusSnapshotLoad { Availability = LexicalAvailability.Unsupported, Detail = "manifest-version", SnapshotId = manifest.SnapshotId };
        }

        var loaded = LoadSnapshot(manifest.SnapshotId);
        if (loaded.Availability != LexicalAvailability.Ready)
        {
            return loaded;
        }

        if (!string.Equals(loaded.ChecksumSha256, manifest.ChecksumSha256, StringComparison.Ordinal))
        {
            return new CorpusSnapshotLoad { Availability = LexicalAvailability.Corrupt, Detail = "manifest-checksum", SnapshotId = manifest.SnapshotId };
        }

        DateTime.TryParse(manifest.PromotedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var promoted);
        return new CorpusSnapshotLoad
        {
            Availability = LexicalAvailability.Ready,
            SnapshotId = manifest.SnapshotId,
            Json = loaded.Json,
            ChecksumSha256 = loaded.ChecksumSha256,
            SnapshotBytes = loaded.SnapshotBytes,
            PromotedAtUtc = promoted == default ? null : promoted,
            DocumentCount = manifest.DocumentCount,
            ChunkCount = manifest.ChunkCount
        };
    }

    public CorpusSnapshotLoad LoadSnapshot(string snapshotId)
    {
        if (_root is null || !IsSnapshotId(snapshotId))
        {
            return new CorpusSnapshotLoad { Availability = LexicalAvailability.Corrupt, Detail = "snapshot-identity" };
        }

        var directory = Path.Combine(_root, "snapshots", snapshotId);
        if (!CorpusPathGuard.IsContained(_root, directory))
        {
            return new CorpusSnapshotLoad { Availability = LexicalAvailability.Corrupt, Detail = "snapshot-escape" };
        }

        var snapshotPath = Path.Combine(directory, "corpus.json");
        var checksumPath = Path.Combine(directory, "corpus.sha256");
        if (!File.Exists(snapshotPath) || !File.Exists(checksumPath))
        {
            return new CorpusSnapshotLoad { Availability = LexicalAvailability.Corrupt, Detail = "snapshot-truncated", SnapshotId = snapshotId };
        }

        byte[] bytes;
        string recorded;
        try
        {
            bytes = File.ReadAllBytes(snapshotPath);
            recorded = File.ReadAllText(checksumPath).Trim();
        }
        catch (Exception)
        {
            return new CorpusSnapshotLoad { Availability = LexicalAvailability.Corrupt, Detail = "snapshot-unreadable", SnapshotId = snapshotId };
        }

        var actual = CorpusHash.Sha256(bytes);
        if (!actual.Equals(recorded, StringComparison.Ordinal) || recorded.Length != 64)
        {
            return new CorpusSnapshotLoad { Availability = LexicalAvailability.Corrupt, Detail = "checksum-mismatch", SnapshotId = snapshotId };
        }

        var json = System.Text.Encoding.UTF8.GetString(bytes);
        if (!SnapshotJson.TryValidate(json, out var detail))
        {
            var availability = detail is "unsupported-schema" or "mixed-version" or "embedding-present" ? LexicalAvailability.Unsupported : LexicalAvailability.Corrupt;
            return new CorpusSnapshotLoad { Availability = availability, Detail = detail, SnapshotId = snapshotId, ChecksumSha256 = actual };
        }

        return new CorpusSnapshotLoad
        {
            Availability = LexicalAvailability.Ready,
            SnapshotId = snapshotId,
            Json = json,
            ChecksumSha256 = actual,
            SnapshotBytes = bytes.Length
        };
    }

    private async Task CommitManifestAsync(string json, CancellationToken ct)
    {
        var root = _root ?? throw new InvalidOperationException("unapproved-root");
        var finalPath = Path.Combine(root, "manifest.json");
        var tempPath = Path.Combine(root, "manifest.json.writing");
        await WriteBytesAsync(tempPath, System.Text.Encoding.UTF8.GetBytes(json), ct);
        await CorpusFilePromotion.RunAsync(() =>
        {
            if (File.Exists(finalPath)) File.Replace(tempPath, finalPath, Path.Combine(root, "manifest.json.bak"), ignoreMetadataErrors: true);
            else File.Move(tempPath, finalPath);
        }, ct);
    }

    private async Task WriteBytesAsync(string path, byte[] bytes, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _probe?.BeforeWrite(path);
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

    private void DeleteContainedDirectory(string path)
    {
        if (_root is null || !CorpusPathGuard.IsContained(Path.Combine(_root, "staging"), path) || !Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(path, recursive: true);
    }

    private static bool IsSnapshotId(string? snapshotId)
    {
        return snapshotId is not null
            && snapshotId.Length == 33
            && snapshotId[0] == 's'
            && snapshotId.Skip(1).All(Uri.IsHexDigit);
    }

    private static CorpusSnapshotCommit Failed(string status, string message)
    {
        return new CorpusSnapshotCommit { Status = status, Message = message };
    }
}
