using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Speaking;
using TCTEnglish.Tests.Infrastructure;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// G01 local: live whisper.cpp loopback silence → adapter NoSpeech.
/// </summary>
[Trait("Category", "AsrLiveAcceptance")]
public sealed class WhisperLiveSilenceAcceptanceTests
{
    [AsrLiveAcceptanceFact]
    public async Task LiveSilenceWav_ThroughAdapter_IsNoSpeech()
    {
        var endpoint = AsrLiveAcceptanceFactAttribute.ParseEndpoint(
            Environment.GetEnvironmentVariable("TCT_ASR_LIVE_BASEURL"))
            ?? throw new InvalidOperationException("Live ASR opt-in is required.");
        var baseUrl = endpoint.AbsoluteUri;

        var limiter = new SpeechInferenceLimiter(Options.Create(new SpeechProcessingOptions
        {
            MaxConcurrentInferences = 1,
            MaxQueuedInferences = 4
        }));
        using var client = new HttpClient { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(65) };
        var transcriber = new WhisperHttpTranscriber(
            client,
            Options.Create(new SpeechProcessingOptions
            {
                WhisperHttpBaseUrl = baseUrl,
                InferenceTimeoutSeconds = 60,
                MaxConcurrentInferences = 1,
                MaxQueuedInferences = 4,
                ModelIdentity = "ggml-tiny.en"
            }),
            limiter,
            NullLogger<WhisperHttpTranscriber>.Instance);

        // Synthetic one-second PCM16/mono/16kHz silence; no private local fixture.
        await using var stream = CreateSilenceWav();
        var result = await transcriber.TranscribeAsync(stream, CancellationToken.None);

        Assert.Equal(SpeechTranscriptionStatus.NoSpeech, result.Status);
        Assert.Equal("no_speech", result.ErrorCode);
        Assert.Equal(string.Empty, result.Transcript);
        Assert.DoesNotContain("BLANK_AUDIO", result.Transcript, StringComparison.OrdinalIgnoreCase);
    }

    private static MemoryStream CreateSilenceWav()
    {
        const int dataBytes = 16000 * 2;
        var stream = new MemoryStream(44 + dataBytes);
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8); writer.Write(36 + dataBytes); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(16000); writer.Write(32000); writer.Write((short)2); writer.Write((short)16);
            writer.Write("data"u8); writer.Write(dataBytes); writer.Write(new byte[dataBytes]);
        }
        stream.Position = 0;
        return stream;
    }
}
