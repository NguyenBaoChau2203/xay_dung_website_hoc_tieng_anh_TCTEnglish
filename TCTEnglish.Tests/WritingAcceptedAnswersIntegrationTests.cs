using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Services.Writing;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingAcceptedAnswersIntegrationTests
{
    [Fact]
    public async Task AdminSavesApprovedVariant_LearnerExactMatchIsAccepted_WithoutLeakingVariants()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var adminClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        using var learnerClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        int exerciseId;
        int sentenceId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var exercise = new WritingExercise
            {
                Title = "Accepted Variants Fixture",
                Level = "beginner",
                ContentType = "emails",
                Topic = "Variants",
                PreviewText = "Variant grading fixture",
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

        var editHtml = await adminClient.GetStringAsync($"/Admin/WritingExerciseManagement/Edit/{exerciseId}");
        Assert.Contains("Đáp án biến thể do Admin duyệt", editHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("I enjoy tea.", editHtml, StringComparison.Ordinal);

        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            adminClient,
            $"/Admin/WritingExerciseManagement/Edit/{exerciseId}");
        var expectedState = WritingAssessmentPolicy.BuildAcceptedAnswersStateToken(
            exerciseId,
            sentenceId,
            "Toi thich tra.",
            "I like tea.",
            Array.Empty<(int Id, string NormalizedHash, long ApprovedAtUtcTicks)>());

        using (var saveResponse = await adminClient.PostAsync(
                   "/Admin/WritingExerciseManagement/SaveAcceptedAnswers",
                   new FormUrlEncodedContent(
                   [
                       new KeyValuePair<string, string>("__RequestVerificationToken", token),
                       new KeyValuePair<string, string>("ExerciseId", exerciseId.ToString()),
                       new KeyValuePair<string, string>("SentenceId", sentenceId.ToString()),
                       new KeyValuePair<string, string>("ExpectedStateToken", expectedState),
                       new KeyValuePair<string, string>("Answers[0]", "I enjoy tea."),
                       new KeyValuePair<string, string>("Answers[1]", ""),
                       new KeyValuePair<string, string>("Answers[2]", ""),
                       new KeyValuePair<string, string>("Answers[3]", ""),
                       new KeyValuePair<string, string>("Answers[4]", "")
                   ])))
        {
            Assert.Equal(HttpStatusCode.Redirect, saveResponse.StatusCode);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var variants = await context.WritingAcceptedAnswers.AsNoTracking()
                .Where(answer => answer.WritingExerciseSentenceId == sentenceId)
                .ToListAsync();
            Assert.Single(variants);
            Assert.Equal("I enjoy tea.", variants[0].AnswerText);
            Assert.Equal(WritingAssessmentPolicy.ComputeNormalizedHash("I enjoy tea."), variants[0].NormalizedHash);
            Assert.Equal(TestDataIds.AdminUserId, variants[0].ApprovedByUserId);
        }

        var practiceBody = await learnerClient.GetStringAsync($"/Home/Writing/Practice/Data?exerciseId={exerciseId}");
        Assert.DoesNotContain("I enjoy tea.", practiceBody, StringComparison.Ordinal);
        Assert.DoesNotContain("local-approved-variant", practiceBody, StringComparison.Ordinal);

        var evaluateToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            learnerClient,
            $"/Home/Writing/Practice?exerciseId={exerciseId}");
        using var evaluateRequest = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    exerciseId,
                    sentenceId,
                    userAnswer = "i enjoy tea"
                }),
                System.Text.Encoding.UTF8,
                "application/json")
        };
        evaluateRequest.Headers.Add("RequestVerificationToken", evaluateToken);
        evaluateRequest.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var evaluate = await learnerClient.SendAsync(evaluateRequest);
        Assert.Equal(HttpStatusCode.OK, evaluate.StatusCode);
        var evaluateBody = await evaluate.Content.ReadAsStringAsync();
        using var evaluateJson = JsonDocument.Parse(evaluateBody);
        Assert.True(evaluateJson.RootElement.GetProperty("success").GetBoolean());
        var data = evaluateJson.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("passed").GetBoolean());
        Assert.Equal("Accepted", data.GetProperty("assessmentStatus").GetString());
        Assert.Equal("local-approved-variant", data.GetProperty("assessmentSource").GetString());
        Assert.False(data.GetProperty("reviewRequired").GetBoolean());
        Assert.DoesNotContain("\"I like tea.\"", evaluateBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BulkEdit_ChangingSentenceWithVariants_IsBlocked()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var adminClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        int exerciseId;
        int sentenceId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var exercise = new WritingExercise
            {
                Title = "Variant Guard Fixture",
                Level = "beginner",
                ContentType = "emails",
                Topic = "Guard",
                PreviewText = "Guard fixture",
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            };
            var sentence = new WritingExerciseSentence
            {
                SortOrder = 1,
                VietnameseText = "Xin chao.",
                EnglishMeaning = "Hello.",
                BreakAfter = false
            };
            sentence.WritingAcceptedAnswers.Add(new WritingAcceptedAnswer
            {
                AnswerText = "Hi.",
                NormalizedHash = WritingAssessmentPolicy.ComputeNormalizedHash("Hi."),
                ApprovedByUserId = TestDataIds.AdminUserId,
                ApprovedAtUtc = DateTime.UtcNow
            });
            exercise.WritingExerciseSentences.Add(sentence);
            context.WritingExercises.Add(exercise);
            await context.SaveChangesAsync();
            exerciseId = exercise.Id;
            sentenceId = sentence.Id;
        }

        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            adminClient,
            $"/Admin/WritingExerciseManagement/Edit/{exerciseId}");

        using var editResponse = await adminClient.PostAsync(
            $"/Admin/WritingExerciseManagement/Edit/{exerciseId}",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("Id", exerciseId.ToString()),
                new KeyValuePair<string, string>("Title", "Variant Guard Fixture"),
                new KeyValuePair<string, string>("Level", "beginner"),
                new KeyValuePair<string, string>("ContentType", "emails"),
                new KeyValuePair<string, string>("Topic", "Guard"),
                new KeyValuePair<string, string>("PreviewText", "Guard fixture"),
                new KeyValuePair<string, string>("FullVietnameseText", "Tam biet."),
                new KeyValuePair<string, string>("FullEnglishText", "Goodbye."),
                new KeyValuePair<string, string>("IsPublished", "true")
            ]));

        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
        var body = await editResponse.Content.ReadAsStringAsync();
        Assert.Contains("đáp án biến thể", body, StringComparison.OrdinalIgnoreCase);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var sentence = await context.WritingExerciseSentences.AsNoTracking()
                .SingleAsync(item => item.Id == sentenceId);
            Assert.Equal("Xin chao.", sentence.VietnameseText);
            Assert.Equal("Hello.", sentence.EnglishMeaning);
            Assert.Equal(1, await context.WritingAcceptedAnswers.CountAsync(a => a.WritingExerciseSentenceId == sentenceId));
        }
    }
}
