using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Speaking;
using TCTEnglish.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace TCTEnglish.Tests;

/// <summary>
/// Opt-in paid OpenAI speech checks. Shared HTTP-boundary budget protects all live audio paths.
/// </summary>
[Trait("Category", "OpenAiSpeechLive")]
public sealed class OpenAiSpeechLiveAcceptanceTests(ITestOutputHelper output)
{
    [OpenAiSpeechLiveFact]
    public async Task LiveSilenceWav_IsNoSpeech_WithinBudget()
    {
        using var wav = CreateSilenceWav(1);
        var (result, latencyMs) = await TranscribeLiveAsync(wav);
        Assert.Equal(SpeechTranscriptionStatus.NoSpeech, result.Status);
        Assert.Equal("no_speech", result.ErrorCode);
        output.WriteLine(
            $"status={result.Status}; model={result.ModelIdentity}; latencyMs={latencyMs}; posts={OpenAiSpeechLiveBudget.Shared.Posts}; audioSeconds={OpenAiSpeechLiveBudget.Shared.AudioSeconds:F1}");
    }

    private static async Task<(SpeechTranscriptionResult Result, long LatencyMs)> TranscribeLiveAsync(Stream wav)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var options = Options.Create(new SpeechProcessingOptions
        {
            Enabled = true,
            InferenceTimeoutSeconds = 30,
            OpenAI = new OpenAiSpeechOptions
            {
                Model = "gpt-transcribe",
                BaseUrl = "https://api.openai.com/v1/",
                MaxResponseBytes = 262144
            }
        });
        var resolver = new OpenAiSpeechCredentialResolver(configuration, options);
        if (!resolver.TryResolve(out var credentials))
        {
            throw new InvalidOperationException("Live speech requires a configured OpenAI key before any paid send.");
        }

        var inner = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(new OpenAiSpeechLiveBudgetHandler(inner, OpenAiSpeechLiveBudget.Shared))
        {
            BaseAddress = credentials.BaseAddress,
            Timeout = TimeSpan.FromSeconds(45)
        };
        var limiter = new SpeechInferenceLimiter(Options.Create(new SpeechProcessingOptions
        {
            MaxConcurrentInferences = 1,
            MaxQueuedInferences = 4
        }));
        var transcriber = new OpenAiSpeechTranscriber(
            client,
            options,
            new FixedCredentials(credentials),
            limiter,
            NullLogger<OpenAiSpeechTranscriber>.Instance);

        var sw = Stopwatch.StartNew();
        var result = await transcriber.TranscribeAsync(wav, CancellationToken.None);
        sw.Stop();
        return (result, sw.ElapsedMilliseconds);
    }

    private static MemoryStream CreateSilenceWav(int seconds)
    {
        var dataBytes = 16_000 * 2 * seconds;
        var stream = new MemoryStream(44 + dataBytes);
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + dataBytes);
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(16000);
            writer.Write(32000);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(dataBytes);
            writer.Write(new byte[dataBytes]);
        }

        stream.Position = 0;
        return stream;
    }

    private sealed class FixedCredentials(OpenAiSpeechCredentials credentials) : IOpenAiSpeechCredentialResolver
    {
        public bool TryResolve(out OpenAiSpeechCredentials resolved)
        {
            resolved = credentials;
            return true;
        }
    }
}
