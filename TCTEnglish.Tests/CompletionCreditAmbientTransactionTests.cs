using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services.Learning;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// SA01: when a caller already owns the EF transaction, credit must not retry/query after
/// InnoDB deadlock rolled the ambient unit back. Faults are injected deterministically.
/// </summary>
public sealed class CompletionCreditAmbientTransactionTests
{
    [Fact]
    public async Task AmbientTransaction_Deadlock1213_PropagatesWithoutRetryQuery()
    {
        var goalsCalls = 0;
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IGoalsService>();
            services.AddScoped<IGoalsService>(_ => new CountingFaultGoalsService(
                () =>
                {
                    Interlocked.Increment(ref goalsCalls);
                    throw MysqlFaultFactory.Deadlock();
                }));
        });
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var credits = scope.ServiceProvider.GetRequiredService<ICompletionCreditService>();
        var update = new GoalsActivityUpdate { XpEarned = 20, SpeakingCompletedCount = 1 };

        await using var ambient = await context.Database.BeginTransactionAsync();
        var ambientId = ambient.TransactionId;

        var thrown = await Assert.ThrowsAsync<MySqlConnector.MySqlException>(() =>
            credits.TryApplySpeakingVideoCompletionAsync(
                TestDataIds.UserId,
                TestDataIds.SpeakingVideoId,
                update,
                CancellationToken.None));

        Assert.Equal(1213, thrown.Number);
        Assert.Equal(1, Volatile.Read(ref goalsCalls));
        Assert.Same(ambient, context.Database.CurrentTransaction);
        Assert.Equal(ambientId, context.Database.CurrentTransaction!.TransactionId);
        Assert.Equal(0, await context.LearningCompletionCredits.CountAsync());
    }

    [Fact]
    public async Task AmbientTransaction_LockWait1205_PropagatesWithoutRetry()
    {
        var goalsCalls = 0;
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IGoalsService>();
            services.AddScoped<IGoalsService>(_ => new CountingFaultGoalsService(
                () =>
                {
                    Interlocked.Increment(ref goalsCalls);
                    throw MysqlFaultFactory.LockWaitTimeout();
                }));
        });
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var credits = scope.ServiceProvider.GetRequiredService<ICompletionCreditService>();

        await using var ambient = await context.Database.BeginTransactionAsync();
        var thrown = await Assert.ThrowsAsync<MySqlConnector.MySqlException>(() =>
            credits.TryApplyWritingExerciseCompletionAsync(
                TestDataIds.UserId,
                exerciseId: 42,
                new GoalsActivityUpdate { XpEarned = 20, WritingCompletedCount = 1 },
                CancellationToken.None));

        Assert.Equal(1205, thrown.Number);
        Assert.Equal(1, Volatile.Read(ref goalsCalls));
        Assert.NotNull(context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task OwnedTransaction_Deadlock1213_RetriesWithFreshAttemptsThenPropagates()
    {
        var goalsCalls = 0;
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IGoalsService>();
            services.AddScoped<IGoalsService>(_ => new CountingFaultGoalsService(
                () =>
                {
                    Interlocked.Increment(ref goalsCalls);
                    throw MysqlFaultFactory.Deadlock();
                }));
        });
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var credits = scope.ServiceProvider.GetRequiredService<ICompletionCreditService>();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Null(context.Database.CurrentTransaction);

        await Assert.ThrowsAsync<MySqlConnector.MySqlException>(() =>
            credits.TryApplySpeakingVideoCompletionAsync(
                TestDataIds.UserId,
                TestDataIds.SpeakingVideoId,
                new GoalsActivityUpdate { XpEarned = 20, SpeakingCompletedCount = 1 },
                CancellationToken.None));

        // Owns the unit: bounded retry (3 attempts in ApplyAsync + final ApplyOnce = 4) or 3.
        Assert.InRange(Volatile.Read(ref goalsCalls), 3, 4);
        Assert.Equal(0, await context.LearningCompletionCredits.CountAsync());
    }

    [Fact]
    public async Task AmbientTransaction_Cancellation_DoesNotBecomeReplay()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IGoalsService>();
            services.AddScoped<IGoalsService>(_ => new CountingFaultGoalsService(
                () => throw new OperationCanceledException("cancelled mid goals")));
        });
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var credits = scope.ServiceProvider.GetRequiredService<ICompletionCreditService>();

        await using var ambient = await context.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            credits.TryApplySpeakingVideoCompletionAsync(
                TestDataIds.UserId,
                TestDataIds.SpeakingVideoId,
                new GoalsActivityUpdate { XpEarned = 20, SpeakingCompletedCount = 1 },
                CancellationToken.None));

        Assert.Equal(0, await context.LearningCompletionCredits.CountAsync());
    }

    private sealed class CountingFaultGoalsService : IGoalsService
    {
        private readonly Action _onRecord;

        public CountingFaultGoalsService(Action onRecord) => _onRecord = onRecord;

        public Task<GoalsViewModel?> GetGoalsAsync(int userId) => Task.FromResult<GoalsViewModel?>(null);

        public Task<OperationResult> UpdateGoalAsync(int userId, GoalArea goalArea, int targetValue)
            => Task.FromResult(OperationResult.Success());

        public Task<OperationResult> RecordActivityAsync(int userId, GoalsActivityUpdate update)
            => Task.FromResult(OperationResult.Success());

        public Task<GoalsActivityRecordResult> RecordLearningActivityAsync(int userId, GoalsActivityUpdate update)
        {
            _onRecord();
            throw new InvalidOperationException("unreachable");
        }

        public Task<StreakUpdateResult> UpdateStreakAndRewardsAsync(int userId)
            => Task.FromResult(new StreakUpdateResult { CurrentStreak = 0 });

        public GoalsActivityUpdate BuildVocabularyActivityUpdate(bool isNewProgress, string? previousStatus, string currentStatus)
            => new();

        public GoalsActivityUpdate BuildSpeakingCompletionActivityUpdate()
            => new() { XpEarned = 20, SpeakingCompletedCount = 1 };

        public GoalsActivityUpdate BuildWritingCompletionActivityUpdate()
            => new() { XpEarned = 20, WritingCompletedCount = 1 };

        public GoalsActivityUpdate BuildReadingCompletionActivityUpdate() => new();

        public GoalsActivityUpdate BuildListeningCompletionActivityUpdate() => new();
    }
}
