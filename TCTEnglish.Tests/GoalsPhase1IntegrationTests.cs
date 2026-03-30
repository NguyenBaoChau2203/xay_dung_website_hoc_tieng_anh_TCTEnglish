using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class GoalsPhase1IntegrationTests
{
    [Fact]
    public async Task GoalsPage_RendersRealGoalAndProgressValues()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedGoalDataAsync(factory, goal: 12, streak: 4, longestStreak: 7);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync("/Goals");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("value=\"12\"", body, StringComparison.Ordinal);
        Assert.Contains(">2/12<", body, StringComparison.Ordinal);
        Assert.DoesNotContain("30/50", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateGoal_PersistsValidValue_AndRedirects()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Goals");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Goals/UpdateGoal")
        {
            Content = new StringContent(
                "GoalEditor.DailyGoal=25",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/Goals", response.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);

        Assert.Equal(25, user.Goal);
    }

    [Fact]
    public async Task UpdateGoal_RejectsOutOfRangeValue_AndKeepsExistingGoal()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SetUserGoalAsync(factory, 9);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Goals");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Goals/UpdateGoal")
        {
            Content = new StringContent(
                "GoalEditor.DailyGoal=999",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("GoalEditor.DailyGoal", body, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);

        Assert.Equal(9, user.Goal);
    }

    [Fact]
    public async Task Dashboard_RendersGoalFromUserState()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SetUserGoalAsync(factory, 18);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync("/Home/Index");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(
            new Regex(@"fa-bullseye.*?<span class=""stat-value"">18</span>", RegexOptions.Singleline),
            body);
    }

    private static async Task SeedGoalDataAsync(
        TestWebApplicationFactory factory,
        int goal,
        int streak,
        int longestStreak)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);

        user.Goal = goal;
        user.Streak = streak;
        user.LongestStreak = longestStreak;
        user.LastStudyDate = BusinessDateHelper.Today;

        context.UserDailyActivities.AddRange(
            new UserDailyActivity
            {
                UserId = TestDataIds.UserId,
                ActivityDate = BusinessDateHelper.Today.AddDays(-1),
                CardsReviewed = 1
            },
            new UserDailyActivity
            {
                UserId = TestDataIds.UserId,
                ActivityDate = BusinessDateHelper.Today,
                CardsReviewed = 2
            });

        await context.SaveChangesAsync();
    }

    private static async Task SetUserGoalAsync(TestWebApplicationFactory factory, int goal)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);

        user.Goal = goal;
        await context.SaveChangesAsync();
    }
}
