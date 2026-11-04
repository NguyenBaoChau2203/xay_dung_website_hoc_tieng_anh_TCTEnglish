using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Speaking;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public class AudioTranscriptImporterTests
{
    [Fact]
    public async Task MeasuredWindows_MapTranscriptToSampleOffsets_AndDeleteOwnedTemp()
    {
        var decoder = new FakeDecoder(21);
        var planner = new FakePlanner(
            new PcmSpeechWindow(1_600, 8_000),
            new PcmSpeechWindow(160_000, 176_000),
            new PcmSpeechWindow(320_000, 328_000));
        var sentRanges = new List<(int Start, int Length)>();
        var transcriber = new FakeTranscriber((call, wav, _) =>
        {
            sentRanges.Add(ReadPcmRange(wav));
            return Task.FromResult(OkText($"phrase-{call}"));
        });
        var result = await Importer(decoder, transcriber, planner).TranscribeAsync("abcdefghijk", default);
        Assert.True(result.Success);
        Assert.Equal(new[] { 0.1, 10.0, 20.0 }, result.Segments.Select(s => s.StartSeconds));
        Assert.Equal(new[] { 0.5, 11.0, 20.5 }, result.Segments.Select(s => s.EndSeconds));
        Assert.Equal(3, transcriber.Calls);
        // WAV payloads contain only the measured window bytes (header excluded below).
        Assert.Equal(new[] { 12800, 32000, 16000 }, sentRanges.Select(r => r.Length));
        Assert.False(Directory.Exists(decoder.Directory));
    }

    [Theory]
    [InlineData("engine_not_ready")]
    [InlineData("engine_busy")]
    [InlineData("invalid_schema")]
    [InlineData("engine_timeout")]
    public async Task EngineFailureAfterOneWindow_NeverReturnsPartialTranscript(string code)
    {
        var decoder = new FakeDecoder(21);
        var planner = new FakePlanner(
            new PcmSpeechWindow(0, 16_000),
            new PcmSpeechWindow(16_000, 32_000));
        var transcriber = new FakeTranscriber((call, _, _) => Task.FromResult(call == 1
            ? OkText("ok")
            : new SpeechTranscriptionResult { Status = SpeechTranscriptionStatus.Unavailable, ErrorCode = code }));
        var result = await Importer(decoder, transcriber, planner).TranscribeAsync("abcdefghijk", default);
        Assert.False(result.Success);
        Assert.Empty(result.Segments);
        Assert.Equal(code, result.ErrorCode);
        Assert.False(Directory.Exists(decoder.Directory));
    }

    [Fact]
    public async Task ProviderTextWithoutSegments_UsesMeasuredWindowTiming()
    {
        var result = await Importer(
                new FakeDecoder(2),
                new FakeTranscriber((_, _, _) => Task.FromResult(OkText("Hello there"))),
                new FakePlanner(new PcmSpeechWindow(0, 16_000)))
            .TranscribeAsync("abcdefghijk", default);
        Assert.True(result.Success);
        Assert.Equal(0, result.Segments[0].StartSeconds);
        Assert.Equal(1, result.Segments[0].EndSeconds);
        Assert.Equal("Hello there", result.Segments[0].Text);
    }

    [Fact]
    public async Task SilencePlanner_IsNotPublished()
    {
        var decoder = new FakeDecoder(1);
        var result = await Importer(decoder, new FakeTranscriber((_, _, _) => throw new Exception("must not infer")), new FakePlanner())
            .TranscribeAsync("abcdefghijk", default);
        Assert.Equal("no_speech", result.ErrorCode);
        Assert.Empty(result.Segments);
        Assert.False(Directory.Exists(decoder.Directory));
    }

    [Fact]
    public async Task NoSpeechWindow_IsSkipped_WithoutFailingImport()
    {
        var planner = new FakePlanner(
            new PcmSpeechWindow(0, 16_000),
            new PcmSpeechWindow(16_000, 32_000));
        var transcriber = new FakeTranscriber((call, _, _) => Task.FromResult(call == 1
            ? new SpeechTranscriptionResult { Status = SpeechTranscriptionStatus.NoSpeech, ErrorCode = "no_speech" }
            : OkText("Kept")));
        var result = await Importer(new FakeDecoder(3), transcriber, planner).TranscribeAsync("abcdefghijk", default);
        Assert.True(result.Success);
        Assert.Single(result.Segments);
        Assert.Equal(1.0, result.Segments[0].StartSeconds);
    }

    [Fact]
    public async Task DownloadByteLimit_StopsBeforeWritingBeyondCap()
    {
        using var source = new MemoryStream(new byte[100]);
        using var output = new MemoryStream();
        var error = await Assert.ThrowsAsync<AudioImportException>(() => AudioTranscriptImporter.CopyBoundedAsync(source, output, 50, default));
        Assert.Equal("audio_too_large", error.Code);
        Assert.True(output.Length <= 50);
    }

    [Theory]
    [InlineData("decode_failed")]
    [InlineData("audio_too_large")]
    [InlineData("decoder_checksum_mismatch")]
    public async Task DecodeFailure_ProducesNoPartialAndCleansTemp(string code)
    {
        var decoder = new FakeDecoder(1) { Failure = code };
        var result = await Importer(decoder, new FakeTranscriber((_, _, _) => throw new Exception("must not infer")), new FakePlanner(new PcmSpeechWindow(0, 16_000)))
            .TranscribeAsync("abcdefghijk", default);
        Assert.Equal(code, result.ErrorCode);
        Assert.Empty(result.Segments);
        Assert.False(Directory.Exists(decoder.Directory));
    }

    [Fact]
    public async Task MissingEngine_DoesNotStartDownloadOrDecode()
    {
        var source = new FakeSource(() => throw new Exception("must not download"));
        var result = await Importer(new FakeDecoder(1), new FakeTranscriber((_, _, _) => throw new Exception()), new FakePlanner(new PcmSpeechWindow(0, 16_000)), source, configured: false)
            .TranscribeAsync("abcdefghijk", default);
        Assert.Equal("engine_not_ready", result.ErrorCode);
    }

    [Fact]
    public async Task RequestCap_FailsClosed()
    {
        var windows = Enumerable.Range(0, 5).Select(i => new PcmSpeechWindow(i * 16_000, (i + 1) * 16_000)).ToArray();
        var result = await Importer(
                new FakeDecoder(6),
                new FakeTranscriber((_, _, _) => Task.FromResult(OkText("x"))),
                new FakePlanner(windows),
                maxRequests: 3)
            .TranscribeAsync("abcdefghijk", default);
        Assert.Equal("request_cap_exceeded", result.ErrorCode);
    }

    [Fact]
    public async Task CallerCancel_PropagatesAndCleansTemp()
    {
        var decoder = new FakeDecoder(1);
        var transcriber = new FakeTranscriber(async (_, _, ct) => { await Task.Delay(Timeout.Infinite, ct); return OkText("x"); });
        using var cts = new CancellationTokenSource(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Importer(decoder, transcriber, new FakePlanner(new PcmSpeechWindow(0, 16_000))).TranscribeAsync("abcdefghijk", cts.Token));
        Assert.False(Directory.Exists(decoder.Directory));
    }

    [Fact]
    public async Task TotalDeadline_ContainsTheWholeImport()
    {
        var decoder = new FakeDecoder(1);
        var transcriber = new FakeTranscriber(async (_, _, ct) => { await Task.Delay(Timeout.Infinite, ct); return OkText("x"); });
        var result = await Importer(decoder, transcriber, new FakePlanner(new PcmSpeechWindow(0, 16_000)), timeout: 1)
            .TranscribeAsync("abcdefghijk", default);
        Assert.Equal("audio_timeout", result.ErrorCode);
        Assert.False(Directory.Exists(decoder.Directory));
    }

    [Fact]
    public async Task CaptionsSuccess_DoesNotCallAudioFallback()
    {
        var service = new CaptionFixture(new NeverImport());
        var result = await service.GetTranscriptForSpeakingImportAsync("abcdefghijk");
        Assert.True(result.IsEnglishUsable);
        Assert.Equal("youtube-captions", result.TranscriptSource);
    }

    [Fact]
    public async Task OverlappingPlannerWindows_FailBeforeAnyProviderCall()
    {
        var transcriber = new FakeTranscriber((_, _, _) => throw new Exception("must not call provider"));
        var planner = new FakePlanner(
            new PcmSpeechWindow(0, 16_000),
            new PcmSpeechWindow(8_000, 24_000));
        var result = await Importer(new FakeDecoder(3), transcriber, planner).TranscribeAsync("abcdefghijk", default);
        Assert.Equal("invalid_timestamps", result.ErrorCode);
        Assert.Equal(0, transcriber.Calls);
        Assert.Empty(result.Segments);
    }

    [Fact]
    public async Task RealPlanner_TwentyOneSecondsWithPause_ImportSucceedsWithFakeProvider()
    {
        var pcm = new byte[21 * AudioImportOptions.PcmBytesPerSecond];
        WriteTonePcm(pcm, 0, 10, 7000);
        WriteTonePcm(pcm, 10.1, 10.9, 7000);
        var decoder = new BytesDecoder(pcm);
        var calls = 0;
        var transcriber = new FakeTranscriber((_, _, _) =>
        {
            calls++;
            return Task.FromResult(OkText("phrase"));
        });
        var result = await Importer(decoder, transcriber, new PcmSpeechWindowPlanner())
            .TranscribeAsync("abcdefghijk", default);
        Assert.True(result.Success, result.ErrorCode);
        Assert.True(result.Segments.Count >= 2, $"segments={result.Segments.Count}");
        Assert.Equal(calls, result.Segments.Count);
        var previousEnd = 0d;
        foreach (var segment in result.Segments)
        {
            Assert.True(segment.StartSeconds >= previousEnd);
            Assert.True(segment.EndSeconds > segment.StartSeconds);
            Assert.True(segment.EndSeconds <= 21 + 1e-6);
            previousEnd = segment.EndSeconds;
        }
    }

    [Fact]
    public async Task ImportWindows_ReleaseSharedInferenceAdmission_ForQueuedLearner()
    {
        var speechOptions = Options.Create(new SpeechProcessingOptions
        {
            Enabled = true,
            InferenceTimeoutSeconds = 10,
            OpenAI = new OpenAiSpeechOptions { Model = "gpt-transcribe", BaseUrl = "https://api.openai.com/v1/" }
        });
        var limiter = new SpeechInferenceLimiter(speechOptions);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lengths = new List<int>();
        var handler = new OpenAiProviderClientTests.Handler(async (request, ct) =>
        {
            var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
            lengths.Add((await multipart.First().ReadAsByteArrayAsync(ct)).Length);
            if (lengths.Count == 1)
            {
                entered.SetResult();
                await resume.Task.WaitAsync(ct);
            }

            return OpenAiProviderClientTests.Response("""{"text":"We study every day."}""");
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var credentials = new FixedCredentials(true);
        var transcriber = new OpenAiSpeechTranscriber(
            http, speechOptions, credentials, limiter, NullLogger<OpenAiSpeechTranscriber>.Instance);
        var planner = new FakePlanner(
            new PcmSpeechWindow(0, 160_000),
            new PcmSpeechWindow(160_000, 176_000));
        var import = Importer(new FakeDecoder(12), transcriber, planner, credentials: credentials)
            .TranscribeAsync("abcdefghijk", default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        using var learnerWav = AudioTranscriptImporter.CreateWav(new byte[32000]);
        var learner = transcriber.TranscribeAsync(learnerWav, default);
        resume.SetResult();
        Assert.Equal(SpeechTranscriptionStatus.Ok, (await learner).Status);
        Assert.True((await import).Success);
        // window1 (10s) → learner clip → window2 (1s); lease released between windows.
        Assert.Equal(new[] { 320044, 32044, 32044 }, lengths);
    }

    private static AudioTranscriptImporter Importer(
        IAudioPcmDecoder decoder,
        ISpeechTranscriber transcriber,
        IPcmSpeechWindowPlanner planner,
        IYoutubeAudioSource? source = null,
        bool configured = true,
        int timeout = 300,
        int maxRequests = 180,
        IOpenAiSpeechCredentialResolver? credentials = null) => new(
        source ?? new FakeSource(() => new MemoryStream(new byte[] { 1, 2, 3 })),
        decoder,
        transcriber,
        planner,
        credentials ?? new FixedCredentials(configured),
        Options.Create(new SpeechProcessingOptions { Enabled = configured }),
        Options.Create(new AudioImportOptions { TotalTimeoutSeconds = timeout, MaxTranscriptionRequests = maxRequests }),
        NullLogger<AudioTranscriptImporter>.Instance);

    private static SpeechTranscriptionResult OkText(string text) => new()
    {
        Status = SpeechTranscriptionStatus.Ok,
        Transcript = text,
        EngineName = OpenAiSpeechTranscriber.EngineName,
        ModelIdentity = "gpt-transcribe"
    };

    private static (int Start, int Length) ReadPcmRange(Stream wav)
    {
        using var reader = new BinaryReader(wav, System.Text.Encoding.ASCII, leaveOpen: true);
        wav.Position = 40;
        var dataLength = reader.ReadInt32();
        return (0, dataLength);
    }

    internal sealed class FakeSource(Func<Stream> open) : IYoutubeAudioSource
    {
        public Task<Stream> OpenAudioAsync(string youtubeId, CancellationToken ct) => Task.FromResult(open());
    }

    private sealed class FakeDecoder(int seconds) : IAudioPcmDecoder
    {
        public string? Directory { get; private set; }
        public string? Failure { get; init; }
        public Task EnsureAvailableAsync(CancellationToken ct) => Task.CompletedTask;
        public async Task DecodeAsync(string sourcePath, string pcmPath, CancellationToken ct)
        {
            Directory = Path.GetDirectoryName(pcmPath);
            if (Failure != null)
            {
                throw new AudioImportException(Failure);
            }

            await File.WriteAllBytesAsync(pcmPath, new byte[seconds * AudioImportOptions.PcmBytesPerSecond], ct);
        }
    }

    private sealed class BytesDecoder(byte[] pcm) : IAudioPcmDecoder
    {
        public string? Directory { get; private set; }
        public Task EnsureAvailableAsync(CancellationToken ct) => Task.CompletedTask;
        public async Task DecodeAsync(string sourcePath, string pcmPath, CancellationToken ct)
        {
            Directory = Path.GetDirectoryName(pcmPath);
            await File.WriteAllBytesAsync(pcmPath, pcm, ct);
        }
    }

    private static void WriteTonePcm(byte[] pcm, double startSeconds, double durationSeconds, short amplitude)
    {
        var start = (int)(startSeconds * 16_000);
        var count = (int)(durationSeconds * 16_000);
        for (var i = 0; i < count; i++)
        {
            var sampleIndex = start + i;
            if (sampleIndex * 2 + 1 >= pcm.Length)
            {
                break;
            }

            var sample = (short)(amplitude * Math.Sin(2 * Math.PI * 400 * i / 16_000.0));
            pcm[sampleIndex * 2] = (byte)(sample & 0xff);
            pcm[sampleIndex * 2 + 1] = (byte)((sample >> 8) & 0xff);
        }
    }

    private sealed class FakePlanner(params PcmSpeechWindow[] windows) : IPcmSpeechWindowPlanner
    {
        public IReadOnlyList<PcmSpeechWindow> Plan(ReadOnlySpan<byte> pcm16Mono16kHz) => windows;
    }

    private sealed class FakeTranscriber(Func<int, Stream, CancellationToken, Task<SpeechTranscriptionResult>> action) : ISpeechTranscriber
    {
        public int Calls { get; private set; }
        public Task<SpeechTranscriptionResult> TranscribeAsync(Stream wav, CancellationToken ct)
        {
            Assert.InRange(wav.Length, 44, 320044);
            return action(++Calls, wav, ct);
        }
    }

    private sealed class NeverImport : IAudioTranscriptImporter
    {
        public Task<AudioImportResult> TranscribeAsync(string youtubeId, CancellationToken ct) => throw new Exception("Captions must bypass audio.");
    }

    private sealed class CaptionFixture(IAudioTranscriptImporter importer) : YoutubeTranscriptService(importer, NullLogger<YoutubeTranscriptService>.Instance)
    {
        public override Task<YoutubeTranscriptResult> GetEnglishCaptionsOnlyAsync(string youtubeId, CancellationToken ct = default) =>
            Task.FromResult(new YoutubeTranscriptResult([], true, "youtube-captions"));
    }

    private sealed class FixedCredentials(bool configured) : IOpenAiSpeechCredentialResolver
    {
        public bool TryResolve(out OpenAiSpeechCredentials credentials)
        {
            if (!configured)
            {
                credentials = default!;
                return false;
            }

            credentials = new OpenAiSpeechCredentials(
                "test-key",
                "test",
                "gpt-transcribe",
                new Uri("https://api.openai.com/v1/"));
            return true;
        }
    }
}
