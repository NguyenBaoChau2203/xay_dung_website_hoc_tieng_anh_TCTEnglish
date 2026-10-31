using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TCTVocabulary.Models;

namespace TCTEnglish.Tests.Infrastructure;

public static class SpeakingAssessmentTestHelper
{
    public static async Task<Guid> SeedCompletedAssessmentAsync(
        TestWebApplicationFactory factory,
        int userId,
        int sentenceId,
        double textMatchScore)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var now = DateTime.UtcNow;
        var assessment = new SpeakingAssessment
        {
            PublicId = Guid.NewGuid(),
            UserId = userId,
            SentenceId = sentenceId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(10),
            EngineName = "test-fixture",
            ModelIdentity = "fixture",
            TranscriptText = "fixture transcript",
            TextMatchScore = textMatchScore,
            SpeechDetected = true,
            Status = "Completed",
            Consumed = false
        };
        context.SpeakingAssessments.Add(assessment);
        await context.SaveChangesAsync();
        return assessment.PublicId;
    }

    public static async Task<HttpRequestMessage> CreateSpeakingProgressRequestAsync(
        TestWebApplicationFactory factory,
        int userId,
        int sentenceId,
        string antiForgeryToken,
        double textMatchScore)
    {
        var assessmentId = await SeedCompletedAssessmentAsync(factory, userId, sentenceId, textMatchScore);
        var payload = $$"""
        {
          "assessmentId": "{{assessmentId}}",
          "totalScore": 1,
          "accuracyScore": 1,
          "fluencyScore": 1,
          "completenessScore": 1
        }
        """;

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/speaking/{sentenceId}/progress")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);
        return request;
    }

    /// <summary>Minimal mono PCM16 16kHz WAV (~0.25s silence).</summary>
    public static byte[] CreateSilentPcm16Mono16kWav(double seconds = 0.25)
    {
        var sampleRate = 16000;
        var sampleCount = Math.Max(1, (int)(sampleRate * seconds));
        var dataSize = sampleCount * 2;
        var bytes = new byte[44 + dataSize];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        BitConverter.GetBytes(36 + dataSize).CopyTo(bytes, 4);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(bytes, 8);
        Encoding.ASCII.GetBytes("fmt ").CopyTo(bytes, 12);
        BitConverter.GetBytes(16).CopyTo(bytes, 16);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 20);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 22);
        BitConverter.GetBytes(sampleRate).CopyTo(bytes, 24);
        BitConverter.GetBytes(sampleRate * 2).CopyTo(bytes, 28);
        BitConverter.GetBytes((short)2).CopyTo(bytes, 32);
        BitConverter.GetBytes((short)16).CopyTo(bytes, 34);
        Encoding.ASCII.GetBytes("data").CopyTo(bytes, 36);
        BitConverter.GetBytes(dataSize).CopyTo(bytes, 40);
        return bytes;
    }

    public static async Task<HttpResponseMessage> PostAssessAsync(
        HttpClient client,
        int sentenceId,
        string antiForgeryToken,
        byte[] wavBytes)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(wavBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(fileContent, "audio", "speech.wav");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/speaking/{sentenceId}/assess")
        {
            Content = content
        };
        request.Headers.Add("RequestVerificationToken", antiForgeryToken);
        return await client.SendAsync(request);
    }
}
