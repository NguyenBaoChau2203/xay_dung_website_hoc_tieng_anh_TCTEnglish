using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingPracticePersistenceIntegrationTests
{
    private const string FailedRewrite = "I hope you are well.";

    [Fact]
    public async Task WritingPracticeData_AfterSubmit_RestoresLastEvaluationSnapshotOnReload()
    {
        await using var factory = CreateFactoryWithFailedRewrite();
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var submitResponse = await SendEvaluateRequestAsync(client, 1, 2, "I feel fine today.");
        var submitBody = await submitResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        Assert.Contains("\"success\":true", submitBody, StringComparison.Ordinal);

        var practiceBody = await client.GetStringAsync("/Home/Writing/Practice/Data?exerciseId=1");
        using var practiceJson = JsonDocument.Parse(practiceBody);
        var restoredSentence = practiceJson.RootElement
            .GetProperty("sentences")
            .EnumerateArray()
            .Single(sentence => sentence.GetProperty("id").GetInt32() == 2);

        Assert.Equal("I feel fine today.", restoredSentence.GetProperty("lastSubmittedAnswer").GetString());
        Assert.False(restoredSentence.GetProperty("lastEvaluationPassed").GetBoolean());

        var lastEvaluation = restoredSentence.GetProperty("lastEvaluation");
        Assert.False(lastEvaluation.GetProperty("passed").GetBoolean());
        Assert.True(lastEvaluation.GetProperty("usedAi").GetBoolean());
        Assert.Equal("ai", lastEvaluation.GetProperty("evaluationSource").GetString());
        Assert.Equal("Hãy sửa lại câu này", lastEvaluation.GetProperty("summaryTitle").GetString());
        Assert.Contains("Ý nghĩa:", lastEvaluation.GetProperty("reviewText").GetString(), StringComparison.Ordinal);
        Assert.Equal(FailedRewrite, lastEvaluation.GetProperty("suggestedRewrite").GetString());
    }

    [Fact]
    public async Task WritingPractice_AfterSubmit_EmbedsPersistedRewriteInInitialPayload()
    {
        await using var factory = CreateFactoryWithFailedRewrite();
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var submitResponse = await SendEvaluateRequestAsync(client, 1, 2, "I feel fine today.");

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        using var practiceResponse = await client.GetAsync("/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId=1");
        var practiceHtml = await practiceResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, practiceResponse.StatusCode);

        using var payloadJson = JsonDocument.Parse(ExtractPracticePayload(practiceHtml));
        var restoredSentence = payloadJson.RootElement
            .EnumerateArray()
            .Single(sentence => sentence.GetProperty("id").GetInt32() == 2);
        var lastEvaluation = restoredSentence.GetProperty("lastEvaluation");

        Assert.False(lastEvaluation.GetProperty("passed").GetBoolean());
        Assert.Equal(FailedRewrite, lastEvaluation.GetProperty("suggestedRewrite").GetString());
        Assert.Contains("Tổng quan:", lastEvaluation.GetProperty("reviewText").GetString(), StringComparison.Ordinal);
    }

    private static TestWebApplicationFactory CreateFactoryWithFailedRewrite()
    {
        return new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiProviderClient>();
            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, _) =>
                Task.FromResult(new AiProviderReply(
                    """
                    {
                      "passed": false,
                      "overallFeedback": "Ý chính chưa sát.",
                      "meaningFeedback": "Cần gần hơn với ý hỏi thăm.",
                      "grammarFeedback": "Câu này còn cần chỉnh ngữ pháp.",
                      "naturalnessFeedback": "Cách diễn đạt này chưa tự nhiên.",
                      "wordChoiceFeedback": "Hãy chọn từ phù hợp hơn với ngữ cảnh.",
                      "suggestedRewrite": "I hope you are well."
                    }
                    """,
                    11,
                    14,
                    25,
                    "test-model",
                    "writing-persistence"))));
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

    private static string ExtractPracticePayload(string practiceHtml)
    {
        const string startTag = "<script id=\"writingPracticeData\" type=\"application/json\">";
        const string endTag = "</script>";

        var startIndex = practiceHtml.IndexOf(startTag, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, "Expected the writing practice payload script tag to be present.");

        startIndex += startTag.Length;
        var endIndex = practiceHtml.IndexOf(endTag, startIndex, StringComparison.Ordinal);
        Assert.True(endIndex >= 0, "Expected the writing practice payload script tag to be closed.");

        return practiceHtml[startIndex..endIndex];
    }
}
