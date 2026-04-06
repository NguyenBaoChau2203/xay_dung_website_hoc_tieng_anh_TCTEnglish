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
