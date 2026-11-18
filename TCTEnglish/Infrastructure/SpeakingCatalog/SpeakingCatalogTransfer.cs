using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Infrastructure.SpeakingCatalog;

public sealed record SpeakingCatalogEntry(string YoutubeId, string WatchUrl, string Level, string DisplayTitle,
    string Topic, int DurationSeconds, string Duration, string ThumbnailUrl, string? ReplacementOfYoutubeId = null, string? CatalogTopic = null);
public sealed record SpeakingCatalogCue(double StartTime, double EndTime, string Text);
public sealed record CatalogCoverage(double StartTime, double EndTime, string Kind, string Evidence);
public sealed record CatalogPlaybackReview(double PositionSeconds, string Region, double TokenAccuracy,
    double MaxTimingErrorSeconds, bool Clipped, string Evidence);
public sealed record CatalogQualityProof(string SourceSha256, string MediaSha256, string CueSha256,
    double MeasuredDurationSeconds, string TranscriptSource, bool FullSpeechReviewed,
    List<CatalogCoverage> Coverage, List<CatalogPlaybackReview> Reviews, List<string> UnresolvedFlags);
public sealed record ReadySpeakingCatalogItem(SpeakingCatalogEntry Item, List<SpeakingCatalogCue> Cues, CatalogQualityProof Proof);
public sealed record SpeakingTransferReceipt(string YoutubeId, int ListeningId, int? SpeakingId,
    string ListeningKey, string SpeakingKey, bool ListeningRetired, bool SpeakingCreated, string? ListeningYoutubeId = null);

/// <summary>Explicit local transfer writer; no startup/API/AI/audio dependency.</summary>
public sealed class SpeakingCatalogTransfer(DbflashcardContext db, IReadOnlyDictionary<string, SpeakingCatalogEntry>? originalSelection = null)
{
    public static readonly string[] Levels = ["A1", "A2", "B1", "B2", "C1", "C2"];
    public static string HashId(string id) => Hash(Encoding.UTF8.GetBytes(id));
    public static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static string CueHash(IReadOnlyList<SpeakingCatalogCue> cues) => Hash(JsonSerializer.SerializeToUtf8Bytes(cues));

    public static void ValidateSelection(IReadOnlyList<SpeakingCatalogEntry> items, bool fullBatch = true)
    {
        if (items.Count == 0 || items.Select(x => x.YoutubeId).Distinct(StringComparer.Ordinal).Count() != items.Count
            || fullBatch && (items.Count != 90 || Levels.Any(l => items.Count(x => x.Level == l) != 15)))
            throw new InvalidDataException("Require the exact distinct selection: 15 videos per A1–C2.");
        foreach (var item in items)
            if (YoutubeUrlHelper.NormalizeYoutubeId(item.YoutubeId) != item.YoutubeId
                || item.WatchUrl != $"https://www.youtube.com/watch?v={item.YoutubeId}"
                || !Levels.Contains(item.Level, StringComparer.Ordinal) || item.DurationSeconds is < 60 or > 7200
                || string.IsNullOrWhiteSpace(item.DisplayTitle) || item.DisplayTitle.Length > 255
                || string.IsNullOrWhiteSpace(item.Topic) || item.Topic.Length > 100
                || item.ThumbnailUrl != YoutubeUrlHelper.BuildDefaultThumbnailUrl(item.YoutubeId)
                || item.CatalogTopic is not null && (string.IsNullOrWhiteSpace(item.CatalogTopic) || item.CatalogTopic.Length > 100)
                || item.ReplacementOfYoutubeId is not null && (YoutubeUrlHelper.NormalizeYoutubeId(item.ReplacementOfYoutubeId) != item.ReplacementOfYoutubeId || item.ReplacementOfYoutubeId == item.YoutubeId))
                throw new InvalidDataException($"Invalid selected source: {item.WatchUrl}");
    }

    public static void ValidateReady(ReadySpeakingCatalogItem ready)
    {
        ValidateSelection([ready.Item], false);
        var p = ready.Proof;
        bool Digest(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if (!Digest(p.SourceSha256) || !Digest(p.MediaSha256) || p.CueSha256 != CueHash(ready.Cues)
            || !double.IsFinite(p.MeasuredDurationSeconds) || Math.Abs(p.MeasuredDurationSeconds - ready.Item.DurationSeconds) > 2
            || p.TranscriptSource is not ("youtube-captions" or "creator-script-aligned" or "gpt-transcribe-aligned")
            || !p.FullSpeechReviewed || p.UnresolvedFlags.Count != 0 || ready.Cues.Count < 3)
            throw new InvalidDataException($"Transcript quality/acquisition incomplete: {ready.Item.WatchUrl}");
        double end = -1;
        foreach (var cue in ready.Cues)
        {
            if (!double.IsFinite(cue.StartTime) || !double.IsFinite(cue.EndTime) || cue.StartTime < 0
                || cue.StartTime < end - .001 || cue.EndTime <= cue.StartTime || cue.EndTime > p.MeasuredDurationSeconds
                || cue.EndTime - cue.StartTime > 20 || string.IsNullOrWhiteSpace(cue.Text)
                || cue.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length > 60)
                throw new InvalidDataException($"Invalid or unsuitable Speaking cue: {ready.Item.WatchUrl}");
            end = cue.EndTime;
        }
        // Whole-media interval accounting is separate from lastEnd/duration.
        double accountedEnd = 0;
        foreach (var range in p.Coverage)
        {
            if (!double.IsFinite(range.StartTime) || !double.IsFinite(range.EndTime)
                || Math.Abs(range.StartTime - accountedEnd) > .01 || range.EndTime <= range.StartTime
                || range.EndTime > p.MeasuredDurationSeconds || string.IsNullOrWhiteSpace(range.Evidence)
                || range.Kind is not ("english-speech" or "silence" or "music" or "non-english-instruction"))
                throw new InvalidDataException("Coverage has a gap, overlap or unreviewed interval.");
            if (range.Kind == "english-speech")
            {
                var covered = range.StartTime;
                foreach (var cue in ready.Cues.Where(c => c.EndTime > range.StartTime && c.StartTime < range.EndTime))
                {
                    if (cue.StartTime > covered + .01) throw new InvalidDataException("Omitted English speech interval.");
                    covered = Math.Max(covered, cue.EndTime);
                }
                if (covered < range.EndTime - .01) throw new InvalidDataException("Omitted English speech tail.");
            }
            accountedEnd = range.EndTime;
        }
        if (Math.Abs(accountedEnd - p.MeasuredDurationSeconds) > .01)
            throw new InvalidDataException("Coverage does not account for the full source duration.");
        bool ValidReview(CatalogPlaybackReview r) => double.IsFinite(r.TokenAccuracy) && r.TokenAccuracy is >= .98 and <= 1
                && r.MaxTimingErrorSeconds is >= 0 and <= .75
                && !r.Clipped && r.PositionSeconds >= 0 && r.PositionSeconds <= p.MeasuredDurationSeconds
                && !string.IsNullOrWhiteSpace(r.Evidence);
        foreach (var region in new[] { "beginning", "middle", "late", "boundary" })
            if (!p.Reviews.Any(r => r.Region == region && ValidReview(r)))
                throw new InvalidDataException($"Missing successful {region} playback/token/timing review.");
        if (!p.Reviews.Any(r => ValidReview(r) && r.Region == "late" && r.PositionSeconds >= p.MeasuredDurationSeconds * .9)
            || !p.Reviews.Any(r => ValidReview(r) && r.Region == "beginning" && r.PositionSeconds <= p.MeasuredDurationSeconds * .1)
            || !p.Reviews.Any(r => ValidReview(r) && r.Region == "middle" && r.PositionSeconds >= p.MeasuredDurationSeconds * .4 && r.PositionSeconds <= p.MeasuredDurationSeconds * .6))
            throw new InvalidDataException("Reviews do not sample beginning, middle and the full-duration tail.");
    }

    public async Task<ListeningLesson> FindListeningSourceAsync(SpeakingCatalogEntry item, CancellationToken ct = default)
    {
        var original = item;
        if (item.ReplacementOfYoutubeId is string oldId)
        {
            if (originalSelection == null || !originalSelection.TryGetValue(oldId, out original)
                || original.Level != item.Level || original.ReplacementOfYoutubeId != null)
                throw new InvalidDataException("Replacement needs its original same-level Listening identity; no numeric/domain shortcut.");
        }
        var rows = await db.ListeningLessons.AsNoTracking().Where(x => x.OwnerUserId == null)
            .Select(x => new { x.Id, x.YoutubeId }).ToListAsync(ct);
        var matches = rows.Where(x => string.Equals(x.YoutubeId, original.YoutubeId, StringComparison.Ordinal)).ToList();
        if (matches.Count != 1) throw new InvalidDataException($"Expected exactly one public Listening source: {item.WatchUrl}");
        var source = await db.ListeningLessons.SingleAsync(x => x.Id == matches[0].Id && x.OwnerUserId == null, ct);
        await db.Entry(source).ReloadAsync(ct);
        if (source.OwnerUserId != null || source.Level != original.Level || source.Title != original.DisplayTitle || source.Topic != original.Topic
            || source.Duration != original.Duration || source.ThumbnailUrl != original.ThumbnailUrl)
            throw new InvalidDataException($"Listening source changed since selection; preserved: {item.WatchUrl}");
        return source;
    }

    public async Task<SpeakingTransferReceipt> RetireListeningAsync(SpeakingCatalogEntry item, CancellationToken ct = default)
    {
        ValidateSelection([item], false);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var source = await FindListeningSourceAsync(item, ct);
        source.IsPublished = false;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(item.YoutubeId, source.Id, null, "tct.listening.youtube." + HashId(source.YoutubeId!), "tct.speaking.youtube." + HashId(item.YoutubeId), true, false, source.YoutubeId);
    }

    public async Task<SpeakingTransferReceipt> PublishAsync(ReadySpeakingCatalogItem ready, CancellationToken ct = default)
    {
        ValidateReady(ready); // before transaction/writes: never expose pending public stubs
        return await WriteAsync(ready, ct);
    }

    /// <summary>Technical acceptance for owner-authorized local UAT, without asserting content QA.</summary>
    public static void ValidateForLocalTest(ReadySpeakingCatalogItem item)
    {
        ValidateSelection([item.Item], false);
        var p = item.Proof;
        bool Digest(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if (!Digest(p.SourceSha256) || !Digest(p.MediaSha256) || p.CueSha256 != CueHash(item.Cues)
            || p.TranscriptSource != "youtube-captions" || !double.IsFinite(p.MeasuredDurationSeconds)
            || Math.Abs(p.MeasuredDurationSeconds - item.Item.DurationSeconds) > 2 || item.Cues.Count is < 3 or > 3000)
            throw new InvalidDataException("Local-test source/media/cue evidence is invalid: " + item.Item.WatchUrl);
        double start = -1;
        foreach (var cue in item.Cues)
        {
            if (!double.IsFinite(cue.StartTime) || !double.IsFinite(cue.EndTime) || cue.StartTime < start
                || cue.StartTime < 0 || cue.EndTime <= cue.StartTime || cue.EndTime > p.MeasuredDurationSeconds
                || cue.EndTime - cue.StartTime > 30 || string.IsNullOrWhiteSpace(cue.Text) || cue.Text.Length > 2000)
                throw new InvalidDataException("Local-test cue has invalid bounds/text: " + item.Item.WatchUrl);
            start = cue.StartTime; // Source caption overlaps are retained for UAT, never declared precise alignment.
        }
    }

    public async Task<SpeakingTransferReceipt> ImportForLocalTestAsync(ReadySpeakingCatalogItem item, CancellationToken ct = default)
    {
        ValidateForLocalTest(item);
        return await WriteAsync(item, ct);
    }

    private async Task<SpeakingTransferReceipt> WriteAsync(ReadySpeakingCatalogItem ready, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var source = await FindListeningSourceAsync(ready.Item, ct);
        var publicRows = await db.SpeakingVideos.AsNoTracking().Where(x => x.OwnerUserId == null)
            .Select(x => new { x.Id, x.YoutubeId }).ToListAsync(ct);
        var matches = publicRows.Where(x => string.Equals(x.YoutubeId, ready.Item.YoutubeId, StringComparison.Ordinal)).ToList();
        if (matches.Count > 1) throw new InvalidDataException("Multiple public Speaking targets; no overwrite allowed.");
        SpeakingVideo target;
        var created = matches.Count == 0;
        if (created)
        {
            var item = ready.Item;
            target = new() { Title = item.DisplayTitle, YoutubeId = item.YoutubeId, Level = item.Level, Topic = item.CatalogTopic ?? item.Topic,
                Duration = item.Duration, ThumbnailUrl = item.ThumbnailUrl, SourceUrl = item.WatchUrl, SourceType = "youtube",
                TranscriptSource = ready.Proof.TranscriptSource, ImportStatus = "ready", OwnerUserId = null, CreatedAt = DateTime.UtcNow,
                SpeakingSentences = ready.Cues.Select(c => new SpeakingSentence { StartTime = c.StartTime, EndTime = c.EndTime, Text = c.Text, VietnameseMeaning = string.Empty }).ToList() };
            db.SpeakingVideos.Add(target);
        }
        else
        {
            target = await db.SpeakingVideos.Include(x => x.SpeakingSentences).SingleAsync(x => x.Id == matches[0].Id, ct);
            var item = ready.Item;
            if (target.Title != item.DisplayTitle || target.Level != item.Level || target.Topic != (item.CatalogTopic ?? item.Topic)
                || target.Duration != item.Duration || target.ThumbnailUrl != item.ThumbnailUrl || target.SourceUrl != item.WatchUrl
                || target.SourceType != "youtube" || target.PlaylistId != null || target.ImportStatus != "ready"
                || target.TranscriptSource != ready.Proof.TranscriptSource
                || !target.SpeakingSentences.OrderBy(c => c.StartTime).Select(c => new SpeakingCatalogCue(c.StartTime, c.EndTime, c.Text)).SequenceEqual(ready.Cues)
                || target.SpeakingSentences.Any(c => c.VietnameseMeaning != string.Empty))
                throw new InvalidDataException($"Existing public Speaking content conflicts: {item.WatchUrl}");
        }
        source.IsPublished = false;
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch { db.ChangeTracker.Clear(); throw; }
        return new(ready.Item.YoutubeId, source.Id, target.Id, "tct.listening.youtube." + HashId(source.YoutubeId!), "tct.speaking.youtube." + HashId(ready.Item.YoutubeId), true, created, source.YoutubeId);
    }

}
