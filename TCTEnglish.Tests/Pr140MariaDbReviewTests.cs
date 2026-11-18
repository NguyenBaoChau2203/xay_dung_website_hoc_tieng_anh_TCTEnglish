using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Moq;
using TCTEnglish.Services.Reading;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

[Collection("MariaDbAcceptance")]
public sealed class Pr140MariaDbReviewTests
{
    [ReadingUpgradeFact]
    public async Task MasterUpgrade_RepeatAndConcurrentCompletion_PreserveDataAndCreditOnce()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connection);
        var run = MariaDbAcceptanceConnectionGuard.RequireRunId();
        var baselineOptions = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb"))
            .ReplaceService<IMigrationsAssembly, MasterBeforePr140MigrationsAssembly>().Options;
        await using (var baseline = new DbflashcardContext(baselineOptions))
        {
            await MariaDbAcceptanceConnectionGuard.RequireFreshBaselineTargetAsync(baseline.Database.GetDbConnection(), target.Database, run);
            // EF applies the actual 22 master migrations and writes its own history.
            // No fabricated history rows or baseline downgrade.
            await baseline.Database.MigrateAsync();
            Assert.Equal(22, (await baseline.Database.GetAppliedMigrationsAsync()).Count());
            await baseline.Database.ExecuteSqlRawAsync("""
                INSERT INTO Users (UserID,Email,PasswordHash,FullName,Role,Status,SecurityVersion,CreatedAt)
                VALUES (901401,'pr140@example.invalid','synthetic-preserve','Reading review','Standard',0,7,UTC_TIMESTAMP());
                INSERT INTO ReadingPassages (Id,Title,Content,Level,Topic,IsPublished,CreatedAt)
                VALUES (901401,'Existing bài đọc','Preserve the existing passage.','A1','Existing topic',1,UTC_TIMESTAMP());
                INSERT INTO ReadingQuestions (Id,PassageId,QuestionText,OrderIndex)
                VALUES (901401,901401,'Legacy question',1);
                INSERT INTO ReadingOptions (Id,QuestionId,OptionText,IsCorrect)
                VALUES (901401,901401,'Existing correct answer',1);
                INSERT INTO UserReadingHistories (UserId,ReadingPassageId,ViewedAt,Score,IsCompleted)
                VALUES (901401,901401,UTC_TIMESTAMP(),78,0);
                INSERT INTO UserDailyActivities (UserID,ActivityDate,CardsReviewed,XpEarned,StreakXpAwarded,
                    NewCardsLearned,VocabularyCompletedCount,QuizzesCompleted,SpeakingCompletedCount,WritingCompletedCount,ReadingCompletedCount,ListeningCompletedCount)
                VALUES (901401,'2026-11-18',12,345,0,0,0,0,0,0,0,0);
                """);
            baseline.WritingExercises.Add(new WritingExercise { UserId = 901401, Title = "Existing private writing",
                Level = "A1", ContentType = "sentence", Topic = "Keep", SourceType = "manual", PreviewText = "Preserve", CreatedAt = DateTime.UtcNow });
            await baseline.SaveChangesAsync();
        }

        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb")).Options;
        await using var db = new DbflashcardContext(options);
        await db.Database.ExecuteSqlRawAsync("UPDATE ReadingPassages SET Title=REPEAT('T',501) WHERE Id=901401");
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync());
        await db.Database.OpenConnectionAsync();
        await using (var schema = db.Database.GetDbConnection().CreateCommand())
        {
            schema.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='Users' AND column_name='ReadingLevel'";
            Assert.Equal(0, Convert.ToInt32(await schema.ExecuteScalarAsync()));
        }
        Assert.Equal(22, (await db.Database.GetAppliedMigrationsAsync()).Count());
        await db.Database.ExecuteSqlRawAsync("UPDATE ReadingPassages SET Title='Existing bài đọc' WHERE Id=901401");
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
        Assert.Equal(26, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        var user = await db.Users.SingleAsync(u => u.UserId == 901401);
        Assert.Equal("synthetic-preserve", user.PasswordHash);
        Assert.Equal(7, user.SecurityVersion);
        Assert.Equal("A1", user.ReadingLevel);
        Assert.Equal("Published", (await db.ReadingPassages.SingleAsync()).Status);
        Assert.Equal(78, (await db.UserReadingHistories.SingleAsync()).Score);
        Assert.Equal(901401, (await db.WritingExercises.SingleAsync()).UserId);
        var activity = await db.UserDailyActivities.AsNoTracking().SingleAsync();
        Assert.Equal(12, activity.CardsReviewed);
        Assert.Equal(345, activity.XpEarned);
        Assert.False(activity.DailyChallengeCompleted);

        var failAfterWrite = true;
        var credits = 0;
        ReadingLearningService Learning(DbflashcardContext context)
        {
            var goals = new Mock<IGoalsService>();
            goals.Setup(g => g.BuildReadingCompletionActivityUpdate()).Returns(new GoalsActivityUpdate { ReadingCompletedCount = 1, XpEarned = 10 });
            goals.Setup(g => g.RecordLearningActivityAsync(901401, It.IsAny<GoalsActivityUpdate>())).Returns(async () =>
            {
                await context.UserDailyActivities.Where(a => a.UserId == 901401)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.ReadingCompletedCount, a => a.ReadingCompletedCount + 1)
                        .SetProperty(a => a.XpEarned, a => a.XpEarned + 10));
                if (failAfterWrite) throw new InvalidOperationException("simulated failure after reward write");
                Interlocked.Increment(ref credits);
                return GoalsActivityRecordResult.Success(1);
            });
            return new ReadingLearningService(context, goals.Object);
        }
        var service = Learning(db);
        var attempt = await service.StartAttemptAsync(901401, 901401, "Practice", null);
        await service.SaveAnswerAsync(901401, attempt.Id, new SaveReadingAnswerRequest { QuestionId = 901401, SelectedOptionId = 901401 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAttemptAsync(901401, attempt.Id));
        db.ChangeTracker.Clear();
        Assert.False((await db.ReadingAttempts.SingleAsync()).IsSubmitted);
        Assert.Equal(345, (await db.UserDailyActivities.AsNoTracking().SingleAsync()).XpEarned);
        failAfterWrite = false;
        await using var left = new DbflashcardContext(options);
        await using var right = new DbflashcardContext(options);
        var results = await Task.WhenAll(Learning(left).SubmitAttemptAsync(901401, attempt.Id), Learning(right).SubmitAttemptAsync(901401, attempt.Id));
        Assert.All(results, result => Assert.Equal(1, result.Score));
        Assert.Equal(1, credits);
        Assert.Equal(1, (await db.UserDailyActivities.AsNoTracking().SingleAsync()).ReadingCompletedCount);
        Assert.Equal(355, (await db.UserDailyActivities.AsNoTracking().SingleAsync()).XpEarned);
        Assert.Single(await db.WritingExercises.ToListAsync());
    }
}

public sealed class ReadingUpgradeFactAttribute : FactAttribute
{
    public ReadingUpgradeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TCT_PR140_READING_UPGRADE") != "1")
            Skip = "Requires a fresh owned MariaDB acceptance database and sentinel; never the dev/hosting DB.";
    }
}

#pragma warning disable EF1001 // Test-only filtering of genuine migrations to reproduce the reviewed master schema.
public sealed class MasterBeforePr140MigrationsAssembly(
    ICurrentDbContext context, IDbContextOptions options, IMigrationsIdGenerator generator,
    IDiagnosticsLogger<DbLoggerCategory.Migrations> logger) : MigrationsAssembly(context, options, generator, logger)
{
    private static readonly HashSet<string> Incoming = ["20261109091315_GuardLegacyReadingLengths", "20261109091316_AddReadingModuleSchema",
        "20261109092305_FixReadingQuestionPointsDefault", "20261116102446_AddUserDailyActivityDailyChallengeCompleted"];
    public override IReadOnlyDictionary<string, TypeInfo> Migrations => base.Migrations
        .Where(pair => !Incoming.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
}
#pragma warning restore EF1001
