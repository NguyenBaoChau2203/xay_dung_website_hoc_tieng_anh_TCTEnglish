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
        Assert.Contains("Vocabulary", body, StringComparison.Ordinal);
        Assert.Contains("2/12", body, StringComparison.Ordinal);
        Assert.DoesNotContain("30/50", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateGoal_CreatesFirstGoalArea_AndRedirects()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Goals");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Goals/UpdateGoal")
        {
            Content = new StringContent(
                "GoalEditor.GoalArea=Vocabulary&GoalEditor.TargetValue=25",
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
        var userGoal = await context.UserGoals.SingleAsync(
            goal => goal.UserId == TestDataIds.UserId && goal.GoalArea == GoalArea.Vocabulary);

        Assert.Equal(25, user.Goal);
        Assert.Equal(25, userGoal.TargetValue);
    }

    [Fact]
    public async Task UpdateGoal_EditsExistingGoalArea()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedUserGoalAsync(factory, GoalArea.Speaking, 2);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Goals");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Goals/UpdateGoal")
        {
            Content = new StringContent(
                "GoalEditor.GoalArea=Speaking&GoalEditor.TargetValue=4",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var speakingGoal = await context.UserGoals.SingleAsync(
            goal => goal.UserId == TestDataIds.UserId && goal.GoalArea == GoalArea.Speaking);

        Assert.Equal(4, speakingGoal.TargetValue);
    }

    [Fact]
    public async Task UpdateGoal_CreatesMultipleGoalAreas()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        await SubmitGoalUpdateAsync(client, GoalArea.Vocabulary, 10);
        await SubmitGoalUpdateAsync(client, GoalArea.Speaking, 3);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var goals = await context.UserGoals
            .Where(goal => goal.UserId == TestDataIds.UserId)
            .OrderBy(goal => goal.GoalArea)
            .ToListAsync();

        Assert.Equal(2, goals.Count);
        Assert.Equal(GoalArea.Vocabulary, goals[0].GoalArea);
        Assert.Equal(10, goals[0].TargetValue);
        Assert.Equal(GoalArea.Speaking, goals[1].GoalArea);
        Assert.Equal(3, goals[1].TargetValue);
    }

    [Fact]
    public async Task UpdateGoal_RejectsOutOfRangeValue_AndKeepsExistingGoal()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedUserGoalAsync(factory, GoalArea.Vocabulary, 9);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Goals");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Goals/UpdateGoal")
        {
            Content = new StringContent(
                "GoalEditor.GoalArea=Vocabulary&GoalEditor.TargetValue=999",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("GoalEditor.TargetValue", body, StringComparison.Ordinal);
        Assert.Contains("data-open-on-load=\"true\"", body, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var goal = await context.UserGoals.SingleAsync(
            item => item.UserId == TestDataIds.UserId && item.GoalArea == GoalArea.Vocabulary);

        Assert.Equal(9, goal.TargetValue);
    }

    [Fact]
    public async Task UpdateGoal_WithoutAntiForgeryToken_ReturnsBadRequest()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Goals/UpdateGoal")
        {
            Content = new StringContent(
                "GoalEditor.GoalArea=Vocabulary&GoalEditor.TargetValue=10",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
        // Use a more robust regex that handles optional whitespace and self-closing tags
        Assert.Matches(new Regex(@"18\s*t[^\s<]*\s*<br\s*/?>\s*c[^\s<]*n", RegexOptions.IgnoreCase | RegexOptions.Singleline), body);
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

    private static async Task SeedUserGoalAsync(
        TestWebApplicationFactory factory,
        GoalArea goalArea,
        int targetValue)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        context.UserGoals.Add(new UserGoal
        {
            UserId = TestDataIds.UserId,
            GoalArea = goalArea,
            TargetValue = targetValue,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        if (goalArea == GoalArea.Vocabulary)
        {
            var user = await context.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);
            user.Goal = targetValue;
        }

        await context.SaveChangesAsync();
    }

    private static async Task SubmitGoalUpdateAsync(HttpClient client, GoalArea goalArea, int targetValue)
    {
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Goals");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Goals/UpdateGoal")
        {
            Content = new StringContent(
                $"GoalEditor.GoalArea={goalArea}&GoalEditor.TargetValue={targetValue}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
