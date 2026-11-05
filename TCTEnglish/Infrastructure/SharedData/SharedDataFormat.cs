using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Globalization;

namespace TCTEnglish.Infrastructure.SharedData;

public sealed class SharedDataException(string message) : Exception(message);
public sealed record SharedRelease(string Id, int Version, string Path, string Sha256);
public sealed record SharedCatalog(int SchemaVersion, List<SharedRelease> Releases);
public sealed record SharedAsset(string File, string Sha256);
public sealed record SharedRecord(string Key, string Kind, Dictionary<string, JsonElement> Values, Dictionary<string, string?> References);
public sealed record SharedPackage(int SchemaVersion, string Id, int Version, List<string> DependsOn, List<SharedRecord> Records, List<string> DeleteKeys, List<SharedAsset> Assets, string? RootKey = null);
public sealed record LoadedSharedPackage(SharedRelease Release, SharedPackage Package, string Directory);

public static partial class SharedDataFormat
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false
    };
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{2,119}$", RegexOptions.CultureInvariant)] private static partial Regex PackageIdPattern();
    [GeneratedRegex("^[a-z0-9][a-z0-9._:-]{2,159}$", RegexOptions.CultureInvariant)] private static partial Regex KeyPattern();
    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)] private static partial Regex DigestPattern();
    [GeneratedRegex("^[a-f0-9]{64}\\.(png|jpg|jpeg|gif|webp|mp3|wav|ogg|mp4|webm|vtt|srt)$", RegexOptions.CultureInvariant)] private static partial Regex AssetPattern();

    public static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    public static string Digest<T>(T value) => Hash(Encoding.UTF8.GetBytes(Canonical(JsonSerializer.SerializeToElement(value, Json))));
    public static string Canonical(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(',', value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(',', value.EnumerateArray().Select(Canonical)) + "]",
        JsonValueKind.Number => value.TryGetDecimal(out var number) ? number.ToString("G29", CultureInfo.InvariantCulture) : value.GetDouble().ToString("R", CultureInfo.InvariantCulture),
        JsonValueKind.String => JsonSerializer.Serialize(value.GetString()),
        _ => value.GetRawText()
    };
    public static void AssertId(string id) { if (!PackageIdPattern().IsMatch(id)) throw new SharedDataException("Invalid package ID."); }
    public static void AssertKey(string key) { if (!KeyPattern().IsMatch(key)) throw new SharedDataException("Invalid stable record/owner key."); }

    public static string SafePath(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains('\\') || relative.Contains(':') || relative.Contains('\0') || relative.Split('/').Any(x => x is ".." or "." or ""))
            throw new SharedDataException("Unsafe package path.");
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!full.StartsWith(fullRoot + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new SharedDataException("Package path escaped root.");
        for (var p = full; p != null; p = Path.GetDirectoryName(p))
        {
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new SharedDataException("Package/media paths cannot traverse links.");
        }
        return full;
    }

    public static async Task<List<LoadedSharedPackage>> LoadAsync(string root, string catalogName = "catalog.json", CancellationToken ct = default)
    {
        var catalog = await ReadAsync<SharedCatalog>(SafePath(root, catalogName), 2_000_000, ct);
        if (catalog.SchemaVersion != 1 || catalog.Releases == null || catalog.Releases.Count > 2000) throw new SharedDataException("Unsupported shared catalog.");
        var result = new List<LoadedSharedPackage>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var release in catalog.Releases)
        {
            AssertId(release.Id);
            if (release.Version < 1 || !DigestPattern().IsMatch(release.Sha256) || !seen.Add(release.Id + ":" + release.Version))
                throw new SharedDataException("Invalid/duplicate catalog release.");
            if (release.Path != $"packages/{release.Id}/{release.Version}/package.json") throw new SharedDataException("Package must use its immutable version directory.");
            var file = SafePath(root, release.Path);
            if (new FileInfo(file).Length > 8_000_000) throw new SharedDataException("Package exceeds size limit.");
            var bytes = await File.ReadAllBytesAsync(file, ct);
            if (bytes.Length > 8_000_000 || Hash(bytes) != release.Sha256) throw new SharedDataException($"Package checksum/size mismatch: {release.Id}/{release.Version}.");
            RejectDuplicateProperties(bytes);
            var package = JsonSerializer.Deserialize<SharedPackage>(bytes, Json) ?? throw new SharedDataException("Empty package.");
            if (package.SchemaVersion != 1 || package.Id != release.Id || package.Version != release.Version || package.Records == null || package.Assets == null || package.DeleteKeys == null || package.DependsOn == null || package.Records.Count > 10000)
                throw new SharedDataException("Invalid package identity/schema.");
            if (package.Assets.Count > 200 || package.DeleteKeys.Count > 10000 || package.DependsOn.Count > 200) throw new SharedDataException("Package item limit exceeded.");
            if (package.RootKey != null) AssertKey(package.RootKey);
            foreach (var dependency in package.DependsOn)
            {
                var pair = dependency.Split(':');
                if (pair.Length != 2 || !int.TryParse(pair[1], out var revision) || revision < 1 || pair[0] == package.Id) throw new SharedDataException("Invalid package dependency.");
                AssertId(pair[0]);
            }
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in package.Records)
            {
                AssertKey(row.Key);
                var spec = SharedDataRegistry.Get(row.Kind);
                if (!keys.Add(row.Key) || row.Values == null || row.References == null || !spec.Fields.Order().SequenceEqual(row.Values.Keys.Order()) ||
                    !spec.References.Select(x => x.Property).Concat(spec.Owners).Order().SequenceEqual(row.References.Keys.Order()))
                    throw new SharedDataException($"Invalid fields/references or duplicate stable key: {row.Key}.");
                foreach (var field in spec.Fields)
                {
                    var property = spec.Property(field);
                    try { _ = row.Values[field].Deserialize(property.PropertyType, Json); }
                    catch (JsonException) { throw new SharedDataException($"Invalid value type: {row.Key}/{field}."); }
                    if (row.Values[field].ValueKind == JsonValueKind.String && row.Values[field].GetString() is { } text)
                    {
                        if (text.Length > 2_000_000 || text.Contains("sk-proj-", StringComparison.Ordinal) || text.Contains("-----BEGIN PRIVATE KEY", StringComparison.Ordinal))
                            throw new SharedDataException("Content appears to contain credentials or exceeds the size limit.");
                        if (field.EndsWith("Url", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(text) && !text.StartsWith("asset:", StringComparison.Ordinal) &&
                            (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo)))
                            throw new SharedDataException($"Local files must use a managed asset reference: {row.Key}/{field}.");
                    }
                }
                foreach (var reference in spec.References)
                {
                    var key = row.References[reference.Property];
                    if (key == null && !reference.Optional) throw new SharedDataException("Required content reference missing.");
                    if (key != null) AssertKey(key);
                }
                foreach (var owner in spec.Owners)
                {
                    var alias = row.References[owner];
                    if (alias == null || !alias.StartsWith("owner:", StringComparison.Ordinal)) throw new SharedDataException("Explicit logical owner alias required.");
                    AssertKey(alias[6..]);
                }
            }
            foreach (var key in package.DeleteKeys) { AssertKey(key); if (!keys.Add(key)) throw new SharedDataException("Duplicate/conflicting deletion."); }
            var directory = Path.GetDirectoryName(file)!;
            var assets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asset in package.Assets)
            {
                if (!AssetPattern().IsMatch(asset.File) || !assets.Add(asset.File) || !DigestPattern().IsMatch(asset.Sha256) || !asset.File.StartsWith(asset.Sha256 + ".", StringComparison.Ordinal)) throw new SharedDataException("Invalid managed media manifest.");
                var assetPath = SafePath(directory, "assets/" + asset.File);
                if (new FileInfo(assetPath).Length > 25_000_000) throw new SharedDataException("Media exceeds size limit; use an approved external HTTPS media source.");
                var assetBytes = await File.ReadAllBytesAsync(assetPath, ct);
                if (assetBytes.Length > 25_000_000 || Hash(assetBytes) != asset.Sha256) throw new SharedDataException("Media checksum/size mismatch.");
                AssertMedia(asset.File, assetBytes);
            }
            foreach (var reference in package.Records.SelectMany(r => r.Values.Where(p => p.Key.EndsWith("Url", StringComparison.Ordinal)).Select(p => p.Value)).Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!).Where(v => v.StartsWith("asset:", StringComparison.Ordinal)))
                if (!assets.Contains(reference[6..])) throw new SharedDataException("Managed media is missing from package manifest.");
            result.Add(new(release, package, directory));
        }
        if (catalogName != ".export-candidate.json") ValidateGraph(result);
        return result.OrderBy(x => x.Package.Id, StringComparer.Ordinal).ThenBy(x => x.Package.Version).ToList();
    }

    private static void ValidateGraph(List<LoadedSharedPackage> packages)
    {
        var remaining = packages.ToList(); var available = new HashSet<string>(StringComparer.Ordinal);
        var identities = new Dictionary<string, (string Package, string Kind)>(StringComparer.Ordinal);
        while (remaining.Count > 0)
        {
            var next = remaining.FirstOrDefault(p => p.Package.DependsOn.All(available.Contains) && (p.Package.Version == 1 || available.Contains(p.Package.Id + ":" + (p.Package.Version - 1))));
            if (next == null) throw new SharedDataException("Catalog has a version gap, missing dependency or cycle.");
            remaining.Remove(next);
            foreach (var row in next.Package.Records)
            {
                var identity = (next.Package.Id, row.Kind);
                if (identities.TryGetValue(row.Key, out var old) && old != identity) throw new SharedDataException("Stable key cannot change kind or package ownership.");
                identities[row.Key] = identity;
            }
            foreach (var row in next.Package.Records)
            foreach (var reference in SharedDataRegistry.Get(row.Kind).References)
                if (row.References[reference.Property] is { } key && (!identities.TryGetValue(key, out var parent) || parent.Kind != reference.Kind)) throw new SharedDataException("Missing/wrong-kind stable reference or package dependency.");
            foreach (var key in next.Package.DeleteKeys)
            {
                if (!identities.TryGetValue(key, out var owner) || owner.Package != next.Package.Id || SharedDataRegistry.Roots.Contains(owner.Kind)) throw new SharedDataException("Deletion must address an owned child record, never an aggregate root.");
                identities.Remove(key);
            }
            available.Add(next.Package.Id + ":" + next.Package.Version);
        }
    }

    public static void AssertMedia(string file, byte[] bytes)
    {
        bool Starts(params byte[] prefix) => bytes.AsSpan().StartsWith(prefix);
        bool At(int offset, string value) => bytes.Length >= offset + value.Length && Encoding.ASCII.GetString(bytes, offset, value.Length) == value;
        var valid = Path.GetExtension(file) switch
        {
            ".png" => Starts(137,80,78,71,13,10,26,10),
            ".jpg" or ".jpeg" => Starts(255,216,255),
            ".gif" => At(0,"GIF87a") || At(0,"GIF89a"),
            ".webp" => At(0,"RIFF") && At(8,"WEBP"),
            ".mp3" => At(0,"ID3") || (bytes.Length > 1 && bytes[0] == 255 && (bytes[1] & 224) == 224),
            ".wav" => At(0,"RIFF") && At(8,"WAVE"),
            ".ogg" => At(0,"OggS"),
            ".mp4" => At(4,"ftyp"),
            ".webm" => Starts(26,69,223,163),
            ".vtt" => Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF').StartsWith("WEBVTT", StringComparison.Ordinal),
            ".srt" => !bytes.Contains((byte)0) && Encoding.UTF8.GetString(bytes).Contains(" --> ", StringComparison.Ordinal),
            _ => false
        };
        if (!valid) throw new SharedDataException("Media header does not match the allowed extension.");
    }

    private static void RejectDuplicateProperties(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        void Visit(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject()) { if (!names.Add(property.Name)) throw new SharedDataException("Duplicate JSON properties are not allowed."); Visit(property.Value); }
            }
            else if (value.ValueKind == JsonValueKind.Array) foreach (var child in value.EnumerateArray()) Visit(child);
        }
        Visit(document.RootElement);
    }

    public static async Task<T> ReadAsync<T>(string path, int limit, CancellationToken ct = default)
    {
        if (new FileInfo(path).Length > limit) throw new SharedDataException("Shared manifest exceeds size limit.");
        var bytes = await File.ReadAllBytesAsync(path, ct); RejectDuplicateProperties(bytes);
        return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new SharedDataException("Empty shared manifest.");
    }
}
