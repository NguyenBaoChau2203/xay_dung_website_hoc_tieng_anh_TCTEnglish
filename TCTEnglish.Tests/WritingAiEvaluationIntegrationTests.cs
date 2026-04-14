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
    public async Task WritingEvaluate_WhenProviderReturnsValidJson_UsesAiEvaluationWithShortLearnerFacingReview()
    {
        IReadOnlyList<AiContextMessage>? capturedMessages = null;

        await using var factory = CreateFactory((messages, _) =>
        {
            capturedMessages = messages;

            return Task.FromResult(new AiProviderReply(
                """
                {
                  "passed": true,
                  "overallFeedback": "Câu này đã đạt yêu cầu.",
                  "meaningFeedback": "Ý chính đã đúng.",
                  "grammarFeedback": "Ngữ pháp ổn.",
                  "naturalnessFeedback": "Câu khá tự nhiên.",
                  "wordChoiceFeedback": "Từ vựng phù hợp.",
                  "suggestedRewrite": ""
                }
                """,
                10,
                12,
                22,
                "test-model",
                "writing-ai-success"));
        });
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await SendEvaluateRequestAsync(client, 1, 1, "Hello!");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(capturedMessages);
        Assert.Equal(2, capturedMessages!.Count);
        Assert.Equal("system", capturedMessages[0].Role);
        Assert.Contains("one short sentence", capturedMessages[0].Content, StringComparison.Ordinal);
        Assert.Contains("around 30 words or less", capturedMessages[0].Content, StringComparison.Ordinal);
        Assert.Contains("If passed is false, suggestedRewrite should normally be one natural learner-facing English sentence.", capturedMessages[0].Content, StringComparison.Ordinal);
        Assert.Equal("user", capturedMessages[1].Role);
        Assert.Contains("Do not teach grammar theory.", capturedMessages[1].Content, StringComparison.Ordinal);
        Assert.Contains("Keep the feedback learner-facing.", capturedMessages[1].Content, StringComparison.Ordinal);
        Assert.Contains("\"suggestedRewrite\" is learner-facing and may be a concrete corrected English sentence.", capturedMessages[1].Content, StringComparison.Ordinal);

        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("usedAi").GetBoolean());
        Assert.Equal("ai", data.GetProperty("evaluationSource").GetString());
        Assert.Equal("Ý chính đã đúng.", data.GetProperty("meaningFeedback").GetString());
        Assert.Equal(string.Empty, data.GetProperty("suggestedRewrite").GetString());

        var reviewText = data.GetProperty("reviewText").GetString();
        Assert.NotNull(reviewText);
        Assert.Contains("Tổng quan: Câu này đã đạt yêu cầu.", reviewText, StringComparison.Ordinal);
        Assert.Contains("\nÝ nghĩa: Ý chính đã đúng.", reviewText, StringComparison.Ordinal);
        Assert.Contains("\nNgữ pháp: Ngữ pháp ổn.", reviewText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WritingEvaluate_WhenFailedAiEvaluationProvidesConcreteRewrite_KeepsAiRewriteVisible()
    {
        const string referenceAnswer = "I hope you are well.";

        await using var factory = CreateFactory((messages, _) =>
        {
            Assert.Contains(messages, message => message.Content.Contains(referenceAnswer, StringComparison.Ordinal));

            return Task.FromResult(new AiProviderReply(
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
                "writing-ai-rewrite-visible"));
        });
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await SendEvaluateRequestAsync(client, 1, 2, "I feel fine today.");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("teacher reference", body, StringComparison.OrdinalIgnoreCase);

        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("usedAi").GetBoolean());
        Assert.False(data.GetProperty("passed").GetBoolean());
        Assert.Equal("ai", data.GetProperty("evaluationSource").GetString());
        Assert.Equal(referenceAnswer, data.GetProperty("suggestedRewrite").GetString());
    }

    [Fact]
    public async Task WritingEvaluate_WhenFailedAiEvaluationReturnsEmptyRewrite_UsesReferenceFallbackRewrite()
    {
        const string referenceAnswer = "I hope you are well.";

        await using var factory = CreateFactory((_, _) =>
            Task.FromResult(new AiProviderReply(
                """
                {
                  "passed": false,
                  "overallFeedback": "Ý chính chưa sát.",
                  "meaningFeedback": "Cần gần hơn với ý hỏi thăm.",
                  "grammarFeedback": "Câu này còn cần chỉnh ngữ pháp.",
                  "naturalnessFeedback": "Cách diễn đạt này chưa tự nhiên.",
                  "wordChoiceFeedback": "Hãy chọn từ phù hợp hơn với ngữ cảnh.",
                  "suggestedRewrite": "   "
                }
                """,
                9,
                11,
                20,
                "test-model",
                "writing-ai-empty-rewrite")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await SendEvaluateRequestAsync(client, 1, 2, "I feel fine today.");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("usedAi").GetBoolean());
        Assert.False(data.GetProperty("passed").GetBoolean());
        Assert.Equal("ai", data.GetProperty("evaluationSource").GetString());
        Assert.Equal(referenceAnswer, data.GetProperty("suggestedRewrite").GetString());
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
    public async Task WritingEvaluate_WhenNarrationLeaksTeacherReference_FallsBackToRuleBasedAndKeepsRewriteLearnerFacing()
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
        Assert.DoesNotContain("teacher answer", body, StringComparison.OrdinalIgnoreCase);

        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.False(data.GetProperty("usedAi").GetBoolean());
        Assert.Equal("rule-based", data.GetProperty("evaluationSource").GetString());
        Assert.Equal(referenceAnswer, data.GetProperty("suggestedRewrite").GetString());
        Assert.DoesNotContain("Use the teacher answer", data.GetProperty("reviewText").GetString(), StringComparison.Ordinal);
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
