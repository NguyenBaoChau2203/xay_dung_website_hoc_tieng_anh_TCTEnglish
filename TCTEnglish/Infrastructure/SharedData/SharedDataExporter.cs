using System.Collections;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Infrastructure.SharedData;

public sealed class SharedDataExporter(DbflashcardContext db, string webRoot, Dictionary<string, int> owners)
{
    private readonly Dictionary<string, IList> rows = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Kind, int Id), (object Row, string Key)> chosen = [];
    private readonly HashSet<(string Kind, int Id)> expanded = [];
    private Dictionary<string, SharedDataBinding> bindings = new(StringComparer.Ordinal);
    private string rootKey = "";

    public async Task<SharedRelease> ExportAsync(string root, string kind, int id, string key, string packageId, CancellationToken ct = default)
    {
        SharedDataFormat.AssertId(packageId); SharedDataFormat.AssertKey(key);
        if (key.Length > 100) throw new SharedDataException("Export root key must be 100 characters or shorter.");
        if (!SharedDataRegistry.Roots.Contains(kind)) throw new SharedDataException("Export a complete content root, not a child/user/history row.");
        Directory.CreateDirectory(root);
        await using var fileLock = new FileStream(SharedDataFormat.SafePath(root, ".export.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var catalogFile = SharedDataFormat.SafePath(root, "catalog.json");
        var catalog = await SharedDataFormat.ReadAsync<SharedCatalog>(catalogFile, 2_000_000, ct);
        var existing = await SharedDataFormat.LoadAsync(root, ct: ct);
        var previous = existing.Where(p => p.Package.Id == packageId).MaxBy(p => p.Package.Version);
        if (previous != null && previous.Package.RootKey != key) throw new SharedDataException("One authored package owns one root. Reuse its root key or create another package.");
        var version = previous?.Package.Version + 1 ?? 1;
        rootKey = key;
        await using var sourceLock = await SharedDataDatabaseLock.AcquireAsync(db, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        bindings = await db.SharedDataBindings.ToDictionaryAsync(b => b.Key, StringComparer.Ordinal, ct);
        var system = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == "system@tct.local", ct);
        if (system != null) owners["system"] = system.UserId;
        foreach (var item in SharedDataRegistry.Entities) rows[item.Key] = await item.Value.Load(db, ct);
        var rootSpec = SharedDataRegistry.Get(kind);
        var source = rows[kind].Cast<object>().SingleOrDefault(x => rootSpec.GetId(x) == id) ?? throw new SharedDataException("Selected source record does not exist.");
        var boundRoot = bindings.Values.SingleOrDefault(b => b.Kind == kind && b.EntityId == id);
        if (boundRoot != null && (boundRoot.Key != key || boundRoot.PackageId != packageId)) throw new SharedDataException("Selected source is already tracked; reuse its package/key.");
        Visit(kind, source, true);
        var records = new List<SharedRecord>(); var depends = new HashSet<string>(StringComparer.Ordinal);
        var assets = new Dictionary<string, (SharedAsset Manifest, byte[] Bytes)>(StringComparer.Ordinal);
        foreach (var item in chosen)
        {
            var spec = SharedDataRegistry.Get(item.Key.Kind);
            var binding = bindings.GetValueOrDefault(item.Value.Key);
            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var field in spec.Fields)
            {
                var value = spec.Property(field).GetValue(item.Value.Row);
                if (field.EndsWith("Url", StringComparison.Ordinal) && value is string url && url.StartsWith('/'))
                {
                    if (!url.StartsWith("/images/", StringComparison.Ordinal) && !url.StartsWith("/uploads/", StringComparison.Ordinal)) throw new SharedDataException("Only selected application media can be exported.");
                    var relative = Uri.UnescapeDataString(url.Split('?', '#')[0]).TrimStart('/');
                    var path = SharedDataFormat.SafePath(webRoot, relative);
                    if (!File.Exists(path)) throw new SharedDataException("Referenced source media is missing; repair it before export.");
                    if (new FileInfo(path).Length > 25_000_000) throw new SharedDataException("Selected media exceeds 25 MB; use an approved external HTTPS media source.");
                    var bytes = await File.ReadAllBytesAsync(path, ct); var hash = SharedDataFormat.Hash(bytes);
                    var file = hash + Path.GetExtension(path).ToLowerInvariant();
                    SharedDataFormat.AssertMedia(file, bytes);
                    assets[file] = (new(file, hash), bytes); value = "asset:" + file;
                }
                values[field] = JsonSerializer.SerializeToElement(value, SharedDataFormat.Json);
            }
            var references = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var reference in spec.References)
            {
                var parentId = spec.Property(reference.Property).GetValue(item.Value.Row) as int?;
                references[reference.Property] = parentId == null ? null : chosen[(reference.Kind, parentId.Value)].Key;
            }
            foreach (var owner in spec.Owners)
            {
                var userId = spec.Property(owner).GetValue(item.Value.Row) as int?;
                var alias = owners.FirstOrDefault(x => x.Value == userId).Key;
                if (alias == null) throw new SharedDataException("Source owner needs a private alias mapping; personal identity must not go into the package.");
                references[owner] = "owner:" + alias;
            }
            var projected = new SharedRecord(item.Value.Key, item.Key.Kind, values, references);
            if (binding != null && binding.PackageId != packageId)
            {
                if (SharedDataFormat.Digest(projected) != binding.Digest) throw new SharedDataException("A dependency has local edits. Export that package first.");
                var dependency = existing.Where(p => p.Package.Id == binding.PackageId).MaxBy(p => p.Package.Version) ?? throw new SharedDataException("Dependency must be exported and committed first.");
                depends.Add(binding.PackageId + ":" + dependency.Package.Version);
            }
            else records.Add(projected);
        }
        var currentKeys = records.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        // Retain old aggregate roots/containers. Retiring a used lesson is a
        // publication edit, never an automatic cascading database deletion.
        var removed = previous?.Package.Records.Where(r => !SharedDataRegistry.Roots.Contains(r.Kind)).Select(r => r.Key).Where(k => !currentKeys.Contains(k)).ToList() ?? [];
        var package = new SharedPackage(1, packageId, version, depends.Order(StringComparer.Ordinal).ToList(), records.OrderBy(r => r.Key, StringComparer.Ordinal).ToList(), removed, assets.Values.Select(a => a.Manifest).ToList(), key);
        var relativePath = $"packages/{packageId}/{version}/package.json";
        var packageFile = SharedDataFormat.SafePath(root, relativePath);
        if (Directory.Exists(Path.GetDirectoryName(packageFile))) throw new SharedDataException("Export version already exists; inspect the unfinished export instead of overwriting it.");
        Directory.CreateDirectory(Path.GetDirectoryName(packageFile)!);
        foreach (var asset in assets)
        {
            var target = SharedDataFormat.SafePath(Path.GetDirectoryName(packageFile)!, "assets/" + asset.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllBytesAsync(target, asset.Value.Bytes, ct);
        }
        var data = JsonSerializer.SerializeToUtf8Bytes(package, SharedDataFormat.Json);
        await File.WriteAllBytesAsync(packageFile, data, ct);
        var release = new SharedRelease(packageId, version, relativePath, SharedDataFormat.Hash(data));
        // Validate the complete candidate before touching source rows or publishing the index.
        var candidate = new SharedCatalog(1, [release]);
        var candidateName = ".export-candidate.json";
        await File.WriteAllTextAsync(SharedDataFormat.SafePath(root, candidateName), JsonSerializer.Serialize(candidate, SharedDataFormat.Json), ct);
        try { await SharedDataFormat.LoadAsync(root, candidateName, ct); }
        finally { File.Delete(SharedDataFormat.SafePath(root, candidateName)); }
        foreach (var record in records)
        {
            var item = chosen.Single(x => x.Value.Key == record.Key);
            var spec = SharedDataRegistry.Get(record.Kind);
            // Register the author's original IDs, so pulling their own package cannot duplicate it.
            var digest = SharedDataFormat.Digest(record);
            if (!bindings.TryGetValue(record.Key, out var binding))
            {
                binding = new() { Key = record.Key, Kind = record.Kind, EntityId = item.Key.Id, PackageId = packageId };
                db.SharedDataBindings.Add(binding);
            }
            binding.Digest = digest;
            foreach (var field in spec.Fields.Where(f => f.EndsWith("Url", StringComparison.Ordinal) && record.Values[f].ValueKind == JsonValueKind.String && record.Values[f].GetString()!.StartsWith("asset:", StringComparison.Ordinal)))
            {
                var file = record.Values[field].GetString()![6..];
                var target = SharedDataFormat.SafePath(webRoot, "uploads/shared/" + file);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (!File.Exists(target)) { await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None); await output.WriteAsync(assets[file].Bytes, ct); }
                else if (SharedDataFormat.Hash(await File.ReadAllBytesAsync(target, ct)) != assets[file].Manifest.Sha256) throw new SharedDataException("Existing managed asset has changed; it was not overwritten.");
                spec.Property(field).SetValue(item.Value.Row, "/uploads/shared/" + file);
            }
        }
        await db.SaveChangesAsync(ct);
        // Publish the nonsecret index atomically; credential stores are never involved.
        catalog.Releases.Add(release);
        var temporary = SharedDataFormat.SafePath(root, ".catalog-export.tmp");
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(catalog, SharedDataFormat.Json), ct);
        await transaction.CommitAsync(ct);
        File.Move(temporary, catalogFile, true);
        return release;
    }

    private void Visit(string kind, object row, bool children)
    {
        SharedDataRegistry.AssertShareable(row);
        var spec = SharedDataRegistry.Get(kind); var identity = (kind, spec.GetId(row));
        if (!chosen.ContainsKey(identity))
        {
            var bound = bindings.Values.SingleOrDefault(b => b.Kind == kind && b.EntityId == identity.Item2);
            chosen[identity] = (row, chosen.Count == 0 ? rootKey : bound?.Key ?? rootKey + "." + Guid.NewGuid().ToString("N"));
            foreach (var reference in spec.References)
            {
                var id = spec.Property(reference.Property).GetValue(row) as int?;
                if (id == null) continue;
                var parentSpec = SharedDataRegistry.Get(reference.Kind);
                var parent = rows[reference.Kind].Cast<object>().SingleOrDefault(r => parentSpec.GetId(r) == id) ?? throw new SharedDataException("Source has a missing content dependency.");
                Visit(reference.Kind, parent, kind == "ClassFolder" && reference.Kind == "Folder");
            }
        }
        if (!children || !expanded.Add(identity)) return;
        foreach (var child in SharedDataRegistry.Entities)
        foreach (var reference in child.Value.References.Where(r => r.Kind == kind && SharedDataRegistry.IsOwnedChild(kind, child.Key, r.Property)))
        foreach (var childRow in rows[child.Key].Cast<object>().Where(r => Equals(child.Value.Property(reference.Property).GetValue(r), identity.Item2)))
            Visit(child.Key, childRow, true);
    }
}
