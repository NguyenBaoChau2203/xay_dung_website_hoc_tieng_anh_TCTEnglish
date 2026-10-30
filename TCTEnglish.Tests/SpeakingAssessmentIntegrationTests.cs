using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services.Speaking;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public class SpeakingAssessmentIntegrationTests
{
    [Fact]
    public async Task Progress_WithoutAssessmentId_Returns409AssessmentRequired()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Speaking/Practice?id={TestDataIds.SpeakingVideoId}");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/speaking/{TestDataIds.SpeakingSentenceOneId}/progress")
        {
            Content = new StringContent(
                """{"totalScore":99,"accuracyScore":99,"fluencyScore":99,"completenessScore":99}""",
                System.Text.Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("RequestVerificationToken", token);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("assessment_required", json.RootElement.GetProperty("code").GetString());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.False(await context.UserSpeakingProgresses.AnyAsync(p =>
            p.UserId == TestDataIds.UserId && p.SentenceId == TestDataIds.SpeakingSentenceOneId));
    }

    [Fact]
    public async Task Progress_WithServerAssessment_IgnoresClientScores()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Speaking/Practice?id={TestDataIds.SpeakingVideoId}");

        using var request = await SpeakingAssessmentTestHelper.CreateSpeakingProgressRequestAsync(
            factory,
            TestDataIds.UserId,
            TestDataIds.SpeakingSentenceOneId,
            token,
            textMatchScore: 78);

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(78, json.RootElement.GetProperty("totalScore").GetDouble());
        Assert.False(json.RootElement.GetProperty("fluencyAvailable").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var progress = await context.UserSpeakingProgresses.SingleAsync(p =>
            p.UserId == TestDataIds.UserId && p.SentenceId == TestDataIds.SpeakingSentenceOneId);
        Assert.Equal(78, progress.TotalScore);
        Assert.Equal(0, progress.FluencyScore);
    }

    [Fact]
    public async Task Assess_WhenEngineNotReady_Returns503WithoutProgress()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Speaking/Practice?id={TestDataIds.SpeakingVideoId}");

        using var response = await SpeakingAssessmentTestHelper.PostAssessAsync(
            client,
            TestDataIds.SpeakingSentenceOneId,
            token,
            SpeakingAssessmentTestHelper.CreateSilentPcm16Mono16kWav());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal("engine_not_ready", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Assess_WithStubTranscriber_NoSpeech_DoesNotAllowCommit()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<ISpeechTranscriber>();
            services.AddSingleton<ISpeechTranscriber>(new StubSpeechTranscriber(SpeechTranscriptionStatus.NoSpeech, string.Empty));
        });
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Speaking/Practice?id={TestDataIds.SpeakingVideoId}");

        using var response = await SpeakingAssessmentTestHelper.PostAssessAsync(
            client,
            TestDataIds.SpeakingSentenceOneId,
            token,
            SpeakingAssessmentTestHelper.CreateSilentPcm16Mono16kWav(0.3));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.False(json.RootElement.GetProperty("canCommitProgress").GetBoolean());
        Assert.False(json.RootElement.GetProperty("speechDetected").GetBoolean());
    }

    [Fact]
    public async Task Assess_WithMatchingStub_ThenProgress_PersistsServerScore()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<ISpeechTranscriber>();
            services.AddSingleton<ISpeechTranscriber>(
                new StubSpeechTranscriber(SpeechTranscriptionStatus.Ok, "Good morning everyone"));
        });
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Speaking/Practice?id={TestDataIds.SpeakingVideoId}");

        using var assessResponse = await SpeakingAssessmentTestHelper.PostAssessAsync(
            client,
            TestDataIds.SpeakingSentenceOneId,
            token,
            SpeakingAssessmentTestHelper.CreateSilentPcm16Mono16kWav(0.3));
        Assert.Equal(HttpStatusCode.OK, assessResponse.StatusCode);
        var assessBody = await assessResponse.Content.ReadAsStringAsync();
        using var assessJson = JsonDocument.Parse(assessBody);
        Assert.True(assessJson.RootElement.GetProperty("canCommitProgress").GetBoolean());
        var assessmentId = assessJson.RootElement.GetProperty("assessmentId").GetGuid();

        using var progressRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/speaking/{TestDataIds.SpeakingSentenceOneId}/progress")
        {
            Content = new StringContent(
                $$"""{"assessmentId":"{{assessmentId}}","totalScore":1,"accuracyScore":1,"fluencyScore":1,"completenessScore":1}""",
                System.Text.Encoding.UTF8,
                "application/json")
        };
        progressRequest.Headers.Add("RequestVerificationToken", token);
        using var progressResponse = await client.SendAsync(progressRequest);
        Assert.Equal(HttpStatusCode.OK, progressResponse.StatusCode);
        var progressBody = await progressResponse.Content.ReadAsStringAsync();
        using var progressJson = JsonDocument.Parse(progressBody);
        Assert.True(progressJson.RootElement.GetProperty("totalScore").GetDouble() >= 70);
    }

    [Fact]
    public async Task Progress_CreditInsertFailure_RollsBackCompletion()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client,
            $"/Speaking/Practice?id={TestDataIds.SpeakingVideoId}");

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER block_credit
                BEFORE INSERT ON LearningCompletionCredits
                BEGIN
                    SELECT RAISE(FAIL, 'block credit');
                END;
                """);
        }

        async Task<HttpResponseMessage> Commit(int sentenceId, double score)
        {
            using var request = await SpeakingAssessmentTestHelper.CreateSpeakingProgressRequestAsync(
                factory, TestDataIds.UserId, sentenceId, token, score);
            return await client.SendAsync(request);
        }

        using (var first = await Commit(TestDataIds.SpeakingSentenceOneId, 90))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using (var second = await Commit(TestDataIds.SpeakingSentenceTwoId, 90))
        {
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }

        using var blocked = await Commit(TestDataIds.SpeakingSentenceThreeId, 90);
        Assert.Equal(HttpStatusCode.InternalServerError, blocked.StatusCode);

        using var scopeAfter = factory.Services.CreateScope();
        var db = scopeAfter.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(0, await db.LearningCompletionCredits.CountAsync());
        Assert.False(await db.UserSpeakingVideoCompletions.AnyAsync(c =>
            c.UserId == TestDataIds.UserId && c.VideoId == TestDataIds.SpeakingVideoId && c.IsCompleted));
    }

    private sealed class StubSpeechTranscriber : ISpeechTranscriber
    {
        private readonly SpeechTranscriptionStatus _status;
        private readonly string _transcript;

        public StubSpeechTranscriber(SpeechTranscriptionStatus status, string transcript)
        {
            _status = status;
            _transcript = transcript;
        }

        public Task<SpeechTranscriptionResult> TranscribeAsync(Stream wavPcm16Mono16kHz, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SpeechTranscriptionResult
            {
                Status = _status,
                Transcript = _transcript,
                EngineName = "stub",
                ModelIdentity = "stub-fixture",
                Message = _status == SpeechTranscriptionStatus.NoSpeech ? "Chưa nhận được lời nói." : null
            });
        }
    }
}
