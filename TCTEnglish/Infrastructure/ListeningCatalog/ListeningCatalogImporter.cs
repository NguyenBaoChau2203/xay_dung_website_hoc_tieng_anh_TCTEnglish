using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Infrastructure.ListeningCatalog;

public sealed record CatalogAvailability(string Status, bool IsPublic, bool PlayableInEmbed);
public sealed record CatalogLevelEvidence(string Url, string Classification, string Rationale);
public sealed record CatalogCaption(string Text, double StartTime, double EndTime);
public sealed record ListeningCatalogItem(
    string YoutubeId, string WatchUrl, string Level, string SourceTitle, string DisplayTitle,
    string Topic, int DurationSeconds, string Duration, string ThumbnailUrl,
    string ChannelId, string ChannelName, string ChannelUrl, CatalogLevelEvidence LevelEvidence,
    CatalogAvailability Availability, string SelectionState);
public sealed record StagedListeningLesson(ListeningCatalogItem Item, List<CatalogCaption> Captions);
public sealed record CatalogImportReceipt(string YoutubeId, int LessonId, string ContentSha256, bool Created);

/// <summary>
/// Explicit local authoring helper, never a startup seeder or a public endpoint.
/// Network staging is separate; this writer has no AI/audio provider dependency.
/// The caller guards its target and holds an exclusive batch lock.
/// </summary>
public sealed class ListeningCatalogImporter(DbflashcardContext db)
{
    public static readonly string[] Levels = ["A1", "A2", "B1", "B2", "C1", "C2"];

    public static void Validate(IReadOnlyList<ListeningCatalogItem> items, bool requireFullBatch = true)
    {
        if (items.Count == 0 || items.Select(x => x.YoutubeId).Distinct(StringComparer.Ordinal).Count() != items.Count)
            throw new InvalidDataException("Empty manifest or duplicate case-sensitive YouTube IDs.");
        if (requireFullBatch && (items.Count != 90 || Levels.Any(l => items.Count(x => x.Level == l) != 15)))
            throw new InvalidDataException("The catalog requires exactly 15 new items per level A1–C2.");
        foreach (var x in items)
        {
            if (!Levels.Contains(x.Level, StringComparer.Ordinal)
                || YoutubeUrlHelper.NormalizeYoutubeId(x.YoutubeId) != x.YoutubeId
                || x.WatchUrl != $"https://www.youtube.com/watch?v={x.YoutubeId}"
                || x.LevelEvidence.Url != x.WatchUrl || string.IsNullOrWhiteSpace(x.LevelEvidence.Rationale)
                || string.IsNullOrWhiteSpace(x.SourceTitle) || string.IsNullOrWhiteSpace(x.ChannelId)
                || string.IsNullOrWhiteSpace(x.DisplayTitle) || x.DisplayTitle.Length > 255
                || string.IsNullOrWhiteSpace(x.Topic) || x.Topic.Length > 100
                || x.DurationSeconds < 60 || x.Duration.Length is < 3 or > 20
                || x.ThumbnailUrl != YoutubeUrlHelper.BuildDefaultThumbnailUrl(x.YoutubeId)
                || x.SelectionState != "selected" || x.Availability != new CatalogAvailability("OK", true, true))
                throw new InvalidDataException($"Invalid or unavailable catalog item: {x.WatchUrl}");
        }
    }

    public static string StableHash(string youtubeId)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(youtubeId)));

    public static string ContentHash(StagedListeningLesson staged)
        => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(staged)));

    public static void ValidateCaptions(StagedListeningLesson staged)
    {
        double previousStart = -1, previousEnd = -1;
        foreach (var line in staged.Captions)
        {
            if (string.IsNullOrWhiteSpace(line.Text) || !double.IsFinite(line.StartTime) || !double.IsFinite(line.EndTime)
                || line.StartTime < 0 || line.StartTime < previousStart || line.EndTime <= line.StartTime
                || line.EndTime < previousEnd || line.EndTime > staged.Item.DurationSeconds + 2)
                throw new InvalidDataException($"Invalid caption time/order: {staged.Item.WatchUrl}");
            previousStart = line.StartTime;
            previousEnd = line.EndTime;
        }
    }

    public static async Task<StagedListeningLesson> StageCaptionsAsync(
        ListeningCatalogItem item, IYoutubeTranscriptService captionsOnly, CancellationToken ct)
    {
        var result = await captionsOnly.GetEnglishCaptionsOnlyAsync(item.YoutubeId, ct);
        var staged = new StagedListeningLesson(item, result.IsEnglishUsable
            ? result.Sentences.Select(x => new CatalogCaption(x.Text, x.StartTime, x.EndTime)).ToList() : []);
        ValidateCaptions(staged);
        return staged;
    }

    public async Task<int?> FindUnchangedAsync(StagedListeningLesson staged, CancellationToken ct = default)
    {
        // MariaDB's default collation is case-insensitive. Materialize public IDs
        // before comparing, including draft roots. Private copies are independent.
        var publicRows = await db.ListeningLessons.AsNoTracking().Where(x => x.OwnerUserId == null)
            .Select(x => new { x.Id, x.YoutubeId }).ToListAsync(ct);
        var matches = publicRows.Where(x => string.Equals(x.YoutubeId, staged.Item.YoutubeId, StringComparison.Ordinal)).ToList();
        if (matches.Count == 0) return null;
        if (matches.Count != 1) throw new InvalidDataException($"Multiple public roots: {staged.Item.WatchUrl}");
        var lesson = await db.ListeningLessons.AsNoTracking().Include(x => x.TranscriptLines)
            .Include(x => x.QuizQuestions).Include(x => x.VocabItems).SingleAsync(x => x.Id == matches[0].Id, ct);
        var x = staged.Item;
        var same = lesson.IsPublished && lesson.Level == x.Level && lesson.Title == x.DisplayTitle && lesson.Topic == x.Topic
            && lesson.Duration == x.Duration && lesson.ThumbnailUrl == x.ThumbnailUrl && lesson.AudioUrl == null
            && lesson.Speaker1Name == null && lesson.Speaker2Name == null && lesson.Speaker1Country == null && lesson.Speaker2Country == null
            && lesson.TranscriptSource == (staged.Captions.Count > 0 ? "youtube-captions" : null)
            && lesson.QuizQuestions.Count == 0 && lesson.VocabItems.Count == 0
            && lesson.TranscriptLines.OrderBy(t => t.OrderIndex).Select(t => new CatalogCaption(t.Text, t.StartTime ?? -1, t.EndTime ?? -1)).SequenceEqual(staged.Captions)
            && lesson.TranscriptLines.All(t => t.Speaker == "Speaker" && t.VietnameseMeaning == null);
        if (!same) throw new InvalidDataException($"Existing public content conflicts; preserved: {x.WatchUrl}");
        return lesson.Id;
    }

    public async Task<CatalogImportReceipt> ImportOneAsync(StagedListeningLesson staged, CancellationToken ct = default)
    {
        Validate([staged.Item], requireFullBatch: false);
        ValidateCaptions(staged);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var existing = await FindUnchangedAsync(staged, ct);
        if (existing.HasValue)
        {
            await transaction.CommitAsync(ct);
            return new(staged.Item.YoutubeId, existing.Value, ContentHash(staged), false);
        }
        var x = staged.Item;
        var lesson = new ListeningLesson
        {
            Title = x.DisplayTitle, Level = x.Level, Topic = x.Topic, YoutubeId = x.YoutubeId,
            Duration = x.Duration, ThumbnailUrl = x.ThumbnailUrl, OwnerUserId = null, IsPublished = true,
            TranscriptSource = staged.Captions.Count > 0 ? "youtube-captions" : null,
            TranscriptLines = staged.Captions.Select((line, index) => new ListeningTranscriptLine
            {
                OrderIndex = index + 1, Speaker = "Speaker", Text = line.Text,
                StartTime = line.StartTime, EndTime = line.EndTime
            }).ToList()
        };
        db.ListeningLessons.Add(lesson);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }
        return new(x.YoutubeId, lesson.Id, ContentHash(staged), true);
    }
}
