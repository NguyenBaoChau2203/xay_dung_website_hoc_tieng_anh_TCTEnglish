using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class GoalsPhase2IntegrationTests
{
    [Fact]
    public async Task GoalsPage_ShowsWeeklyEmptyState_WhenUserHasNoDailyActivity()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedGoalOnlyAsync(factory, goal: 9);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync("/Goals");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("chart-empty-state", body, StringComparison.Ordinal);
        Assert.Contains(">0/9<", body, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"chart-bar", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetGoalsAsync_FillsMissingWeeklyDays_FromUserDailyActivity()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var today = DateTime.UtcNow.Date;
        await SeedDailyActivitiesAsync(
            factory,
            goal: 12,
            new UserDailyActivity
            {
                UserId = TestDataIds.UserId,
                ActivityDate = today.AddDays(-4),
                CardsReviewed = 3,
                XpEarned = 15
            },
            new UserDailyActivity
            {
                UserId = TestDataIds.UserId,
                ActivityDate = today,
                CardsReviewed = 5,
                XpEarned = 25
            });

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IGoalsService>();

        var model = await service.GetGoalsAsync(TestDataIds.UserId);

        Assert.NotNull(model);
        Assert.Equal(7, model!.WeeklyActivity.Count);
        Assert.True(model.HasWeeklyActivity);
        Assert.Equal(5, model.TodayProgressValue);
        Assert.Equal(42, model.ProgressPercent);
        Assert.Equal(0, model.WeeklyActivity[0].ActivityCount);
        Assert.Equal(0, model.WeeklyActivity[1].ActivityCount);
        Assert.Equal(3, model.WeeklyActivity[2].ActivityCount);
        Assert.Equal(0, model.WeeklyActivity[3].ActivityCount);
        Assert.Equal(0, model.WeeklyActivity[4].ActivityCount);
        Assert.Equal(0, model.WeeklyActivity[5].ActivityCount);
        Assert.Equal(5, model.WeeklyActivity[6].ActivityCount);
        Assert.Equal(today.ToString("dd/MM"), model.WeeklyActivity[6].FullLabel);
    }

    private static async Task SeedGoalOnlyAsync(TestWebApplicationFactory factory, int goal)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);

        user.Goal = goal;
        user.Streak = 0;
        user.LongestStreak = 0;
        user.LastStudyDate = null;

        await context.SaveChangesAsync();
    }

    private static async Task SeedDailyActivitiesAsync(
        TestWebApplicationFactory factory,
        int goal,
        params UserDailyActivity[] activities)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);

        user.Goal = goal;
        user.Streak = 2;
        user.LongestStreak = 4;
        user.LastStudyDate = DateTime.UtcNow.Date;

        context.UserDailyActivities.AddRange(activities);
        await context.SaveChangesAsync();
    }
}
