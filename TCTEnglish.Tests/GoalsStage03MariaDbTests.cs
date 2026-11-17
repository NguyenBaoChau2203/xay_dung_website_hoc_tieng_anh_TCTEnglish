using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Services.Learning;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

[Collection("MariaDbAcceptance")]
public sealed class GoalsStage03MariaDbTests
{
    private static async Task<MariaDbAcceptanceFactory> CreateAsync(GoalsStage03RegressionTests.TestClock clock)
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var run = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");
        var target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connection);
        await using (var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb")).Options))
        {
            await MariaDbAcceptanceConnectionGuard.VerifyOnlineIdentityAsync(db.Database.GetDbConnection(), target.Database,
                MariaDbAcceptanceConnectionGuard.RequireRunId(run));
            await db.Database.MigrateAsync();
        }
        var factory = new MariaDbAcceptanceFactory(connection, run, s => GoalsStage03RegressionTests.Configure(s, clock));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<DbflashcardContext>().UserBadges.ToListAsync());
        return factory;
    }

    [MariaDbAcceptanceFact]
    public async Task ConcurrentWritingSpeakingVocabulary_OneDayOneBonus_NoLostCountersOrDuplicateIntents()
    {
        for (var iteration = 0; iteration < 5; iteration++)
        {
            var clock = new GoalsStage03RegressionTests.TestClock(new DateTime(2026, 11, 14, 16, 59, 59));
            await using var factory = await CreateAsync(clock);
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var arrived = 0;
            async Task Submit(int feature)
            {
                using var scope = factory.Services.CreateScope();
                if (Interlocked.Increment(ref arrived) == 3) ready.TrySetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
                if (feature == 0)
                    Assert.True((await scope.ServiceProvider.GetRequiredService<IWritingService>()
                        .EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId))!.Passed);
                else if (feature == 1)
                    Assert.True((await scope.ServiceProvider.GetRequiredService<ICompletionCreditService>()
                        .TryApplySpeakingVideoCompletionAsync(TestDataIds.UserId, TestDataIds.SpeakingVideoId,
                            new GoalsActivityUpdate { SpeakingCompletedCount = 1, XpEarned = 20 }, CancellationToken.None)).Applied);
                else
                    Assert.Equal(OperationStatus.Success, (await scope.ServiceProvider.GetRequiredService<IGoalsService>()
                        .RecordLearningActivityAsync(TestDataIds.UserId, new GoalsActivityUpdate { CardsReviewed = 1, XpEarned = 5 })).Status);
            }
            await Task.WhenAll(Submit(0), Submit(1), Submit(2)).WaitAsync(TimeSpan.FromSeconds(30));
            using var verify = factory.Services.CreateScope();
            var db = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var day = await db.UserDailyActivities.SingleAsync();
            Assert.Equal(new DateTime(2026, 11, 14), day.ActivityDate);
            Assert.Equal(35, day.XpEarned);
            Assert.Equal(10, day.StreakXpAwarded);
            Assert.Equal(1, day.CardsReviewed);
            Assert.Equal(1, day.SpeakingCompletedCount);
            Assert.Equal(0, day.WritingCompletedCount);
            Assert.Equal(1, (await db.Users.AsNoTracking().SingleAsync(u => u.UserId == TestDataIds.UserId)).Streak);
            Assert.Equal("Speaking", (await db.LearningCompletionCredits.SingleAsync()).Feature);
            var keys = await db.LearningNotificationOutboxEvents.Select(o => o.NotificationEventKey).ToListAsync();
            Assert.Equal(keys.Count, keys.Distinct().Count());
            var page = (await verify.ServiceProvider.GetRequiredService<IGoalsService>().GetGoalsAsync(TestDataIds.UserId))!;
            Assert.True(page.StudyDays[^1].HasConfirmedStudy);
            Assert.False(page.StudyDays[^1].AllGoalsMet); // Speaking met, Writing still incomplete.
        }
    }

    [MariaDbAcceptanceFact]
    public async Task MidnightReplayAndMigrationRepeat_PreserveHistoryAndFirstCompletionRewards()
    {
        var clock = new GoalsStage03RegressionTests.TestClock(new DateTime(2026, 11, 14, 16, 59, 59));
        await using var factory = await CreateAsync(clock);
        await WritingCompletionTransactionTests.SeedOneSentenceAsync(factory);
        async Task Submit()
        {
            using var s = factory.Services.CreateScope();
            await s.ServiceProvider.GetRequiredService<IWritingService>().EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId);
        }
        await Submit();
        clock.Now = clock.Now.AddSeconds(1);
        await Submit();
        await Submit();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var rows = await db.UserDailyActivities.OrderBy(a => a.ActivityDate).ToListAsync();
        Assert.Equal(new[] { 30, 10 }, rows.Select(a => a.XpEarned));
        Assert.Equal(new[] { 1, 0 }, rows.Select(a => a.WritingCompletedCount));
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == TestDataIds.UserId);
        Assert.Equal(2, user.Streak);
        Assert.Equal(2, user.LongestStreak);
        var credits = await db.LearningCompletionCredits.CountAsync();
        var awards = await db.UserBadges.CountAsync();
        var users = await db.Users.CountAsync();
        var attempts = await db.UserWritingAttempts.OrderBy(a => a.Id).Select(a => new { a.Id, a.CreatedAtUtc, a.SubmittedAnswer }).ToListAsync();
        var history = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.False(db.Database.HasPendingModelChanges());
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
        Assert.Equal(history, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(attempts, await db.UserWritingAttempts.OrderBy(a => a.Id).Select(a => new { a.Id, a.CreatedAtUtc, a.SubmittedAnswer }).ToListAsync());
        Assert.Equal(credits, await db.LearningCompletionCredits.CountAsync());
        Assert.Equal(awards, await db.UserBadges.CountAsync());
        Assert.Equal(users, await db.Users.CountAsync());
    }

    [MariaDbAcceptanceFact]
    public async Task PartialPractice_RealOutboxInsertFailure_RollsBack_ThenRetryQualifiesOnce()
    {
        var clock = new GoalsStage03RegressionTests.TestClock(new DateTime(2026, 11, 14, 0, 0, 0));
        await using var factory = await CreateAsync(clock);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER stage03_outbox_failure BEFORE INSERT ON LearningNotificationOutbox
            FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'stage03 controlled outbox failure'
            """);
        try
        {
            using var failure = factory.Services.CreateScope();
            await Assert.ThrowsAnyAsync<Exception>(() => failure.ServiceProvider.GetRequiredService<IWritingService>()
                .EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId));
            Assert.Empty(await db.UserWritingAttempts.ToListAsync());
            Assert.Empty(await db.UserWritingSentenceProgresses.ToListAsync());
            Assert.Empty(await db.UserWritingExerciseProgresses.ToListAsync());
            Assert.Empty(await db.UserDailyActivities.ToListAsync());
            Assert.Empty(await db.LearningCompletionCredits.ToListAsync());
            Assert.Empty(await db.UserBadges.ToListAsync());
            Assert.Equal(0, (await db.Users.AsNoTracking().SingleAsync(u => u.UserId == TestDataIds.UserId)).Streak);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER stage03_outbox_failure"); }
        using var retry = factory.Services.CreateScope();
        var writing = retry.ServiceProvider.GetRequiredService<IWritingService>();
        await writing.EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId);
        await writing.EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId);
        Assert.Equal(2, await db.UserWritingAttempts.CountAsync());
        Assert.Equal(10, (await db.UserDailyActivities.AsNoTracking().SingleAsync()).XpEarned);
        Assert.Empty(await db.LearningCompletionCredits.ToListAsync());
    }

    [MariaDbAcceptanceFact]
    public async Task TargetRevisions_CurrentEditsPreserveFinishedGoalDay_OnMariaDb()
    {
        var clock = new GoalsStage03RegressionTests.TestClock(new DateTime(2026, 11, 10, 0, 0, 0));
        await using var factory = await CreateAsync(clock);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        // Fixture goals were created by the acceptance host, not by this controlled clock.
        await db.UserGoals.ExecuteDeleteAsync();
        var goals = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        await goals.UpdateGoalAsync(TestDataIds.UserId, GoalArea.Writing, 1);
        clock.Now = clock.Now.AddDays(1);
        await goals.RecordLearningActivityAsync(TestDataIds.UserId, new GoalsActivityUpdate { WritingCompletedCount = 1, XpEarned = 20 });
        clock.Now = clock.Now.AddDays(1);
        await goals.UpdateGoalAsync(TestDataIds.UserId, GoalArea.Writing, 5);
        await goals.UpdateGoalAsync(TestDataIds.UserId, GoalArea.Speaking, 2);
        var before = (await goals.GetGoalsAsync(TestDataIds.UserId))!.StudyDays[^2];
        Assert.True(before.AllGoalsMet);
        await goals.UpdateGoalAsync(TestDataIds.UserId, GoalArea.Writing, 0);
        var after = (await goals.GetGoalsAsync(TestDataIds.UserId))!.StudyDays[^2];
        Assert.True(after.AllGoalsMet);
        Assert.Equal(before.MetGoalCount, after.MetGoalCount);
        Assert.Equal(30, await db.UserDailyActivities.SumAsync(a => a.XpEarned));
        Assert.Equal(new DateTime(2026, 11, 12), (await db.UserGoalTargetRevisions.OrderByDescending(r => r.Id).FirstAsync()).EffectiveFromUtc.Date);
    }
}
