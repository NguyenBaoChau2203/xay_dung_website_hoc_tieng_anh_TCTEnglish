using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Models;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingCompletionTransactionTests
{
    [Theory]
    [InlineData("credit")]
    [InlineData("outbox")]
    [InlineData("cancel")]
    public async Task FinalSubmit_FailureRollsBackEntireUnit_AndRetryCreditsOnce(string fault)
    {
        await using var factory = new TestWebApplicationFactory();
        using var cancellation = new CancellationTokenSource();
        var interceptor = new CompletionFailure(fault, cancellation);
        factory.SaveInterceptors.Add(interceptor);
        await factory.InitializeAsync();
        await VerifyFailureAndRetryAsync(factory, interceptor, cancellation);
    }

    internal static async Task VerifyFailureAndRetryAsync(TestWebApplicationFactory factory, CompletionFailure interceptor,
        CancellationTokenSource cancellation, Func<Task>? beforeRetry = null)
    {
        await SeedOneSentenceAsync(factory);
        interceptor.Enabled = true;
        using (var scope = factory.Services.CreateScope())
        {
            var writing = scope.ServiceProvider.GetRequiredService<IWritingService>();
            await Assert.ThrowsAnyAsync<Exception>(() => writing.EvaluateWritingSentenceAsync(1, 1, "Hello!",
                TestDataIds.UserId, cancellation.Token));
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            Assert.Empty(await db.UserWritingAttempts.ToListAsync());
            Assert.Empty(await db.UserWritingSentenceProgresses.ToListAsync());
            Assert.Empty(await db.UserWritingExerciseProgresses.ToListAsync());
            Assert.Empty(await db.LearningCompletionCredits.ToListAsync());
            Assert.Empty(await db.UserDailyActivities.ToListAsync());
            Assert.Empty(await db.LearningNotificationOutboxEvents.ToListAsync());
            Assert.Equal(0, (await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId)).Streak);
        }
        if (beforeRetry != null) await beforeRetry();
        interceptor.Enabled = false;
        using (var scope = factory.Services.CreateScope())
        {
            var writing = scope.ServiceProvider.GetRequiredService<IWritingService>();
            Assert.True((await writing.EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId))!.Passed);
            Assert.True((await writing.EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId))!.Passed);
        }
        await AssertOnceAsync(factory, 2);
    }

    internal static async Task SeedOneSentenceAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        await db.WritingExerciseSentences.Where(s => s.WritingExerciseId == 1 && s.Id != 1).ExecuteDeleteAsync();
        // Ensure an award intent is observable in both SQLite and MariaDB factories.
        if (!await db.UserGoals.AnyAsync(g => g.UserId == TestDataIds.UserId && g.GoalArea == GoalArea.Writing))
        {
            db.UserGoals.Add(new UserGoal { UserId = TestDataIds.UserId, GoalArea = GoalArea.Writing,
                TargetValue = 1, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
    }

    internal static async Task AssertOnceAsync(TestWebApplicationFactory factory, int attemptCount, int sentenceCount = 1)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(attemptCount, await db.UserWritingAttempts.CountAsync());
        var progress = await db.UserWritingExerciseProgresses.SingleAsync();
        Assert.True(progress.IsCompleted);
        Assert.Equal(attemptCount, progress.AttemptCount);
        var sentenceProgress = await db.UserWritingSentenceProgresses.ToListAsync();
        Assert.Equal(sentenceCount, sentenceProgress.Count);
        Assert.All(sentenceProgress, p => Assert.True(p.IsPassed));
        Assert.Equal(sentenceCount, progress.PassedSentenceCount);
        var credit = await db.LearningCompletionCredits.SingleAsync();
        Assert.Equal("Writing", credit.Feature);
        Assert.Equal(20, credit.XpAwarded);
        var daily = await db.UserDailyActivities.SingleAsync();
        Assert.Equal(1, daily.WritingCompletedCount);
        Assert.Equal(20 + daily.StreakXpAwarded, daily.XpEarned);
        var intents = await db.LearningNotificationOutboxEvents.ToListAsync();
        Assert.NotEmpty(intents);
        Assert.Equal(intents.Count, intents.Select(i => i.NotificationEventKey).Distinct().Count());
    }

    internal sealed class CompletionFailure(string fault, CancellationTokenSource cancellation) : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Enabled) return ValueTask.FromResult(result);
            var entries = eventData.Context!.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList();
            if ((fault == "credit" && entries.Any(e => e.Entity is LearningCompletionCredit))
                || (fault == "outbox" && entries.Any(e => e.Entity is LearningNotificationOutbox)))
                throw new DbUpdateException("Injected Writing " + fault + " insert failure");
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (Enabled && fault == "cancel" && eventData.Context!.ChangeTracker.Entries<UserWritingExerciseProgress>().Any())
                cancellation.Cancel();
            return ValueTask.FromResult(result);
        }
    }
}

[Collection("MariaDbAcceptance")]
public sealed class WritingCompletionMariaDbTests
{
    private static async Task<MariaDbAcceptanceFactory> CreateAsync(Action<MariaDbAcceptanceFactory>? configure = null)
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
        var factory = new MariaDbAcceptanceFactory(connection, run);
        configure?.Invoke(factory);
        await factory.InitializeAsync();
        return factory;
    }

    [MariaDbAcceptanceTheory]
    [InlineData("credit")]
    [InlineData("outbox")]
    [InlineData("cancel")]
    public async Task FinalSubmit_FailureRollsBackAndCanRetry(string fault)
    {
        using var cancellation = new CancellationTokenSource();
        var interceptor = new WritingCompletionTransactionTests.CompletionFailure(fault, cancellation);
        await using var factory = await CreateAsync(f => f.SaveInterceptors.Add(interceptor));
        await WritingCompletionTransactionTests.VerifyFailureAndRetryAsync(factory, interceptor, cancellation);
    }

    [MariaDbAcceptanceFact]
    public async Task ConcurrentFinalSubmits_BothPersist_OneCreditAndAwardIntent()
    {
        for (var iteration = 0; iteration < 5; iteration++)
        {
            var barrier = new SubmissionBarrier();
            await using var factory = await CreateAsync(f => f.SaveInterceptors.Add(barrier));
            await WritingCompletionTransactionTests.SeedOneSentenceAsync(factory);
            barrier.Enabled = true;
            async Task SubmitAsync()
            {
                using var scope = factory.Services.CreateScope();
                var writing = scope.ServiceProvider.GetRequiredService<IWritingService>();
                var result = await writing.EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId);
                Assert.True(result!.Passed);
                Assert.Equal("completed", result.ExerciseStatusKey);
            }
            await Task.WhenAll(SubmitAsync(), SubmitAsync()).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(2, barrier.Arrivals);
            await WritingCompletionTransactionTests.AssertOnceAsync(factory, 2);
        }
    }

    [MariaDbAcceptanceFact]
    public async Task DifferentFinalSentences_ConcurrentSubmits_DoNotLoseCompletion()
    {
        var barrier = new SubmissionBarrier();
        await using var factory = await CreateAsync(f => f.SaveInterceptors.Add(barrier));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            await db.WritingExerciseSentences.Where(s => s.WritingExerciseId == 1 && s.Id > 2).ExecuteDeleteAsync();
        }
        barrier.Enabled = true;
        async Task SubmitAsync(int id, string answer)
        {
            using var scope = factory.Services.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IWritingService>()
                .EvaluateWritingSentenceAsync(1, id, answer, TestDataIds.UserId);
            Assert.True(result!.Passed);
        }
        await Task.WhenAll(SubmitAsync(1, "Hello!"), SubmitAsync(2, "I hope you are well."))
            .WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(2, barrier.Arrivals);
        await WritingCompletionTransactionTests.AssertOnceAsync(factory, 2, 2);
    }

    [MariaDbAcceptanceFact]
    public async Task RealCreditInsertRejection_RollsBackAllWrites_AndRetryCreditsOnce()
    {
        await using var factory = await CreateAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER stage02_credit_failure BEFORE INSERT ON LearningCompletionCredits
            FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'stage02 credit insert failure'
            """);
        try
        {
            using var cancellation = new CancellationTokenSource();
            await WritingCompletionTransactionTests.VerifyFailureAndRetryAsync(factory,
                new WritingCompletionTransactionTests.CompletionFailure("credit", cancellation), cancellation,
                () => db.Database.ExecuteSqlRawAsync("DROP TRIGGER stage02_credit_failure"));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS stage02_credit_failure");
        }
    }

    [MariaDbAcceptanceFact]
    public async Task MigrationRepeat_PreservesSavedAttemptProgressCredit_AndModelHasNoPendingChanges()
    {
        await using var factory = await CreateAsync();
        await WritingCompletionTransactionTests.SeedOneSentenceAsync(factory);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IWritingService>()
            .EvaluateWritingSentenceAsync(1, 1, "Hello!", TestDataIds.UserId);
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.False(db.Database.HasPendingModelChanges());
        var history = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        await db.Database.MigrateAsync();
        Assert.Equal(history, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        await WritingCompletionTransactionTests.AssertOnceAsync(factory, 1);
    }

    // Rendezvous before either request's first write/serialization lock. Works on
    // both the original INSERT-first path and the repaired FOR UPDATE-first path.
    private sealed class SubmissionBarrier : DbCommandInterceptor
    {
        private readonly HashSet<Guid> _contexts = [];
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Enabled { get; set; }
        public int Arrivals => _contexts.Count;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && (command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal)
                || (command.CommandText.Contains("INSERT INTO", StringComparison.Ordinal)
                    && command.CommandText.Contains("UserWritingAttempts", StringComparison.Ordinal))))
            {
                lock (_contexts)
                {
                    if (_contexts.Add(eventData.Context!.ContextId.InstanceId) && _contexts.Count == 2) _both.TrySetResult();
                }
                await _both.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }
}
