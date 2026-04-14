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

public sealed class GoalsPhase7IntegrationTests
{
    [Fact]
    public async Task WritingEvaluate_FirstExerciseCompletion_AwardsXpExactlyOnce_OnReplay()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var targetSentenceId = await SeedWritingExerciseReadyForFinalSentenceAsync(factory, exerciseId: 1);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            "/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId=1");

        using var firstResponse = await client.SendAsync(CreateWritingEvaluateRequest(antiForgeryToken, 1, targetSentenceId, "Hello!"));
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using (var firstPayload = JsonDocument.Parse(firstBody))
        {
            Assert.True(firstPayload.RootElement.GetProperty("success").GetBoolean());
            Assert.True(firstPayload.RootElement.GetProperty("data").GetProperty("passed").GetBoolean());
        }

        using var replayResponse = await client.SendAsync(CreateWritingEvaluateRequest(antiForgeryToken, 1, targetSentenceId, "Hello!"));
        var replayBody = await replayResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);

        using (var replayPayload = JsonDocument.Parse(replayBody))
        {
            Assert.True(replayPayload.RootElement.GetProperty("success").GetBoolean());
            Assert.True(replayPayload.RootElement.GetProperty("data").GetProperty("passed").GetBoolean());
        }

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var today = BusinessDateHelper.Today;

        var activity = await context.UserDailyActivities
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == TestDataIds.UserId && candidate.ActivityDate == today);

        var exerciseProgress = await context.UserWritingExerciseProgresses
            .AsNoTracking()
            .SingleAsync(candidate =>
                candidate.UserId == TestDataIds.UserId
                && candidate.WritingExerciseId == 1);

        var sentenceProgress = await context.UserWritingSentenceProgresses
            .AsNoTracking()
            .SingleAsync(candidate =>
                candidate.UserId == TestDataIds.UserId
                && candidate.SentenceId == targetSentenceId);

        Assert.Equal(1, activity.WritingCompletedCount);
        Assert.Equal(30, activity.XpEarned);
        Assert.Equal(10, activity.StreakXpAwarded);
        Assert.True(exerciseProgress.IsCompleted);
        Assert.Equal(exerciseProgress.TotalSentenceCount, exerciseProgress.PassedSentenceCount);
        Assert.Equal(2, sentenceProgress.AttemptCount);
        Assert.True(sentenceProgress.IsPassed);
        Assert.False(string.IsNullOrWhiteSpace(sentenceProgress.AcceptedAnswer));
    }

    private static HttpRequestMessage CreateWritingEvaluateRequest(
        string antiForgeryToken,
        int exerciseId,
        int sentenceId,
        string userAnswer)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        {
            Content = new StringContent(
                $$"""{"exerciseId":{{exerciseId}},"sentenceId":{{sentenceId}},"userAnswer":"{{userAnswer}}"}""",
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Add("RequestVerificationToken", antiForgeryToken);
        return request;
    }

    private static async Task<int> SeedWritingExerciseReadyForFinalSentenceAsync(
        TestWebApplicationFactory factory,
        int exerciseId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var sentenceIds = await context.WritingExerciseSentences
            .AsNoTracking()
            .Where(sentence => sentence.WritingExerciseId == exerciseId)
            .OrderBy(sentence => sentence.SortOrder)
            .Select(sentence => sentence.Id)
            .ToListAsync();

        var targetSentenceId = sentenceIds.First();
        var seededAt = DateTime.UtcNow.AddMinutes(-5);

        context.UserWritingSentenceProgresses.AddRange(sentenceIds
            .Skip(1)
            .Select(sentenceId => new UserWritingSentenceProgress
            {
                UserId = TestDataIds.UserId,
                WritingExerciseId = exerciseId,
                SentenceId = sentenceId,
                AttemptCount = 1,
                IsPassed = true,
                AcceptedAnswer = $"Accepted sentence {sentenceId}",
                LastAttemptAt = seededAt,
                PassedAt = seededAt
            }));

        context.UserWritingExerciseProgresses.Add(new UserWritingExerciseProgress
        {
            UserId = TestDataIds.UserId,
            WritingExerciseId = exerciseId,
            TotalSentenceCount = sentenceIds.Count,
            PassedSentenceCount = sentenceIds.Count - 1,
            AttemptCount = sentenceIds.Count - 1,
            IsCompleted = false,
            LastAttemptAt = seededAt
        });

        await context.SaveChangesAsync();
        return targetSentenceId;
    }
}
