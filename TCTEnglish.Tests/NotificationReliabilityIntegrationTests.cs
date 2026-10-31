using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Models;
using TCTEnglish.Services.Notifications;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class NotificationReliabilityIntegrationTests
{
    [Theory]
    [InlineData(16, 59)] // 23:59 VN
    [InlineData(17, 1)]  // 00:01 VN next calendar UTC day still same/next business edge
    [InlineData(23, 59)] // 06:59 VN
    [InlineData(0, 1)]   // 07:01 VN
    public async Task LearningBusinessClock_BusinessDate_MatchesVietnamWindows(int utcHour, int utcMinute)
    {
        // Anchor: 2026-10-29 00:00 VN = 2026-10-28 17:00 UTC
        var utc = new DateTime(2026, 10, 28, 17, 0, 0, DateTimeKind.Utc)
            .AddHours(utcHour - 17)
            .AddMinutes(utcMinute);
        // Rebuild explicitly for clarity per case
        utc = utcHour switch
        {
            16 => new DateTime(2026, 10, 29, 16, 59, 0, DateTimeKind.Utc), // 23:59 VN Oct 29
            17 => new DateTime(2026, 10, 29, 17, 1, 0, DateTimeKind.Utc),  // 00:01 VN Oct 30
            23 => new DateTime(2026, 10, 29, 23, 59, 0, DateTimeKind.Utc), // 06:59 VN Oct 30
            0 => new DateTime(2026, 10, 30, 0, 1, 0, DateTimeKind.Utc),    // 07:01 VN Oct 30
            _ => utc
        };

        var provider = new FakeTimeProvider(utc);
        var clock = new LearningBusinessClock(provider);

        var expected = utcHour switch
        {
            16 => new DateTime(2026, 10, 29),
            17 => new DateTime(2026, 10, 30),
            23 => new DateTime(2026, 10, 30),
            0 => new DateTime(2026, 10, 30),
            _ => clock.BusinessDate
        };

        Assert.Equal(expected, clock.BusinessDate);
        Assert.Equal(expected, clock.ToBusinessDateFromUtcStorage(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified)));
    }

    [Fact]
    public async Task StreakRecord_UsesPreviousLongestSnapshot_OnlyOncePerRecord()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var streak = scope.ServiceProvider.GetRequiredService<IStreakService>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var user = await context.Users.FirstAsync(u => u.UserId == TestDataIds.UserId);
        user.Streak = 4;
        user.LongestStreak = 4;
        user.LastStudyDate = BusinessDateHelper.Today.AddDays(-1);
        await context.SaveChangesAsync();

        var result = await streak.UpdateStreakWithMetadataAsync(TestDataIds.UserId);
        Assert.True(result.DidIncrease);
        Assert.Equal(4, result.PreviousLongestStreak);
        Assert.Equal(5, result.CurrentStreak);

        var count = await context.Notifications.CountAsync(n =>
            n.UserId == TestDataIds.UserId && n.Type == NotificationType.StreakRecord);
        Assert.Equal(1, count);

        // Same day again — no increase, no new record
        var again = await streak.UpdateStreakWithMetadataAsync(TestDataIds.UserId);
        Assert.False(again.DidIncrease);
        count = await context.Notifications.CountAsync(n =>
            n.UserId == TestDataIds.UserId && n.Type == NotificationType.StreakRecord);
        Assert.Equal(1, count);

        // Replay generation with same metadata must stay idempotent via EventKey
        await notifications.GenerateStreakNotificationsAsync(TestDataIds.UserId, result);
        count = await context.Notifications.CountAsync(n =>
            n.UserId == TestDataIds.UserId && n.Type == NotificationType.StreakRecord);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GoalNotifications_SpeakingAndWritingSameDay_BothCreated()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var goals = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var clock = scope.ServiceProvider.GetRequiredService<LearningBusinessClock>();

        context.UserGoals.AddRange(
            new UserGoal
            {
                UserId = TestDataIds.UserId,
                GoalArea = GoalArea.Speaking,
                TargetValue = 1,
                IsActive = true,
                CreatedAt = clock.UtcNow,
                UpdatedAt = clock.UtcNow
            },
            new UserGoal
            {
                UserId = TestDataIds.UserId,
                GoalArea = GoalArea.Writing,
                TargetValue = 1,
                IsActive = true,
                CreatedAt = clock.UtcNow,
                UpdatedAt = clock.UtcNow
            });
        await context.SaveChangesAsync();

        var speaking = await goals.RecordLearningActivityAsync(
            TestDataIds.UserId,
            goals.BuildSpeakingCompletionActivityUpdate());
        await notifications.GenerateGoalNotificationsAsync(TestDataIds.UserId, speaking);

        var writing = await goals.RecordLearningActivityAsync(
            TestDataIds.UserId,
            goals.BuildWritingCompletionActivityUpdate());
        await notifications.GenerateGoalNotificationsAsync(TestDataIds.UserId, writing);

        var completed = await context.Notifications
            .Where(n => n.UserId == TestDataIds.UserId && n.Type == NotificationType.GoalCompleted)
            .ToListAsync();

        Assert.Equal(2, completed.Count);
        Assert.Contains(completed, n => n.EventKey != null && n.EventKey.Contains("Speaking", StringComparison.Ordinal));
        Assert.Contains(completed, n => n.EventKey != null && n.EventKey.Contains("Writing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NotificationPaging_UsesDbTotals_Beyond500()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var service = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var clock = scope.ServiceProvider.GetRequiredService<LearningBusinessClock>();

        var rows = new List<Notification>();
        for (var i = 0; i < 520; i++)
        {
            rows.Add(new Notification
            {
                UserId = TestDataIds.UserId,
                Type = NotificationType.AdminAnnouncement,
                Title = $"N{i}",
                Message = "x",
                IsRead = i < 10, // 510 unread
                CreatedAt = clock.UtcNow.AddMinutes(-i)
            });
        }

        context.Notifications.AddRange(rows);
        await context.SaveChangesAsync();

        var page = await service.GetNotificationsPageAsync(TestDataIds.UserId, page: 1, pageSize: 20, unreadOnly: true);
        Assert.Equal(510, page.UnreadCount);
        Assert.Equal(510, page.TotalCount);
        Assert.Equal(20, page.Items.Count);
        Assert.True(page.HasMore);
        Assert.All(page.Items, item => Assert.False(item.IsRead));
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTime utcNow) => _utcNow = new DateTimeOffset(utcNow);

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
