using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTEnglish.ViewModels;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Verifies HTTP/Razor rendering, accessibility, and form contracts for the redesigned Claymorphism Goals page.
/// Ensures single visible H1, Bento overview cards, all 5 skill practice links, modal/antiforgery contracts,
/// weekly activity semantics, and badge collection rendering.
/// </summary>
public sealed class GoalsUiRenderTests
{
    private static async Task SeedFullGoalsDataAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var user = await context.Users.FirstAsync(u => u.UserId == TestDataIds.UserId);
        user.Goal = 15;
        user.Streak = 5;
        user.LongestStreak = 10;
        user.LastStudyDate = BusinessDateHelper.Today;

        context.UserGoals.AddRange(
            new UserGoal { UserId = TestDataIds.UserId, GoalArea = GoalArea.Vocabulary, TargetValue = 15, IsActive = true },
            new UserGoal { UserId = TestDataIds.UserId, GoalArea = GoalArea.Speaking, TargetValue = 2, IsActive = true },
            new UserGoal { UserId = TestDataIds.UserId, GoalArea = GoalArea.Writing, TargetValue = 1, IsActive = true }
        );

        context.UserDailyActivities.Add(new UserDailyActivity
        {
            UserId = TestDataIds.UserId,
            ActivityDate = BusinessDateHelper.Today,
            CardsReviewed = 10,
            SpeakingCompletedCount = 1,
            WritingCompletedCount = 1,
            XpEarned = 50
        });

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GoalsPage_RendersClaymorphismTheme_AndSingleH1()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedFullGoalsDataAsync(factory);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/Goals");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        // Single H1 enforcement
        Assert.Single(Regex.Matches(html, "<h1[\\s>]"));
        Assert.Contains("Mục tiêu &amp; Tiến độ", html);

        // Claymorphism theme classes
        Assert.Contains("tct-theme", html);
        Assert.Contains("goals-ui", html);
        Assert.Contains("goals-header", html);
        Assert.Contains("data-testid=\"goal-header-cta\"", html);
    }

    [Fact]
    public async Task GoalsPage_RendersBentoOverview_StreakAndDailyGoalCards()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedFullGoalsDataAsync(factory);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/Goals");
        var html = await response.Content.ReadAsStringAsync();

        // Streak Card
        Assert.Contains("goals-streak-card", html);
        Assert.Contains("Chuỗi học tập", html);
        Assert.Contains("5 ngày", html);
        Assert.Contains("10 ngày", html); // Longest streak

        // Daily Goal Card
        Assert.Contains("goals-daily-card", html);
        Assert.Contains("Mục tiêu hôm nay", html);
        Assert.Contains("goals-circle-svg", html);
        Assert.Contains("goals-progress-track", html);

        // Achievement Spotlight Card
        Assert.Contains("goals-spotlight-card", html);
        Assert.Contains("Thành tích", html);
    }

    [Fact]
    public async Task GoalsPage_RendersDynamicSkillPracticeHub_AllFiveSkills()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedFullGoalsDataAsync(factory);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/Goals");
        var html = await response.Content.ReadAsStringAsync();

        // All 5 skill areas rendered with direct navigation practice links
        Assert.Contains("Tiến độ kỹ năng", html);
        Assert.Contains("Từ vựng", html);
        Assert.Contains("/Vocabulary", html);

        Assert.Contains("Luyện nói", html);
        Assert.Contains("/Speaking", html);

        Assert.Contains("Kỹ năng viết", html);
        Assert.Contains("/Writing", html);

        Assert.Contains("Kỹ năng đọc", html);
        Assert.Contains("/Reading", html);

        Assert.Contains("Kỹ năng nghe", html);
        Assert.Contains("/Listening", html);
    }

    [Fact]
    public async Task GoalsPage_PreservesModalAndFormContracts_Antiforgery_Presets()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedFullGoalsDataAsync(factory);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/Goals");
        var html = await response.Content.ReadAsStringAsync();

        // Modal contracts
        Assert.Contains("id=\"goalEditorModal\"", html);
        Assert.Contains("data-open-on-load=\"false\"", html);
        Assert.Contains("data-testid=\"goal-modal-title\" data-goal-mode=\"edit\"", html);
        Assert.Contains("data-testid=\"goal-modal-submit\" data-goal-mode=\"edit\"", html);

        // Antiforgery & Form inputs
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("name=\"GoalEditor.GoalArea\"", html);
        Assert.Contains("name=\"GoalEditor.TargetValue\"", html);
        Assert.Contains("class=\"form-control form-control-lg goal-input\"", html);
        Assert.Contains("goal-stepper-btn", html);
        Assert.Contains("goal-modal-icon-badge", html);
        Assert.Contains("goal-tip-box", html);

        // Quick presets
        Assert.Contains("goal-preset-btn", html);
        Assert.Contains("data-preset=\"10\"", html);
    }

    [Fact]
    public async Task GoalsPage_RendersWeeklyActivityChart_AccessibleSemantics()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedFullGoalsDataAsync(factory);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/Goals");
        var html = await response.Content.ReadAsStringAsync();

        // Honest chart labeling
        Assert.Contains("Ôn từ vựng hàng tuần", html);
        Assert.Contains("role=\"list\"", html);
        Assert.Contains("role=\"listitem\"", html);
        Assert.Contains("chart-column", html);
        Assert.Contains("chart-bar-anim", html);
    }

    [Fact]
    public async Task GoalsPage_RendersEmptyStateContracts_WhenUserHasNoGoals()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.FirstAsync(u => u.UserId == TestDataIds.UserId);
        user.Goal = null;
        user.Streak = 0;
        await context.SaveChangesAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/Goals");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("data-testid=\"goal-header-cta\"", html);
        Assert.Contains("data-goal-mode=\"create\"", html);
        Assert.Contains("data-testid=\"goal-empty-state-cta\"", html);
        Assert.Contains("data-testid=\"goal-modal-title\" data-goal-mode=\"create\"", html);
        Assert.Contains("data-testid=\"goal-modal-submit\" data-goal-mode=\"create\"", html);
        Assert.Contains("chart-empty-state", html);
        Assert.Contains("Biểu đồ sẽ sáng dần khi bạn bắt đầu ôn thẻ", html);
    }
}
