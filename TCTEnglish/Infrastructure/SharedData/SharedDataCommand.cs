using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Infrastructure.SharedData;

public static class SharedDataCommand
{
    public static void AssertTarget(string connectionString, string environment, string target, bool productionApproved)
    {
        // Parse synonyms/duplicate aliases exactly as the actual provider does.
        var b = new MySqlConnector.MySqlConnectionStringBuilder(connectionString);
        var host = b.Server; var database = b.Database;
        var port = b.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var user = b.UserID;
        var loopback = host is "127.0.0.1" or "localhost" or "::1";
        var valid = target switch
        {
            "local" => environment == "Development" && loopback && database == "tctenglish_dev" && port == "3307" && user == "tct_dev",
            "ci" => environment == "Development" && Environment.GetEnvironmentVariable("CI") == "true" && loopback && database == "tct_ci" && user == "tct_ci" && port == "33307",
            "staging" => environment == "Staging" && loopback && database == "nhtctsnn_tctenglish_staging" && user == "nhtctsnn_tct_stage" && port == "3306",
            "production" => environment == "Production" && loopback && database == "nhtctsnn_tctenglish" && port == "3306" && productionApproved,
            _ => false
        };
        if (!valid) throw new SharedDataException("Shared-data target guard rejected this database/environment. No content was read or written.");
    }

    public static async Task<int> RunAsync(WebApplication app, string[] args, CancellationToken ct = default)
    {
        void Output(string message) => app.Logger.LogInformation("{SharedDataMessage}", message);
        string Arg(string name, string? fallback = null)
        {
            var matches = args.Where(x => x.StartsWith("--shared-" + name + "=", StringComparison.Ordinal)).ToArray();
            if (matches.Length > 1) throw new SharedDataException("Duplicate shared-data argument.");
            return matches.SingleOrDefault()?.Split('=', 2)[1] ?? fallback ?? throw new SharedDataException("Missing argument: --shared-" + name);
        }
        try
        {
            var action = Arg("data"); var target = Arg("target", "local");
            var root = Path.GetFullPath(Arg("root", Path.Combine(app.Environment.ContentRootPath, "..", "data", "shared")));
            var packages = await SharedDataFormat.LoadAsync(root, ct: ct);
            if (target == "production")
            {
                var approved = await SharedDataFormat.LoadAsync(root, "production.json", ct);
                if (approved.Any(a => !packages.Any(p => p.Release == a.Release))) throw new SharedDataException("Production approval does not match the reviewed catalog.");
                packages = approved;
            }
            if (action == "validate")
            {
                Output($"Shared data valid: {packages.Count} immutable releases, {packages.Sum(p => p.Package.Records.Count)} record revisions."); return 0;
            }
            if (action == "approve")
            {
                if (target != "local") throw new SharedDataException("Prepare production approval in a reviewed source checkout.");
                var id = Arg("package");
                if (!int.TryParse(Arg("version"), out var version)) throw new SharedDataException("Approval needs a package version.");
                var approved = await SharedDataFormat.LoadAsync(root, "production.json", ct);
                var selected = approved.ToDictionary(p => p.Package.Id + ":" + p.Package.Version, StringComparer.Ordinal);
                void Include(string packageId, int maximum)
                {
                    var revisions = packages.Where(p => p.Package.Id == packageId && p.Package.Version <= maximum).OrderBy(p => p.Package.Version).ToList();
                    if (revisions.Count != maximum) throw new SharedDataException("Approval requires a complete immutable version chain.");
                    foreach (var p in revisions)
                    {
                        if (!selected.TryAdd(p.Package.Id + ":" + p.Package.Version, p)) continue;
                        foreach (var dep in p.Package.DependsOn) { var pair = dep.Split(':'); if (pair.Length != 2 || !int.TryParse(pair[1], out var v)) throw new SharedDataException("Invalid package dependency."); Include(pair[0], v); }
                    }
                }
                Include(id, version);
                var manifest = new SharedCatalog(1, selected.Values.Select(p => p.Release).OrderBy(p => p.Id, StringComparer.Ordinal).ThenBy(p => p.Version).ToList());
                var temp = SharedDataFormat.SafePath(root, ".production-approval.tmp");
                await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(manifest, SharedDataFormat.Json), ct);
                File.Move(temp, SharedDataFormat.SafePath(root, "production.json"), true);
                Output("Production approval manifest prepared. Review its exact hashes in a PR; the production deployment gate and environment approval are still required."); return 0;
            }
            if (action is not ("sync" or "status" or "export" or "map-owner" or "migrate")) throw new SharedDataException("Choose shared-data validate, status, export, map-owner or sync.");
            if (action == "migrate" && target != "local") throw new SharedDataException("Shared CLI migration is Docker-local only; hosting must use its reviewed release bundle.");
            var connection = app.Configuration.GetConnectionString("DefaultConnection") ?? "";
            AssertTarget(connection, app.Environment.EnvironmentName, target,
                Environment.GetEnvironmentVariable("TCT_SHARED_PRODUCTION_APPROVED") == "true");
            if (action == "export" && target is not ("local" or "staging")) throw new SharedDataException("Content authoring/export is limited to local/staging.");
            var webRoot = Path.GetFullPath(Arg("webroot", app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot")));
            if (target is "production" or "staging")
            {
                var expectedSite = target == "production" ? "httpdocs" : "staging.tctenglish.site";
                if (Path.GetFileName(webRoot) != "wwwroot" || Path.GetFileName(Path.GetDirectoryName(webRoot)) != expectedSite) throw new SharedDataException("Hosted content media must stay in the verified site's wwwroot.");
            }
            var ownerFile = Arg("owners", Path.Combine(app.Environment.ContentRootPath, "..", ".appdata", "local", "shared-data", "owners.json"));
            var owners = File.Exists(ownerFile) ? await SharedDataFormat.ReadAsync<Dictionary<string, int>>(ownerFile, 100000, ct) : new(StringComparer.Ordinal);
            foreach (var owner in owners) { SharedDataFormat.AssertKey(owner.Key); if (owner.Value < 1 || owner.Key == "system") throw new SharedDataException("Owner mappings require positive IDs; system is reserved."); }
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            if (action == "migrate")
            {
                await db.Database.MigrateAsync(ct);
                Output("Committed local schema migrations applied from the isolated CLI build."); return 0;
            }
            if (!owners.ContainsKey("team-owner") && target is "local" or "staging")
            {
                var candidates = db.Users.AsNoTracking().Where(u => u.Role == "Admin");
                if (target == "staging") candidates = candidates.Where(u => u.Email == "admin@tct-stage.local");
                var ownerId = await candidates.OrderBy(u => u.UserId).Select(u => (int?)u.UserId).FirstOrDefaultAsync(ct);
                if (ownerId.HasValue) owners["team-owner"] = ownerId.Value;
            }
            if (action == "map-owner")
            {
                var alias = Arg("alias"); SharedDataFormat.AssertKey(alias);
                if (target != "local" || alias == "system" || !int.TryParse(Arg("id"), out var userId) || !await db.Users.AnyAsync(u => u.UserId == userId, ct)) throw new SharedDataException("Owner alias needs an existing local user; system is reserved.");
                owners[alias] = userId; Directory.CreateDirectory(Path.GetDirectoryName(ownerFile)!);
                var temp = ownerFile + ".tmp";
                await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(owners, SharedDataFormat.Json), ct); File.Move(temp, ownerFile, true);
                Output("Private owner alias saved; no password, role or user record changed."); return 0;
            }
            if (action == "export")
            {
                if (!int.TryParse(Arg("id"), out var id) || id < 1) throw new SharedDataException("Export requires a positive source ID.");
                var release = await new SharedDataExporter(db, webRoot, owners).ExportAsync(root, Arg("kind"), id, Arg("key"), Arg("package"), ct);
                Output($"Exported {release.Id}/{release.Version}; review {release.Path}, media and catalog.json before committing."); return 0;
            }
            var engine = new SharedDataEngine(db, webRoot, owners);
            if (action == "status")
            {
                foreach (var line in await engine.InspectAsync(packages, ct)) Output(line);
                return 0;
            }
            await engine.ApplyAsync(packages, ct);
            foreach (var line in engine.Report.Take(100)) Output(line);
            Output($"Shared-data sync complete: {packages.Count} catalog releases checked, {engine.Report.Count} record operations. Personal data preserved.");
            return 0;
        }
        catch (SharedDataException ex) { Console.Error.WriteLine(ex.Message); return 2; }
        catch (Exception ex) when (ex is JsonException or IOException or DbUpdateException or DbException or InvalidOperationException or ArgumentException)
        {
            Console.Error.WriteLine($"Shared-data operation failed ({ex.GetType().Name}). No credentials or content values are logged. Inspect package validity, owner mappings and database readiness."); return 3;
        }
    }
}
