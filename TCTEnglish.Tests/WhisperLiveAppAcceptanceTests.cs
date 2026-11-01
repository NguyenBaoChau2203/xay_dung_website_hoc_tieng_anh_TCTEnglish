using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Services.Speaking;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WhisperLiveAppAcceptanceTests
{
    [LiveSpeechFact]
    public async Task SpeechThroughApp_CompletionAndReplay_KeepOneCreditAfterReload()
    {
        var endpoint = AsrLiveAcceptanceFactAttribute.ParseEndpoint(
            Environment.GetEnvironmentVariable("TCT_ASR_LIVE_BASEURL"))!;
        var wav = await File.ReadAllBytesAsync(
            Environment.GetEnvironmentVariable("TCT_ASR_LIVE_SPEECH_WAV")!);
        Assert.InRange(wav.Length, 44, 2 * 1024 * 1024);

        await using var factory = new TestWebApplicationFactory(services =>
            services.PostConfigure<SpeechProcessingOptions>(options =>
            {
                options.WhisperHttpBaseUrl = endpoint.AbsoluteUri.TrimEnd('/');
                options.ModelIdentity = "live-app-acceptance";
            }));
        await factory.InitializeAsync();
        var sentenceIds = new[]
        {
            TestDataIds.SpeakingSentenceOneId,
            TestDataIds.SpeakingSentenceTwoId,
            TestDataIds.SpeakingSentenceThreeId
        };
        using (var scope = factory.Services.CreateScope())
        {
            Assert.IsType<WhisperHttpTranscriber>(scope.ServiceProvider.GetRequiredService<ISpeechTranscriber>());
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            foreach (var sentence in await db.SpeakingSentences.Where(s => sentenceIds.Contains(s.Id)).ToListAsync())
                sentence.Text = "Good morning everyone";
            await db.SaveChangesAsync();
        }

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(
            client, $"/Speaking/Practice?id={TestDataIds.SpeakingVideoId}");

        async Task<HttpResponseMessage> Commit(int sentenceId, Guid assessmentId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/speaking/{sentenceId}/progress")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { assessmentId }), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("RequestVerificationToken", token);
            return await client.SendAsync(request);
        }

        Guid lastAssessment = default;
        foreach (var sentenceId in sentenceIds)
        {
            using var assessed = await SpeakingAssessmentTestHelper.PostAssessAsync(client, sentenceId, token, wav);
            var assessedBody = await assessed.Content.ReadAsStringAsync();
            Assert.True(assessed.StatusCode == HttpStatusCode.OK, assessedBody);
            using var assessment = JsonDocument.Parse(assessedBody);
            Assert.True(assessment.RootElement.GetProperty("speechDetected").GetBoolean());
            Assert.True(assessment.RootElement.GetProperty("canCommitProgress").GetBoolean());
            lastAssessment = assessment.RootElement.GetProperty("assessmentId").GetGuid();
            using var committed = await Commit(sentenceId, lastAssessment);
            Assert.True(committed.StatusCode == HttpStatusCode.OK, await committed.Content.ReadAsStringAsync());
        }

        using var replay = await Commit(sentenceIds[^1], lastAssessment);
        Assert.Contains(replay.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        using var reload = await client.GetAsync($"/Speaking/Practice?id={TestDataIds.SpeakingVideoId}");
        Assert.Equal(HttpStatusCode.OK, reload.StatusCode);

        using var after = factory.Services.CreateScope();
        var saved = after.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var progress = await saved.UserSpeakingProgresses.AsNoTracking().Where(p =>
            p.UserId == TestDataIds.UserId && sentenceIds.Contains(p.SentenceId)).ToListAsync();
        Assert.Equal(3, progress.Count);
        Assert.All(progress, p => Assert.InRange(p.TotalScore, 80, 100));
        Assert.Equal(3, await saved.SpeakingAssessments.CountAsync(a => a.UserId == TestDataIds.UserId && a.EngineName == "whisper.cpp"));
        Assert.True(await saved.UserSpeakingVideoCompletions.AnyAsync(c =>
            c.UserId == TestDataIds.UserId && c.VideoId == TestDataIds.SpeakingVideoId && c.IsCompleted));
        Assert.Equal(1, await saved.LearningCompletionCredits.CountAsync());
    }

    private sealed class LiveSpeechFactAttribute : FactAttribute
    {
        public LiveSpeechFactAttribute()
        {
            if (AsrLiveAcceptanceFactAttribute.ParseEndpoint(Environment.GetEnvironmentVariable("TCT_ASR_LIVE_BASEURL")) is null
                || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TCT_ASR_LIVE_SPEECH_WAV")))
                Skip = "Set explicit loopback TCT_ASR_LIVE_BASEURL and a permitted PCM16 mono16k WAV saying 'Good morning everyone'.";
        }
    }
}
