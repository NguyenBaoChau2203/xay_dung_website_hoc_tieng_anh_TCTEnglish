using Microsoft.EntityFrameworkCore;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

[Collection("MariaDbAcceptance")]
public sealed class SpeakingComponentScoresMariaDbUpgradeTests
{
    [SpeakingMetricsUpgradeFact]
    public async Task NullableMetricsUpgradeAndRepeatPreserveExistingScoresReceiptsAndOwnership()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connection);
        var run = MariaDbAcceptanceConnectionGuard.RequireRunId(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID"));
        await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb")).Options);
        await MariaDbAcceptanceConnectionGuard.RequireFreshBaselineTargetAsync(db.Database.GetDbConnection(), target.Database, run);
        await db.Database.MigrateAsync("20261111114120_AddAccountFunctionalCompletion");
        var user = new User { Email = "metrics-upgrade@example.invalid", PasswordHash = "synthetic-only", Role = Roles.Admin, FullName = "Metrics migration fixture" };
        var video = new SpeakingVideo { Title = "Metrics fixture", YoutubeId = "abcdefghijk", SpeakingSentences = [
            new SpeakingSentence { Text = "hello world", VietnameseMeaning = "Xin chào", StartTime = 0, EndTime = 2 }] };
        db.Users.Add(user); db.SpeakingVideos.Add(video); await db.SaveChangesAsync();
        video.OwnerUserId = user.UserId;
        var sentence = video.SpeakingSentences.Single();
        var at = new DateTime(2026, 11, 13, 3, 4, 5, DateTimeKind.Utc);
        db.UserSpeakingProgresses.Add(new() { UserId = user.UserId, SentenceId = sentence.Id, TotalScore = 90,
            AccuracyScore = 90, FluencyScore = 0, CompletenessScore = 0, PracticedAt = at });
        await db.SaveChangesAsync();
        // Old schema deliberately has no metric columns: insert the historical receipt using its old contract.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO SpeakingAssessments (PublicId,UserId,SentenceId,CreatedAtUtc,ExpiresAtUtc,EngineName,
                ModelIdentity,TranscriptText,TextMatchScore,SpeechDetected,Status,Consumed,ConsumedAtUtc)
            VALUES ({Guid.NewGuid()},{user.UserId},{sentence.Id},{at},{at.AddMinutes(10)},
                'fixture','fixture','hello world',90,1,'Completed',1,{at})
            """);
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var receipt = await db.SpeakingAssessments.SingleAsync();
        Assert.Null(receipt.FluencyScore); Assert.Null(receipt.CompletenessScore); Assert.Null(receipt.MetricVersion);
        Assert.Equal(90, receipt.TextMatchScore); Assert.Equal("hello world", receipt.TranscriptText);
        Assert.True(receipt.Consumed); Assert.Equal(at, receipt.ConsumedAtUtc);
        Assert.Equal(user.UserId, receipt.UserId); Assert.Equal(sentence.Id, receipt.SentenceId);
        Assert.Equal(user.UserId, (await db.SpeakingVideos.SingleAsync()).OwnerUserId);
        var progress = await db.UserSpeakingProgresses.SingleAsync();
        Assert.Equal(90, progress.TotalScore); Assert.Equal(0, progress.FluencyScore); Assert.Equal(at, progress.PracticedAt);
        receipt.FluencyScore = 0; receipt.CompletenessScore = 75.25; receipt.MetricVersion = 1;
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        receipt = await db.SpeakingAssessments.SingleAsync();
        Assert.Equal(0, receipt.FluencyScore); Assert.Equal(75.25, receipt.CompletenessScore); Assert.Equal(1, receipt.MetricVersion);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
}

public sealed class SpeakingMetricsUpgradeFactAttribute : FactAttribute
{
    public SpeakingMetricsUpgradeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TCT_SPEAKING_METRICS_UPGRADE") != "1"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")))
            Skip = "Requires an explicitly opted-in virgin disposable MariaDB acceptance DB and sentinel.";
    }
}
