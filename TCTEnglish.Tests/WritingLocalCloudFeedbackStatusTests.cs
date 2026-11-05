using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Writing;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingLocalCloudFeedbackStatusTests
{
    [Fact]
    public async Task LocalMode_OptionalCloudFeedback_KeepsNeedsReview_OnSubmitAndReload()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IWritingAiEvaluationService>();
            services.AddSingleton<IWritingAiEvaluationService>(new FastSupplementalCloud());
            services.AddSingleton<IOptions<WritingEvaluationOptions>>(
                Options.Create(new WritingEvaluationOptions
                {
                    EvaluationMode = "Local",
                    CloudEnhancementEnabled = true,
                    CloudEnhancementTimeoutSeconds = 3
                }));
        });
        await factory.InitializeAsync();

        int exerciseId;
        int sentenceId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var exercise = new WritingExercise
            {
                Title = "Local Cloud Feedback Fixture",
                Level = "beginner",
                ContentType = "emails",
                Topic = "Feedback",
                PreviewText = "NeedsReview + cloud",
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            };
            exercise.WritingExerciseSentences.Add(new WritingExerciseSentence
            {
                SortOrder = 1,
                VietnameseText = "Toi thich tra.",
                EnglishMeaning = "I like tea.",
                BreakAfter = false
            });
            context.WritingExercises.Add(exercise);
            await context.SaveChangesAsync();
            exerciseId = exercise.Id;
            sentenceId = exercise.WritingExerciseSentences.Single().Id;
        }

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(
            factory,
            TestDataIds.UserId,
            Roles.Standard);

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Home/Writing/Practice?exerciseId={exerciseId}");

        using (var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
               {
                   Content = JsonContent.Create(new
                   {
                       exerciseId,
                       sentenceId,
                       userAnswer = "I enjoy tea."
                   })
               })
        {
            request.Headers.Add("RequestVerificationToken", antiForgeryToken);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var data = json.RootElement.GetProperty("data");
            Assert.Equal("NeedsReview", data.GetProperty("assessmentStatus").GetString());
            Assert.Equal("local-review", data.GetProperty("evaluationSource").GetString());
            Assert.Equal("local-review", data.GetProperty("assessmentSource").GetString());
            Assert.True(data.GetProperty("usedAi").GetBoolean());
            Assert.Contains("cloud", data.GetProperty("optionalFeedbackSource").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.False(data.GetProperty("passed").GetBoolean());
        }

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var attempt = await context.UserWritingAttempts.AsNoTracking()
                .Where(item => item.UserId == TestDataIds.UserId && item.WritingExerciseSentenceId == sentenceId)
                .OrderByDescending(item => item.Id)
                .FirstAsync();
            Assert.False(attempt.Passed);
            Assert.Equal("local-review", attempt.EvaluationSource);
        }

        var practiceHtml = await client.GetStringAsync($"/Home/Writing/Practice?exerciseId={exerciseId}");
        Assert.Contains("\"assessmentStatus\":\"NeedsReview\"", practiceHtml.Replace(" ", string.Empty), StringComparison.Ordinal);

        var dataBody = await client.GetStringAsync($"/Home/Writing/Practice/Data?exerciseId={exerciseId}");
        using var dataJson = JsonDocument.Parse(dataBody);
        var sentence = dataJson.RootElement
            .GetProperty("sentences")
            .EnumerateArray()
            .First(item => item.GetProperty("id").GetInt32() == sentenceId);
        var last = sentence.GetProperty("lastEvaluation");
        Assert.Equal("NeedsReview", last.GetProperty("assessmentStatus").GetString());
        Assert.Equal("local-review", last.GetProperty("evaluationSource").GetString());
    }

    private sealed class FastSupplementalCloud : IWritingAiEvaluationService
    {
        public Task<WritingAiEvaluationResult?> TryEvaluateSentenceAsync(
            WritingAiEvaluationRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WritingAiEvaluationResult?>(new WritingAiEvaluationResult(
                Passed: false,
                OverallFeedback: "Optional cloud note",
                MeaningFeedback: "Meaning is close",
                GrammarFeedback: "Grammar is fine",
                NaturalnessFeedback: "Sounds natural",
                WordChoiceFeedback: "Word choice is fine",
                SuggestedRewrite: "I like tea."));
        }
    }
}
