using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

[Collection("MariaDbAcceptance")]
public sealed class SpeakingRecentVideosMariaDbTests
{
    [SpeakingRecentUpgradeFact]
    public async Task UpgradeRepeatConcurrencyAndCleanupPreserveExistingData()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connection);
        var run = MariaDbAcceptanceConnectionGuard.RequireRunId();
        DbflashcardContext Open() => new(new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb"), sql => sql.EnableRetryOnFailure()).Options);
        await using var db = Open();
        await MariaDbAcceptanceConnectionGuard.RequireFreshBaselineTargetAsync(db.Database.GetDbConnection(), target.Database, run);
        await db.Database.MigrateAsync("20261113112346_AddSpeakingAssessmentComponentScores");
        var user = new User { Email = "recent-upgrade@example.invalid", PasswordHash = "synthetic-only", Role = Roles.Premium };
        var video = new SpeakingVideo { Title = "Original title", YoutubeId = "abcdefghijk",
            SpeakingSentences = [new() { Text = "Keep original cue", StartTime = 0, EndTime = 2 }] };
        db.Users.Add(user); db.SpeakingVideos.Add(video); await db.SaveChangesAsync();
        var sentenceId = video.SpeakingSentences.Single().Id;
        var at = new DateTime(2026, 11, 13, 1, 2, 3, DateTimeKind.Utc);
        db.UserSpeakingProgresses.Add(new() { UserId = user.UserId, SentenceId = sentenceId,
            TotalScore = 91, AccuracyScore = 91, PracticedAt = at });
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync(); await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        Assert.Equal("Original title", (await db.SpeakingVideos.SingleAsync()).Title);
        Assert.Equal("Keep original cue", (await db.SpeakingSentences.SingleAsync()).Text);
        Assert.Equal("synthetic-only", (await db.Users.SingleAsync()).PasswordHash);
        var progress = await db.UserSpeakingProgresses.SingleAsync();
        Assert.Equal(91, progress.TotalScore); Assert.Equal(at, progress.PracticedAt);
        Assert.Empty(await db.UserSpeakingVideoVisits.ToListAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        // Independent relational contexts reproduce simultaneous first visits.
        var visits = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            await using var context = Open();
            return await Service(context).RecordVideoVisitAsync(user.UserId, video.Id);
        }));
        Assert.All(visits, time => Assert.NotNull(time));
        Assert.Single(await db.UserSpeakingVideoVisits.AsNoTracking().ToListAsync());
        Assert.Single(await Service(db).GetRecentVideosAsync(user.UserId));
        var future = DateTime.UtcNow.AddHours(1);
        await db.UserSpeakingVideoVisits.ExecuteUpdateAsync(s => s.SetProperty(v => v.LastOpenedAtUtc, future));
        var retained = await Service(db).RecordVideoVisitAsync(user.UserId, video.Id);
        Assert.True(Math.Abs((retained!.Value - future).TotalMilliseconds) < 1);
        Assert.Equal(91, (await db.UserSpeakingProgresses.AsNoTracking().SingleAsync()).TotalScore);
        db.SpeakingVideos.Remove(await db.SpeakingVideos.SingleAsync()); await db.SaveChangesAsync();
        Assert.Empty(await db.UserSpeakingVideoVisits.ToListAsync());

        await using var factory = new MariaDbAcceptanceFactory(connection, run);
        await factory.InitializeAsync();
        await SpeakingRecentVideosIntegrationTests.AssertAccountCleanupAsync(factory, keepLedger: false);
        await SpeakingRecentVideosIntegrationTests.AssertAccountCleanupAsync(factory, keepLedger: true);
    }

    private static SpeakingService Service(DbflashcardContext db) => new(db, null!, NullLogger<SpeakingService>.Instance);
}

public sealed class SpeakingRecentUpgradeFactAttribute : FactAttribute
{
    public SpeakingRecentUpgradeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TCT_SPEAKING_RECENT_UPGRADE") != "1"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")))
            Skip = "Requires an opted-in virgin disposable MariaDB acceptance DB and sentinel.";
    }
}
