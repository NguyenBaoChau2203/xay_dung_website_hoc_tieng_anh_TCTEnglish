using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingAdminContentIntegrationTests
{
    [Fact]
    public async Task AdminWriting_CreatePublishedExercise_SplitsConfiguredPunctuationAndLearnerCanConsumeIt()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var adminClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        using var browseClient = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var learnerClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        const string title = "Phase 7 Publish Check";
        const string topic = "Admin Alignment";
        const string previewText = "Practice a short follow-up email that uses colon and semicolon sentence breaks.";
        const string fullVietnameseText = "Xin chao Lan: toi muon cap nhat lich. Chung ta can chot som; cam on ban.";
        const string fullEnglishText = "Hi Lan: I want to update the schedule. We need to finalize soon; thank you.";

        using var createResponse = await PostCreateAsync(
            adminClient,
            new WritingExerciseFormPayload
            {
                Title = title,
                Topic = topic,
                PreviewText = previewText,
                FullVietnameseText = fullVietnameseText,
                FullEnglishText = fullEnglishText,
                IsPublished = true
            });

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        int createdExerciseId;

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var exercise = await context.WritingExercises
                .AsNoTracking()
                .Include(item => item.WritingExerciseSentences)
                .SingleAsync(item => item.Title == title);

            createdExerciseId = exercise.Id;

            Assert.True(exercise.IsPublished);
            Assert.Equal("beginner", exercise.Level);
            Assert.Equal("emails", exercise.ContentType);

            var sentences = exercise.WritingExerciseSentences
                .OrderBy(item => item.SortOrder)
                .Select(item => item.VietnameseText)
                .ToList();

            Assert.Equal(4, sentences.Count);
            Assert.Equal("Xin chao Lan:", sentences[0]);
            Assert.Equal("toi muon cap nhat lich.", sentences[1]);
            Assert.Equal("Chung ta can chot som;", sentences[2]);
            Assert.Equal("cam on ban.", sentences[3]);
        }

        var listBody = await browseClient.GetStringAsync(
            $"/Home/Writing/Exercises/Data?level=beginner&contentType=emails&topic={Uri.EscapeDataString(topic)}");

        using var listJson = JsonDocument.Parse(listBody);
        var listExercise = listJson.RootElement
            .GetProperty("exercises")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == createdExerciseId);

        Assert.Equal(title, listExercise.GetProperty("title").GetString());
        Assert.Equal(previewText, listExercise.GetProperty("previewText").GetString());
        Assert.Equal(4, listExercise.GetProperty("sentenceCount").GetInt32());

        using var practiceResponse = await learnerClient.GetAsync($"/Home/Writing/Practice/Data?exerciseId={createdExerciseId}");
        var practiceBody = await practiceResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, practiceResponse.StatusCode);

        using var practiceJson = JsonDocument.Parse(practiceBody);
        Assert.Equal(createdExerciseId, practiceJson.RootElement.GetProperty("exerciseId").GetInt32());
        Assert.Equal(title, practiceJson.RootElement.GetProperty("exerciseTitle").GetString());
        Assert.Equal(previewText, practiceJson.RootElement.GetProperty("exercisePreviewText").GetString());
        Assert.Equal(4, practiceJson.RootElement.GetProperty("totalSentenceCount").GetInt32());

        var firstSentence = practiceJson.RootElement.GetProperty("sentences")[0];
        Assert.Equal("Xin chao Lan:", firstSentence.GetProperty("vietnameseText").GetString());
        Assert.False(firstSentence.TryGetProperty("englishMeaning", out _));
    }

    [Fact]
    public async Task AdminWriting_EditPublishToggle_HidesAndRepublishesLearnerContent()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var adminClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        using var browseClient = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var learnerClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        var hiddenPayload = await LoadFormPayloadAsync(factory, exerciseId: 1);
        hiddenPayload.PreviewText = "Hidden from learners while the content is being reviewed.";
        hiddenPayload.IsPublished = false;

        using var hideResponse = await PostEditAsync(adminClient, exerciseId: 1, hiddenPayload);
        Assert.Equal(HttpStatusCode.Redirect, hideResponse.StatusCode);

        var hiddenListBody = await browseClient.GetStringAsync("/Home/Writing/Exercises/Data?level=beginner&contentType=emails");
        using var hiddenListJson = JsonDocument.Parse(hiddenListBody);
        var hiddenIds = hiddenListJson.RootElement
            .GetProperty("exercises")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt32())
            .ToList();

        Assert.DoesNotContain(1, hiddenIds);

        using var hiddenPracticeResponse = await learnerClient.GetAsync("/Home/Writing/Practice/Data?exerciseId=1");
        Assert.Equal(HttpStatusCode.NotFound, hiddenPracticeResponse.StatusCode);

        var publishedPayload = await LoadFormPayloadAsync(factory, exerciseId: 1);
        publishedPayload.PreviewText = "Published again after admin review.";
        publishedPayload.IsPublished = true;

        using var publishResponse = await PostEditAsync(adminClient, exerciseId: 1, publishedPayload);
        Assert.Equal(HttpStatusCode.Redirect, publishResponse.StatusCode);

        var visibleListBody = await browseClient.GetStringAsync(
            "/Home/Writing/Exercises/Data?level=beginner&contentType=emails&topic=Personal%20Check-In");
        using var visibleListJson = JsonDocument.Parse(visibleListBody);
        var republishedExercise = visibleListJson.RootElement
            .GetProperty("exercises")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == 1);

        Assert.Equal("Published again after admin review.", republishedExercise.GetProperty("previewText").GetString());

        using var visiblePracticeResponse = await learnerClient.GetAsync("/Home/Writing/Practice/Data?exerciseId=1");
        var visiblePracticeBody = await visiblePracticeResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, visiblePracticeResponse.StatusCode);

        using var visiblePracticeJson = JsonDocument.Parse(visiblePracticeBody);
        Assert.Equal("Published again after admin review.", visiblePracticeJson.RootElement.GetProperty("exercisePreviewText").GetString());
    }

    [Fact]
    public async Task AdminWriting_CreateDraftExercise_RemainsHiddenFromLearners()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var adminClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        using var browseClient = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var learnerClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        const string title = "Phase 7 Draft Check";
        const string topic = "Draft Only";
        const string previewText = "Draft content should stay off the learner surface until published.";

        using var createResponse = await PostCreateAsync(
            adminClient,
            new WritingExerciseFormPayload
            {
                Title = title,
                Topic = topic,
                PreviewText = previewText,
                FullVietnameseText = "Xin chao Minh. Toi dang luu ban nhap.",
                FullEnglishText = "Hello Minh. I am saving a draft.",
                IsPublished = false
            });

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        int createdExerciseId;

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var exercise = await context.WritingExercises
                .AsNoTracking()
                .SingleAsync(item => item.Title == title);

            createdExerciseId = exercise.Id;

            Assert.False(exercise.IsPublished);
            Assert.Equal(topic, exercise.Topic);
            Assert.Equal(previewText, exercise.PreviewText);
        }

        var listBody = await browseClient.GetStringAsync(
            $"/Home/Writing/Exercises/Data?level=beginner&contentType=emails&topic={Uri.EscapeDataString(topic)}");

        using var listJson = JsonDocument.Parse(listBody);
        var visibleIds = listJson.RootElement
            .GetProperty("exercises")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt32())
            .ToList();

        Assert.DoesNotContain(createdExerciseId, visibleIds);

        using var practiceResponse = await learnerClient.GetAsync($"/Home/Writing/Practice/Data?exerciseId={createdExerciseId}");
        Assert.Equal(HttpStatusCode.NotFound, practiceResponse.StatusCode);
    }

    [Fact]
    public async Task AdminWriting_EditFullText_ResplitsSentencesAndPreservesExistingSentenceIds()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var adminClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);

        var payload = await LoadFormPayloadAsync(factory, exerciseId: 1);
        List<int> originalSentenceIds;
        int trackedAttemptId;

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            originalSentenceIds = await context.WritingExerciseSentences
                .AsNoTracking()
                .Where(item => item.WritingExerciseId == 1)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .Select(item => item.Id)
                .ToListAsync();

            var trackedAttempt = new UserWritingAttempt
            {
                UserId = TestDataIds.UserId,
                WritingExerciseId = 1,
                WritingExerciseSentenceId = originalSentenceIds[0],
                SubmittedAnswer = "Hello there.",
                Passed = true,
                UsedAi = false,
                EvaluationSource = "rule-based",
                SummaryTitle = "Looks good",
                SummaryText = "Kept for regression coverage.",
                ReviewText = "Initial sentence attempt should survive admin edits.",
                CreatedAtUtc = DateTime.UtcNow
            };

            context.UserWritingAttempts.Add(trackedAttempt);
            await context.SaveChangesAsync();
            trackedAttemptId = trackedAttempt.Id;
        }

        payload.Title = "Updated Full Text Check";
        payload.Topic = "Retention Check";
        payload.PreviewText = "Editing full text should preserve stable sentence ids.";
        payload.FullVietnameseText = "Xin chao Minh. Hom nay toi gui ban ban cap nhat.\n\nChung ta can chot lich. Cam on ban.";
        payload.FullEnglishText = "Hello Minh. Today I am sending you an update.\n\nWe need to finalize the schedule. Thank you.";
        payload.IsPublished = true;

        using var editResponse = await PostEditAsync(adminClient, exerciseId: 1, payload);
        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var exercise = await context.WritingExercises
                .AsNoTracking()
                .Include(item => item.WritingExerciseSentences)
                .SingleAsync(item => item.Id == 1);

            var updatedSentences = exercise.WritingExerciseSentences
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .ToList();

            Assert.Equal("Updated Full Text Check", exercise.Title);
            Assert.Equal("Retention Check", exercise.Topic);
            Assert.Equal("Editing full text should preserve stable sentence ids.", exercise.PreviewText);
            Assert.Equal(4, updatedSentences.Count);
            Assert.Equal(originalSentenceIds.Take(4).ToList(), updatedSentences.Select(item => item.Id).ToList());

            Assert.Collection(
                updatedSentences,
                sentence =>
                {
                    Assert.Equal("Xin chao Minh.", sentence.VietnameseText);
                    Assert.Equal("Hello Minh.", sentence.EnglishMeaning);
                    Assert.False(sentence.BreakAfter);
                },
                sentence =>
                {
                    Assert.Equal("Hom nay toi gui ban ban cap nhat.", sentence.VietnameseText);
                    Assert.Equal("Today I am sending you an update.", sentence.EnglishMeaning);
                    Assert.True(sentence.BreakAfter);
                },
                sentence =>
                {
                    Assert.Equal("Chung ta can chot lich.", sentence.VietnameseText);
                    Assert.Equal("We need to finalize the schedule.", sentence.EnglishMeaning);
                    Assert.False(sentence.BreakAfter);
                },
                sentence =>
                {
                    Assert.Equal("Cam on ban.", sentence.VietnameseText);
                    Assert.Equal("Thank you.", sentence.EnglishMeaning);
                    Assert.True(sentence.BreakAfter);
                });

            var trackedAttempt = await context.UserWritingAttempts
                .AsNoTracking()
                .SingleAsync(item => item.Id == trackedAttemptId);

            Assert.Equal(originalSentenceIds[0], trackedAttempt.WritingExerciseSentenceId);
            Assert.Contains(updatedSentences, sentence => sentence.Id == trackedAttempt.WritingExerciseSentenceId);
        }
    }

    [Fact]
    public async Task AdminWriting_DeleteExercise_RemovesLearnerVisibility()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var adminClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
        using var browseClient = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var learnerClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var deleteResponse = await PostDeleteAsync(adminClient, exerciseId: 1);
        Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            Assert.False(await context.WritingExercises.AsNoTracking().AnyAsync(item => item.Id == 1));
        }

        var visibleListBody = await browseClient.GetStringAsync("/Home/Writing/Exercises/Data?level=beginner&contentType=emails");
        using var visibleListJson = JsonDocument.Parse(visibleListBody);
        var visibleIds = visibleListJson.RootElement
            .GetProperty("exercises")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt32())
            .ToList();

        Assert.DoesNotContain(1, visibleIds);

        using var practiceResponse = await learnerClient.GetAsync("/Home/Writing/Practice/Data?exerciseId=1");
        Assert.Equal(HttpStatusCode.NotFound, practiceResponse.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostCreateAsync(HttpClient client, WritingExerciseFormPayload payload)
    {
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            "/Admin/WritingExerciseManagement/Create");

        return await client.PostAsync(
            "/Admin/WritingExerciseManagement/Create",
            BuildFormContent(payload, antiForgeryToken));
    }

    private static async Task<HttpResponseMessage> PostEditAsync(HttpClient client, int exerciseId, WritingExerciseFormPayload payload)
    {
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Admin/WritingExerciseManagement/Edit/{exerciseId}");

        return await client.PostAsync(
            $"/Admin/WritingExerciseManagement/Edit/{exerciseId}",
            BuildFormContent(payload, antiForgeryToken));
    }

    private static async Task<HttpResponseMessage> PostDeleteAsync(HttpClient client, int exerciseId)
    {
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            "/Admin/WritingExerciseManagement");

        return await client.PostAsync(
            "/Admin/WritingExerciseManagement/Delete",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken),
                new KeyValuePair<string, string>("id", exerciseId.ToString())
            ]));
    }

    private static FormUrlEncodedContent BuildFormContent(WritingExerciseFormPayload payload, string antiForgeryToken)
    {
        var values = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", antiForgeryToken),
            new("Id", payload.Id.ToString()),
            new("Title", payload.Title),
            new("Level", payload.Level),
            new("ContentType", payload.ContentType),
            new("Topic", payload.Topic),
            new("PreviewText", payload.PreviewText),
            new("FullVietnameseText", payload.FullVietnameseText),
            new("FullEnglishText", payload.FullEnglishText),
            new("IsPublished", payload.IsPublished ? "true" : "false")
        };

        return new FormUrlEncodedContent(values);
    }

    private static async Task<WritingExerciseFormPayload> LoadFormPayloadAsync(TestWebApplicationFactory factory, int exerciseId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var exercise = await context.WritingExercises
            .AsNoTracking()
            .Include(item => item.WritingExerciseSentences)
            .SingleAsync(item => item.Id == exerciseId);

        var orderedSentences = exercise.WritingExerciseSentences
            .OrderBy(item => item.SortOrder)
            .ToList();

        return new WritingExerciseFormPayload
        {
            Id = exercise.Id,
            Title = exercise.Title,
            Level = exercise.Level,
            ContentType = exercise.ContentType,
            Topic = exercise.Topic,
            PreviewText = exercise.PreviewText,
            FullVietnameseText = BuildFullText(orderedSentences, item => item.VietnameseText),
            FullEnglishText = BuildFullText(orderedSentences, item => item.EnglishMeaning),
            IsPublished = exercise.IsPublished
        };
    }

    private static string BuildFullText(
        IReadOnlyList<WritingExerciseSentence> sentences,
        Func<WritingExerciseSentence, string> selector)
    {
        var paragraphs = new List<string>();
        var currentParagraph = new List<string>();

        foreach (var sentence in sentences)
        {
            currentParagraph.Add(selector(sentence));

            if (sentence.BreakAfter)
            {
                paragraphs.Add(string.Join(" ", currentParagraph));
                currentParagraph.Clear();
            }
        }

        if (currentParagraph.Count > 0)
        {
            paragraphs.Add(string.Join(" ", currentParagraph));
        }

        return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
    }

    private sealed class WritingExerciseFormPayload
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Level { get; set; } = "beginner";
        public string ContentType { get; set; } = "emails";
        public string Topic { get; set; } = string.Empty;
        public string PreviewText { get; set; } = string.Empty;
        public string FullVietnameseText { get; set; } = string.Empty;
        public string FullEnglishText { get; set; } = string.Empty;
        public bool IsPublished { get; set; }
    }
}
