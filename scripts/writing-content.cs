// .NET SDK 10 file-based utility. No application host or provider is started.
#:project ../TCTEnglish/TCTEnglish.csproj
#:package Microsoft.EntityFrameworkCore.Relational@9.0.4
#:property PublishAot=false
#:property PublishTrimmed=false

using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;
using TCTEnglish.Infrastructure.SharedData;
using TCTEnglish.Infrastructure.WritingContent;
using TCTVocabulary.Models;

if (args.Length != 3 || !new[] { "baseline", "validate", "dry-run", "pilot", "import", "export", "verify", "roundtrip" }.Contains(args[0]))
    throw new ArgumentException("Use <baseline|validate|dry-run|pilot|import|export|verify|roundtrip> <repo> <beginner|intermediate|advanced>.");
var action = args[0]; var repo = Path.GetFullPath(args[1]); var level = args[2];
if (!WritingContentBatch.Levels.Contains(level)) throw new ArgumentException("Unknown batch level.");
var json = WritingContentBatch.Json;
var contentRoot = Path.Combine(repo, "data/writing/2026-11-14");
var state = Path.Combine(repo, ".appdata/local/writing-content-20261114");
Directory.CreateDirectory(state);
using var fileLock = new FileStream(Path.Combine(state, "import.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
var baselinePath = Path.Combine(state, "baseline.json");
var batchBaselinePath = Path.Combine(state, level + "-before.json");
var receiptPath = Path.Combine(state, level + "-receipt.json");
var catalogRoot = Path.Combine(repo, "data/shared");
var baseline = File.Exists(baselinePath) ? await Read<Baseline>(baselinePath) : null;
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? throw new InvalidOperationException("Local child-process connection required.");
var parsed = new MySqlConnectionStringBuilder(connection);
if (parsed.Server is not ("127.0.0.1" or "localhost") || !(parsed.Port == 3307 && parsed.Database == "tctenglish_dev"
    || parsed.Port is >= 3308 and <= 3399 && parsed.Database.StartsWith("tct_acceptance_writing_", StringComparison.Ordinal)))
    throw new InvalidOperationException("Only the known local DB or isolated Writing acceptance DB is allowed.");
if ((action == "roundtrip") != parsed.Database.StartsWith("tct_acceptance_writing_", StringComparison.Ordinal))
    throw new InvalidOperationException("Roundtrip must target a separate disposable DB; all authoring operations target dev only.");
await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
    .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb")).Options);
await db.Database.OpenConnectionAsync();
await using (var version = db.Database.GetDbConnection().CreateCommand())
{
    version.CommandText = "SELECT SUBSTRING_INDEX(VERSION(),'-',1)";
    if ((string)(await version.ExecuteScalarAsync())! != "10.5.23") throw new InvalidOperationException("MariaDB 10.5.23 required.");
}
if (action != "roundtrip" && (await db.Database.GetPendingMigrationsAsync()).Any()) throw new InvalidOperationException("Pending migrations; apply reviewed local schema separately.");
if (action == "baseline")
{
    var target = baseline != null && level != "beginner" ? batchBaselinePath : baselinePath;
    if (File.Exists(target)) { Console.WriteLine("Baseline already exists; preserved without rewriting."); return; }
    await using var dbLock = await ContentLock.Acquire(db);
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
    var old = await Load();
    var cells = WritingContentBatch.Levels.SelectMany(l => WritingContentBatch.Formats.Select(f => new Cell(l, f,
        old.Count(x => x.UserId == null && x.IsPublished && x.Level == l && x.ContentType == f)))).ToList();
    baseline = new(DateTime.UtcNow, parsed.Database, cells, old.Select(x => x.Id).ToList(), await Snapshot(),
        (await SharedDataFormat.LoadAsync(catalogRoot)).Select(x => x.Release).ToList(),
        SharedDataFormat.Hash(await File.ReadAllBytesAsync(Path.Combine(catalogRoot, "production.json"))));
    // Content-only snapshot (including private text) is deliberately ignored. No user navigation is serialized.
    await Write(Path.Combine(state, target == baselinePath ? "old-writing-content.json" : level + "-before-writing-content.json"), old.Select(x => new { x.Id, x.UserId, x.Title, x.Level, x.ContentType,
        x.Topic, x.SourceType, x.PreviewText, x.IsPublished, x.CreatedAt,
        Sentences = x.WritingExerciseSentences.OrderBy(s => s.SortOrder).Select(s => new { s.Id, s.SortOrder, s.VietnameseText,
            s.EnglishMeaning, s.BreakAfter, Answers = s.WritingAcceptedAnswers.Select(a => new { a.Id, a.AnswerText, a.NormalizedHash, a.ApprovedByUserId, a.ApprovedAtUtc }) }) }));
    await Write(target, baseline); await tx.CommitAsync();
    Console.WriteLine(JsonSerializer.Serialize(cells, json)); return;
}
if (baseline == null) throw new InvalidOperationException("Read-only baseline is mandatory before any authoring import.");
var batchBaseline = File.Exists(batchBaselinePath) ? await Read<Baseline>(batchBaselinePath) : null;
if (level != "beginner" && action != "roundtrip" && batchBaseline == null)
    throw new InvalidOperationException("Capture this level's additive baseline first; preserve the original baseline.");
var lessons = await Read<List<WritingContentLesson>>(Path.Combine(contentRoot, level + ".json"));
WritingContentBatch.Validate(lessons, level);
using (var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(contentRoot, "manifest.json"))))
{
    var slots = manifest.RootElement.GetProperty("slots").EnumerateArray().ToList();
    if (slots.Count != 108 || slots.Select(x => x.GetProperty("key").GetString()).Distinct().Count() != 108
        || slots.Select(x => x.GetProperty("slot").GetString()).Distinct().Count() != 108) throw new InvalidDataException("Manifest must reserve 108 unique identities.");
    foreach (var l in WritingContentBatch.Levels) foreach (var f in WritingContentBatch.Formats)
        if (slots.Count(x => x.GetProperty("level").GetString() == l && x.GetProperty("contentType").GetString() == f) != 6)
            throw new InvalidDataException("Manifest cell quota mismatch.");
    foreach (var lesson in lessons)
    {
        var slot = slots.Single(x => x.GetProperty("key").GetString() == lesson.Key);
        if (slot.GetProperty("level").GetString() != level || slot.GetProperty("contentType").GetString() != lesson.ContentType
            || slot.GetProperty("title").GetString() != lesson.Title || slot.GetProperty("topic").GetString() != lesson.Topic)
            throw new InvalidDataException("Manifest metadata mismatch.");
        if (slot.TryGetProperty("contentSha256", out var expectedHash) && expectedHash.GetString() != SharedDataFormat.Digest(lesson))
            throw new InvalidDataException("Manifest content hash mismatch: " + lesson.Key);
    }
}
if (action == "roundtrip")
{
    // Endpoint/name guard above and empty-database guard below precede all DDL. No drops or resets.
    if ((await Tables()).Count != 0) throw new InvalidOperationException("Roundtrip requires a fresh, empty disposable database.");
    await db.Database.MigrateAsync();
    var before = await db.Users.CountAsync();
    var packages = (await SharedDataFormat.LoadAsync(catalogRoot)).Where(x => lessons.Any(l => l.Key == x.Package.RootKey)).ToList();
    if (packages.Count != 36 || packages.Any(x => x.Package.DependsOn.Count != 0)) throw new InvalidDataException("Expected 36 self-contained packages.");
    var engine = new SharedDataEngine(db, Path.Combine(state, "roundtrip-webroot"), new());
    await engine.ApplyAsync(packages); db.ChangeTracker.Clear();
    var imported = await Load();
    foreach (var lesson in lessons) WritingContentBatch.AssertMatches(lesson, imported.Single(x => x.Title == lesson.Title && x.SourceType == lesson.SourceType));
    var first = await Snapshot();
    await engine.ApplyAsync(packages); db.ChangeTracker.Clear();
    var second = await Snapshot();
    if (SharedDataFormat.Digest(first) != SharedDataFormat.Digest(second)) throw new InvalidDataException("Roundtrip repeat changed rows.");
    foreach (var row in await Load()) foreach (var s in row.WritingExerciseSentences)
    {
        if (!TCTEnglish.Services.Writing.WritingAssessmentPolicy.Assess(s.EnglishMeaning, s.EnglishMeaning).Passed) throw new InvalidDataException("Roundtrip reference rejected.");
        foreach (var a in s.WritingAcceptedAnswers)
            if (!TCTEnglish.Services.Writing.WritingAssessmentPolicy.Assess(s.EnglishMeaning, a.AnswerText, s.WritingAcceptedAnswers.Select(x => x.AnswerText)).Passed) throw new InvalidDataException("Roundtrip variant rejected.");
    }
    await Write(Path.Combine(state, level + "-roundtrip.json"), new { completedAtUtc = DateTime.UtcNow, provider = "MariaDB 10.5.23", lessons = imported.Count,
        sentences = imported.Sum(x => x.WritingExerciseSentences.Count), variants = imported.Sum(x => x.WritingExerciseSentences.Sum(s => s.WritingAcceptedAnswers.Count)), repeatChangedRows = 0,
        systemOwnersCreated = await db.Users.CountAsync() - before, packages = packages.Select(x => x.Release) });
    Console.WriteLine("Roundtrip: 36 lessons; persisted bilingual/paragraph/variant comparison and local assessment passed; repeat changed 0 rows."); return;
}
var existing = await Load();
double maxSimilarity = 0;
foreach (var lesson in lessons)
    foreach (var old in existing.Where(x => baseline.OldIds.Contains(x.Id) || x.Level != level))
    {
        var text = string.Join(' ', old.WritingExerciseSentences.OrderBy(s => s.SortOrder).Select(s => s.EnglishMeaning));
        var similarity = WritingContentBatch.Similarity(WritingContentBatch.Text(lesson), text);
        maxSimilarity = Math.Max(maxSimilarity, similarity);
        if (WritingContentBatch.Normalize(old.Title) == WritingContentBatch.Normalize(lesson.Title) || similarity >= .75)
            throw new InvalidDataException("Duplicate with existing/earlier-batch content: " + lesson.Key);
    }
Console.WriteLine($"Validated 36 lessons / {lessons.Sum(x => x.Sentences.Count)} pairs / {lessons.Sum(x => x.Sentences.Sum(s => s.ApprovedAlternatives.Count))} variants; existing/earlier-batch max 5-word similarity={maxSimilarity:F4}.");
if (action == "validate") return;
var receipt = File.Exists(receiptPath) ? await Read<Receipt>(receiptPath) : new Receipt(level, new());
if (receipt.Level != level) throw new InvalidDataException("Receipt level mismatch.");
var selected = action == "pilot" ? lessons.GroupBy(x => x.ContentType).Select(x => x.First()).ToList() : lessons;
if (action is "dry-run" or "pilot" or "import")
{
    await using var dbLock = await ContentLock.Acquire(db);
    await using var tx = await db.Database.BeginTransactionAsync();
    db.ChangeTracker.Clear(); existing = await Load();
    var approver = await db.Users.AsNoTracking().Where(x => x.Email == "system@tct.local" && x.Role == "System")
        .Select(x => (int?)x.UserId).SingleOrDefaultAsync();
    // Existing canonical system alias is preferred; otherwise use the already-present Admin, never create a real identity.
    approver ??= await db.Users.AsNoTracking().Where(x => x.Email == "system@tct.local").Select(x => (int?)x.UserId).SingleOrDefaultAsync();
    approver ??= await db.Users.AsNoTracking().Where(x => x.Role == "Admin").OrderBy(x => x.UserId).Select(x => (int?)x.UserId).FirstOrDefaultAsync();
    if (!approver.HasValue) throw new InvalidOperationException("An existing mapped canonical owner is required.");
    var created = new List<(WritingContentLesson Lesson, WritingExercise Row)>();
    foreach (var lesson in selected)
    {
        // Receipt IDs are checked against portable content, not trusted as identity by themselves.
        var saved = receipt.Items.SingleOrDefault(x => x.Key == lesson.Key);
        var found = saved != null ? existing.SingleOrDefault(x => x.Id == saved.Id)
            : existing.SingleOrDefault(x => x.SourceType == lesson.SourceType && x.Title == lesson.Title && x.Level == lesson.Level);
        if (saved != null && (saved.ContentSha256 != SharedDataFormat.Digest(lesson) || found == null)) throw new InvalidDataException("Receipt conflict: " + lesson.Key);
        if (found != null)
        {
            if (baseline.OldIds.Contains(found.Id)) throw new InvalidDataException("Cannot recycle a pre-existing identity.");
            WritingContentBatch.AssertMatches(lesson, found);
            if (saved == null) receipt.Items.Add(new(lesson.Key, found.Id, SharedDataFormat.Digest(lesson)));
            continue;
        }
        var row = WritingContentBatch.Create(lesson, approver.Value); db.WritingExercises.Add(row); created.Add((lesson, row));
    }
    if (action == "dry-run") { await tx.RollbackAsync(); Console.WriteLine($"Dry-run: would create {created.Count}; existing {selected.Count-created.Count}; no writes."); return; }
    await db.SaveChangesAsync();
    foreach (var item in created) receipt.Items.Add(new(item.Lesson.Key, item.Row.Id, SharedDataFormat.Digest(item.Lesson)));
    // Pending receipt is durable before commit; if interrupted, exact-content recovery handles both commit outcomes.
    await Write(receiptPath + ".pending", receipt);
    await tx.CommitAsync(); await Write(receiptPath, receipt);
    Console.WriteLine($"{action}: created {created.Count} lessons / {created.Sum(x => x.Lesson.Sentences.Count)} sentences / {created.Sum(x => x.Lesson.Sentences.Sum(s => s.ApprovedAlternatives.Count))} variants; no-op {selected.Count-created.Count}.");
}
else if (action == "export")
{
    if (receipt.Items.Count != 36) throw new InvalidDataException("Import the complete batch before exporting.");
    var packages = await SharedDataFormat.LoadAsync(catalogRoot);
    var owners = await db.Users.AsNoTracking().Where(u => u.Email == "system@tct.local" || u.Role == "Admin").OrderBy(u => u.UserId).ToListAsync();
    var mapping = new Dictionary<string, int>();
    var system = owners.SingleOrDefault(u => u.Email == "system@tct.local"); if (system != null) mapping["system"] = system.UserId;
    var admin = owners.FirstOrDefault(u => u.Role == "Admin"); if (admin != null) mapping["team-owner"] = admin.UserId;
    var inspection = await new SharedDataEngine(db, Path.Combine(repo, "TCTEnglish/wwwroot"), mapping).InspectAsync(packages);
    var exported = 0;
    foreach (var lesson in lessons)
    {
        var item = receipt.Items.Single(x => x.Key == lesson.Key);
        var row = existing.Single(x => x.Id == item.Id); WritingContentBatch.AssertMatches(lesson, row);
        var packageId = lesson.Key; var previous = packages.SingleOrDefault(x => x.Package.Id == packageId);
        if (previous != null)
        {
            var binding = await db.SharedDataBindings.AsNoTracking().SingleAsync(x => x.Key == lesson.Key);
            if (binding.EntityId != item.Id || inspection.Any(x => x.StartsWith("local-edit") && previous.Package.Records.Any(r => x.EndsWith(r.Key))))
                throw new InvalidDataException("Existing export conflict; preserve files and resolve.");
            continue;
        }
        await new SharedDataExporter(db, Path.Combine(repo, "TCTEnglish/wwwroot"), mapping)
            .ExportAsync(catalogRoot, "WritingExercise", item.Id, lesson.Key, packageId);
        db.ChangeTracker.Clear(); exported++;
        await Write(Path.Combine(state, level + "-export-checkpoint.json"), new { completedAtUtc = DateTime.UtcNow, lastKey = lesson.Key, exported });
    }
    Console.WriteLine($"Export: new immutable packages {exported}; existing {36-exported}.");
}
if (action is "verify" or "import" or "pilot" or "export")
{
    db.ChangeTracker.Clear(); var after = await Snapshot();
    foreach (var captured in batchBaseline == null ? new[] { baseline } : new[] { baseline, batchBaseline })
    {
        foreach (var table in captured.Rows)
        {
            var current = after[table.Key].ToList();
            foreach (var hash in table.Value) if (!current.Remove(hash)) throw new InvalidDataException("Preservation mismatch: " + table.Key);
            if (!table.Key.StartsWith("shareddata", StringComparison.OrdinalIgnoreCase)
                && !new[] { "writingexercises", "writingexercisesentences", "writingacceptedanswers" }.Contains(table.Key.ToLowerInvariant()) && current.Count != 0)
                throw new InvalidDataException("Unexpected added rows: " + table.Key);
        }
        foreach (var oldPackage in captured.Packages)
            if (SharedDataFormat.Hash(await File.ReadAllBytesAsync(Path.Combine(catalogRoot, oldPackage.Path))) != oldPackage.Sha256)
                throw new InvalidDataException("Pre-existing package changed.");
        if (SharedDataFormat.Hash(await File.ReadAllBytesAsync(Path.Combine(catalogRoot, "production.json"))) != captured.ProductionSha256)
            throw new InvalidDataException("Production approval manifest changed.");
    }
    var all = await Load();
    foreach (var item in receipt.Items) WritingContentBatch.AssertMatches(lessons.Single(l => l.Key == item.Key), all.Single(x => x.Id == item.Id));
    var cells = baseline.Cells.Select(c => new { c.Level, c.Format, before = c.Count,
        added = all.Count(x => !baseline.OldIds.Contains(x.Id) && x.UserId == null && x.IsPublished && x.Level == c.Level && x.ContentType == c.Format),
        after = all.Count(x => x.UserId == null && x.IsPublished && x.Level == c.Level && x.ContentType == c.Format) }).ToList();
    if (action == "verify")
    {
        if (receipt.Items.Count != 36) throw new InvalidDataException("Incomplete current batch receipt.");
        foreach (var cell in cells)
        {
            var otherPath = Path.Combine(state, cell.Level + "-receipt.json");
            var otherReceipt = File.Exists(otherPath) ? await Read<Receipt>(otherPath) : new Receipt(cell.Level, new());
            var otherContent = Path.Combine(contentRoot, cell.Level + ".json");
            var expected = File.Exists(otherContent) ? (await Read<List<WritingContentLesson>>(otherContent))
                .Count(x => x.ContentType == cell.Format && otherReceipt.Items.Any(i => i.Key == x.Key)) : 0;
            if (cell.added != expected) throw new InvalidDataException("Batch quota/preservation mismatch: " + cell.Level + "/" + cell.Format);
        }
    }
    await Write(Path.Combine(state, level + "-verification.json"), new { completedAtUtc = DateTime.UtcNow, receiptItems = receipt.Items.Count,
        preservedTables = baseline.Rows.Count, preservedRows = baseline.Rows.Sum(x => x.Value.Count),
        preBatch = batchBaseline == null ? null : new { preservedTables = batchBaseline.Rows.Count,
            preservedRows = batchBaseline.Rows.Sum(x => x.Value.Count), preservedPackageVersions = batchBaseline.Packages.Count }, cells });
    Console.WriteLine($"Preserved {baseline.Rows.Sum(x=>x.Value.Count)} original rows across {baseline.Rows.Count} tables; packages/production bytes unchanged.");
    if (batchBaseline != null) Console.WriteLine($"Also preserved {batchBaseline.Rows.Sum(x=>x.Value.Count)} pre-{level} rows and {batchBaseline.Packages.Count} existing package versions.");
}

async Task<T> Read<T>(string path) => JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path), json)!;
async Task Write<T>(string path, T value)
{
    var temp = path + ".tmp";
    await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(value, json) + "\n");
    File.Move(temp, path, true);
}
async Task<List<WritingExercise>> Load() => await db.WritingExercises.AsNoTracking()
    .Include(x => x.WritingExerciseSentences).ThenInclude(x => x.WritingAcceptedAnswers).ToListAsync();
async Task<List<string>> Tables()
{
    var names = new List<string>(); await using var command = db.Database.GetDbConnection().CreateCommand();
    command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
    command.CommandText = "SHOW TABLES"; await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync()) names.Add(reader.GetString(0)); return names;
}
async Task<Dictionary<string, List<string>>> Snapshot()
{
    var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    foreach (var table in (await Tables()).Order(StringComparer.Ordinal))
    {
        if (table.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_')) throw new InvalidDataException("Unexpected table identifier.");
        var hashes = new List<string>(); await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"SELECT * FROM `{table}`"; await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var values = new object[reader.FieldCount]; reader.GetValues(values);
            hashes.Add(SharedDataFormat.Hash(JsonSerializer.SerializeToUtf8Bytes(values, json)));
        }
        result[table] = hashes.Order(StringComparer.Ordinal).ToList();
    }
    return result;
}
record Cell(string Level, string Format, int Count);
record Baseline(DateTime CapturedAtUtc, string Database, List<Cell> Cells, List<int> OldIds,
    Dictionary<string, List<string>> Rows, List<SharedRelease> Packages, string ProductionSha256);
record ReceiptItem(string Key, int Id, string ContentSha256);
record Receipt(string Level, List<ReceiptItem> Items);

sealed class ContentLock(DbflashcardContext db, string name) : IAsyncDisposable
{
    public static async Task<ContentLock> Acquire(DbflashcardContext db)
    {
        var name = "tct.shared." + SharedDataFormat.Hash(System.Text.Encoding.UTF8.GetBytes(db.Database.GetDbConnection().Database))[..24];
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT GET_LOCK(@name,30)";
        var parameter = command.CreateParameter(); parameter.ParameterName = "@name"; parameter.Value = name; command.Parameters.Add(parameter);
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1) throw new InvalidOperationException("Another content operation is running.");
        return new(db, name);
    }
    public async ValueTask DisposeAsync()
    {
        await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = "SELECT RELEASE_LOCK(@name)";
        var parameter = command.CreateParameter(); parameter.ParameterName = "@name"; parameter.Value = name; command.Parameters.Add(parameter);
        await command.ExecuteScalarAsync();
    }
}
