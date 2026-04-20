using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingProgressIntegrationTests
{
    [Fact]
    public async Task WritingProgress_ListAndJsonShowRealMetadataForTheCurrentUser()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        await SendEvaluateRequestAsync(client, 1, 1, "Hello!");

        var listResponse = await client.GetAsync("/Home/Writing/Exercises?level=beginner&contentType=emails&status=in-progress");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        var jsonBody = await client.GetStringAsync("/Home/Writing/Exercises/Data?level=beginner&contentType=emails&status=in-progress");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains("writing-status-badge", listBody, StringComparison.Ordinal);
        Assert.Contains("writing-attempt-badge", listBody, StringComparison.Ordinal);
        Assert.Contains("name=\"status\"", listBody, StringComparison.OrdinalIgnoreCase);

        using var json = JsonDocument.Parse(jsonBody);
        var firstExercise = json.RootElement.GetProperty("exercises")[0];

        Assert.True(json.RootElement.GetProperty("showProgressMetadata").GetBoolean());
        Assert.Equal("in-progress", json.RootElement.GetProperty("selectedStatus").GetString());
        Assert.Equal("in-progress", firstExercise.GetProperty("statusKey").GetString());
        Assert.Equal("Đang luyện", firstExercise.GetProperty("statusLabel").GetString());
        Assert.Equal(1, firstExercise.GetProperty("attemptCount").GetInt32());
        Assert.Equal(1, firstExercise.GetProperty("completedSentenceCount").GetInt32());
        Assert.Equal("Tiếp tục", firstExercise.GetProperty("startActionLabel").GetString());
        Assert.False(firstExercise.TryGetProperty("englishMeaning", out _));
    }

    [Fact]
    public async Task WritingProgress_PracticePageRestoresAcceptedSentencesAndResumePoint()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        await SendEvaluateRequestAsync(client, 1, 1, "Hello!");
        await SendEvaluateRequestAsync(client, 1, 2, "I hope you are well.");

        var response = await client.GetAsync("/Home/Writing/Practice?level=beginner&contentType=emails&exerciseId=1&status=in-progress");
        var body = await response.Content.ReadAsStringAsync();
        var practiceJsonBody = await client.GetStringAsync("/Home/Writing/Practice/Data?exerciseId=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("2/14 câu đã xong", body, StringComparison.Ordinal);
        Assert.Contains("data-resume-sentence-id=\"3\"", body, StringComparison.Ordinal);
        Assert.Contains(">Hello!<", body, StringComparison.Ordinal);
        Assert.Contains("Lần làm gần đây", body, StringComparison.Ordinal);

        using var practiceJson = JsonDocument.Parse(practiceJsonBody);
        Assert.Equal(2, practiceJson.RootElement.GetProperty("completedSentenceCount").GetInt32());
        Assert.Equal(2, practiceJson.RootElement.GetProperty("attemptCount").GetInt32());
        Assert.Equal(3, practiceJson.RootElement.GetProperty("resumeSentenceId").GetInt32());
        Assert.Equal("in-progress", practiceJson.RootElement.GetProperty("statusKey").GetString());

        var firstSentence = practiceJson.RootElement.GetProperty("sentences")[0];
        Assert.True(firstSentence.GetProperty("hasAccepted").GetBoolean());
        Assert.Equal("Hello!", firstSentence.GetProperty("acceptedAnswer").GetString());
        Assert.False(firstSentence.TryGetProperty("englishMeaning", out _));
    }

    [Fact]
    public async Task WritingProgress_IsScopedToTheCurrentUser()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var ownerClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var outsiderClient = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);

        await SendEvaluateRequestAsync(ownerClient, 1, 1, "Hello!");

        var outsiderJsonBody = await outsiderClient.GetStringAsync("/Home/Writing/Exercises/Data?level=beginner&contentType=emails&status=all");
        using var outsiderJson = JsonDocument.Parse(outsiderJsonBody);
        var firstExercise = outsiderJson.RootElement.GetProperty("exercises")[0];

        Assert.True(outsiderJson.RootElement.GetProperty("showProgressMetadata").GetBoolean());
        Assert.Equal("not-started", firstExercise.GetProperty("statusKey").GetString());
        Assert.Equal(0, firstExercise.GetProperty("attemptCount").GetInt32());
        Assert.Equal(0, firstExercise.GetProperty("completedSentenceCount").GetInt32());
    }

    private static async Task SendEvaluateRequestAsync(
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
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
    }
}
