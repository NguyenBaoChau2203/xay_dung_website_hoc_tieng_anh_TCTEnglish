using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Moq;
using TCTEnglish.Infrastructure.ListeningCatalog;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;
using static TCTEnglish.Tests.SharedDataPackageTests;

namespace TCTEnglish.Tests;

public sealed class ListeningCatalogImportTests
{
    public static ListeningCatalogItem Item(string id = "Abcdef123_-", string level = "C2") => new(
        id, $"https://www.youtube.com/watch?v={id}", level, "Creator Proficiency lesson", "Proficiency lesson",
        "Exam practice", 300, "5:00", YoutubeUrlHelper.BuildDefaultThumbnailUrl(id), "channel", "Creator",
        "https://www.youtube.com/@creator", new($"https://www.youtube.com/watch?v={id}", "creator-labelled", "Creator course explicitly labels C2"),
        new("OK", true, true), "selected");

    private static async Task WithDb(Func<DbflashcardContext, Task> action)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        await action(db);
    }

    [Fact]
    public void ManifestRejectsDuplicatesAndIncompleteDistribution()
    {
        Assert.Throws<InvalidDataException>(() => ListeningCatalogImporter.Validate([Item(), Item()], false));
        Assert.Throws<InvalidDataException>(() => ListeningCatalogImporter.Validate([Item()]));
        ListeningCatalogImporter.Validate([Item(), Item("abcdef123_-")], false);
        Assert.NotEqual(ListeningCatalogImporter.StableHash("Abcdef123_-"), ListeningCatalogImporter.StableHash("abcdef123_-"));
        Assert.Throws<InvalidDataException>(() => ListeningCatalogImporter.Validate([Item() with { Availability = new("OK", true, false) }], false));
    }

    [Fact]
    public Task RerunPreservesIdsCaptionChildrenAndLearnerProgress() => WithDb(async db =>
    {
        var importer = new ListeningCatalogImporter(db);
        var staged = new StagedListeningLesson(Item(), [new("The first genuine caption.", 0, 2), new("The next caption.", 2, 4)]);
        var first = await importer.ImportOneAsync(staged);
        var childIds = await db.ListeningTranscriptLines.OrderBy(x => x.OrderIndex).Select(x => x.Id).ToListAsync();
        db.Users.Add(new() { UserId = 191, Email = "fixture@example.test", PasswordHash = "fixture" });
        db.UserListeningProgresses.Add(new() { UserId = 191, LessonId = first.LessonId, QuizScore = 83 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var second = await importer.ImportOneAsync(staged);
        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.LessonId, second.LessonId);
        Assert.Equal(first.ContentSha256, second.ContentSha256);
        Assert.Equal(childIds, await db.ListeningTranscriptLines.OrderBy(x => x.OrderIndex).Select(x => x.Id).ToListAsync());
        Assert.Equal(83, (await db.UserListeningProgresses.SingleAsync()).QuizScore);
        Assert.Single(await db.ListeningLessons.ToListAsync());
    });

    [Fact]
    public Task PrivateCopyIsIndependentAndCaseDistinctPublicIdsAreNotDuplicates() => WithDb(async db =>
    {
        db.Users.Add(new() { UserId = 191, Email = "fixture@example.test", PasswordHash = "fixture" });
        var privateRow = new ListeningLesson { Title = "Personal", Level = "B1", Topic = "Personal topic", YoutubeId = Item().YoutubeId, OwnerUserId = 191, IsPublished = true };
        db.ListeningLessons.Add(privateRow);
        await db.SaveChangesAsync();
        var privateId = privateRow.Id;
        var importer = new ListeningCatalogImporter(db);
        await importer.ImportOneAsync(new(Item(), []));
        await importer.ImportOneAsync(new(Item("abcdef123_-"), []));
        Assert.Equal(3, await db.ListeningLessons.CountAsync());
        db.ChangeTracker.Clear();
        var preserved = await db.ListeningLessons.SingleAsync(x => x.Id == privateId);
        Assert.Equal(191, preserved.OwnerUserId);
        Assert.Equal("Personal", preserved.Title);
        Assert.Equal("B1", preserved.Level);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task PublicConflictOrDraftStopsWithoutOverwriting(bool published) => WithDb(async db =>
    {
        db.ListeningLessons.Add(new() { Title = "Existing curation", Level = "B1", Topic = "Keep", YoutubeId = Item().YoutubeId, IsPublished = published });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => new ListeningCatalogImporter(db).ImportOneAsync(new(Item(), [])));
        var existing = await db.ListeningLessons.SingleAsync();
        Assert.Equal("Existing curation", existing.Title);
        Assert.Equal(published, existing.IsPublished);
    });

    [Fact]
    public Task CancellationAndInvalidCaptionsLeaveNoPartialRoot() => WithDb(async db =>
    {
        var importer = new ListeningCatalogImporter(db);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => importer.ImportOneAsync(new(Item(), []), new CancellationToken(true)));
        await Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportOneAsync(new(Item(), [new("Bad", double.NaN, 3)])));
        Assert.Empty(await db.ListeningLessons.ToListAsync());
        Assert.Empty(await db.ListeningTranscriptLines.ToListAsync());
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StagingUsesOnlyCaptionMethodAndHonorsMissingCaptions(bool usable)
    {
        var service = new Mock<IYoutubeTranscriptService>(MockBehavior.Strict);
        var item = Item();
        service.Setup(x => x.GetEnglishCaptionsOnlyAsync(item.YoutubeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YoutubeTranscriptResult(usable ? [new() { Text = "Real caption", StartTime = 1, EndTime = 3 }] : [], usable, usable ? "youtube-captions" : null));
        var staged = await ListeningCatalogImporter.StageCaptionsAsync(Item(), service.Object, default);
        Assert.Equal(usable ? 1 : 0, staged.Captions.Count);
        service.Verify(x => x.GetEnglishCaptionsOnlyAsync(item.YoutubeId, It.IsAny<CancellationToken>()), Times.Once);
        service.VerifyNoOtherCalls(); // ASR fallback, metadata and broad transcript calls all fail closed.
    }

    [Fact]
    public async Task CaptionInsertFailureRollsBackRootAndCanResumeCleanly()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var failure = new FailCaptionInsert();
        await using var db = new SharedTestContext(new DbContextOptionsBuilder<DbflashcardContext>()
            .UseSqlite(connection).AddInterceptors(failure).Options);
        await db.Database.EnsureCreatedAsync();
        var staged = new StagedListeningLesson(Item(), [new("Valid caption", 1, 3)]);
        var importer = new ListeningCatalogImporter(db);
        await Assert.ThrowsAsync<DbUpdateException>(() => importer.ImportOneAsync(staged));
        Assert.Empty(await db.ListeningLessons.ToListAsync());
        Assert.Empty(await db.ListeningTranscriptLines.ToListAsync());
        failure.Enabled = false;
        Assert.True((await importer.ImportOneAsync(staged)).Created);
        Assert.Single(await db.ListeningLessons.ToListAsync());
        Assert.Single(await db.ListeningTranscriptLines.ToListAsync());
    }

    private sealed class FailCaptionInsert : DbCommandInterceptor
    {
        public bool Enabled { get; set; } = true;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.Contains("INSERT INTO \"ListeningTranscriptLines\"", StringComparison.Ordinal))
                throw new InvalidOperationException("Synthetic child persistence failure");
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
