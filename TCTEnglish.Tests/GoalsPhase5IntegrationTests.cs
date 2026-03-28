using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class GoalsPhase5IntegrationTests
{
    [Fact]
    public async Task GoalsPage_ShowsRecentBadgeHighlight_AndChartTooltips()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var today = DateTime.UtcNow.Date;
        await SeedGoalsStateAsync(
            factory,
            goal: 8,
            streak: 1,
            longestStreak: 1,
            lastStudyDate: today,
            new UserDailyActivity
            {
                UserId = TestDataIds.UserId,
                ActivityDate = today,
                CardsReviewed = 4,
                XpEarned = 10
            });
        await RecordActivityAsync(
            factory,
            new GoalsActivityUpdate
            {
                CardsReviewed = 1,
                XpEarned = 0
            });

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/Goals");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("goals-insight-banner", body, StringComparison.Ordinal);
        Assert.Contains("goals-badge-card is-unlocked is-recently-unlocked", body, StringComparison.Ordinal);
        Assert.Contains("data-tooltip=", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateGoal_RedirectFollowUp_RendersGoalToast()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Goals");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Goals/UpdateGoal")
        {
            Content = new StringContent(
                "GoalEditor.DailyGoal=14",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var redirectResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, redirectResponse.StatusCode);

        var followUpRoute = redirectResponse.Headers.Location?.ToString() ?? "/Goals";
        using var followUpResponse = await client.GetAsync(followUpRoute);
        var body = await followUpResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, followUpResponse.StatusCode);
        Assert.Contains("goalUpdateToast", body, StringComparison.Ordinal);
        Assert.Contains("goals-feedback-toast", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LearningRecord_ResponseIncludesXpEarned_ForRewardToast()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Vocabulary/Study?setId=302");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/LearningApi/record")
        {
            Content = new StringContent(
                """{"cardId":401,"masteryLevel":"good","timestamp":"2026-03-29T00:00:00Z"}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = JsonDocument.Parse(body);
        Assert.True(payload.RootElement.TryGetProperty("xpEarned", out var xpEarned));
        Assert.Equal(10, xpEarned.GetInt32());
    }

    private static async Task SeedGoalsStateAsync(
        TestWebApplicationFactory factory,
        int goal,
        int streak,
        int longestStreak,
        DateTime? lastStudyDate,
        params UserDailyActivity[] activities)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(candidate => candidate.UserId == TestDataIds.UserId);

        user.Goal = goal;
        user.Streak = streak;
        user.LongestStreak = longestStreak;
        user.LastStudyDate = lastStudyDate;

        context.UserDailyActivities.AddRange(activities);
        await context.SaveChangesAsync();
    }

    private static async Task RecordActivityAsync(TestWebApplicationFactory factory, GoalsActivityUpdate update)
    {
        using var scope = factory.Services.CreateScope();
        var goalsService = scope.ServiceProvider.GetRequiredService<IGoalsService>();

        var result = await goalsService.RecordActivityAsync(TestDataIds.UserId, update);

        Assert.Equal(OperationStatus.Success, result.Status);
    }
}
