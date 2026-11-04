using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Speaking;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>Positive offline speech-through-app coverage with fake OpenAI HTTP (no paid calls).</summary>
public sealed class OpenAiSpeechAssessmentAppTests
{
    [Fact]
    public async Task Assess_FakeOpenAiHttp_ThenProgressCommitAndReplay_KeepOneCredit()
    {
        var handler = new TranscriptSequenceHandler(
            "Good morning everyone", "Let's start the meeting", "Please share your updates");
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IOpenAiSpeechCredentialResolver>();
            services.AddSingleton<IOpenAiSpeechCredentialResolver>(new ReadyCredentials());
            services.RemoveAll<ISpeechTranscriber>();
            services.AddSingleton<ISpeechTranscriber>(sp =>
            {
                var client = new HttpClient(handler, disposeHandler: false)
                {
                    BaseAddress = new Uri("https://api.openai.com/v1/")
                };
                return new OpenAiSpeechTranscriber(
                    client,
                    sp.GetRequiredService<IOptions<SpeechProcessingOptions>>(),
                    sp.GetRequiredService<IOpenAiSpeechCredentialResolver>(),
                    sp.GetRequiredService<ISpeechInferenceLimiter>(),
                    NullLogger<OpenAiSpeechTranscriber>.Instance);
            });
        });
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client, $"/Speaking/Practice?id={TestDataIds.SpeakingVideoId}");

        var sentenceIds = new[]
        {
            TestDataIds.SpeakingSentenceOneId,
            TestDataIds.SpeakingSentenceTwoId,
            TestDataIds.SpeakingSentenceThreeId
        };

        Guid lastAssessment = default;
        foreach (var sentenceId in sentenceIds)
        {
            using var assessed = await SpeakingAssessmentTestHelper.PostAssessAsync(
                client,
                sentenceId,
                token,
                SpeakingAssessmentTestHelper.CreateSilentPcm16Mono16kWav(0.4));
            var body = await assessed.Content.ReadAsStringAsync();
            Assert.True(assessed.StatusCode == HttpStatusCode.OK, body);
            using var json = JsonDocument.Parse(body);
            Assert.True(json.RootElement.GetProperty("speechDetected").GetBoolean());
            Assert.True(json.RootElement.GetProperty("canCommitProgress").GetBoolean());
            lastAssessment = json.RootElement.GetProperty("assessmentId").GetGuid();

            using var progress = new HttpRequestMessage(HttpMethod.Post, $"/api/speaking/{sentenceId}/progress")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { assessmentId = lastAssessment }),
                    Encoding.UTF8,
                    "application/json")
            };
            progress.Headers.Add("RequestVerificationToken", token);
            using var committed = await client.SendAsync(progress);
            Assert.True(committed.StatusCode == HttpStatusCode.OK, await committed.Content.ReadAsStringAsync());
        }

        var beforeReplay = await ReadCompletionStateAsync(factory);
        Assert.Equal(1, beforeReplay.CreditCount);
        Assert.True(beforeReplay.XpAwarded > 0);
        Assert.Equal(1, beforeReplay.CompletedVideos);

        using var replay = new HttpRequestMessage(HttpMethod.Post, $"/api/speaking/{sentenceIds[^1]}/progress")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { assessmentId = lastAssessment }),
                Encoding.UTF8,
                "application/json")
        };
        replay.Headers.Add("RequestVerificationToken", token);
        using var replayResponse = await client.SendAsync(replay);
        Assert.Contains(replayResponse.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        Assert.Equal(3, handler.Calls);
        Assert.Equal(beforeReplay, await ReadCompletionStateAsync(factory));
    }

    private static async Task<(int CreditCount, int XpAwarded, int CompletedVideos)> ReadCompletionStateAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var credits = await db.LearningCompletionCredits.AsNoTracking()
            .Where(c => c.UserId == TestDataIds.UserId && c.Feature == "Speaking")
            .ToListAsync();
        var completions = await db.UserSpeakingVideoCompletions.AsNoTracking()
            .CountAsync(c => c.UserId == TestDataIds.UserId && c.VideoId == TestDataIds.SpeakingVideoId && c.IsCompleted);
        return (credits.Count, credits.Sum(c => c.XpAwarded), completions);
    }

    private sealed class ReadyCredentials : IOpenAiSpeechCredentialResolver
    {
        public bool TryResolve(out OpenAiSpeechCredentials credentials)
        {
            credentials = new OpenAiSpeechCredentials(
                "test-key",
                "test",
                "gpt-transcribe",
                new Uri("https://api.openai.com/v1/"));
            return true;
        }
    }

    private sealed class TranscriptSequenceHandler(params string[] transcripts) : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref Calls);
            Assert.InRange(call, 1, transcripts.Length);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { text = transcripts[call - 1] }), Encoding.UTF8, "application/json")
            });
        }
    }
}
