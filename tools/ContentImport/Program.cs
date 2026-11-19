using System.Text.Json;
using ContentImport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using TCTEnglish.Services.Speaking;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

// Local authoring tool (never a startup seeder or endpoint). Use scripts/content-import.ps1.
// Usage: ContentImport <ValidateVocab|DryRunVocab|ImportVocab|...> <repoRoot> [--only A01,B02]
var action = args.Length > 0 ? args[0] : "";
var repo = Path.GetFullPath(args.Length > 1 ? args[1] : ".");
var only = args.SkipWhile(a => a != "--only").Skip(1).FirstOrDefault()?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

if (action == "ValidateVocab")
{
    // Offline: every manifest must deserialize and have unique codes and counts the contract expects.
    var listening = new ListeningImporter(null!, repo).Read();
    foreach (var lesson in listening.Lessons) ListeningImporter.Validate(lesson);
    Console.WriteLine($"{listening.Lessons.Count} listening lessons validated.");
    var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var total = 0;
    foreach (var file in VocabImporter.ManifestFiles(repo))
    {
        var m = VocabImporter.Read(file);
        if (!codes.Add(m.Set.Code)) throw new InvalidDataException($"Duplicate set code {m.Set.Code}.");
        var expected = m.Set.Code[0] == 'C' ? 20 : 25;
        if (m.Cards.Count != expected) throw new InvalidDataException($"{m.Set.Code}: {m.Cards.Count} cards, expected {expected}.");
        total += m.Cards.Count;
    }
    Console.WriteLine($"{codes.Count} manifests, {total} cards.");
    return;
}

if (action == "StageCaptions")
{
    // Network-only: the application's own captions-only service (no audio, no AI) for the given YouTube ids.
    var ids = args.SkipWhile(a => a != "--ids").Skip(1).FirstOrDefault()?
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    var outDir = Path.Combine(repo, ".appdata/local/content-build/listening/captions");
    Directory.CreateDirectory(outDir);
    var service = new YoutubeTranscriptService(new BlockAudio(), NullLogger<YoutubeTranscriptService>.Instance);
    foreach (var id in ids)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var result = await service.GetEnglishCaptionsOnlyAsync(id, timeout.Token);
            var lines = result.Sentences.Select(x => new { text = x.Text, start = x.StartTime, end = x.EndTime }).ToList();
            await File.WriteAllTextAsync(Path.Combine(outDir, id + ".json"),
                JsonSerializer.Serialize(new { id, usable = result.IsEnglishUsable, source = result.TranscriptSource, error = result.ErrorCode, lines }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"{id}: usable={result.IsEnglishUsable} lines={lines.Count} source={result.TranscriptSource}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{id}: failed {ex.GetType().Name}");
        }
    }
    return;
}

var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("Use scripts/content-import.ps1; the local connection is required.");
var target = new MySqlConnectionStringBuilder(connection);
if (target.Server != "127.0.0.1" || target.Port != 3307 || target.Database != "tctenglish_dev" || target.UserID != "tct_dev")
    throw new InvalidOperationException("Refusing a non-local content import target.");

await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
    .UseMySql(connection, new MariaDbServerVersion(new Version(10, 5, 23))).Options);
await db.Database.OpenConnectionAsync();
await using (var command = db.Database.GetDbConnection().CreateCommand())
{
    command.CommandText = "SELECT GET_LOCK('tct.content-import', 0)";
    if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1) throw new InvalidOperationException("Another content import is running.");
}

switch (action)
{
    case "DryRunVocab":
    case "ImportVocab":
    {
        var write = action == "ImportVocab";
        var importer = new VocabImporter(db, repo);
        int created = 0, updated = 0, unchanged = 0, images = 0;
        foreach (var file in VocabImporter.ManifestFiles(repo))
        {
            var manifest = VocabImporter.Read(file);
            if (only != null && !only.Contains(manifest.Set.Code)) continue;
            var r = await importer.ImportAsync(manifest, write);
            created += r.Created; updated += r.Updated; unchanged += r.Unchanged; images += r.ImagesCopied;
            Console.WriteLine($"{r.Code} {r.Set}: created={r.Created} updated={r.Updated} unchanged={r.Unchanged} images={r.ImagesCopied}"
                + (r.Extra.Count > 0 ? $" EXTRA(not touched)={string.Join(',', r.Extra)}" : ""));
            db.ChangeTracker.Clear();
        }
        Console.WriteLine($"{(write ? "Imported" : "Dry run")}: created={created} updated={updated} unchanged={unchanged} imagesCopied={images}");
        break;
    }
    case "DryRunListening":
    case "ImportListening":
    {
        var write = action == "ImportListening";
        var importer = new ListeningImporter(db, repo);
        var manifest = importer.Read();
        foreach (var lesson in manifest.Lessons)
        {
            var r = await importer.ImportOneAsync(lesson, write);
            Console.WriteLine($"{lesson.Level} {r.YoutubeId} {r.Action}{(r.LessonId.HasValue ? " id=" + r.LessonId : "")} - {r.Title}");
            db.ChangeTracker.Clear();
        }
        Console.WriteLine($"{(write ? "Imported" : "Dry run")}: {manifest.Lessons.Count} lessons in the manifest");
        break;
    }
    default:
        throw new InvalidOperationException("Unknown action. Use ValidateVocab, DryRunVocab, ImportVocab, DryRunListening or ImportListening.");
}

sealed class BlockAudio : IAudioTranscriptImporter
{
    public Task<AudioImportResult> TranscribeAsync(string youtubeId, CancellationToken ct)
        => throw new InvalidOperationException("Audio/ASR is forbidden: captions only.");
}
