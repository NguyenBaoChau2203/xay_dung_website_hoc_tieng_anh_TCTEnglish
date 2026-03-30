using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTEnglish.ViewModels;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class GoalsPhase4IntegrationTests
{
    [Fact]
    public async Task GetGoalsAsync_IsReadOnly_AndRecordActivityAsyncAwardsBadges()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var today = BusinessDateHelper.Today;
        await SeedGoalsStateAsync(
            factory,
            streak: 3,
            longestStreak: 3,
            lastStudyDate: today,
            new UserDailyActivity
            {
                UserId = TestDataIds.UserId,
                ActivityDate = today,
                CardsReviewed = 4,
                XpEarned = 15
            });

        var modelBeforeAward = await LoadGoalsAsync(factory);

        Assert.NotNull(modelBeforeAward);
        Assert.Contains(modelBeforeAward!.Badges, badge => badge.Code == "first-session" && !badge.IsUnlocked);
        Assert.Contains(modelBeforeAward.Badges, badge => badge.Code == "three-day-streak" && !badge.IsUnlocked);

        using (var verificationScope = factory.Services.CreateScope())
        {
            var verificationContext = verificationScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var existingAwardsCount = await verificationContext.UserBadges
                .AsNoTracking()
                .CountAsync(userBadge => userBadge.UserId == TestDataIds.UserId);

            Assert.Equal(0, existingAwardsCount);
        }

        await RecordActivityAsync(
            factory,
            new GoalsActivityUpdate
            {
                CardsReviewed = 1,
                XpEarned = 0
            });

        var modelAfterAward = await LoadGoalsAsync(factory);

        Assert.NotNull(modelAfterAward);
        Assert.Contains(modelAfterAward!.Badges, badge => badge.Code == "first-session" && badge.IsUnlocked);
        Assert.Contains(modelAfterAward.Badges, badge => badge.Code == "three-day-streak" && badge.IsUnlocked);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var awardedCodes = await context.UserBadges
            .AsNoTracking()
            .Where(userBadge => userBadge.UserId == TestDataIds.UserId)
            .Join(
                context.Badges.AsNoTracking(),
                userBadge => userBadge.BadgeId,
                badge => badge.Id,
                (_, badge) => badge.Code)
            .ToListAsync();

        Assert.Contains("first-session", awardedCodes);
        Assert.Contains("three-day-streak", awardedCodes);
    }

    [Fact]
    public async Task GetGoalsAsync_ReturnsLockedBadgeProgressFromRealTotals()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var today = BusinessDateHelper.Today;
        await SeedGoalsStateAsync(
            factory,
            streak: 1,
            longestStreak: 2,
            lastStudyDate: today,
            new UserDailyActivity
            {
                UserId = TestDataIds.UserId,
                ActivityDate = today.AddDays(-1),
                CardsReviewed = 2,
                XpEarned = 15
            },
            new UserDailyActivity
            {
                UserId = TestDataIds.UserId,
                ActivityDate = today,
                CardsReviewed = 3,
                XpEarned = 25
            });

        var model = await LoadGoalsAsync(factory);
        var xpCollector = Assert.Single(model!.Badges.Where(badge => badge.Code == "xp-collector"));

        Assert.False(xpCollector.IsUnlocked);
        Assert.Equal(40, xpCollector.ProgressValue);
        Assert.Equal(50, xpCollector.TargetValue);
        Assert.Equal(80, xpCollector.ProgressPercent);
        Assert.Equal("40/50 XP", xpCollector.ProgressLabel);
    }

    [Fact]
    public async Task RecordActivityAsync_DoesNotDuplicateExistingUserBadges()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var today = BusinessDateHelper.Today;
        await SeedGoalsStateAsync(
            factory,
            streak: 1,
            longestStreak: 1,
            lastStudyDate: today,
            new UserDailyActivity
            {
                UserId = TestDataIds.UserId,
                ActivityDate = today,
                CardsReviewed = 1,
                XpEarned = 10
            });
        await SeedUserBadgeAsync(factory, "first-session");
        await RecordActivityAsync(
            factory,
            new GoalsActivityUpdate
            {
                CardsReviewed = 1,
                XpEarned = 2
            });
        await RecordActivityAsync(
            factory,
            new GoalsActivityUpdate
            {
                CardsReviewed = 1,
                XpEarned = 2
            });

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var firstSessionBadgeId = await context.Badges
            .AsNoTracking()
            .Where(badge => badge.Code == "first-session")
            .Select(badge => badge.Id)
            .SingleAsync();

        var duplicateCount = await context.UserBadges
            .AsNoTracking()
            .CountAsync(userBadge =>
                userBadge.UserId == TestDataIds.UserId
                && userBadge.BadgeId == firstSessionBadgeId);

        Assert.Equal(1, duplicateCount);
    }

    [Fact]
    public async Task GetGoalsAsync_TreatsSqlServerDatetime2BadgeAwardAsUtcForRecentUnlock()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var today = BusinessDateHelper.Today;
        await SeedGoalsStateAsync(
            factory,
            streak: 1,
            longestStreak: 1,
            lastStudyDate: today,
            new UserDailyActivity
            {
                UserId = TestDataIds.UserId,
                ActivityDate = today,
                CardsReviewed = 1,
                XpEarned = 5
            });

        var sqlServerDatetime2UtcValue = DateTime.SpecifyKind(
            today.AddDays(-1).AddHours(17).AddMinutes(30),
            DateTimeKind.Unspecified);

        await SeedUserBadgeAsync(factory, "first-session", sqlServerDatetime2UtcValue);

        var model = await LoadGoalsAsync(factory);
        var badge = Assert.Single(model!.Badges.Where(candidate => candidate.Code == "first-session"));

        Assert.True(badge.IsUnlocked);
        Assert.True(badge.IsRecentlyUnlocked);
    }

    private static async Task RecordActivityAsync(TestWebApplicationFactory factory, GoalsActivityUpdate update)
    {
        using var scope = factory.Services.CreateScope();
        var goalsService = scope.ServiceProvider.GetRequiredService<IGoalsService>();

        var result = await goalsService.RecordActivityAsync(TestDataIds.UserId, update);

        Assert.Equal(OperationStatus.Success, result.Status);
    }

    private static async Task<GoalsViewModel?> LoadGoalsAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var goalsService = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        return await goalsService.GetGoalsAsync(TestDataIds.UserId);
    }

    private static async Task SeedGoalsStateAsync(
        TestWebApplicationFactory factory,
        int streak,
        int longestStreak,
        DateTime? lastStudyDate,
        params UserDailyActivity[] activities)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(candidate => candidate.UserId == TestDataIds.UserId);

        user.Streak = streak;
        user.LongestStreak = longestStreak;
        user.LastStudyDate = lastStudyDate;

        context.UserDailyActivities.AddRange(activities);
        await context.SaveChangesAsync();
    }

    private static async Task SeedUserBadgeAsync(
        TestWebApplicationFactory factory,
        string badgeCode,
        DateTime? awardedAt = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var badgeId = await context.Badges
            .AsNoTracking()
            .Where(badge => badge.Code == badgeCode)
            .Select(badge => badge.Id)
            .SingleAsync();

        context.UserBadges.Add(new UserBadge
        {
            UserId = TestDataIds.UserId,
            BadgeId = badgeId,
            AwardedAt = awardedAt ?? DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }
}
