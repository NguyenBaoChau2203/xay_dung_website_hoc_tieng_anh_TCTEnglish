using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using TCTEnglish.Infrastructure.ListeningCatalog;
using TCTEnglish.Services.Speaking;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
var repo = Path.GetFullPath(args[1]);
var state = Path.Combine(repo, ".appdata/local/reach-listening-import");
Directory.CreateDirectory(state);
using var batchLock = new FileStream(Path.Combine(state, "import.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
var manifest = await Read<Manifest>(Path.Combine(repo, "data/listening/reach-a1-c2-catalog.json"));
ListeningCatalogImporter.Validate(manifest.Items);
var action = args[0];
if (action == "Import")
    throw new InvalidOperationException("This Listening batch is superseded by Speaking; authoring is disabled. Historical evidence is read-only.");
var pilot = args.Contains("--pilot", StringComparer.Ordinal);
var items = pilot ? manifest.Items.GroupBy(x => x.Level).Select(x => x.First()).ToList() : manifest.Items;
Directory.CreateDirectory(Path.Combine(state, "captions"));
Directory.CreateDirectory(Path.Combine(state, "receipts"));

if (action == "Stage")
{
    var captions = new YoutubeTranscriptService(new BlockAudio(), NullLogger<YoutubeTranscriptService>.Instance);
    foreach (var item in items)
    {
        var path = Path.Combine(state, "captions", item.YoutubeId + ".json");
        if (File.Exists(path)) continue;
        StagedListeningLesson staged;
        string? error = null;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            staged = await ListeningCatalogImporter.StageCaptionsAsync(item, captions, timeout.Token);
            if (staged.Captions.Count == 0) error = "No usable English captions retrieved by captions-only service; no ASR attempted.";
        }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidDataException)
        {
            error = ex is OperationCanceledException ? "Captions-only retrieval timed out after 20 seconds." : "Caption time/order validation failed.";
            staged = new(item, []);
        }
        await Write(path, new CaptionStage(staged.Captions, error, DateTime.UtcNow));
        Console.WriteLine($"{item.Level} {item.WatchUrl}: captions={staged.Captions.Count}; {error}");
    }
    return;
}

var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("Use scripts/import-listening-catalog.ps1; local connection is required.");
var target = new MySqlConnectionStringBuilder(connection);
if (target.Server != "127.0.0.1" || target.Port != 3307 || target.Database != "tctenglish_dev" || target.UserID != "tct_dev")
    throw new InvalidOperationException("Refusing non-local catalog target.");
await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
    .UseMySql(connection, new MariaDbServerVersion(new Version(10, 5, 23))).Options);
await db.Database.OpenConnectionAsync();
await using (var command = db.Database.GetDbConnection().CreateCommand())
{
    command.CommandText = "SELECT GET_LOCK('tct.reach-listening-catalog', 0)";
    if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1) throw new InvalidOperationException("Catalog writer already running.");
}
var importer = new ListeningCatalogImporter(db);
if (action == "Snapshot")
{
    var snapshot = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    var tables = new List<string>();
    await using (var cmd = db.Database.GetDbConnection().CreateCommand())
    {
        cmd.CommandText = "SHOW TABLES";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
    }
    foreach (var table in tables.Where(x => !x.StartsWith("SharedData", StringComparison.OrdinalIgnoreCase)))
    {
        if (table.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_'))) throw new InvalidDataException("Unexpected table identifier.");
        var hashes = new List<string>();
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $"SELECT * FROM `{table}`";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            hashes.Add(Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(values))));
        }
        snapshot[table] = hashes.Order(StringComparer.Ordinal).ToList();
    }
    var beforePath = Path.Combine(state, "preservation-before.json");
    if (!File.Exists(beforePath)) { await Write(beforePath, snapshot); Console.WriteLine($"Baseline: {snapshot.Count} tables, hashes only."); }
    else
    {
        var before = await Read<Dictionary<string, List<string>>>(beforePath);
        foreach (var table in before)
            if (!snapshot.TryGetValue(table.Key, out var after) || table.Value.GroupBy(x => x).Any(g => after.Count(x => x == g.Key) < g.Count()))
                throw new InvalidDataException($"Original rows changed or disappeared: {table.Key}");
        await Write(Path.Combine(state, "preservation-after.json"), snapshot);
        Console.WriteLine($"PRESERVED: all original rows across {before.Count} tables (including accounts/private content/progress).");
    }
    return;
}

var stagedItems = new List<StagedListeningLesson>();
foreach (var item in items)
{
    var stage = await Read<CaptionStage>(Path.Combine(state, "captions", item.YoutubeId + ".json"));
    var staged = new StagedListeningLesson(item, stage.Captions);
    ListeningCatalogImporter.ValidateCaptions(staged);
    stagedItems.Add(staged);
}
// Full preflight before any write. A conflict aborts this run, without changing curation.
var existing = new Dictionary<string, int?>(StringComparer.Ordinal);
foreach (var staged in stagedItems) existing[staged.Item.YoutubeId] = await importer.FindUnchangedAsync(staged);
foreach (var level in ListeningCatalogImporter.Levels)
{
    var group = stagedItems.Where(x => x.Item.Level == level).ToList();
    Console.WriteLine($"{level}: selected={group.Count}, create={group.Count(x => !existing[x.Item.YoutubeId].HasValue)}, unchanged={group.Count(x => existing[x.Item.YoutubeId].HasValue)}, videoOnly={group.Count(x => x.Captions.Count == 0)}, conflict=0, unavailable=0");
}
if (action == "DryRun") return;
if (action != "Import") throw new InvalidOperationException("Unknown action.");
foreach (var staged in stagedItems)
{
    var receipt = await importer.ImportOneAsync(staged);
    // Atomic file replacement; a commit/file interruption resumes by matching complete content.
    await Write(Path.Combine(state, "receipts", staged.Item.YoutubeId + ".json"), receipt);
    Console.WriteLine($"{staged.Item.Level} {staged.Item.YoutubeId}: id={receipt.LessonId}; created={receipt.Created}");
}

async Task<T> Read<T>(string path) => JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path), json)
    ?? throw new InvalidDataException($"Invalid JSON: {Path.GetFileName(path)}");
async Task Write<T>(string path, T value)
{
    var temp = path + ".tmp";
    await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(value, json) + "\n");
    File.Move(temp, path, overwrite: true);
}
record Manifest(List<ListeningCatalogItem> Items);
record CaptionStage(List<CatalogCaption> Captions, string? Error, DateTime ObservedAtUtc);
sealed class BlockAudio : IAudioTranscriptImporter
{
    public Task<AudioImportResult> TranscribeAsync(string youtubeId, CancellationToken ct)
        => throw new InvalidOperationException("Audio/ASR is forbidden in this captions-only batch.");
}
