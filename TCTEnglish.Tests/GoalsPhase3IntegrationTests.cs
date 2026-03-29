using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class GoalsPhase3IntegrationTests
{
    [Fact]
    public async Task DailyChallenge_CorrectAnswer_RecordsGoalsActivityAndXp()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Home/Index");
        using var dashboardResponse = await client.GetAsync("/Home/Index");
        var dashboardBody = await dashboardResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);

        var challengeMatches = Regex.Matches(
            dashboardBody,
            "data-id=\"(?<id>\\d+)\"\\s+data-token=\"(?<token>[^\"]+)\"",
            RegexOptions.Singleline);

        Assert.NotEmpty(challengeMatches);

        var challengeToken = challengeMatches[0].Groups["token"].Value;
        Assert.False(string.IsNullOrWhiteSpace(challengeToken));

        var foundCorrectAnswer = false;
        foreach (Match option in challengeMatches)
        {
            var selectedCardId = option.Groups["id"].Value;
            using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/CheckAnswer")
            {
                Content = new StringContent(
                    $"selectedCardId={selectedCardId}&challengeToken={Uri.EscapeDataString(challengeToken)}",
                    Encoding.UTF8,
                    "application/x-www-form-urlencoded")
            };
            request.Headers.Add("RequestVerificationToken", antiForgeryToken);

            using var answerResponse = await client.SendAsync(request);
            var answerBody = await answerResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, answerResponse.StatusCode);

            using var payload = JsonDocument.Parse(answerBody);
            if (payload.RootElement.TryGetProperty("correct", out var isCorrect) && isCorrect.GetBoolean())
            {
                foundCorrectAnswer = true;
                break;
            }
        }

        Assert.True(foundCorrectAnswer, "Daily challenge options did not contain a correct answer payload.");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var today = DateTime.UtcNow.Date;
        var activity = await context.UserDailyActivities
            .AsNoTracking()
            .SingleAsync(a => a.UserId == TestDataIds.UserId && a.ActivityDate == today);

        Assert.Equal(1, activity.CardsReviewed);
        Assert.Equal(1, activity.QuizzesCompleted);
        Assert.Equal(10, activity.XpEarned);
    }

    [Fact]
    public async Task LearningRecord_NewCard_CreatesDailyActivityAndUpdatesGoalsProgress()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SetUserGoalAsync(factory, 3);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Vocabulary/Study?setId=302");
        using var request = CreateLearningRecordRequest("good", antiForgeryToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var goalsService = scope.ServiceProvider.GetRequiredService<IGoalsService>();
        var today = DateTime.UtcNow.Date;

        var activity = await context.UserDailyActivities
            .AsNoTracking()
            .SingleAsync(a => a.UserId == TestDataIds.UserId && a.ActivityDate == today);

        Assert.Equal(1, activity.CardsReviewed);
        Assert.Equal(1, activity.NewCardsLearned);
        Assert.Equal(10, activity.XpEarned);

        var model = await goalsService.GetGoalsAsync(TestDataIds.UserId);

        Assert.NotNull(model);
        Assert.Equal(1, model!.TodayProgressValue);
        Assert.Equal(33, model.ProgressPercent);
    }

    [Fact]
    public async Task LearningRecord_MasteredTransition_AwardsMasteryXpWithoutDoubleCounting()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SeedExistingProgressAsync(factory, status: "Reviewing", repetitionCount: 1);
        await SeedDailyActivityAsync(
            factory,
            new UserDailyActivity
            {
                UserId = TestDataIds.UserId,
                ActivityDate = DateTime.UtcNow.Date,
                CardsReviewed = 2,
                NewCardsLearned = 1,
                XpEarned = 4
            });
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Vocabulary/Study?setId=302");
        using var request = CreateLearningRecordRequest("easy", antiForgeryToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var today = DateTime.UtcNow.Date;

        var activity = await context.UserDailyActivities
            .AsNoTracking()
            .SingleAsync(a => a.UserId == TestDataIds.UserId && a.ActivityDate == today);
        var progress = await context.LearningProgresses
            .AsNoTracking()
            .SingleAsync(lp => lp.UserId == TestDataIds.UserId && lp.CardId == TestDataIds.UserCardId);

        Assert.Equal(3, activity.CardsReviewed);
        Assert.Equal(1, activity.NewCardsLearned);
        Assert.Equal(19, activity.XpEarned);
        Assert.Equal("Mastered", progress.Status);
    }

    [Fact]
    public async Task RecordActivityAsync_AggregatesIntoSingleDailyRowAcrossScopes()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        await RecordActivityAsync(
            factory,
            new GoalsActivityUpdate
            {
                CardsReviewed = 1,
                NewCardsLearned = 1,
                XpEarned = 10
            });
        await RecordActivityAsync(
            factory,
            new GoalsActivityUpdate
            {
                CardsReviewed = 2,
                QuizzesCompleted = 1,
                XpEarned = 5
            });

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var today = DateTime.UtcNow.Date;
        var rows = await context.UserDailyActivities
            .AsNoTracking()
            .Where(a => a.UserId == TestDataIds.UserId && a.ActivityDate == today)
            .ToListAsync();

        var activity = Assert.Single(rows);
        Assert.Equal(3, activity.CardsReviewed);
        Assert.Equal(1, activity.NewCardsLearned);
        Assert.Equal(1, activity.QuizzesCompleted);
        Assert.Equal(15, activity.XpEarned);
    }

    private static HttpRequestMessage CreateLearningRecordRequest(string masteryLevel, string antiForgeryToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/LearningApi/record")
        {
            Content = new StringContent(
                $$"""{"cardId":401,"masteryLevel":"{{masteryLevel}}","timestamp":"2026-03-28T00:00:00Z"}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);
        return request;
    }

    private static async Task RecordActivityAsync(TestWebApplicationFactory factory, GoalsActivityUpdate update)
    {
        using var scope = factory.Services.CreateScope();
        var goalsService = scope.ServiceProvider.GetRequiredService<IGoalsService>();

        var result = await goalsService.RecordActivityAsync(TestDataIds.UserId, update);

        Assert.Equal(OperationStatus.Success, result.Status);
    }

    private static async Task SetUserGoalAsync(TestWebApplicationFactory factory, int goal)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await context.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);

        user.Goal = goal;
        await context.SaveChangesAsync();
    }

    private static async Task SeedExistingProgressAsync(TestWebApplicationFactory factory, string status, int repetitionCount)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        context.LearningProgresses.Add(new LearningProgress
        {
            UserId = TestDataIds.UserId,
            CardId = TestDataIds.UserCardId,
            Status = status,
            RepetitionCount = repetitionCount,
            WrongCount = 0,
            LastReviewedDate = DateTime.UtcNow.Date.AddDays(-1),
            NextReviewDate = DateTime.UtcNow.Date
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedDailyActivityAsync(TestWebApplicationFactory factory, UserDailyActivity activity)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        context.UserDailyActivities.Add(activity);
        await context.SaveChangesAsync();
    }
}
