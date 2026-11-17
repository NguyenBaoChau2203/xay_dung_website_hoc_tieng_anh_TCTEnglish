using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using TCTEnglish.Services.Learning;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

[Collection("MariaDbAcceptance")]
public sealed class GoalsStage04MariaDbTests
{
    internal const string Migration = "20261114081306_AddBadgeTiersAndDiscoveryAwards";

    private static DbflashcardContext Open(string connection) => new(new DbContextOptionsBuilder<DbflashcardContext>()
        .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb")).Options);

    private static async Task<MariaDbAcceptanceFactory> FactoryAsync(GoalsStage03RegressionTests.TestClock clock)
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connection);
        await using var db = Open(connection);
        await MariaDbAcceptanceConnectionGuard.VerifyOnlineIdentityAsync(db.Database.GetDbConnection(), target.Database,
            MariaDbAcceptanceConnectionGuard.RequireRunId(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID")));
        await db.Database.MigrateAsync();
        var factory = new MariaDbAcceptanceFactory(connection, Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID"),
            s => GoalsStage03RegressionTests.Configure(s, clock));
        await factory.InitializeAsync();
        return factory;
    }

    [MariaDbAcceptanceFact]
    public async Task RealWritingJourney_EarnsEveryAward_WithUniqueDurableIntents()
    {
        var clock = GoalsStage04RegressionTests.Clock();
        await using var factory = await FactoryAsync(clock);
        await GoalsStage04RegressionTests.RealWritingJourneyAsync(factory, clock);
    }

    [MariaDbAcceptanceFact]
    public async Task ConcurrentAwardAndCatchUp_AllTen_OneGrantAndIntent_OrSilentRetroactiveGrant()
    {
        var clock = GoalsStage04RegressionTests.Clock();
        await using var factory = await FactoryAsync(clock);
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            foreach (var userId in new[] { TestDataIds.UserId, TestDataIds.OutsiderUserId })
            {
                await GoalsStage04RegressionTests.SeedEvidenceAsync(db, userId,
                    new Badge { MetricType = BadgeMetricType.DistinctWritingExercisesCompleted }, 50, clock.Now);
                await GoalsStage04RegressionTests.SeedEvidenceAsync(db, userId,
                    new Badge { MetricType = BadgeMetricType.TotalXp }, 500, clock.Now.AddDays(-40));
                await GoalsStage04RegressionTests.SeedEvidenceAsync(db, userId,
                    new Badge { MetricType = BadgeMetricType.ConfirmedLongestStreak }, 30, clock.Now);
            }
        }
        async Task Earn()
        {
            using var s = factory.Services.CreateScope();
            Assert.Equal(OperationStatus.Success, (await s.ServiceProvider.GetRequiredService<IGoalsService>()
                .RecordLearningActivityAsync(TestDataIds.UserId, new GoalsActivityUpdate { CardsReviewed = 1 })).Status);
        }
        async Task CatchUp()
        {
            using var s = factory.Services.CreateScope();
            await ((GoalsService)s.ServiceProvider.GetRequiredService<IGoalsService>())
                .ReconcileNewBadgesAsync(new[] { TestDataIds.OutsiderUserId }, dryRun: false);
        }
        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Earn()).Concat(Enumerable.Range(0, 6).Select(_ => CatchUp())))
            .WaitAsync(TimeSpan.FromSeconds(45));
        using var verify = factory.Services.CreateScope();
        var dbAfter = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        foreach (var userId in new[] { TestDataIds.UserId, TestDataIds.OutsiderUserId })
            Assert.Equal(10, await dbAfter.UserBadges.CountAsync(b => b.UserId == userId && b.BadgeId >= 13));
        Assert.Equal(10, await dbAfter.UserBadges.CountAsync(b => b.UserId == TestDataIds.OutsiderUserId && b.IsRetroactive));
        Assert.False(await dbAfter.LearningNotificationOutboxEvents.AnyAsync(o => o.UserId == TestDataIds.OutsiderUserId));
        Assert.False(await dbAfter.Notifications.AnyAsync(o => o.UserId == TestDataIds.OutsiderUserId));
        var keys = await dbAfter.LearningNotificationOutboxEvents.Where(o => o.UserId == TestDataIds.UserId && o.EventType == "BadgeEarned")
            .Select(o => o.NotificationEventKey).ToListAsync();
        Assert.Equal(10, keys.Count(k => int.Parse(k!.Split(':')[^1]) >= 13));
        Assert.Equal(keys.Count, keys.Distinct().Count());
        var dispatcher = verify.ServiceProvider.GetRequiredService<TCTEnglish.Services.Notifications.ILearningOutboxDispatcher>();
        await dispatcher.DrainAsync(100, CancellationToken.None);
        await dispatcher.DrainAsync(100, CancellationToken.None);
        var delivered = await dbAfter.Notifications.AsNoTracking().Where(n => n.UserId == TestDataIds.UserId
            && n.Type == TCTEnglish.Models.NotificationType.BadgeEarned).Select(n => n.EventKey).ToListAsync();
        Assert.Equal(10, delivered.Count(k => int.Parse(k!.Split(':')[^1]) >= 13));
        Assert.Equal(delivered.Count, delivered.Distinct().Count());
    }

    [MariaDbAcceptanceFact]
    public async Task RealOutboxFailure_RollsBackGrantAndLearning_ThenRetryPersistsOnce()
    {
        var clock = GoalsStage04RegressionTests.Clock();
        await using var factory = await FactoryAsync(clock);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        await GoalsStage04RegressionTests.SeedEvidenceAsync(db, TestDataIds.UserId,
            new Badge { MetricType = BadgeMetricType.DistinctWritingExercisesCompleted }, 3, clock.Now);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER stage04_outbox_failure BEFORE INSERT ON LearningNotificationOutbox
            FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'stage04 controlled failure'
            """);
        try
        {
            using var failure = factory.Services.CreateScope();
            await Assert.ThrowsAnyAsync<Exception>(() => failure.ServiceProvider.GetRequiredService<IGoalsService>()
                .RecordLearningActivityAsync(TestDataIds.UserId, new GoalsActivityUpdate { CardsReviewed = 1 }));
            Assert.Empty(await db.UserBadges.AsNoTracking().ToListAsync());
            Assert.Empty(await db.UserDailyActivities.AsNoTracking().ToListAsync());
            Assert.Equal(0, (await db.Users.AsNoTracking().SingleAsync(u => u.UserId == TestDataIds.UserId)).Streak);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER stage04_outbox_failure"); }
        using var retry = factory.Services.CreateScope();
        await retry.ServiceProvider.GetRequiredService<IGoalsService>().RecordLearningActivityAsync(TestDataIds.UserId,
            new GoalsActivityUpdate { CardsReviewed = 1 });
        Assert.Single(await db.UserBadges.AsNoTracking().Where(b => b.BadgeId == 13).ToListAsync());
        Assert.Single(await db.LearningNotificationOutboxEvents.AsNoTracking().Where(o => o.NotificationEventKey == $"badge:{TestDataIds.UserId}:13").ToListAsync());
    }

    // Each upgrade scenario creates its own explicitly bounded disposable database after validating fixture ownership.
    private static async Task<string> FreshUpgradeAsync(string suffix)
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connection);
        var run = MariaDbAcceptanceConnectionGuard.RequireRunId(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID"));
        await using var owner = Open(connection);
        await MariaDbAcceptanceConnectionGuard.VerifyOnlineIdentityAsync(owner.Database.GetDbConnection(), target.Database, run);
        var name = $"tct_acceptance_badge04_{suffix}_{Guid.NewGuid():N}";
        await owner.Database.ExecuteSqlRawAsync($"CREATE DATABASE `{name}` CHARACTER SET utf8mb4");
        var builder = new MySqlConnectionStringBuilder(connection) { Database = name };
        await using var fresh = Open(builder.ConnectionString);
        await fresh.Database.OpenConnectionAsync();
        await MariaDbAcceptanceConnectionGuard.BootstrapSentinelAsync(fresh.Database.GetDbConnection(), name, run);
        await MariaDbAcceptanceConnectionGuard.RequireFreshBaselineTargetAsync(fresh.Database.GetDbConnection(), name, run);
        // Use the immediate previous migration from assembly; never downgrade an existing fixture.
        var previous = fresh.GetService<IMigrationsAssembly>().Migrations.Keys.TakeWhile(id => id != Migration).Last();
        await fresh.Database.MigrateAsync(previous);
        return builder.ConnectionString;
    }

    [MariaDbAcceptanceFact]
    public async Task UpgradeRepeat_PreservesOldIdsCodesGrantsProgressAndDates_AndRefusesLossyDown()
    {
        await using var db = Open(await FreshUpgradeAsync("upgrade"));
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Users (UserID, Email, PasswordHash, FullName, Role, Status, Streak, LongestStreak) VALUES (91001,'badge-upgrade@example.invalid','synthetic','Synthetic','Standard',0,0,14);
            INSERT INTO UserBadges (UserID,BadgeID,AwardedAt) VALUES (91001,3,'2025-01-01 12:34:56');
            INSERT INTO UserDailyActivities (UserID,ActivityDate,XpEarned,WritingCompletedCount) VALUES (91001,'2025-01-01',120,3);
            """);
        var old = await db.Database.SqlQueryRaw<string>("SELECT CONCAT(Id, ':', Code) AS Value FROM Badges ORDER BY Id").ToListAsync();
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(old, await db.Badges.Where(b => b.Id <= 12).OrderBy(b => b.Id).Select(b => b.Id + ":" + b.Code).ToListAsync());
        var earned = await db.UserBadges.SingleAsync();
        Assert.Equal(new DateTime(2025, 1, 1, 12, 34, 56), earned.AwardedAt);
        Assert.False(earned.IsRetroactive);
        Assert.Equal(3, earned.BadgeId);
        Assert.Equal(14, (await db.Users.SingleAsync()).LongestStreak);
        Assert.Equal(120, (await db.UserDailyActivities.SingleAsync()).XpEarned);
        Assert.Equal(22, await db.Badges.CountAsync());
        db.UserBadges.Add(new UserBadge { UserId = 91001, BadgeId = 21, AwardedAt = new DateTime(2026, 11, 14) });
        await db.SaveChangesAsync();
        var previous = db.GetService<IMigrationsAssembly>().Migrations.Keys.TakeWhile(id => id != Migration).Last();
        var refusal = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync(previous));
        Assert.Contains("prevent downgrade", refusal.ToString());
        Assert.Equal(2, await db.UserBadges.CountAsync());
    }

    [MariaDbAcceptanceFact]
    public async Task CatalogCollisions_StopBeforeDdlAndPreserveCustomRows()
    {
        foreach (var collision in new[] { "id", "code", "legacy" })
        {
            await using var db = Open(await FreshUpgradeAsync(collision));
            if (collision == "legacy")
                await db.Database.ExecuteSqlRawAsync("UPDATE Badges SET Code='custom-legacy' WHERE Id=1");
            else
                await db.Database.ExecuteSqlRawAsync(collision == "id"
                    ? "INSERT INTO Badges (Id,Code,Name,Description,IconClass,MetricType,ThresholdValue,SortOrder) VALUES (13,'custom-award','Custom','Preserve','fas fa-award',1,2,100)"
                    : "INSERT INTO Badges (Id,Code,Name,Description,IconClass,MetricType,ThresholdValue,SortOrder) VALUES (900,'writing-mood','Custom','Preserve','fas fa-award',1,2,100)");
            var before = await db.Database.SqlQueryRaw<string>("SELECT CONCAT(Id, ':', Code) AS Value FROM Badges ORDER BY Id").ToListAsync();
            var refusal = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync());
            Assert.Contains("Badge catalog identity conflict", refusal.ToString());
            Assert.Equal(before, await db.Database.SqlQueryRaw<string>("SELECT CONCAT(Id, ':', Code) AS Value FROM Badges ORDER BY Id").ToListAsync());
            Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.columns WHERE table_schema=DATABASE() AND column_name='DifficultyKey'").SingleAsync());
            Assert.DoesNotContain(Migration, await db.Database.GetAppliedMigrationsAsync());
        }
    }
}
