using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Services.Learning;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public class GoalsCompletionRecoveryTests
{
    [Fact]
    public async Task SpeakingCompletionCredit_AppliesOnce_AndEnqueuesOutbox()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var credits = scope.ServiceProvider.GetRequiredService<ICompletionCreditService>();
        var goals = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var update = goals.BuildSpeakingCompletionActivityUpdate();
        var first = await credits.TryApplySpeakingVideoCompletionAsync(
            TestDataIds.UserId,
            TestDataIds.SpeakingVideoId,
            update,
            CancellationToken.None);
        var second = await credits.TryApplySpeakingVideoCompletionAsync(
            TestDataIds.UserId,
            TestDataIds.SpeakingVideoId,
            update,
            CancellationToken.None);

        Assert.True(first.Applied);
        Assert.False(second.Applied);
        Assert.Equal(1, await context.LearningCompletionCredits.CountAsync(c =>
            c.UserId == TestDataIds.UserId && c.Feature == "Speaking"));
        var outbox = await context.LearningNotificationOutboxEvents
            .Where(o => o.UserId == TestDataIds.UserId && o.ProcessedAtUtc == null)
            .ToListAsync();
        Assert.NotEmpty(outbox);
        Assert.All(outbox, row => Assert.Equal(2, row.PayloadVersion));
        Assert.DoesNotContain(outbox, row => row.EventType == "learning.completion");
        Assert.DoesNotContain(outbox, row => row.EventType == "GoalCompleted");
    }

    [Fact]
    public async Task CompletionCredit_RejectsNegativeIncrements()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var credits = scope.ServiceProvider.GetRequiredService<ICompletionCreditService>();

        var result = await credits.TryApplyWritingExerciseCompletionAsync(
            TestDataIds.UserId,
            exerciseId: 1,
            new GoalsActivityUpdate { XpEarned = -5, WritingCompletedCount = 1 },
            CancellationToken.None);

        Assert.False(result.Applied);
        Assert.Equal(OperationStatus.Invalid, result.Status);
    }
}
