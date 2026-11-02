using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Services.Speaking;
using TCTEnglish.Tests.Infrastructure;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AudioImportLiveAcceptanceTests
{
    [MediaLiveFact]
    public async Task FakeMediaDownload_RealPinnedDecodeAndWhisper_ProduceActualOrderedTimestamps()
    {
        var endpoint = AsrLiveAcceptanceFactAttribute.ParseEndpoint(Environment.GetEnvironmentVariable("TCT_ASR_LIVE_BASEURL"))
            ?? throw new InvalidOperationException("Explicit loopback ASR opt-in required.");
        var media = Environment.GetEnvironmentVariable("TCT_AUDIO_IMPORT_LIVE_MEDIA")!;
        Assert.True(File.Exists(media), "Explicit media fixture is missing.");
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IYoutubeAudioSource>();
            services.AddSingleton<IYoutubeAudioSource>(new FixtureHttpSource(media));
            services.PostConfigure<SpeechProcessingOptions>(o => { o.WhisperHttpBaseUrl = endpoint.AbsoluteUri.TrimEnd('/'); o.ModelIdentity = "tiny.en-b5454-media-acceptance"; });
        });
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        Assert.IsType<FfmpegPcmDecoder>(scope.ServiceProvider.GetRequiredService<IAudioPcmDecoder>());
        Assert.IsType<WhisperHttpTranscriber>(scope.ServiceProvider.GetRequiredService<ISpeechTranscriber>());
        var result = await scope.ServiceProvider.GetRequiredService<IAudioTranscriptImporter>().TranscribeAsync("abcdefghijk", default);
        Assert.True(result.Success, result.ErrorCode);
        Assert.True(result.Segments.Count >= 3, "Fixture must span multiple chunks and contain at least three spoken phrases.");
        Assert.Contains(result.Segments, s => s.StartSeconds >= 10);
        var previousEnd = 0d;
        foreach (var segment in result.Segments)
        {
            Assert.True(segment.StartSeconds >= previousEnd);
            Assert.True(segment.EndSeconds > segment.StartSeconds);
            Assert.InRange(segment.EndSeconds, 0, 30);
            previousEnd = segment.EndSeconds;
        }
        Assert.Contains("morning", string.Join(' ', result.Segments.Select(s => s.Text)), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FixtureHttpSource(string media) : IYoutubeAudioSource
    {
        public async Task<Stream> OpenAudioAsync(string youtubeId, CancellationToken ct)
        {
            using var client = new HttpClient(new OpenAiProviderClientTests.Handler((_, _) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead(media)) })));
            // Fake download HTTP; real bounded copy/decode/ASR downstream. No YouTube or cloud call.
            var bytes = await client.GetByteArrayAsync("http://fixture.local/speech.m4a", ct);
            return new MemoryStream(bytes);
        }
    }

    private sealed class MediaLiveFactAttribute : FactAttribute
    {
        public MediaLiveFactAttribute()
        {
            var media = Environment.GetEnvironmentVariable("TCT_AUDIO_IMPORT_LIVE_MEDIA");
            var endpoint = Environment.GetEnvironmentVariable("TCT_ASR_LIVE_BASEURL");
            if (string.IsNullOrWhiteSpace(media) && string.IsNullOrWhiteSpace(endpoint))
                Skip = "Opt in with TCT_AUDIO_IMPORT_LIVE_MEDIA and loopback TCT_ASR_LIVE_BASEURL after starting local speech explicitly.";
            // Partial/invalid explicit opt-in fails in the test; never silently skips broken configuration.
        }
    }
}
