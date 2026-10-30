using System.Net;
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

public sealed class WritingAvailabilityIntegrationTests
{
    [Fact]
    public async Task WritingEvaluate_LocalMode_ExactAnswer_DoesNotCallAiProvider_AndPersistsAccepted()
    {
        var aiCallCount = 0;

        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiProviderClient>();
            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, _, _) =>
            {
                Interlocked.Increment(ref aiCallCount);
                throw new InvalidOperationException("AI must not be called in Local mode.");
            }));
        });
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var hintResponse = await client.GetAsync("/Home/Writing/Practice/Hint?exerciseId=1&sentenceId=1");
        Assert.Equal(HttpStatusCode.OK, hintResponse.StatusCode);
        var hintBody = await hintResponse.Content.ReadAsStringAsync();
        using var hintJson = JsonDocument.Parse(hintBody);
        var reference = hintJson.RootElement.TryGetProperty("data", out var hintData)
            ? hintData.GetProperty("hintText").GetString()
            : hintJson.RootElement.GetProperty("hintText").GetString();
        Assert.False(string.IsNullOrWhiteSpace(reference));

        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            "/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId=1");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        {
            Content = JsonContent.Create(new
            {
                exerciseId = 1,
                sentenceId = 1,
                userAnswer = reference
            })
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, aiCallCount);

        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("passed").GetBoolean());
        Assert.False(data.GetProperty("usedAi").GetBoolean());
        Assert.Equal("Accepted", data.GetProperty("assessmentStatus").GetString());
        Assert.Equal("local-exact", data.GetProperty("assessmentSource").GetString());
        Assert.False(data.GetProperty("reviewRequired").GetBoolean());

        var reloadBody = await client.GetStringAsync("/Home/Writing/Practice/Data?exerciseId=1");
        using var reloadJson = JsonDocument.Parse(reloadBody);
        var reloaded = reloadJson.RootElement
            .GetProperty("sentences")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == 1);
        Assert.True(reloaded.GetProperty("hasAccepted").GetBoolean());
        Assert.True(reloaded.GetProperty("lastEvaluationPassed").GetBoolean());
    }

    [Fact]
    public async Task WritingEvaluate_LocalMode_Paraphrase_NeedsReview_WithoutCallingAi()
    {
        var aiCallCount = 0;

        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiProviderClient>();
            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, _, _) =>
            {
                Interlocked.Increment(ref aiCallCount);
                return Task.FromResult(new AiProviderReply("{}", 0, 0, 0, "x", "x"));
            }));
        });
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            "/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId=1");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/Writing/Practice/Evaluate")
        {
            Content = JsonContent.Create(new
            {
                exerciseId = 1,
                sentenceId = 1,
                userAnswer = "This is a totally different sentence that should need review."
            })
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, aiCallCount);

        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        Assert.False(data.GetProperty("passed").GetBoolean());
        Assert.Equal("NeedsReview", data.GetProperty("assessmentStatus").GetString());
        Assert.True(data.GetProperty("reviewRequired").GetBoolean());
        Assert.False(data.GetProperty("canAutoAdvance").GetBoolean());
    }
}
