using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingPracticeValidationIntegrationTests
{
    [Fact]
    public async Task WritingEvaluate_WhenLearnerSubmitsAnotherSentenceFromSameExercise_RejectsItBeforeAiAndKeepsProgressIncomplete()
    {
        var aiCallCount = 0;

        await using var factory = CreateFactory(() =>
        {
            aiCallCount++;
            return Task.FromResult(new AiProviderReply(
                """
                {
                  "passed": true,
                  "overallFeedback": "Unexpected AI call.",
                  "meaningFeedback": "Unexpected AI call.",
                  "grammarFeedback": "Unexpected AI call.",
                  "naturalnessFeedback": "Unexpected AI call.",
                  "wordChoiceFeedback": "Unexpected AI call.",
                  "suggestedRewrite": ""
                }
                """,
                1,
                1,
                1,
                "test-model",
                "writing-scope-guard"));
        });
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await SendEvaluateRequestAsync(client, 1, 4, "I want to know how you are doing.");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, aiCallCount);

        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.False(data.GetProperty("passed").GetBoolean());
        Assert.False(data.GetProperty("canAutoAdvance").GetBoolean());
        Assert.False(data.GetProperty("usedAi").GetBoolean());
        Assert.Equal("rule-based", data.GetProperty("evaluationSource").GetString());
        Assert.Equal("Bạn đang trả lời sang câu khác", data.GetProperty("summaryTitle").GetString());
        Assert.DoesNotContain("englishMeaning", body, StringComparison.Ordinal);

        var practiceBody = await client.GetStringAsync("/Home/Writing/Practice/Data?exerciseId=1");
        using var practiceJson = JsonDocument.Parse(practiceBody);
        var sentence = practiceJson.RootElement
            .GetProperty("sentences")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == 4);

        Assert.False(sentence.GetProperty("hasAccepted").GetBoolean());
        Assert.Equal("I want to know how you are doing.", sentence.GetProperty("lastSubmittedAnswer").GetString());
        Assert.False(sentence.GetProperty("lastEvaluationPassed").GetBoolean());
        Assert.Equal(0, practiceJson.RootElement.GetProperty("completedSentenceCount").GetInt32());
    }

    [Fact]
    public async Task WritingEvaluate_WhenLearnerPastesMultipleSentences_RejectsItBeforeAiAndKeepsExerciseUnfinished()
    {
        var aiCallCount = 0;
        const string pastedAnswer = "I want to know how you are doing. How was your weekend? Did you do anything special?";

        await using var factory = CreateFactory(() =>
        {
            aiCallCount++;
            return Task.FromResult(new AiProviderReply(
                """
                {
                  "passed": true,
                  "overallFeedback": "Unexpected AI call.",
                  "meaningFeedback": "Unexpected AI call.",
                  "grammarFeedback": "Unexpected AI call.",
                  "naturalnessFeedback": "Unexpected AI call.",
                  "wordChoiceFeedback": "Unexpected AI call.",
                  "suggestedRewrite": ""
                }
                """,
                1,
                1,
                1,
                "test-model",
                "writing-scope-guard"));
        });
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await SendEvaluateRequestAsync(client, 1, 4, pastedAnswer);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, aiCallCount);

        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.False(data.GetProperty("passed").GetBoolean());
        Assert.False(data.GetProperty("usedAi").GetBoolean());
        Assert.Equal("Chỉ gửi một câu mỗi lần", data.GetProperty("summaryTitle").GetString());

        var practiceBody = await client.GetStringAsync("/Home/Writing/Practice/Data?exerciseId=1");
        using var practiceJson = JsonDocument.Parse(practiceBody);

        Assert.Equal(0, practiceJson.RootElement.GetProperty("completedSentenceCount").GetInt32());

        var sentence = practiceJson.RootElement
            .GetProperty("sentences")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == 4);

        Assert.False(sentence.GetProperty("hasAccepted").GetBoolean());
        Assert.Equal(pastedAnswer, sentence.GetProperty("lastSubmittedAnswer").GetString());
        Assert.False(sentence.GetProperty("lastEvaluationPassed").GetBoolean());
    }

    private static TestWebApplicationFactory CreateFactory(Func<Task<AiProviderReply>> handler)
    {
        return new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiProviderClient>();
            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, _) => handler()));
        });
    }

    private static async Task<HttpResponseMessage> SendEvaluateRequestAsync(
        HttpClient client,
        int exerciseId,
        int sentenceId,
        string userAnswer)
    {
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId={exerciseId}");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        {
            Content = JsonContent.Create(new
            {
                exerciseId,
                sentenceId,
                userAnswer
            })
        };

        request.Headers.Add("RequestVerificationToken", antiForgeryToken);
        return await client.SendAsync(request);
    }
}
