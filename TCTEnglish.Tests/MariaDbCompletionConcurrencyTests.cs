using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Services.Learning;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// G03 MariaDB concurrency. Requires TCT_ACCEPTANCE_MARIADB against an isolated
/// acceptance DB (guarded). Does not reuse tctenglish-local-mariadb-data.
/// </summary>
[Trait("Category", "MariaDbAcceptance")]
public sealed class MariaDbCompletionConcurrencyTests
{
    [MariaDbAcceptanceFact]
    public async Task DualSpeakingCompletion_SameUserContent_AppliesOneCredit()
    {
        var connectionString = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var runId = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var factory = new MariaDbAcceptanceFactory(connectionString, runId);
            await factory.InitializeAsync();

            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var arrived = 0;
            var overlapObserved = 0;
            var results = new System.Collections.Concurrent.ConcurrentDictionary<string, CompletionCreditApplyResult>();
            var stages = new System.Collections.Concurrent.ConcurrentBag<(string Worker, string Stage, long ElapsedMs)>();
            var sw = Stopwatch.StartNew();

            async Task WorkerAsync(string name)
            {
                using var scope = factory.Services.CreateScope();
                var credits = scope.ServiceProvider.GetRequiredService<ICompletionCreditService>();
                var goals = scope.ServiceProvider.GetRequiredService<IGoalsService>();
                var update = goals.BuildSpeakingCompletionActivityUpdate();
                stages.Add((name, "prepared", sw.ElapsedMilliseconds));

                if (Interlocked.Increment(ref arrived) == 2)
                {
                    ready.TrySetResult();
                }

                using var readyCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await ready.Task.WaitAsync(readyCts.Token);
                stages.Add((name, "both-ready", sw.ElapsedMilliseconds));

                using var releaseCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await release.Task.WaitAsync(releaseCts.Token);
                Interlocked.Increment(ref overlapObserved);
                stages.Add((name, "enter-critical", sw.ElapsedMilliseconds));

                var result = await credits.TryApplySpeakingVideoCompletionAsync(
                    TestDataIds.UserId,
                    TestDataIds.SpeakingVideoId,
                    update,
                    CancellationToken.None);
                results[name] = result;
                stages.Add((name, "completed", sw.ElapsedMilliseconds));
            }

            var workers = Task.WhenAll(WorkerAsync("a"), WorkerAsync("b"));
            using var rendezvousCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ready.Task.WaitAsync(rendezvousCts.Token);
            Assert.Equal(2, Volatile.Read(ref arrived));
            release.TrySetResult();
            await workers;

            Assert.True(Volatile.Read(ref overlapObserved) >= 2, "Both workers must enter the critical section after the shared release.");
            Assert.Contains(stages, item => item.Worker == "a" && item.Stage == "both-ready");
            Assert.Contains(stages, item => item.Worker == "b" && item.Stage == "both-ready");

            var applied = results.Values.Where(item => item.Applied).ToList();
            var losers = results.Values.Where(item => !item.Applied).ToList();
            Assert.Single(applied);
            Assert.Single(losers);
            Assert.True(losers[0].Replay, $"Loser must be a valid credit replay, status={losers[0].Status}.");
            Assert.True(applied[0].XpEarned > 0);
            Assert.Equal(0, losers[0].XpEarned);

            using var verifyScope = factory.Services.CreateScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            Assert.Equal(1, await db.LearningCompletionCredits.CountAsync(item =>
                item.UserId == TestDataIds.UserId
                && item.Feature == "Speaking"
                && item.ContentKey == $"video:{TestDataIds.SpeakingVideoId}"));
            Assert.Equal(
                applied[0].XpEarned,
                await db.LearningCompletionCredits
                    .Where(item => item.UserId == TestDataIds.UserId && item.Feature == "Speaking")
                    .SumAsync(item => item.XpAwarded));

            // User/daily activity must land once (speaking XP + optional single streak XP).
            var daily = await db.UserDailyActivities.AsNoTracking()
                .SingleAsync(item => item.UserId == TestDataIds.UserId);
            Assert.Equal(applied[0].XpEarned + daily.StreakXpAwarded, daily.XpEarned);
            Assert.True(daily.StreakXpAwarded is 0 or 10);
            Assert.Equal(1, await db.UserDailyActivities.CountAsync(item => item.UserId == TestDataIds.UserId));

            var outbox = await db.LearningNotificationOutboxEvents.AsNoTracking()
                .Where(item => item.UserId == TestDataIds.UserId)
                .ToListAsync();
            Assert.NotEmpty(outbox);
            Assert.Equal(
                outbox.Count,
                outbox.Select(item => item.NotificationEventKey).Distinct().Count());
        }
    }
}
