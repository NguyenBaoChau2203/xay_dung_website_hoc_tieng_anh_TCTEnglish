using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Speaking;
using TCTEnglish.Tests.Infrastructure;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AudioImportLiveAcceptanceTests
{
    [MediaLiveFact]
    public async Task FakeMediaDownload_RealPinnedDecodeAndOpenAiWindows_ProduceMeasuredOffsets()
    {
        if (!OpenAiSpeechLiveFactAttribute.IsEnabled())
        {
            throw new InvalidOperationException("Set TCT_OPENAI_SPEECH_LIVE=1; loopback Whisper flags must not enable cloud audio.");
        }

        var media = Environment.GetEnvironmentVariable("TCT_AUDIO_IMPORT_LIVE_MEDIA")
            ?? throw new InvalidOperationException("Explicit media fixture is required.");
        Assert.True(File.Exists(media), "Explicit media fixture is missing.");

        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IYoutubeAudioSource>();
            services.AddSingleton<IYoutubeAudioSource>(new FixtureFileSource(media));
            services.RemoveAll<IOpenAiSpeechCredentialResolver>();
            services.AddSingleton<IOpenAiSpeechCredentialResolver, OpenAiSpeechCredentialResolver>();
            services.RemoveAll<ISpeechTranscriber>();
            services.AddSingleton<ISpeechTranscriber>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<SpeechProcessingOptions>>();
                var credentials = sp.GetRequiredService<IOpenAiSpeechCredentialResolver>();
                if (!credentials.TryResolve(out var resolved))
                {
                    throw new InvalidOperationException("Live import requires a configured OpenAI key before any paid send.");
                }

                var inner = new HttpClientHandler { AllowAutoRedirect = false };
                var client = new HttpClient(new OpenAiSpeechLiveBudgetHandler(inner, OpenAiSpeechLiveBudget.Shared))
                {
                    BaseAddress = resolved.BaseAddress,
                    Timeout = TimeSpan.FromSeconds(45)
                };
                return new OpenAiSpeechTranscriber(
                    client,
                    options,
                    credentials,
                    sp.GetRequiredService<ISpeechInferenceLimiter>(),
                    NullLogger<OpenAiSpeechTranscriber>.Instance);
            });
        });
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        Assert.IsType<FfmpegPcmDecoder>(scope.ServiceProvider.GetRequiredService<IAudioPcmDecoder>());
        var result = await scope.ServiceProvider.GetRequiredService<IAudioTranscriptImporter>().TranscribeAsync("abcdefghijk", default);
        Assert.True(result.Success, result.ErrorCode);
        Assert.True(result.Segments.Count >= 3, "Fixture must produce at least three measured speech windows.");
        Assert.Contains(result.Segments, s => s.StartSeconds >= 10);
        var previousEnd = 0d;
        foreach (var segment in result.Segments)
        {
            Assert.True(segment.StartSeconds >= previousEnd);
            Assert.True(segment.EndSeconds > segment.StartSeconds);
            Assert.InRange(segment.EndSeconds, 0, 30);
            previousEnd = segment.EndSeconds;
        }
    }

    private sealed class FixtureFileSource(string media) : IYoutubeAudioSource
    {
        public Task<Stream> OpenAudioAsync(string youtubeId, CancellationToken ct)
            => Task.FromResult<Stream>(File.OpenRead(media));
    }

    private sealed class MediaLiveFactAttribute : FactAttribute
    {
        public MediaLiveFactAttribute()
        {
            var media = Environment.GetEnvironmentVariable("TCT_AUDIO_IMPORT_LIVE_MEDIA");
            if (string.IsNullOrWhiteSpace(media) && !OpenAiSpeechLiveFactAttribute.IsEnabled())
            {
                Skip = "Opt in with TCT_OPENAI_SPEECH_LIVE=1 and TCT_AUDIO_IMPORT_LIVE_MEDIA after owner budget approval.";
            }
        }
    }
}
