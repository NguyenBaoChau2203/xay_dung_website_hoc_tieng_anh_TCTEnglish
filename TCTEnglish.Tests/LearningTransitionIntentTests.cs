using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Models;
using TCTEnglish.Services.Learning;
using TCTEnglish.Services.Notifications;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class LearningTransitionIntentTests
{
    [Fact]
    public async Task StreakRecord_RequiresARealIncreasePastPreviousLongest()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        await ApplyAsync(factory, TestDataIds.UserId, streak: 4, longest: 5, contentKey: "video:801", daysAgo: 1);
        Assert.DoesNotContain(await EventKeysAsync(factory, TestDataIds.UserId), key => key.StartsWith("streak-record:", StringComparison.Ordinal));

        await ApplyAsync(factory, TestDataIds.OutsiderUserId, streak: 5, longest: 5, contentKey: "video:802", daysAgo: 1);
        var recordKeys = await EventKeysAsync(factory, TestDataIds.OutsiderUserId);
        Assert.Single(recordKeys, key => key.StartsWith("streak-record:", StringComparison.Ordinal));

        await ApplyAsync(factory, TestDataIds.OutsiderUserId, streak: 6, longest: 6, contentKey: "video:803", daysAgo: 0);
        Assert.Single(await EventKeysAsync(factory, TestDataIds.OutsiderUserId), key => key.StartsWith("streak-record:", StringComparison.Ordinal));

        await ApplyAsync(factory, TestDataIds.MemberUserId, streak: 4, longest: 5, contentKey: "video:804", daysAgo: 10);
        Assert.DoesNotContain(await EventKeysAsync(factory, TestDataIds.MemberUserId), key => key.StartsWith("streak-record:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BadgeIntent_UsesOnlyBadgesAwardedByTheOperation()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var clock = scope.ServiceProvider.GetRequiredService<LearningBusinessClock>();
            context.UserBadges.Add(new UserBadge
            {
                UserId = TestDataIds.UserId,
                BadgeId = 1,
                AwardedAt = clock.UtcNow.AddMinutes(-1)
            });
            await context.SaveChangesAsync();
        }

        await ApplyAsync(factory, TestDataIds.UserId, streak: 2, longest: 2, contentKey: "video:811", daysAgo: 1);
        var keys = await EventKeysAsync(factory, TestDataIds.UserId);
        Assert.Contains(keys, key => key == $"badge:{TestDataIds.UserId}:2");
        Assert.DoesNotContain(keys, key => key == $"badge:{TestDataIds.UserId}:1");
    }

    [Fact]
    public async Task GoalCompleted_FollowsTheSpeakingGoal_AndDrainKeepsOccurrenceTime()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        DateTime occurred;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var clock = scope.ServiceProvider.GetRequiredService<LearningBusinessClock>();
            occurred = clock.UtcNow;
            context.UserGoals.Add(new UserGoal
            {
                UserId = TestDataIds.UserId,
                GoalArea = GoalArea.Speaking,
                TargetValue = 1,
                IsActive = true,
                CreatedAt = occurred,
                UpdatedAt = occurred
            });
            await context.SaveChangesAsync();
        }

        await ApplyAsync(factory, TestDataIds.UserId, streak: 1, longest: 1, contentKey: "video:821", daysAgo: 1);
        var keys = await EventKeysAsync(factory, TestDataIds.UserId);
        Assert.Contains(keys, key => key.Contains(":Speaking:", StringComparison.Ordinal) && key.EndsWith(":100", StringComparison.Ordinal));

        using var drainScope = factory.Services.CreateScope();
        var dispatcher = drainScope.ServiceProvider.GetRequiredService<ILearningOutboxDispatcher>();
        var drained = await dispatcher.DrainAsync(20, CancellationToken.None);
        Assert.True(drained > 0);
        var contextAfter = drainScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var notification = await contextAfter.Notifications.SingleAsync(n => n.UserId == TestDataIds.UserId && n.Type == NotificationType.GoalCompleted);
        Assert.Equal(occurred, notification.CreatedAt, TimeSpan.FromSeconds(2));
    }

    private static async Task ApplyAsync(
        TestWebApplicationFactory factory,
        int userId,
        int streak,
        int longest,
        string contentKey,
        int daysAgo)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var clock = scope.ServiceProvider.GetRequiredService<LearningBusinessClock>();
        var credits = scope.ServiceProvider.GetRequiredService<ICompletionCreditService>();
        var user = await context.Users.SingleAsync(u => u.UserId == userId);
        user.Streak = streak;
        user.LongestStreak = longest;
        user.LastStudyDate = clock.BusinessDate.AddDays(-daysAgo);
        await context.SaveChangesAsync();

        var videoId = int.Parse(contentKey.Split(':')[1], System.Globalization.CultureInfo.InvariantCulture);
        var result = await credits.TryApplySpeakingVideoCompletionAsync(
            userId,
            videoId,
            new GoalsActivityUpdate { SpeakingCompletedCount = 1, XpEarned = 5 },
            CancellationToken.None);
        Assert.True(result.Applied, result.ErrorMessage);
    }

    private static async Task<List<string>> EventKeysAsync(TestWebApplicationFactory factory, int userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        return await context.LearningNotificationOutboxEvents.AsNoTracking()
            .Where(item => item.UserId == userId && item.NotificationEventKey != null)
            .Select(item => item.NotificationEventKey!)
            .ToListAsync();
    }
}
