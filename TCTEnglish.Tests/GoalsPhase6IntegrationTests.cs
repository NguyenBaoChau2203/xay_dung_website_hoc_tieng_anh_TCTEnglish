using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class GoalsPhase6IntegrationTests
{
    [Fact]
    public async Task RecordLearningActivityAsync_NewBusinessDay_AwardsStreakXpExactlyOnce()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedUserStreakStateAsync(factory, BusinessDateHelper.Today.AddDays(-1), streak: 2, longestStreak: 2);

        using (var scope = factory.Services.CreateScope())
        {
            var goalsService = scope.ServiceProvider.GetRequiredService<IGoalsService>();

            var firstResult = await goalsService.RecordLearningActivityAsync(
                TestDataIds.UserId,
                new GoalsActivityUpdate
                {
                    CardsReviewed = 1,
                    XpEarned = 5
                });

            Assert.Equal(OperationStatus.Success, firstResult.Status);
            Assert.Equal(3, firstResult.Streak);

            var secondResult = await goalsService.RecordLearningActivityAsync(
                TestDataIds.UserId,
                new GoalsActivityUpdate
                {
                    CardsReviewed = 1,
                    XpEarned = 5
                });

            Assert.Equal(OperationStatus.Success, secondResult.Status);
            Assert.Equal(3, secondResult.Streak);
        }

        using var verificationScope = factory.Services.CreateScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var today = BusinessDateHelper.Today;

        var activity = await context.UserDailyActivities
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == TestDataIds.UserId && candidate.ActivityDate == today);

        Assert.Equal(10, activity.StreakXpAwarded);
        Assert.Equal(20, activity.XpEarned);
    }

    [Fact]
    public async Task UpdateStreakAndRewardsAsync_StreakOnlyPath_CreatesDailyActivityAndDoesNotDuplicateXp()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedUserStreakStateAsync(factory, BusinessDateHelper.Today.AddDays(-1), streak: 1, longestStreak: 1);

        using (var scope = factory.Services.CreateScope())
        {
            var goalsService = scope.ServiceProvider.GetRequiredService<IGoalsService>();

            var firstResult = await goalsService.UpdateStreakAndRewardsAsync(TestDataIds.UserId);
            var secondResult = await goalsService.UpdateStreakAndRewardsAsync(TestDataIds.UserId);

            Assert.True(firstResult.DidIncrease);
            Assert.Equal(10, firstResult.StreakXpAwarded);
            Assert.False(secondResult.DidIncrease);
            Assert.Equal(0, secondResult.StreakXpAwarded);
        }

        using var verificationScope = factory.Services.CreateScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var activity = await context.UserDailyActivities
            .AsNoTracking()
            .SingleAsync(candidate =>
                candidate.UserId == TestDataIds.UserId
                && candidate.ActivityDate == BusinessDateHelper.Today);

        Assert.Equal(10, activity.XpEarned);
        Assert.Equal(10, activity.StreakXpAwarded);
    }

    [Fact]
    public async Task RecordActivityAsync_FeatureBadgeThresholdReached_UnlocksWithoutDuplicates()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        await RecordActivityAsync(
            factory,
            new GoalsActivityUpdate
            {
                VocabularyCompletedCount = 1
            });

        await RecordActivityAsync(
            factory,
            new GoalsActivityUpdate
            {
                WritingCompletedCount = 5
            });

        await RecordActivityAsync(
            factory,
            new GoalsActivityUpdate
            {
                WritingCompletedCount = 1
            });

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var unlockedFeatureBadgeCodes = await context.UserBadges
            .AsNoTracking()
            .Where(userBadge => userBadge.UserId == TestDataIds.UserId)
            .Join(
                context.Badges.AsNoTracking(),
                userBadge => userBadge.BadgeId,
                badge => badge.Id,
                (_, badge) => badge.Code)
            .Where(code => code == "vocabulary-first-mastered" || code == "writing-five-exercises")
            .ToListAsync();

        Assert.Contains("vocabulary-first-mastered", unlockedFeatureBadgeCodes);
        Assert.Contains("writing-five-exercises", unlockedFeatureBadgeCodes);
        Assert.Equal(2, unlockedFeatureBadgeCodes.Count);
    }

    private static async Task RecordActivityAsync(TestWebApplicationFactory factory, GoalsActivityUpdate update)
    {
        using var scope = factory.Services.CreateScope();
        var goalsService = scope.ServiceProvider.GetRequiredService<IGoalsService>();

        var result = await goalsService.RecordActivityAsync(TestDataIds.UserId, update);

        Assert.Equal(OperationStatus.Success, result.Status);
    }

    private static async Task SeedUserStreakStateAsync(
        TestWebApplicationFactory factory,
        DateTime? lastStudyDate,
        int streak,
        int longestStreak)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(candidate => candidate.UserId == TestDataIds.UserId);

        user.LastStudyDate = lastStudyDate;
        user.Streak = streak;
        user.LongestStreak = longestStreak;

        await context.SaveChangesAsync();
    }
}
