using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingAiEvaluationIntegrationTests
{
    [Fact]
    public async Task WritingEvaluate_WhenProviderReturnsValidJson_UsesAiEvaluation()
    {
        await using var factory = CreateFactory((_, _) => Task.FromResult(new AiProviderReply(
            """
            {
              "passed": true,
              "overallFeedback": "Overall good",
              "meaningFeedback": "Meaning ok",
              "grammarFeedback": "Grammar ok",
              "naturalnessFeedback": "Natural enough",
              "wordChoiceFeedback": "Word choice ok",
              "suggestedRewrite": ""
            }
            """,
            10,
            12,
            22,
            "test-model",
            "writing-ai-success")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await SendEvaluateRequestAsync(client, 1, 1, "Hello!");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("usedAi").GetBoolean());
        Assert.Equal("ai", data.GetProperty("evaluationSource").GetString());
        Assert.Equal("Meaning ok", data.GetProperty("meaningFeedback").GetString());
    }

    [Fact]
    public async Task WritingEvaluate_WhenProviderFails_FallsBackToRuleBasedEvaluation()
    {
        await using var factory = CreateFactory((_, _) =>
            throw new AiProviderException("AI provider unavailable.", AiProviderException.ErrorCodeProviderUnavailable, true));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await SendEvaluateRequestAsync(client, 1, 1, "Hello!");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.False(data.GetProperty("usedAi").GetBoolean());
        Assert.Equal("rule-based", data.GetProperty("evaluationSource").GetString());
    }

    [Fact]
    public async Task WritingEvaluate_WhenProviderReturnsInvalidJson_FallsBackToRuleBasedEvaluation()
    {
        await using var factory = CreateFactory((_, _) =>
            Task.FromResult(new AiProviderReply("not-json", 0, 0, 0, "test-model", "writing-ai-invalid-json")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await SendEvaluateRequestAsync(client, 1, 1, "Hello!");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.False(data.GetProperty("usedAi").GetBoolean());
        Assert.Equal("rule-based", data.GetProperty("evaluationSource").GetString());
    }

    [Fact]
    public async Task WritingEvaluate_WhenProviderEchoesReferenceAnswer_FallsBackWithoutLeakingReference()
    {
        const string referenceAnswer = "I hope you are well.";

        await using var factory = CreateFactory((messages, _) =>
        {
            Assert.Contains(messages, message => message.Content.Contains(referenceAnswer, StringComparison.Ordinal));

            return Task.FromResult(new AiProviderReply(
                """
                {
                  "passed": false,
                  "overallFeedback": "Use the teacher answer: I hope you are well.",
                  "meaningFeedback": "Need closer meaning",
                  "grammarFeedback": "Grammar needs work",
                  "naturalnessFeedback": "Sounds unnatural",
                  "wordChoiceFeedback": "Pick simpler words",
                  "suggestedRewrite": "I hope you are well."
                }
                """,
                8,
                9,
                17,
                "test-model",
                "writing-ai-leak"));
        });
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await SendEvaluateRequestAsync(client, 1, 2, "I feel fine today.");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(referenceAnswer, body, StringComparison.Ordinal);

        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.False(data.GetProperty("usedAi").GetBoolean());
        Assert.Equal("rule-based", data.GetProperty("evaluationSource").GetString());
    }

    private static TestWebApplicationFactory CreateFactory(
        Func<IReadOnlyList<AiContextMessage>, CancellationToken, Task<AiProviderReply>> handler)
    {
        return new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiProviderClient>();
            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient(handler));
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
