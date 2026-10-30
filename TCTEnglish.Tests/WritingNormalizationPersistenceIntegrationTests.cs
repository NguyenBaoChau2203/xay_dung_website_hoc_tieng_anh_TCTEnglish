using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingNormalizationPersistenceIntegrationTests
{
    [Theory]
    [InlineData("The temperature is -5 degrees.", "The temperature is 5 degrees.")]
    [InlineData("I paid $50.", "I paid €50.")]
    [InlineData("The discount is 5%.", "The discount is 5.")]
    [InlineData("The value is <5.", "The value is >5.")]
    [InlineData("The distance is 1,000 meters.", "The distance is 1.000 meters.")]
    public async Task MeaningChangingNormalizedAnswers_DoNotPassOrGrantCompletionCredit(
        string reference,
        string answer)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var seeded = await SeedPublishedSentenceAsync(factory, reference);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(
            factory,
            TestDataIds.UserId,
            Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId={seeded.ExerciseId}");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        {
            Content = JsonContent.Create(new
            {
                exerciseId = seeded.ExerciseId,
                sentenceId = seeded.SentenceId,
                userAnswer = answer
            })
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var json = JsonDocument.Parse(body))
        {
            var data = json.RootElement.GetProperty("data");
            Assert.False(data.GetProperty("passed").GetBoolean());
            Assert.False(data.GetProperty("canAutoAdvance").GetBoolean());
            Assert.NotEqual("Accepted", data.GetProperty("assessmentStatus").GetString());
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var attempt = await db.UserWritingAttempts.AsNoTracking()
                .SingleAsync(item =>
                    item.UserId == TestDataIds.UserId
                    && item.WritingExerciseId == seeded.ExerciseId
                    && item.WritingExerciseSentenceId == seeded.SentenceId);
            Assert.False(attempt.Passed);
            Assert.Equal(0, await db.LearningCompletionCredits.CountAsync(item =>
                item.UserId == TestDataIds.UserId
                && item.Feature == "Writing"
                && item.ContentKey == $"exercise:{seeded.ExerciseId}"));
        }

        var reloadBody = await client.GetStringAsync(
            $"/Home/Writing/Practice/Data?exerciseId={seeded.ExerciseId}");
        using var reloadJson = JsonDocument.Parse(reloadBody);
        var reloaded = reloadJson.RootElement
            .GetProperty("sentences")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == seeded.SentenceId);
        Assert.False(reloaded.GetProperty("lastEvaluationPassed").GetBoolean());
        Assert.Equal(answer, reloaded.GetProperty("lastSubmittedAnswer").GetString());
    }

    [Fact]
    public async Task ExactMeaningPreservingAnswer_StillAccepted()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var seeded = await SeedPublishedSentenceAsync(factory, "The temperature is -5 degrees.");
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(
            factory,
            TestDataIds.UserId,
            Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId={seeded.ExerciseId}");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        {
            Content = JsonContent.Create(new
            {
                exerciseId = seeded.ExerciseId,
                sentenceId = seeded.SentenceId,
                userAnswer = "The temperature is -5 degrees."
            })
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("data").GetProperty("passed").GetBoolean());
    }

    private static async Task<(int ExerciseId, int SentenceId)> SeedPublishedSentenceAsync(
        TestWebApplicationFactory factory,
        string englishMeaning)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var exerciseId = (await context.WritingExercises.MaxAsync(item => (int?)item.Id) ?? 0) + 1;
        var sentenceId = (await context.WritingExerciseSentences.MaxAsync(item => (int?)item.Id) ?? 0) + 1;

        context.WritingExercises.Add(new WritingExercise
        {
            Id = exerciseId,
            UserId = null,
            Title = "Normalization regression",
            Level = "beginner",
            ContentType = "emails",
            Topic = "Acceptance",
            SourceType = "catalog",
            PreviewText = "Normalization regression sentence.",
            IsPublished = true,
            CreatedAt = DateTime.UtcNow
        });
        context.WritingExerciseSentences.Add(new WritingExerciseSentence
        {
            Id = sentenceId,
            WritingExerciseId = exerciseId,
            SortOrder = 1,
            VietnameseText = "Câu kiểm tra chuẩn hóa.",
            EnglishMeaning = englishMeaning,
            BreakAfter = true
        });
        await context.SaveChangesAsync();
        return (exerciseId, sentenceId);
    }
}
