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

public sealed class GoalsPhase3IntegrationTests
{
    [Fact]
    public async Task SaveSpeakingProgress_StoresSentenceScoreForCurrentUser()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Speaking/Practice?id={TestDataIds.SpeakingVideoId}");
        using var request = CreateSpeakingProgressRequest(TestDataIds.SpeakingSentenceOneId, antiForgeryToken, 78);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var progress = await context.UserSpeakingProgresses
            .AsNoTracking()
            .SingleAsync(candidate =>
                candidate.UserId == TestDataIds.UserId
                && candidate.SentenceId == TestDataIds.SpeakingSentenceOneId);

        Assert.Equal(78, progress.TotalScore);
    }

    [Fact]
    public async Task SaveSpeakingProgress_FirstVideoCompletion_AwardsXpExactlyOnce()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Speaking/Practice?id={TestDataIds.SpeakingVideoId}");

        using (var sentenceOneResponse = await client.SendAsync(CreateSpeakingProgressRequest(TestDataIds.SpeakingSentenceOneId, antiForgeryToken, 85)))
        {
            Assert.Equal(HttpStatusCode.OK, sentenceOneResponse.StatusCode);
        }

        using (var sentenceTwoResponse = await client.SendAsync(CreateSpeakingProgressRequest(TestDataIds.SpeakingSentenceTwoId, antiForgeryToken, 88)))
        {
            Assert.Equal(HttpStatusCode.OK, sentenceTwoResponse.StatusCode);
        }

        using var completionResponse = await client.SendAsync(CreateSpeakingProgressRequest(TestDataIds.SpeakingSentenceThreeId, antiForgeryToken, 92));
        var completionBody = await completionResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, completionResponse.StatusCode);

        using (var payload = JsonDocument.Parse(completionBody))
        {
            Assert.True(payload.RootElement.GetProperty("firstTimeVideoCompletion").GetBoolean());
            Assert.Equal(20, payload.RootElement.GetProperty("xpEarned").GetInt32());
        }

        using var replayResponse = await client.SendAsync(CreateSpeakingProgressRequest(TestDataIds.SpeakingSentenceThreeId, antiForgeryToken, 95));
        var replayBody = await replayResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);

        using (var payload = JsonDocument.Parse(replayBody))
        {
            Assert.False(payload.RootElement.GetProperty("firstTimeVideoCompletion").GetBoolean());
            Assert.Equal(0, payload.RootElement.GetProperty("xpEarned").GetInt32());
        }

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var today = BusinessDateHelper.Today;

        var activity = await context.UserDailyActivities
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == TestDataIds.UserId && candidate.ActivityDate == today);
        var completion = await context.UserSpeakingVideoCompletions
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == TestDataIds.UserId && candidate.VideoId == TestDataIds.SpeakingVideoId);

        Assert.Equal(1, activity.SpeakingCompletedCount);
        Assert.Equal(30, activity.XpEarned);
        Assert.Equal(10, activity.StreakXpAwarded);
        Assert.True(completion.IsCompleted);
    }

    [Fact]
    public async Task SaveSpeakingProgress_DoesNotAffectAnotherUsersCompletionState()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        using var outsiderClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            outsiderClient,
            $"/Speaking/Practice?id={TestDataIds.SpeakingVideoId}");

        using var response = await outsiderClient.SendAsync(CreateSpeakingProgressRequest(TestDataIds.SpeakingSentenceOneId, antiForgeryToken, 90));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var targetUserProgressRows = await context.UserSpeakingProgresses
            .AsNoTracking()
            .CountAsync(candidate => candidate.UserId == TestDataIds.UserId);
        var targetUserCompletionRows = await context.UserSpeakingVideoCompletions
            .AsNoTracking()
            .CountAsync(candidate => candidate.UserId == TestDataIds.UserId);

        var outsiderProgressRows = await context.UserSpeakingProgresses
            .AsNoTracking()
            .CountAsync(candidate => candidate.UserId == TestDataIds.OutsiderUserId);

        Assert.Equal(0, targetUserProgressRows);
        Assert.Equal(0, targetUserCompletionRows);
        Assert.Equal(1, outsiderProgressRows);
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
        var today = BusinessDateHelper.Today;

        var activity = await context.UserDailyActivities
            .AsNoTracking()
            .SingleAsync(a => a.UserId == TestDataIds.UserId && a.ActivityDate == today);

        Assert.Equal(1, activity.CardsReviewed);
        Assert.Equal(1, activity.NewCardsLearned);
        Assert.Equal(20, activity.XpEarned);
        Assert.Equal(10, activity.StreakXpAwarded);

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
                ActivityDate = BusinessDateHelper.Today,
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
        var today = BusinessDateHelper.Today;

        var activity = await context.UserDailyActivities
            .AsNoTracking()
            .SingleAsync(a => a.UserId == TestDataIds.UserId && a.ActivityDate == today);
        var progress = await context.LearningProgresses
            .AsNoTracking()
            .SingleAsync(lp => lp.UserId == TestDataIds.UserId && lp.CardId == TestDataIds.UserCardId);

        Assert.Equal(3, activity.CardsReviewed);
        Assert.Equal(1, activity.NewCardsLearned);
        Assert.Equal(29, activity.XpEarned);
        Assert.Equal(10, activity.StreakXpAwarded);
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
        var today = BusinessDateHelper.Today;
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

    private static HttpRequestMessage CreateSpeakingProgressRequest(int sentenceId, string antiForgeryToken, int totalScore)
    {
        var payload = $$"""
        {
          "totalScore": {{totalScore}},
          "accuracyScore": {{totalScore}},
          "fluencyScore": {{totalScore}},
          "completenessScore": {{totalScore}}
        }
        """;

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/speaking/{sentenceId}/progress")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
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
            LastReviewedDate = BusinessDateHelper.Today.AddDays(-1),
            NextReviewDate = BusinessDateHelper.Today
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
