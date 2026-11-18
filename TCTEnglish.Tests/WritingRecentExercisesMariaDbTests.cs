using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

[Collection("MariaDbAcceptance")]
public sealed class WritingRecentExercisesMariaDbTests
{
    [WritingRecentUpgradeFact]
    public async Task UpgradeRepeatPreservationAccessConcurrencyAndRealCleanup()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var target = MariaDbAcceptanceConnectionGuard.ParseAndValidateOffline(connection);
        var run = MariaDbAcceptanceConnectionGuard.RequireRunId();
        await using var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(connection, ServerVersion.Parse("10.5.23-mariadb")).Options);
        await MariaDbAcceptanceConnectionGuard.RequireFreshBaselineTargetAsync(db.Database.GetDbConnection(), target.Database, run);
        await db.Database.MigrateAsync(GoalsStage04MariaDbTests.Migration);
        db.Users.Add(new User { UserId = 91001, Email = "preserve@example.invalid", PasswordHash = "synthetic-only", Role = Roles.Standard, Streak = 7 });
        await db.SaveChangesAsync();
        var before = await WritingRecentExercisesTests.RewardSnapshot(db);
        var lessons = await db.WritingExercises.OrderBy(e => e.Id).Select(e => new { e.Id, e.Title, e.CreatedAt }).ToListAsync();
        await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        Assert.Equal(before, await WritingRecentExercisesTests.RewardSnapshot(db));
        Assert.Equal(lessons, await db.WritingExercises.OrderBy(e => e.Id).Select(e => new { e.Id, e.Title, e.CreatedAt }).ToListAsync());
        Assert.Empty(await db.UserWritingExerciseVisits.ToListAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        var indexes = await db.Database.SqlQueryRaw<string>("SELECT DISTINCT INDEX_NAME AS Value FROM information_schema.statistics WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='UserWritingExerciseVisits'").ToListAsync();
        Assert.Contains("PRIMARY", indexes);
        Assert.Contains("IX_UserWritingExerciseVisits_UserId_Opened_Exercise", indexes);
        Assert.Contains("IX_UserWritingExerciseVisits_WritingExerciseId", indexes);
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME='UserWritingExerciseVisits' AND DELETE_RULE='CASCADE'").SingleAsync());

        var clock = new GoalsStage03RegressionTests.TestClock(new DateTime(2026, 11, 15, 1, 0, 0));
        await using var factory = new MariaDbAcceptanceFactory(connection, run, s => GoalsStage03RegressionTests.Configure(s, clock));
        var competingInsert = new WritingRecentExercisesTests.CompetingInsert();
        factory.SaveInterceptors.Add(competingInsert);
        await factory.InitializeAsync();
        await WritingRecentExercisesTests.AssertOrderAccessAndIsolation(factory, clock);
        Assert.True(competingInsert.Inserted);
        await WritingRecentExercisesTests.AssertCleanup(factory, false, concurrent: true);
        await WritingRecentExercisesTests.AssertCleanup(factory, true, concurrent: true);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        // Fresh pair, independent contexts, controlled request timestamps.
        var rewards = await WritingRecentExercisesTests.RewardSnapshot(context);
        var tasks = new List<Task<DateTime?>>();
        for (var i = 0; i < 12; i++)
        {
            clock.Now = clock.Now.AddSeconds(1);
            tasks.Add(VisitAsync());
            async Task<DateTime?> VisitAsync()
            {
                using var request = factory.Services.CreateScope();
                return await request.ServiceProvider.GetRequiredService<IWritingService>().RecordExerciseVisitAsync(TestDataIds.OutsiderUserId, 1);
            }
        }
        var times = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(45));
        Assert.All(times, t => Assert.NotNull(t));
        var visit = await context.UserWritingExerciseVisits.AsNoTracking().SingleAsync(v => v.UserId == TestDataIds.OutsiderUserId && v.WritingExerciseId == 1);
        Assert.Equal(clock.Now, DateTime.SpecifyKind(visit.LastOpenedAtUtc, DateTimeKind.Utc));
        clock.Now = clock.Now.AddDays(-1);
        Assert.Equal(DateTime.SpecifyKind(visit.LastOpenedAtUtc, DateTimeKind.Utc),
            await scope.ServiceProvider.GetRequiredService<IWritingService>().RecordExerciseVisitAsync(TestDataIds.OutsiderUserId, 1));
        Assert.Equal(rewards, await WritingRecentExercisesTests.RewardSnapshot(context));
        // Real exercise deletion cascades history without orphaning a user.
        var disposable = await context.WritingExercises.FirstAsync(e => e.SourceType == "fixture" && e.UserId == null);
        await scope.ServiceProvider.GetRequiredService<IWritingService>().RecordExerciseVisitAsync(TestDataIds.OutsiderUserId, disposable.Id);
        context.WritingExercises.Remove(disposable); await context.SaveChangesAsync();
        Assert.False(await context.UserWritingExerciseVisits.AnyAsync(v => v.WritingExerciseId == disposable.Id));
    }
}

public sealed class WritingRecentUpgradeFactAttribute : FactAttribute
{
    public WritingRecentUpgradeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TCT_WRITING_RECENT_UPGRADE") != "1"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")))
            Skip = "Requires opted-in fresh disposable MariaDB acceptance database.";
    }
}
