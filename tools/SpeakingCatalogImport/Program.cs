using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using TCTEnglish.Infrastructure.SharedData;
using TCTEnglish.Infrastructure.SpeakingCatalog;
using TCTVocabulary.Models;

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, NewLine = "\n" };
var action = args[0]; var repo = Path.GetFullPath(args[1]);
var state = Path.Combine(repo, ".appdata/local/speaking-catalog-transfer");
Directory.CreateDirectory(state);
using var operationLock = new FileStream(Path.Combine(state, "transfer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
var manifest = await Read<Manifest>(Path.Combine(repo, "data/speaking/reach-a1-c2-catalog.json"));
var oldBytes = await File.ReadAllBytesAsync(Path.Combine(repo, manifest.Supersedes.Path));
if (SpeakingCatalogTransfer.Hash(oldBytes) != manifest.Supersedes.Sha256) throw new InvalidDataException("Original selection hash changed.");
var old = JsonSerializer.Deserialize<Manifest>(oldBytes, json)!;
SpeakingCatalogTransfer.ValidateSelection(manifest.Items);
if (!manifest.Items.Select(x => x.ReplacementOfYoutubeId ?? x.YoutubeId).Order(StringComparer.Ordinal).SequenceEqual(old.Items.Select(x => x.YoutubeId).Order(StringComparer.Ordinal)))
    throw new InvalidDataException("The Speaking selection must map one-to-one to the original 90 Listening sources.");
var originalSelection = old.Items.ToDictionary(x => x.YoutubeId, StringComparer.Ordinal);
var replacements = manifest.Replacements ?? [];
if (replacements.Count != manifest.Items.Count(x => x.ReplacementOfYoutubeId != null)) throw new InvalidDataException("Replacement decision count mismatch.");
foreach (var item in manifest.Items.Where(x => x.ReplacementOfYoutubeId != null))
{
    var decision = replacements.SingleOrDefault(x => x.OriginalYoutubeId == item.ReplacementOfYoutubeId && x.ReplacementYoutubeId == item.YoutubeId);
    if (decision == null || decision.OwnerAuthorization != "owner-chat-2026-11-13-replace-unavailable-with-same-level-transcript"
        || decision.Level != item.Level || originalSelection[item.ReplacementOfYoutubeId!].Level != item.Level
        || originalSelection.ContainsKey(item.YoutubeId) || decision.Reason != "audio-http-403" || decision.EvidenceUrl != item.WatchUrl)
        throw new InvalidDataException("Replacement lacks the current owner decision and same-level evidence.");
}
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? throw new InvalidOperationException("Local connection required.");
var target = new MySqlConnectionStringBuilder(connection);
if (target.Server != "127.0.0.1" || target.Port != 3307 || target.Database != "tctenglish_dev" || target.UserID != "tct_dev")
    throw new InvalidOperationException("Refusing non-local transfer target.");
await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
    .UseMySql(connection, new MariaDbServerVersion(new Version(10, 5, 23))).Options);
await db.Database.OpenConnectionAsync();
await using (var command = db.Database.GetDbConnection().CreateCommand())
{
    command.CommandText = "SELECT GET_LOCK('tct.speaking-catalog-transfer',0)";
    if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1) throw new InvalidOperationException("Transfer writer already running.");
}
var transfer = new SpeakingCatalogTransfer(db, originalSelection);
var catalogRoot = Path.Combine(repo, "data/shared");
var selected = new Dictionary<string, ListeningLesson>(StringComparer.Ordinal);
// Full identity preflight before baseline, any mutation or package write.
foreach (var item in manifest.Items) selected.Add(item.YoutubeId, await transfer.FindListeningSourceAsync(item));
var selectedIds = selected.Values.Select(x => x.Id).ToHashSet();
Directory.CreateDirectory(Path.Combine(state, "receipts"));
Directory.CreateDirectory(Path.Combine(state, "ready"));
if (File.Exists(Path.Combine(catalogRoot,".catalog-export.tmp")) && action == "ImportLocalTest")
    await RecoverExportIndex();

if (action == "Snapshot")
{
    var beforePath = Path.Combine(state, "preservation-before.json");
    var snapshot = await Snapshot();
    if (!File.Exists(beforePath))
    {
        await Write(beforePath, snapshot);
        var speaking = await db.SpeakingVideos.AsNoTracking().Select(x => new { x.Id, x.OwnerUserId, x.YoutubeId }).ToListAsync();
        var originalPublic = speaking.Where(x => x.OwnerUserId == null).ToList();
        if (speaking.Any(x => manifest.Items.Any(i => string.Equals(i.YoutubeId, x.YoutubeId, StringComparison.Ordinal))))
            throw new InvalidDataException("Unexpected Speaking overlap at initial baseline; inspect without overwrite.");
        await Write(Path.Combine(state, "inventory.json"), new
        {
            capturedAtUtc = DateTime.UtcNow, selected = selected.Select(x => new { youtubeId = x.Key, listeningId = x.Value.Id, speakingId = (int?)null }),
            publicSpeaking = originalPublic, speakingTotal = speaking.Count,
            speakingSentenceIds = await db.SpeakingSentences.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(),
            selectedListeningProgress = await db.UserListeningProgresses.CountAsync(x => selectedIds.Contains(x.LessonId)),
            listeningPrivateIds = await db.ListeningLessons.Where(x => x.OwnerUserId != null).Select(x => x.Id).ToListAsync(),
            originalPublicListeningIds = await db.ListeningLessons.Where(x => x.OwnerUserId == null && x.IsPublished && !selectedIds.Contains(x.Id)).Select(x => x.Id).ToListAsync()
        });
        await Write(Path.Combine(state, "catalog-before.json"), await Read<SharedCatalog>(Path.Combine(catalogRoot, "catalog.json")));
        Console.WriteLine($"Baseline captured: {snapshot.Count} tables, {originalPublic.Count} existing public Speaking, {selected.Count} exact Listening sources.");
    }
    else
    {
        var before = await Read<Dictionary<string, List<PreservedRow>>>(beforePath);
        foreach (var table in before)
        {
            if (!snapshot.TryGetValue(table.Key, out var after)) throw new InvalidDataException("Original table disappeared: " + table.Key);
            var available = after.Select(x => x.Hash).ToList();
            foreach (var row in table.Value)
            {
                var i = available.FindIndex(x => x == row.Hash || x == row.AuthorizedRetiredHash);
                if (i < 0) throw new InvalidDataException("Original row changed/disappeared outside the 90 authorized flags: " + table.Key);
                available.RemoveAt(i);
            }
        }
        await Write(Path.Combine(state, "preservation-after.json"), snapshot);
        Console.WriteLine($"PRESERVED: all original rows in {before.Count} tables, allowing only exact selected Listening IsPublished=false changes.");
    }
    return;
}
if (!File.Exists(Path.Combine(state, "inventory.json")) || !File.Exists(Path.Combine(state, "before-transfer.sql")))
    throw new InvalidOperationException("Require the verified initial inventory and private backup before transfer commands.");

if (action == "Status")
{
    var publicSpeaking = await db.SpeakingVideos.AsNoTracking().Where(x => x.OwnerUserId == null).Select(x => new { x.Id, x.YoutubeId }).ToListAsync();
    foreach (var level in SpeakingCatalogTransfer.Levels)
    {
        var group = manifest.Items.Where(x => x.Level == level).ToList();
        Console.WriteLine($"{level}: selected={group.Count}; listeningRetired={group.Count(x => !selected[x.YoutubeId].IsPublished)}; speakingPublished={group.Count(x => publicSpeaking.Any(v => string.Equals(v.YoutubeId,x.YoutubeId,StringComparison.Ordinal)))}; qualityFiles={group.Count(x => File.Exists(Path.Combine(state,"ready",x.YoutubeId+".json")))}");
    }
    return;
}
if (action == "Stage")
{
    var summary = new List<object>();
    foreach (var item in manifest.Items)
    {
        var sourcePath = Path.Combine(state, "captions", item.YoutubeId + ".json");
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);
        var media = await Read<MeasuredMedia>(Path.Combine(state, "media", item.YoutubeId, "media.json"));
        var mediaHash = SpeakingCatalogTransfer.Hash(await File.ReadAllBytesAsync(Path.Combine(state, "media", item.YoutubeId, item.YoutubeId + ".m4a")));
        if (media.YoutubeId != item.YoutubeId || mediaHash != media.SourceSha256) throw new InvalidDataException("Media staging hash conflict: " + item.WatchUrl);
        var candidatePaths = new[] { "-g10c2", "-g10c1", "-g10", "" }
            .SelectMany(s => new[] { Path.Combine(state, "alignment", item.YoutubeId + s, "ctc-candidate.json"), Path.Combine(state, "alignment", item.YoutubeId + s, "candidate.json") }).Where(File.Exists).ToList();
        string? candidatePath = null;
        var sourceTranscript = await Read<CandidateTranscript>(sourcePath);
        if (sourceTranscript.YoutubeId != item.YoutubeId) throw new InvalidDataException("Caption cache identity conflict: " + item.WatchUrl);
        var sourceCues = sourceTranscript.Cues;
        var englishSourceCues = sourceCues.Where(x => !System.Text.RegularExpressions.Regex.IsMatch(x.Text, @"^\s*\[(music|applause|laughter)\]\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)).ToList();
        foreach (var path in candidatePaths.OrderBy(p => p.EndsWith("ctc-candidate.json", StringComparison.Ordinal) ? 0 : 1))
        {
            var candidate = await Read<CandidateTranscript>(path);
            if (candidate.Cues.Select(x => x.Text).SequenceEqual(sourceCues.Select(x => x.Text))
                || candidate.Cues.Select(x => x.Text).SequenceEqual(englishSourceCues.Select(x => x.Text))) { candidatePath = path; break; }
        }
        var raw = await Read<CandidateTranscript>(candidatePath ?? sourcePath);
        if (candidatePath != null && (raw.YoutubeId != item.YoutubeId || raw.SourceSha256 != SpeakingCatalogTransfer.Hash(sourceBytes)
            || raw.MediaSha256 != mediaHash || raw.MeasuredDurationSeconds != media.MeasuredDurationSeconds))
            throw new InvalidDataException("Alignment candidate/source/media fingerprint mismatch: " + item.WatchUrl);
        var flags = (raw.UnresolvedFlags ?? ["uncorrected_source_caption_timing"]).Concat(new[] { "whole_speech_interval_review_pending", "manual_token_accuracy_pending", "beginning_middle_late_boundary_playback_pending" }).Distinct(StringComparer.Ordinal).ToList();
        for (var i = 0; i < raw.Cues.Count; i++)
        {
            var cue = raw.Cues[i];
            if (!double.IsFinite(cue.StartTime) || !double.IsFinite(cue.EndTime) || cue.StartTime < 0 || cue.EndTime <= cue.StartTime
                || cue.EndTime > media.MeasuredDurationSeconds || cue.EndTime - cue.StartTime > 20
                || string.IsNullOrWhiteSpace(cue.Text) || cue.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length > 60)
                flags.Add("invalid_or_unsuitable_cue_" + i);
            if (i > 0 && cue.StartTime < raw.Cues[i - 1].EndTime - .001) flags.Add("overlapping_cue_" + i);
        }
        var proof = new CatalogQualityProof(SpeakingCatalogTransfer.Hash(sourceBytes), mediaHash, SpeakingCatalogTransfer.CueHash(raw.Cues),
            media.MeasuredDurationSeconds, "youtube-captions", false, [], [], flags);
        var draft = new ReadySpeakingCatalogItem(item, raw.Cues, proof);
        var fingerprint = SpeakingCatalogTransfer.Hash(JsonSerializer.SerializeToUtf8Bytes(draft, json));
        var folder = Path.Combine(state, "staged", item.YoutubeId); Directory.CreateDirectory(folder);
        var output = Path.Combine(folder, fingerprint + ".json");
        if (!File.Exists(output)) await Write(output, draft); // immutable drafts; never overwrite a review
        summary.Add(new { item.YoutubeId, listeningYoutubeId = item.ReplacementOfYoutubeId ?? item.YoutubeId, stageFile = Path.GetRelativePath(state, output),
            cueCount = raw.Cues.Count, measuredDurationSeconds = media.MeasuredDurationSeconds, lastCueEnd = raw.Cues.LastOrDefault()?.EndTime, unresolvedFlags = flags.Count, qualityPassed = false });
        Console.WriteLine($"STAGED {item.Level} {item.YoutubeId}; cues={raw.Cues.Count}; quality=false");
    }
    await Write(Path.Combine(state, "staged-summary.json"), summary);
    return;
}
if (action is not ("Retire" or "Publish" or "ImportLocalTest")) throw new InvalidOperationException("Unknown action.");
var items = args.Contains("--pilot") ? SpeakingCatalogTransfer.Levels.Select(l => manifest.Items.First(x => x.Level == l)).ToList() : manifest.Items;
var ready = new Dictionary<string, ReadySpeakingCatalogItem>(StringComparer.Ordinal);
if (action == "ImportLocalTest")
{
    // Owner explicitly requested local UAT before staging/release QA. This CLI
    // already rejects every target except this checkout's loopback development DB.
    var staged = await Read<List<StagedReference>>(Path.Combine(state, "staged-summary.json"));
    Directory.CreateDirectory(Path.Combine(state, "local-test"));
    foreach (var item in items)
    {
        var entry = staged.Single(x => x.YoutubeId == item.YoutubeId);
        var draftPath = Path.GetFullPath(Path.Combine(state, entry.StageFile));
        if (!draftPath.StartsWith(Path.GetFullPath(Path.Combine(state, "staged")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Staging path escaped task state.");
        var draft = await Read<ReadySpeakingCatalogItem>(draftPath);
        if (draft.Item != item) throw new InvalidDataException("Staged source identity mismatch: " + item.WatchUrl);
        var captionsFile = Path.Combine(state, "captions", item.YoutubeId + ".json");
        var mediaFile = Path.Combine(state, "media", item.YoutubeId, item.YoutubeId + ".m4a");
        if (draft.Proof.SourceSha256 != SpeakingCatalogTransfer.Hash(await File.ReadAllBytesAsync(captionsFile))
            || draft.Proof.MediaSha256 != SpeakingCatalogTransfer.Hash(await File.ReadAllBytesAsync(mediaFile)))
            throw new InvalidDataException("Local UAT source/media hash conflict: " + item.WatchUrl);
        var original = await Read<CandidateTranscript>(captionsFile);
        if (original.YoutubeId != item.YoutubeId) throw new InvalidDataException("Caption identity conflict.");
        var speechOnly = original.Cues.Where(x => !System.Text.RegularExpressions.Regex.IsMatch(x.Text, @"^\s*\[(music|applause|laughter)\]\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)).Select(x => x.Text);
        if (!draft.Cues.Select(x => x.Text).SequenceEqual(original.Cues.Select(x => x.Text))
            && !draft.Cues.Select(x => x.Text).SequenceEqual(speechOnly))
            throw new InvalidDataException("Local UAT transcript text differs from acquired full caption sequence.");
        var flags = draft.Proof.UnresolvedFlags.Append("owner-local-test-before-staging-qa").ToList();
        var localSourceCues = draft.Cues;
        if (draft.Cues.Zip(draft.Cues.Skip(1)).Any(pair => pair.Second.StartTime < pair.First.StartTime))
        {
            localSourceCues = original.Cues;
            flags.Add("alignment_reordered_adjacent_utterances_using_original_caption_sequence_for_local_uat");
        }
        var cues = localSourceCues.Select((cue,index) =>
        {
            if (cue.EndTime > draft.Proof.MeasuredDurationSeconds)
            {
                flags.Add($"source_caption_end_bounded_to_measured_media_at_{index}: {cue.EndTime:R} -> {draft.Proof.MeasuredDurationSeconds:R}");
                return cue with { EndTime = draft.Proof.MeasuredDurationSeconds };
            }
            return cue;
        }).ToList();
        // Only ends beyond the actual file are bounded; no guessed onset,
        // proportional timing, deleted speech or fabricated quality evidence.
        var local = draft with { Cues = cues, Proof = draft.Proof with { CueSha256 = SpeakingCatalogTransfer.CueHash(cues), UnresolvedFlags = flags, FullSpeechReviewed = false } };
        SpeakingCatalogTransfer.ValidateForLocalTest(local);
        ready.Add(item.YoutubeId, local);
        await Write(Path.Combine(state,"local-test",item.YoutubeId+".json"), local);
    }
}
if (action == "Publish")
    foreach (var item in items)
    {
        var approvedPath = Path.Combine(state, "ready", item.YoutubeId + ".json");
        if (!File.Exists(approvedPath)) throw new InvalidDataException("Quality-approved transcript is missing: " + item.WatchUrl);
        var proof = await Read<ReadySpeakingCatalogItem>(approvedPath);
        if (proof.Item != item) throw new InvalidDataException("Ready file/source metadata mismatch: " + item.WatchUrl);
        var sourceFile = proof.Proof.TranscriptSource switch
        {
            "youtube-captions" => Path.Combine(state, "captions", item.YoutubeId + ".json"),
            "creator-script-aligned" => Path.Combine(state, "creator-text", item.YoutubeId + ".txt"),
            _ => throw new InvalidDataException("Paid ASR publication requires the approved acquisition/cost receipt adapter.")
        };
        var mediaFile = Path.Combine(state, "media", item.YoutubeId, item.YoutubeId + ".m4a");
        if (proof.Proof.TranscriptSource == "youtube-captions" && (await Read<CandidateTranscript>(sourceFile)).YoutubeId != item.YoutubeId)
            throw new InvalidDataException("Caption cache identity mismatch: " + item.WatchUrl);
        if (proof.Proof.SourceSha256 != SpeakingCatalogTransfer.Hash(await File.ReadAllBytesAsync(sourceFile))
            || proof.Proof.MediaSha256 != SpeakingCatalogTransfer.Hash(await File.ReadAllBytesAsync(mediaFile)))
            throw new InvalidDataException("Quality evidence does not match acquired source/media: " + item.WatchUrl);
        SpeakingCatalogTransfer.ValidateReady(proof); ready.Add(item.YoutubeId, proof);
    }
foreach (var item in items)
{
    var receipt = action == "Retire" ? await transfer.RetireListeningAsync(item)
        : action == "ImportLocalTest" ? await transfer.ImportForLocalTestAsync(ready[item.YoutubeId]) : await transfer.PublishAsync(ready[item.YoutubeId]);
    var receiptPath = Path.Combine(state, "receipts", item.YoutubeId + ".json");
    if (action == "Retire" && File.Exists(receiptPath))
    {
        var prior = await Read<SpeakingTransferReceipt>(receiptPath);
        if (prior.YoutubeId != receipt.YoutubeId || prior.ListeningId != receipt.ListeningId
            || prior.ListeningKey != receipt.ListeningKey || prior.SpeakingKey != receipt.SpeakingKey)
            throw new InvalidDataException("Prior transfer identity conflicts: " + item.WatchUrl);
        if (prior.SpeakingId is int targetId)
        {
            var targetRow = await db.SpeakingVideos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetId && x.OwnerUserId == null);
            if (targetRow?.YoutubeId != item.YoutubeId) throw new InvalidDataException("Prior Speaking mapping changed; preserve and inspect.");
            receipt = receipt with { SpeakingId = prior.SpeakingId };
        }
    }
    // Canonical exporter owns version/digest/bindings. Export withdrawals before Sync.
    await ExportIfChanged("ListeningLesson", receipt.ListeningId, receipt.ListeningKey, "listening-youtube-" + SpeakingCatalogTransfer.HashId(receipt.ListeningYoutubeId ?? item.YoutubeId), false);
    if (receipt.SpeakingId.HasValue)
        await ExportIfChanged("SpeakingVideo", receipt.SpeakingId.Value, receipt.SpeakingKey, "speaking-youtube-" + SpeakingCatalogTransfer.HashId(item.YoutubeId), true);
    await Write(receiptPath, receipt);
    Console.WriteLine($"{item.Level} {item.YoutubeId}: retired={receipt.ListeningRetired}; speakingId={receipt.SpeakingId?.ToString() ?? "pending-transcript"}");
    db.ChangeTracker.Clear();
}

async Task ExportIfChanged(string kind, int id, string key, string package, bool speaking)
{
    var catalog = await Read<SharedCatalog>(Path.Combine(catalogRoot, "catalog.json"));
    var last = catalog.Releases.Where(x => x.Id == package).MaxBy(x => x.Version);
    if (last != null)
    {
        var prior = await Read<SharedPackage>(Path.Combine(catalogRoot, last.Path));
        var root = prior.Records.Single(x => x.Key == key);
        var binding = await db.SharedDataBindings.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key);
        if (binding?.Kind != kind || binding.EntityId != id) throw new InvalidDataException("Binding identity conflict: " + key);
        if (!speaking && root.Values.TryGetValue("IsPublished", out var published) && !published.GetBoolean()) return;
        if (speaking)
        {
            if (SpeakingCatalogTransfer.Hash(await File.ReadAllBytesAsync(Path.Combine(catalogRoot,last.Path))) != last.Sha256)
                throw new InvalidDataException("Immutable Speaking package hash changed.");
            var video = await db.SpeakingVideos.AsNoTracking().Include(x=>x.SpeakingSentences).SingleAsync(x=>x.Id==id && x.OwnerUserId==null);
            var packageBindings = await db.SharedDataBindings.AsNoTracking().Where(x=>x.PackageId==package).ToDictionaryAsync(x=>x.Key,StringComparer.Ordinal);
            if(prior.Records.Count!=video.SpeakingSentences.Count+1)throw new InvalidDataException("Speaking child count changed since export.");
            foreach(var record in prior.Records)
            {
                if(!packageBindings.TryGetValue(record.Key,out var mapped)||mapped.Kind!=record.Kind||mapped.Digest!=SharedDataFormat.Digest(record))
                    throw new InvalidDataException("Speaking package binding changed; preserve and inspect.");
                object row = record.Kind=="SpeakingVideo" && mapped.EntityId==video.Id ? video
                    : record.Kind=="SpeakingSentence" ? video.SpeakingSentences.Single(x=>x.Id==mapped.EntityId)
                    : throw new InvalidDataException("Unexpected Speaking package record.");
                foreach(var value in record.Values)
                {
                    var property=row.GetType().GetProperty(value.Key)??throw new InvalidDataException("Unexpected canonical content field.");
                    var actual=JsonSerializer.SerializeToElement(property.GetValue(row),property.PropertyType,SharedDataFormat.Json);
                    if(actual.GetRawText()!=value.Value.GetRawText())throw new InvalidDataException("Tracked Speaking content has local edits: "+record.Key);
                }
            }
            return;
        }
    }
    SharedRelease release;
    try
    {
        release = await new SharedDataExporter(db, Path.Combine(repo, "TCTEnglish/wwwroot"), new()).ExportAsync(catalogRoot, kind, id, key, package);
    }
    catch (Exception error) when (error is UnauthorizedAccessException or IOException && File.Exists(Path.Combine(catalogRoot,".catalog-export.tmp")))
    {
        await RecoverExportIndex();
        release = (await Read<SharedCatalog>(Path.Combine(catalogRoot,"catalog.json"))).Releases.Where(x=>x.Id==package).MaxBy(x=>x.Version)
            ?? throw new InvalidDataException("Recovered index does not contain the expected canonical export.");
    }
    Console.WriteLine($"EXPORTED {release.Id}/{release.Version}");
}
async Task RecoverExportIndex()
{
    // The canonical exporter commits bindings before the final atomic index
    // rename. A transient Windows reader can interrupt only that final step.
    // Resume the exact generated index after validating every package and binding.
    await using var exportLock = new FileStream(Path.Combine(catalogRoot,".export.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    var current = await Read<SharedCatalog>(Path.Combine(catalogRoot,"catalog.json"));
    var pending = await Read<SharedCatalog>(Path.Combine(catalogRoot,".catalog-export.tmp"));
    if (pending.Releases.Count != current.Releases.Count+1 || !pending.Releases.Take(current.Releases.Count).SequenceEqual(current.Releases))
        throw new InvalidDataException("Interrupted export index is not the exact one-release extension; preserve it for review.");
    var last = pending.Releases[^1];
    if (!last.Id.StartsWith("speaking-youtube-",StringComparison.Ordinal) || !manifest.Items.Any(x=>last.Id=="speaking-youtube-"+SpeakingCatalogTransfer.HashId(x.YoutubeId)))
        throw new InvalidDataException("Interrupted export does not belong to this selected batch.");
    var loaded = await SharedDataFormat.LoadAsync(catalogRoot,".catalog-export.tmp");
    var added = loaded.Single(x=>x.Release==last);
    var bindings = await db.SharedDataBindings.AsNoTracking().ToListAsync();
    foreach (var record in added.Package.Records)
        if (!bindings.Any(x=>x.Key==record.Key && x.Kind==record.Kind && x.PackageId==last.Id && x.Digest==SharedDataFormat.Digest(record)))
            throw new InvalidDataException("Interrupted export did not commit exact canonical bindings.");
    var edits = await new SharedDataEngine(db,Path.Combine(repo,"TCTEnglish/wwwroot"),new()).InspectAsync(loaded);
    if(edits.Any(x=>x.StartsWith("local-edit ",StringComparison.Ordinal)))throw new InvalidDataException("Local content changed during interrupted export; preserve and inspect.");
    db.ChangeTracker.Clear();
    for(var attempt=0;;attempt++)
    {
        try { File.Move(Path.Combine(catalogRoot,".catalog-export.tmp"),Path.Combine(catalogRoot,"catalog.json"),true); break; }
        catch(Exception error) when(error is UnauthorizedAccessException or IOException && attempt<5) { await Task.Delay(200*(attempt+1)); }
    }
    Console.WriteLine($"RESUMED exact canonical export index {last.Id}/{last.Version}; existing package bytes preserved.");
}
async Task<Dictionary<string,List<PreservedRow>>> Snapshot()
{
    var tables = new List<string>();
    await using (var cmd = db.Database.GetDbConnection().CreateCommand())
    {
        cmd.CommandText = "SHOW TABLES"; await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) tables.Add(r.GetString(0));
    }
    var result = new Dictionary<string,List<PreservedRow>>(StringComparer.Ordinal);
    foreach (var table in tables.Where(x => !x.StartsWith("shareddata", StringComparison.OrdinalIgnoreCase)))
    {
        if (table.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_'))) throw new InvalidDataException("Unexpected table identifier.");
        var rows = new List<PreservedRow>();
        await using var cmd = db.Database.GetDbConnection().CreateCommand(); cmd.CommandText = $"SELECT * FROM `{table}`";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var values = new object[r.FieldCount]; r.GetValues(values);
            var hash = SpeakingCatalogTransfer.Hash(JsonSerializer.SerializeToUtf8Bytes(values)); string? retired = null;
            if (table.Equals("listeninglessons", StringComparison.OrdinalIgnoreCase) && selectedIds.Contains(Convert.ToInt32(values[r.GetOrdinal("Id")])))
            {
                var flag = r.GetOrdinal("IsPublished"); values[flag] = Convert.ChangeType(0, values[flag].GetType());
                retired = SpeakingCatalogTransfer.Hash(JsonSerializer.SerializeToUtf8Bytes(values));
            }
            rows.Add(new(hash, retired));
        }
        result[table] = rows;
    }
    return result;
}
async Task<T> Read<T>(string path) => JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path),json) ?? throw new InvalidDataException("Invalid data: " + Path.GetFileName(path));
async Task Write<T>(string path,T value){await File.WriteAllTextAsync(path+".tmp",JsonSerializer.Serialize(value,json)+"\n");File.Move(path+".tmp",path,true);}
record SupersededSource(string Path,string Sha256);
record Manifest(List<SpeakingCatalogEntry> Items,SupersededSource Supersedes,List<ReplacementDecision>? Replacements = null);
record ReplacementDecision(string OriginalYoutubeId,string ReplacementYoutubeId,string Level,string Reason,string OwnerAuthorization,string EvidenceUrl);
record CandidateTranscript(List<SpeakingCatalogCue> Cues,List<string>? UnresolvedFlags,string? YoutubeId,string? SourceSha256,string? MediaSha256,double? MeasuredDurationSeconds);
record MeasuredMedia(string YoutubeId,string SourceSha256,double MeasuredDurationSeconds);
record PreservedRow(string Hash,string? AuthorizedRetiredHash);
record StagedReference(string YoutubeId,string StageFile);
