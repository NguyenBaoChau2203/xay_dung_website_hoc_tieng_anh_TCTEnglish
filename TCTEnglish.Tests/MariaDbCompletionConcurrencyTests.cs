using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Services.Learning;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// G03 MariaDB concurrency. Run only with TCT_ACCEPTANCE_MARIADB set against an
/// isolated acceptance container (not tctenglish-local-mariadb-data).
/// </summary>
[Trait("Category", "MariaDbAcceptance")]
public sealed class MariaDbCompletionConcurrencyTests
{
    [MariaDbAcceptanceFact]
    public async Task DualSpeakingCompletion_SameUserContent_AppliesOneCredit()
    {
        var connectionString = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var factory = new MariaDbAcceptanceFactory(connectionString);
            await factory.InitializeAsync();

            var barrier = new Barrier(2);
            var results = new System.Collections.Concurrent.ConcurrentDictionary<string, CompletionCreditApplyResult>();

            async Task WorkerAsync(string name)
            {
                using var scope = factory.Services.CreateScope();
                var credits = scope.ServiceProvider.GetRequiredService<ICompletionCreditService>();
                var goals = scope.ServiceProvider.GetRequiredService<IGoalsService>();
                var update = goals.BuildSpeakingCompletionActivityUpdate();
                barrier.SignalAndWait(TimeSpan.FromSeconds(10));
                var result = await credits.TryApplySpeakingVideoCompletionAsync(
                    TestDataIds.UserId,
                    TestDataIds.SpeakingVideoId,
                    update,
                    CancellationToken.None);
                results[name] = result;
            }

            await Task.WhenAll(WorkerAsync("a"), WorkerAsync("b"));

            Assert.Equal(1, results.Values.Count(item => item.Applied));
            Assert.Equal(1, results.Values.Count(item => !item.Applied));

            using var verifyScope = factory.Services.CreateScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            Assert.Equal(1, await db.LearningCompletionCredits.CountAsync(item =>
                item.UserId == TestDataIds.UserId
                && item.Feature == "Speaking"
                && item.ContentKey == $"video:{TestDataIds.SpeakingVideoId}"));
        }
    }
}
