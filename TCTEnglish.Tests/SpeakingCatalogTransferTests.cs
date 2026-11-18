using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Infrastructure.SpeakingCatalog;
using TCTVocabulary.Models;
using Xunit;
using static TCTEnglish.Tests.SharedDataPackageTests;

namespace TCTEnglish.Tests;

public sealed class SpeakingCatalogTransferTests
{
    [Fact]
    public Task LocalTestImportAcceptsPendingQaAndPreservesRealOverlapsAndRepeatIds() => WithDb(async db =>
    {
        var prepared = Ready(Item() with { CatalogTopic = "Ăn uống" });
        prepared = prepared with { Proof = prepared.Proof with { FullSpeechReviewed = false, Coverage = [], Reviews = [], UnresolvedFlags = ["owner-local-test-before-staging"] } };
        var cues = prepared.Cues.Select((c,i) => i == 1 ? c with { StartTime = 9.5 } : c).ToList();
        prepared = prepared with { Cues = cues, Proof = prepared.Proof with { CueSha256 = SpeakingCatalogTransfer.CueHash(cues) } };
        db.ListeningLessons.Add(Source(prepared.Item)); await db.SaveChangesAsync();
        var writer = new SpeakingCatalogTransfer(db);
        await Assert.ThrowsAsync<InvalidDataException>(() => writer.PublishAsync(prepared));
        var first = await writer.ImportForLocalTestAsync(prepared);
        var ids = await db.SpeakingSentences.OrderBy(x => x.StartTime).Select(x => x.Id).ToListAsync();
        var repeat = await writer.ImportForLocalTestAsync(prepared);
        Assert.True(first.SpeakingCreated); Assert.False(repeat.SpeakingCreated);
        Assert.Equal(ids, await db.SpeakingSentences.OrderBy(x => x.StartTime).Select(x => x.Id).ToListAsync());
        Assert.Equal(9.5, (await db.SpeakingSentences.OrderBy(x => x.StartTime).Skip(1).FirstAsync()).StartTime);
        Assert.Equal("ready", (await db.SpeakingVideos.SingleAsync()).ImportStatus);
        Assert.Null((await db.SpeakingVideos.SingleAsync()).OwnerUserId);
        Assert.Equal("Ăn uống", (await db.SpeakingVideos.SingleAsync()).Topic);
    });

    [Fact]
    public Task LocalTestImportRejectsBrokenAcquisitionAndOutOfMediaBoundsBeforeMutation() => WithDb(async db =>
    {
        var ready = Ready(); db.ListeningLessons.Add(Source(ready.Item)); await db.SaveChangesAsync();
        var writer = new SpeakingCatalogTransfer(db);
        var bad = ready.Cues.ToList(); bad[^1] = bad[^1] with { EndTime = 2000 };
        await Assert.ThrowsAsync<InvalidDataException>(() => writer.ImportForLocalTestAsync(ready with { Cues = bad, Proof = ready.Proof with { CueSha256 = SpeakingCatalogTransfer.CueHash(bad) } }));
        await Assert.ThrowsAsync<InvalidDataException>(() => writer.ImportForLocalTestAsync(ready with { Proof = ready.Proof with { SourceSha256 = "invalid" } }));
        Assert.Empty(await db.SpeakingVideos.ToListAsync());
        Assert.True((await db.ListeningLessons.SingleAsync()).IsPublished);
    });
    public static SpeakingCatalogEntry Item(string id = "Abcdef123_-", int duration = 1900) => new(
        id, "https://www.youtube.com/watch?v=" + id, "C2", "Proficiency fixture", "Exam", duration,
        duration == 1900 ? "31:40" : "5:00", "https://img.youtube.com/vi/" + id + "/hqdefault.jpg");
    public static ReadySpeakingCatalogItem Ready(SpeakingCatalogEntry? item = null)
    {
        item ??= Item();
        var cues = new List<SpeakingCatalogCue>();
        for (double t = 0; t < item.DurationSeconds; t += 10)
            cues.Add(new(t, Math.Min(t + 10, item.DurationSeconds), "Synthetic meaningful English utterance for an offline fixture."));
        var proof = new CatalogQualityProof(new('a', 64), new('b', 64), SpeakingCatalogTransfer.CueHash(cues),
            item.DurationSeconds, "youtube-captions", true, [new(0, item.DurationSeconds, "english-speech", "Synthetic complete coverage")],
            [new(1, "beginning", .99, .2, false, "Synthetic review"), new(item.DurationSeconds / 2d, "middle", .99, .2, false, "Synthetic review"),
             new(item.DurationSeconds - 2, "late", .99, .2, false, "Synthetic review"), new(300, "boundary", .99, .2, false, "Synthetic review")], []);
        return new(item, cues, proof);
    }
    private static async Task WithDb(Func<DbflashcardContext, Task> action)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(); await action(db);
    }
    public static ListeningLesson Source(SpeakingCatalogEntry item) => new() { Title = item.DisplayTitle, YoutubeId = item.YoutubeId,
        Level = item.Level, Topic = item.Topic, Duration = item.Duration, ThumbnailUrl = item.ThumbnailUrl, IsPublished = true };

    [Fact]
    public void WholeVideoGateRejectsTruncatedTailAndUnreviewedCoverageEvenWithNonemptyText()
    {
        var ready = Ready(); SpeakingCatalogTransfer.ValidateReady(ready);
        var truncated = ready.Cues.Where(c => c.EndTime <= 1800).ToList();
        Assert.Throws<InvalidDataException>(() => SpeakingCatalogTransfer.ValidateReady(ready with { Cues = truncated, Proof = ready.Proof with { CueSha256 = SpeakingCatalogTransfer.CueHash(truncated) } }));
        Assert.Throws<InvalidDataException>(() => SpeakingCatalogTransfer.ValidateReady(ready with { Proof = ready.Proof with { FullSpeechReviewed = false } }));
        Assert.Throws<InvalidDataException>(() => SpeakingCatalogTransfer.ValidateReady(ready with { Proof = ready.Proof with { Coverage = [new(0, 1800, "english-speech", "Missing full tail")] } }));
        Assert.Throws<InvalidDataException>(() => SpeakingCatalogTransfer.ValidateReady(ready with { Proof = ready.Proof with { Reviews = ready.Proof.Reviews.Where(x => x.Region != "late").ToList() } }));
        Assert.Throws<InvalidDataException>(() => SpeakingCatalogTransfer.ValidateReady(ready with { Proof = ready.Proof with { UnresolvedFlags = ["Unreviewed speech gap"] } }));
        var misplaced = ready.Proof.Reviews.Select(r => r.Region == "late" ? r with { PositionSeconds = 1 } : r).ToList();
        misplaced.Add(new(1899, "late", .2, 8, true, "Failed tail playback"));
        Assert.Throws<InvalidDataException>(() => SpeakingCatalogTransfer.ValidateReady(ready with { Proof = ready.Proof with { Reviews = misplaced } }));
    }

    [Fact]
    public Task RetirementChangesOnlySelectedFlagsAndPreservesPrivateOriginalAndHistory() => WithDb(async db =>
    {
        var item = Item(); var selected = Source(item);
        var original = Source(Item("original123"));
        var privateRow = Source(item); privateRow.OwnerUserId = 191;
        db.Users.Add(new() { UserId = 191, Email = "fixture@example.test", PasswordHash = "fixture" });
        db.ListeningLessons.AddRange(selected, original, privateRow); await db.SaveChangesAsync();
        db.UserListeningProgresses.Add(new() { UserId = 191, LessonId = selected.Id, QuizScore = 81 }); await db.SaveChangesAsync();
        var transfer = new SpeakingCatalogTransfer(db);
        var first = await transfer.RetireListeningAsync(item); var second = await transfer.RetireListeningAsync(item);
        Assert.Equal(first.ListeningId, second.ListeningId); Assert.Null(first.SpeakingId);
        Assert.False((await db.ListeningLessons.SingleAsync(x => x.Id == selected.Id)).IsPublished);
        Assert.True((await db.ListeningLessons.SingleAsync(x => x.Id == original.Id)).IsPublished);
        Assert.True((await db.ListeningLessons.SingleAsync(x => x.Id == privateRow.Id)).IsPublished);
        Assert.Equal(81, (await db.UserListeningProgresses.SingleAsync()).QuizScore);
        Assert.Empty(await db.SpeakingVideos.ToListAsync());
    });

    [Fact]
    public Task PublishFullLongVideoAndRepeatKeepSentenceIdsAndDomainHistorySeparate() => WithDb(async db =>
    {
        var ready = Ready(); var source = Source(ready.Item);
        db.Users.Add(new() { UserId = 191, Email = "fixture@example.test", PasswordHash = "fixture" });
        db.ListeningLessons.Add(source); await db.SaveChangesAsync();
        db.UserListeningProgresses.Add(new() { UserId = 191, LessonId = source.Id, QuizScore = 81 }); await db.SaveChangesAsync();
        var transfer = new SpeakingCatalogTransfer(db);
        var first = await transfer.PublishAsync(ready);
        var sentenceIds = await db.SpeakingSentences.OrderBy(c => c.StartTime).Select(c => c.Id).ToListAsync();
        db.ChangeTracker.Clear();
        var second = await transfer.PublishAsync(ready);
        Assert.True(first.SpeakingCreated); Assert.False(second.SpeakingCreated);
        Assert.Equal(first.SpeakingId, second.SpeakingId);
        Assert.Equal(sentenceIds, await db.SpeakingSentences.OrderBy(c => c.StartTime).Select(c => c.Id).ToListAsync());
        Assert.Equal(1900, (await db.SpeakingSentences.OrderBy(c => c.EndTime).LastAsync()).EndTime);
        Assert.Empty(await db.UserSpeakingProgresses.ToListAsync());
        Assert.Equal(81, (await db.UserListeningProgresses.SingleAsync()).QuizScore);
        Assert.NotEqual(first.ListeningKey, first.SpeakingKey);
    });

    [Fact]
    public Task InvalidQualityOrExistingSpeakingConflictPublishesNoStubAndDoesNotRetireSource() => WithDb(async db =>
    {
        var ready = Ready(); var source = Source(ready.Item);
        db.ListeningLessons.Add(source); await db.SaveChangesAsync();
        var transfer = new SpeakingCatalogTransfer(db);
        await Assert.ThrowsAsync<InvalidDataException>(() => transfer.PublishAsync(ready with { Proof = ready.Proof with { FullSpeechReviewed = false } }));
        Assert.Empty(await db.SpeakingVideos.ToListAsync());
        db.SpeakingVideos.Add(new() { Title = "Original Speaking content", YoutubeId = ready.Item.YoutubeId, CreatedAt = DateTime.UtcNow }); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => transfer.PublishAsync(ready));
        db.ChangeTracker.Clear();
        Assert.True((await db.ListeningLessons.SingleAsync()).IsPublished);
        Assert.Equal("Original Speaking content", (await db.SpeakingVideos.SingleAsync()).Title);
    });

    [Fact]
    public Task OwnerAuthorizedReplacementKeepsOriginalListeningIdentityAndNewSpeakingKey() => WithDb(async db =>
    {
        var original = Item(); var replacement = Item("Replacement", 300) with { ReplacementOfYoutubeId = original.YoutubeId };
        var source = Source(original); db.ListeningLessons.Add(source); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => new SpeakingCatalogTransfer(db).PublishAsync(Ready(replacement)));
        var writer = new SpeakingCatalogTransfer(db, new Dictionary<string, SpeakingCatalogEntry>(StringComparer.Ordinal) { [original.YoutubeId] = original });
        await Assert.ThrowsAsync<InvalidDataException>(() => writer.PublishAsync(Ready(replacement with { Level = "B1" })));
        var receipt = await writer.PublishAsync(Ready(replacement));
        Assert.Equal(original.YoutubeId, receipt.ListeningYoutubeId);
        Assert.Equal(source.Id, receipt.ListeningId);
        Assert.EndsWith(SpeakingCatalogTransfer.HashId(original.YoutubeId), receipt.ListeningKey);
        Assert.EndsWith(SpeakingCatalogTransfer.HashId(replacement.YoutubeId), receipt.SpeakingKey);
        Assert.False((await db.ListeningLessons.SingleAsync()).IsPublished);
        Assert.Equal(replacement.YoutubeId, (await db.SpeakingVideos.SingleAsync()).YoutubeId);
        Assert.False((await writer.PublishAsync(Ready(replacement))).SpeakingCreated);
    });
}
