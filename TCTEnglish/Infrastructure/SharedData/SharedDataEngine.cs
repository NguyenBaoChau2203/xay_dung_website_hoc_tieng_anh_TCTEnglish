using System.Collections;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Infrastructure.SharedData;

/// <summary>Private CLI service; the caller verifies environment/DB before use.</summary>
public sealed class SharedDataEngine(DbflashcardContext db, string webRoot, Dictionary<string, int> owners)
{
    private Dictionary<string, SharedDataBinding> bindings = new(StringComparer.Ordinal);
    private Dictionary<string, IList> rows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> selected = new(StringComparer.Ordinal);
    private readonly List<string> createdFiles = [];
    public List<string> Report { get; } = [];

    public async Task<List<string>> InspectAsync(IReadOnlyList<LoadedSharedPackage> packages, CancellationToken ct = default)
    {
        bindings = await db.SharedDataBindings.AsNoTracking().ToDictionaryAsync(b => b.Key, StringComparer.Ordinal, ct);
        var applied = await db.SharedDataApplications.AsNoTracking().ToListAsync(ct);
        rows = await LoadRowsAsync(bindings.Values.Select(b => b.Kind).Distinct(StringComparer.Ordinal), false, ct);
        var system = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == "system@tct.local", ct);
        if (system != null) owners["system"] = system.UserId;
        var output = packages.Select(p => $"{(applied.Any(a => a.PackageId == p.Package.Id && a.Version == p.Package.Version && a.Digest == p.Release.Sha256) ? "applied" : "pending")} {p.Package.Id}/{p.Package.Version}").ToList();
        foreach (var binding in bindings.Values)
        {
            var row = FindRow(binding);
            var expected = packages.Reverse().SelectMany(p => p.Package.Records).FirstOrDefault(r => r.Key == binding.Key);
            if (row == null || SharedDataFormat.Digest(Project(binding.Key, binding.Kind, row, expected)) != binding.Digest)
                output.Add($"local-edit {binding.Kind} {binding.Key}");
        }
        return output;
    }

    public async Task ApplyAsync(IReadOnlyList<LoadedSharedPackage> packages, CancellationToken ct = default)
    {
        if (db.ChangeTracker.HasChanges()) throw new SharedDataException("Use a clean, dedicated content context.");
        selected.Clear(); Report.Clear();
        var connection = db.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close) await db.Database.OpenConnectionAsync(ct);
        var mysql = db.Database.IsMySql();
        var lockName = "tct.shared." + SharedDataFormat.Hash(System.Text.Encoding.UTF8.GetBytes(connection.Database))[..24];
        var locked = false;
        try
        {
            if (mysql)
            {
                var result = await ScalarAsync("SELECT GET_LOCK(@name,30)", null, ct, ("@name", lockName));
                if (Convert.ToInt32(result) != 1) throw new SharedDataException("Another data release is running; retry later.");
                locked = true;
            }
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                bindings = await db.SharedDataBindings.ToDictionaryAsync(x => x.Key, StringComparer.Ordinal, ct);
                var applied = await db.SharedDataApplications.ToListAsync(ct);
                foreach (var previous in applied)
                {
                    var file = packages.FirstOrDefault(p => p.Package.Id == previous.PackageId && p.Package.Version == previous.Version);
                    if (file != null && file.Release.Sha256 != previous.Digest) throw new SharedDataException($"Applied package was rewritten: {previous.PackageId}/{previous.Version}.");
                }
                var pending = packages.Where(p => !applied.Any(a => a.PackageId == p.Package.Id && a.Version == p.Package.Version)).ToList();
                var completed = applied.Select(a => a.PackageId + ":" + a.Version).ToHashSet(StringComparer.Ordinal);
                var kinds = pending.SelectMany(p => p.Package.Records.Select(r => r.Kind)).Concat(pending.SelectMany(p => p.Package.DeleteKeys).Where(bindings.ContainsKey).Select(k => bindings[k].Kind)).Distinct(StringComparer.Ordinal);
                rows = await LoadRowsAsync(kinds, mysql, ct);
                foreach (var package in OrderPackages(pending, completed))
                {
                    var current = package.Package;
                    var lastVersion = applied.Where(a => a.PackageId == current.Id).Select(a => a.Version).DefaultIfEmpty(0).Max();
                    if (current.Version != lastVersion + 1) throw new SharedDataException($"Package version must follow {lastVersion}: {current.Id}.");
                    await EnsureOwnersAsync(current, ct);
                    var ordered = OrderRecords(current.Records);
                    foreach (var layer in ordered)
                    {
                        var newBindings = new List<(SharedDataBinding Binding, object Row, SharedEntitySpec Spec)>();
                        foreach (var record in layer)
                        {
                            var spec = SharedDataRegistry.Get(record.Kind);
                            ValidateValues(record, spec);
                            var digest = SharedDataFormat.Digest(record);
                            object row;
                            if (bindings.TryGetValue(record.Key, out var binding))
                            {
                                if (binding.Kind != record.Kind || binding.PackageId != current.Id) throw new SharedDataException($"Stable key belongs to another kind/package: {record.Key}.");
                                row = FindRow(binding) ?? throw new SharedDataException($"Tracked record was removed locally: {record.Key}. Export/resolve the change first.");
                                SharedDataRegistry.AssertShareable(row);
                                var actual = SharedDataFormat.Digest(Project(record.Key, record.Kind, row, record));
                                if (actual != digest && actual != binding.Digest) throw new SharedDataException($"Local edit conflict: {record.Key}. Export or resolve it; nothing in this batch was committed.");
                                if (actual != digest) SetValues(record, spec, row);
                                binding.Digest = digest;
                                selected[record.Key] = row;
                                Report.Add($"{(actual == digest ? "unchanged" : "updated")} {record.Kind} {record.Key}");
                            }
                            else
                            {
                                // Adopt only a unique exact match, never a title/ID match or another owner's item.
                                var occupied = bindings.Values.Where(x => x.Kind == record.Kind).Select(x => x.EntityId).ToHashSet();
                                var candidates = rows[record.Kind].Cast<object>().Where(x => !occupied.Contains(spec.GetId(x)))
                                    .Where(IsShareable).Where(x => SharedDataFormat.Digest(Project(record.Key, record.Kind, x, record)) == digest).ToList();
                                if (candidates.Count > 1) throw new SharedDataException($"Ambiguous existing data: {record.Key}. Bind the intended record explicitly before release.");
                                row = candidates.SingleOrDefault() ?? Activator.CreateInstance(spec.Type)!;
                                if (candidates.Count == 0)
                                {
                                    SetValues(record, spec, row); SetCreationDates(row); db.Add(row); rows[record.Kind].Add(row);
                                }
                                binding = new() { Key = record.Key, Kind = record.Kind, PackageId = current.Id, Digest = digest };
                                bindings.Add(record.Key, binding); selected[record.Key] = row;
                                newBindings.Add((binding, row, spec));
                                Report.Add($"{(candidates.Count == 0 ? "added" : "adopted")} {record.Kind} {record.Key}");
                            }
                        }
                        await db.SaveChangesAsync(ct);
                        foreach (var item in newBindings) { item.Binding.EntityId = item.Spec.GetId(item.Row); db.SharedDataBindings.Add(item.Binding); }
                        await db.SaveChangesAsync(ct);
                    }
                    await DeleteAsync(current, ct);
                    var receipt = new SharedDataApplication { PackageId = current.Id, Version = current.Version, Digest = package.Release.Sha256, AppliedAtUtc = DateTime.UtcNow };
                    db.SharedDataApplications.Add(receipt); applied.Add(receipt); completed.Add(current.Id + ":" + current.Version);
                    await db.SaveChangesAsync(ct);
                }
                // Assets are immutable: repair missing copies and detect local tampering on reruns too.
                foreach (var package in packages) await CopyAssetsAsync(package, ct);
                await transaction.CommitAsync(ct);
                createdFiles.Clear();
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                foreach (var file in createdFiles)
                {
                    // Only this operation's new immutable objects, never an existing upload.
                    var name = Path.GetFileName(file);
                    if (File.Exists(file) && SharedDataFormat.Hash(await File.ReadAllBytesAsync(file, CancellationToken.None)) == name.Split('.')[0]) File.Delete(file);
                }
                createdFiles.Clear();
                throw;
            }
        }
        finally
        {
            if (locked) await ScalarAsync("SELECT RELEASE_LOCK(@name)", null, CancellationToken.None, ("@name", lockName));
            if (close) await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<Dictionary<string, IList>> LoadRowsAsync(IEnumerable<string> kinds, bool lockRows, CancellationToken ct)
    {
        var result = new Dictionary<string, IList>(StringComparer.Ordinal);
        foreach (var kind in kinds.Order(StringComparer.Ordinal))
        {
            var spec = SharedDataRegistry.Get(kind);
            if (lockRows)
            {
                var entity = db.Model.FindEntityType(spec.Type)!;
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                var column = entity.FindProperty(spec.Id)!.GetColumnName(table)!;
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandText = $"SELECT `{column}` FROM `{table.Name}` FOR UPDATE";
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) { }
            }
            result[kind] = await spec.Load(db, ct);
        }
        return result;
    }

    private static bool IsShareable(object row)
    {
        try { SharedDataRegistry.AssertShareable(row); return true; } catch (SharedDataException) { return false; }
    }

    private async Task EnsureOwnersAsync(SharedPackage package, CancellationToken ct)
    {
        foreach (var alias in package.Records.SelectMany(r => SharedDataRegistry.Get(r.Kind).Owners.Select(p => r.References[p]![6..])).Distinct(StringComparer.Ordinal))
        {
            if (owners.ContainsKey(alias))
            {
                if (!await db.Users.AnyAsync(u => u.UserId == owners[alias], ct)) throw new SharedDataException($"Owner mapping is invalid: {alias}.");
                continue;
            }
            if (alias != "system") throw new SharedDataException($"Missing private owner mapping: {alias}. See shared-data runbook.");
            var system = await db.Users.SingleOrDefaultAsync(u => u.Email == "system@tct.local", ct);
            if (system == null)
            {
                system = new User { Email = "system@tct.local", FullName = "TCT System", Role = "System", Status = UserStatus.Blocked, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), 12), CreatedAt = DateTime.UtcNow };
                db.Users.Add(system); await db.SaveChangesAsync(ct);
            }
            owners[alias] = system.UserId;
        }
    }

    private void ValidateValues(SharedRecord record, SharedEntitySpec spec)
    {
        var model = db.Model.FindEntityType(spec.Type)!;
        foreach (var field in spec.AllFields)
        {
            if (!record.Values.TryGetValue(field, out var value)) continue;
            var property = model.FindProperty(field)!;
            if (value.ValueKind == JsonValueKind.Null && !property.IsNullable) throw new SharedDataException($"Required content value missing: {record.Key}/{field}.");
            if (value.ValueKind == JsonValueKind.String && property.GetMaxLength() is int max && value.GetString()!.Length > max) throw new SharedDataException($"Content value exceeds schema length: {record.Key}/{field}.");
        }
    }

    private void SetValues(SharedRecord record, SharedEntitySpec spec, object row)
    {
        foreach (var field in spec.AllFields)
        {
            var property = spec.Property(field);
            // A missing optional field (older package) imports as the property default.
            var value = record.Values.TryGetValue(field, out var element)
                ? element.Deserialize(property.PropertyType, SharedDataFormat.Json)
                : (property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null);
            if (field.EndsWith("Url", StringComparison.Ordinal) && value is string text && text.StartsWith("asset:", StringComparison.Ordinal)) value = "/uploads/shared/" + text[6..];
            property.SetValue(row, value);
        }
        foreach (var reference in spec.References)
        {
            var key = record.References[reference.Property];
            spec.Property(reference.Property).SetValue(row, key == null ? null : ResolveId(key, reference.Kind));
        }
        foreach (var owner in spec.Owners) spec.Property(owner).SetValue(row, owners[record.References[owner]![6..]]);
    }

    private static void SetCreationDates(object row)
    {
        foreach (var name in new[] { "CreatedAt", "CreatedAtUtc", "ApprovedAtUtc", "AddedAt" })
            if (row.GetType().GetProperty(name) is { PropertyType: var type } p && type == typeof(DateTime)) p.SetValue(row, DateTime.UtcNow);
    }

    private int ResolveId(string key, string kind)
    {
        if (selected.TryGetValue(key, out var row) && row.GetType() == SharedDataRegistry.Get(kind).Type) return SharedDataRegistry.Get(kind).GetId(row);
        if (bindings.TryGetValue(key, out var binding) && binding.Kind == kind) return binding.EntityId;
        throw new SharedDataException($"Missing/wrong-kind dependency: {key}.");
    }

    private object? FindRow(SharedDataBinding binding) => rows.TryGetValue(binding.Kind, out var set) ? set.Cast<object>().SingleOrDefault(x => SharedDataRegistry.Get(binding.Kind).GetId(x) == binding.EntityId) : null;

    internal SharedRecord Project(string key, string kind, object row, SharedRecord? expected = null)
    {
        var spec = SharedDataRegistry.Get(kind);
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var field in spec.AllFields)
        {
            var value = spec.Property(field).GetValue(row);
            if (spec.OptionalFields.Contains(field) && spec.IsDefaultOptionalValue(field, value) && expected?.Values.ContainsKey(field) != true) continue;
            if (field.EndsWith("Url", StringComparison.Ordinal) && value is string s && s.StartsWith("/uploads/shared/", StringComparison.Ordinal)) value = "asset:" + s[16..];
            values[field] = JsonSerializer.SerializeToElement(value, SharedDataFormat.Json);
        }
        var refs = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var reference in spec.References)
        {
            var id = spec.Property(reference.Property).GetValue(row) as int?;
            if (id == null) { refs[reference.Property] = null; continue; }
            refs[reference.Property] = bindings.Values.FirstOrDefault(b => b.Kind == reference.Kind && b.EntityId == id)?.Key;
            if (refs[reference.Property] == null && expected?.References[reference.Property] is { } desired && selected.TryGetValue(desired, out var parent) && SharedDataRegistry.Get(reference.Kind).GetId(parent) == id)
                refs[reference.Property] = desired;
            refs[reference.Property] ??= "unmapped:" + reference.Kind + ":" + id;
        }
        foreach (var owner in spec.Owners)
        {
            var id = spec.Property(owner).GetValue(row) as int?;
            var desired = expected?.References[owner];
            refs[owner] = desired != null && owners.TryGetValue(desired[6..], out var desiredId) && desiredId == id ? desired :
                owners.FirstOrDefault(p => p.Value == id).Key is { } alias ? "owner:" + alias : "owner:unmapped";
        }
        return new(key, kind, values, refs);
    }

    private static IEnumerable<LoadedSharedPackage> OrderPackages(List<LoadedSharedPackage> pending, HashSet<string> completed)
    {
        var remaining = new List<LoadedSharedPackage>(pending);
        var available = new HashSet<string>(completed, StringComparer.Ordinal);
        while (remaining.Count > 0)
        {
            var next = remaining.FirstOrDefault(p => p.Package.DependsOn.All(available.Contains) && (p.Package.Version == 1 || available.Contains(p.Package.Id + ":" + (p.Package.Version - 1))));
            if (next == null) throw new SharedDataException("Package dependencies are missing or cyclic.");
            remaining.Remove(next); available.Add(next.Package.Id + ":" + next.Package.Version); yield return next;
        }
    }

    private static List<List<SharedRecord>> OrderRecords(List<SharedRecord> records)
    {
        var remaining = records.ToDictionary(r => r.Key, StringComparer.Ordinal);
        var ordered = new List<List<SharedRecord>>();
        while (remaining.Count > 0)
        {
            var layer = remaining.Values.Where(r => SharedDataRegistry.Get(r.Kind).References.All(f => r.References[f.Property] is not { } k || !remaining.ContainsKey(k))).ToList();
            if (layer.Count == 0) throw new SharedDataException("Content dependency cycle.");
            foreach (var item in layer) remaining.Remove(item.Key);
            ordered.Add(layer);
        }
        return ordered;
    }

    private async Task DeleteAsync(SharedPackage package, CancellationToken ct)
    {
        var pending = package.DeleteKeys.ToList();
        while (pending.Count > 0)
        {
            var progress = false;
            foreach (var key in pending.ToList())
            {
                if (!bindings.TryGetValue(key, out var binding) || binding.PackageId != package.Id) throw new SharedDataException($"Deletion must refer to this package's tracked record: {key}.");
                if (SharedDataRegistry.Roots.Contains(binding.Kind)) throw new SharedDataException("Aggregate roots cannot be deleted by data sync. Unpublish/deactivate them or request a separate reviewed cleanup.");
                var row = FindRow(binding);
                if (row == null) { db.SharedDataBindings.Remove(binding); bindings.Remove(key); pending.Remove(key); progress = true; continue; }
                if (SharedDataFormat.Digest(Project(key, binding.Kind, row)) != binding.Digest) throw new SharedDataException($"Local deletion conflict: {key}.");
                var entityType = db.Model.FindEntityType(row.GetType())!;
                var blocked = false;
                foreach (var fk in entityType.GetReferencingForeignKeys())
                {
                    if (fk.Properties.Count != 1) throw new SharedDataException("Composite dependent requires an explicit adapter.");
                    var table = StoreObjectIdentifier.Table(fk.DeclaringEntityType.GetTableName()!, fk.DeclaringEntityType.GetSchema());
                    var column = fk.Properties[0].GetColumnName(table)!;
                    if (Convert.ToInt64(await ScalarAsync($"SELECT COUNT(*) FROM `{table.Name}` WHERE `{column}`=@id", db.Database.CurrentTransaction?.GetDbTransaction(), ct, ("@id", binding.EntityId))) > 0) { blocked = true; break; }
                }
                if (blocked) continue;
                db.Remove(row); db.SharedDataBindings.Remove(binding); bindings.Remove(key); rows[binding.Kind].Remove(row); pending.Remove(key); progress = true;
                await db.SaveChangesAsync(ct);
                Report.Add($"removed {binding.Kind} {key}");
            }
            if (!progress) throw new SharedDataException("Deletion has dependent content or learner history. Keep the old record or publish a separate content edition; no cascade was allowed.");
        }
    }

    private async Task CopyAssetsAsync(LoadedSharedPackage package, CancellationToken ct)
    {
        foreach (var asset in package.Package.Assets)
        {
            var source = SharedDataFormat.SafePath(package.Directory, "assets/" + asset.File);
            var target = SharedDataFormat.SafePath(webRoot, "uploads/shared/" + asset.File);
            var bytes = await File.ReadAllBytesAsync(source, ct);
            if (SharedDataFormat.Hash(bytes) != asset.Sha256) throw new SharedDataException("Media changed during import.");
            if (File.Exists(target))
            {
                if (SharedDataFormat.Hash(await File.ReadAllBytesAsync(target, ct)) != asset.Sha256) throw new SharedDataException("Local managed media conflict; existing file preserved.");
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using (var file = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { createdFiles.Add(target); await file.WriteAsync(bytes, ct); }
        }
    }

    private async Task<object?> ScalarAsync(string sql, DbTransaction? transaction, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = sql; command.Transaction = transaction;
        foreach (var item in parameters) { var p = command.CreateParameter(); p.ParameterName = item.Name; p.Value = item.Value; command.Parameters.Add(p); }
        return await command.ExecuteScalarAsync(ct);
    }
}
