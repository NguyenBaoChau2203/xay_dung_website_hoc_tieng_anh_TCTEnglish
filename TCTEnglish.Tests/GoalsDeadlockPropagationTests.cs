using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// SA01 companion: Goals must not swallow 1213/1205 and continue Increment on a dead transaction.
/// Unique conflicts remain recoverable; deadlock/lock-wait must propagate.
/// </summary>
public sealed class GoalsDeadlockPropagationTests
{
    [Fact]
    public async Task RecordLearningActivity_DeadlockOnInsert_PropagatesWithoutIncrementRecovery()
    {
        await using var factory = new TestWebApplicationFactory();
        factory.SaveInterceptors.Add(new ThrowMysqlOnUserDailyInsertInterceptor(MysqlFaultFactory.Deadlock()));
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var goals = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        // Ensure no existing daily row so Insert path is taken.
        await context.UserDailyActivities.ExecuteDeleteAsync();

        var thrown = await Assert.ThrowsAsync<MySqlConnector.MySqlException>(() =>
            goals.RecordLearningActivityAsync(
                TestDataIds.UserId,
                new GoalsActivityUpdate { XpEarned = 20, SpeakingCompletedCount = 1 }));

        Assert.Equal(1213, thrown.Number);
        Assert.Equal(0, await context.UserDailyActivities.CountAsync(a => a.UserId == TestDataIds.UserId));
    }

    [Fact]
    public async Task RecordLearningActivity_LockWaitOnInsert_Propagates()
    {
        await using var factory = new TestWebApplicationFactory();
        factory.SaveInterceptors.Add(new ThrowMysqlOnUserDailyInsertInterceptor(MysqlFaultFactory.LockWaitTimeout()));
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var goals = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        await context.UserDailyActivities.ExecuteDeleteAsync();

        var thrown = await Assert.ThrowsAsync<MySqlConnector.MySqlException>(() =>
            goals.RecordLearningActivityAsync(
                TestDataIds.UserId,
                new GoalsActivityUpdate { XpEarned = 20, WritingCompletedCount = 1 }));

        Assert.Equal(1205, thrown.Number);
    }

    private sealed class ThrowMysqlOnUserDailyInsertInterceptor : SaveChangesInterceptor
    {
        private readonly Exception _fault;

        public ThrowMysqlOnUserDailyInsertInterceptor(Exception fault) => _fault = fault;

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            ThrowIfDailyInsert(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDailyInsert(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void ThrowIfDailyInsert(DbContext? context)
        {
            if (context is null)
            {
                return;
            }

            if (context.ChangeTracker.Entries<UserDailyActivity>()
                .Any(entry => entry.State == EntityState.Added))
            {
                throw _fault;
            }
        }
    }
}
