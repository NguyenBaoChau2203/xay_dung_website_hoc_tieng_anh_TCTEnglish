using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Speaking;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public class AudioTranscriptImporterTests
{
    [Fact]
    public async Task MultiChunkImport_OffsetsActualTimestamps_AndDeletesOnlyOwnedTempDirectory()
    {
        var decoder = new FakeDecoder(21);
        var transcriber = new FakeTranscriber((_, _) => Task.FromResult(Ok(0.1, 0.5)));
        var result = await Importer(decoder, transcriber).TranscribeAsync("abcdefghijk", default);
        Assert.True(result.Success);
        Assert.Equal(new[] { 0.1, 10.1, 20.1 }, result.Segments.Select(s => s.StartSeconds));
        Assert.Equal(new[] { 0.5, 10.5, 20.5 }, result.Segments.Select(s => s.EndSeconds));
        Assert.Equal(3, transcriber.Calls);
        Assert.False(Directory.Exists(decoder.Directory));
    }

    [Theory]
    [InlineData("engine_not_ready")]
    [InlineData("engine_busy")]
    [InlineData("invalid_schema")]
    [InlineData("engine_timeout")]
    public async Task EngineFailureAfterOneChunk_NeverReturnsPartialTranscript(string code)
    {
        var decoder = new FakeDecoder(21);
        var transcriber = new FakeTranscriber((call, _) => Task.FromResult(call == 1 ? Ok(0, 1)
            : new SpeechTranscriptionResult { Status = SpeechTranscriptionStatus.Unavailable, ErrorCode = code }));
        var result = await Importer(decoder, transcriber).TranscribeAsync("abcdefghijk", default);
        Assert.False(result.Success);
        Assert.Empty(result.Segments);
        Assert.Equal(code, result.ErrorCode);
        Assert.False(Directory.Exists(decoder.Directory));
    }

    [Theory]
    [InlineData(-0.1, 0.5)]
    [InlineData(1, 0.5)]
    [InlineData(0, 11)]
    [InlineData(double.NaN, 1)]
    public async Task InvalidTimestampRange_IsRejectedWithoutFabricatedTimecodes(double start, double end)
    {
        var decoder = new FakeDecoder(10);
        var result = await Importer(decoder, new FakeTranscriber((_, _) => Task.FromResult(Ok(start, end))))
            .TranscribeAsync("abcdefghijk", default);
        Assert.Equal("invalid_timestamps", result.ErrorCode);
        Assert.Empty(result.Segments);
    }

    [Fact]
    public async Task PlainTextWithoutSegments_IsRejected()
    {
        var result = await Importer(new FakeDecoder(1), new FakeTranscriber((_, _) => Task.FromResult(
            new SpeechTranscriptionResult { Status = SpeechTranscriptionStatus.Ok, Transcript = "Hello" })))
            .TranscribeAsync("abcdefghijk", default);
        Assert.Equal("missing_timestamps", result.ErrorCode);
    }

    [Fact]
    public async Task Silence_IsNotPublished()
    {
        var decoder = new FakeDecoder(1);
        var result = await Importer(decoder, new FakeTranscriber((_, _) => Task.FromResult(
            new SpeechTranscriptionResult { Status = SpeechTranscriptionStatus.NoSpeech }))).TranscribeAsync("abcdefghijk", default);
        Assert.Equal("no_speech", result.ErrorCode);
        Assert.Empty(result.Segments);
        Assert.False(Directory.Exists(decoder.Directory));
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
        var result = await Importer(decoder, new FakeTranscriber((_, _) => throw new Exception("must not infer")))
            .TranscribeAsync("abcdefghijk", default);
        Assert.Equal(code, result.ErrorCode);
        Assert.Empty(result.Segments);
        Assert.False(Directory.Exists(decoder.Directory));
    }

    [Fact]
    public async Task MissingEngine_DoesNotStartDownloadOrDecode()
    {
        var source = new FakeSource(() => throw new Exception("must not download"));
        var result = await Importer(new FakeDecoder(1), new FakeTranscriber((_, _) => throw new Exception()), source, configured: false)
            .TranscribeAsync("abcdefghijk", default);
        Assert.Equal("engine_not_ready", result.ErrorCode);
    }

    [Fact]
    public async Task CallerCancel_PropagatesAndCleansTemp()
    {
        var decoder = new FakeDecoder(1);
        var transcriber = new FakeTranscriber(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Ok(0, 1); });
        using var cts = new CancellationTokenSource(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Importer(decoder, transcriber).TranscribeAsync("abcdefghijk", cts.Token));
        Assert.False(Directory.Exists(decoder.Directory));
    }

    [Fact]
    public async Task TotalDeadline_ContainsTheWholeImport()
    {
        var decoder = new FakeDecoder(1);
        var transcriber = new FakeTranscriber(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Ok(0, 1); });
        var result = await Importer(decoder, transcriber, timeout: 1).TranscribeAsync("abcdefghijk", default);
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
    public async Task ImportChunks_ReleaseSharedInferenceAdmission_ForQueuedLearner()
    {
        var speechOptions = Options.Create(new SpeechProcessingOptions { WhisperHttpBaseUrl = "http://127.0.0.1:8092", InferenceTimeoutSeconds = 10 });
        var limiter = new SpeechInferenceLimiter(speechOptions);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lengths = new List<int>();
        var handler = new OpenAiProviderClientTests.Handler(async (request, ct) =>
        {
            var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
            lengths.Add((await multipart.First().ReadAsByteArrayAsync(ct)).Length);
            if (lengths.Count == 1) { entered.SetResult(); await resume.Task.WaitAsync(ct); }
            return OpenAiProviderClientTests.Response("""{"text":"We study every day.","segments":[{"start":0,"end":0.5,"text":"We study every day."}]}""");
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri(speechOptions.Value.WhisperHttpBaseUrl) };
        var transcriber = new WhisperHttpTranscriber(http, speechOptions, limiter, NullLogger<WhisperHttpTranscriber>.Instance);
        var import = Importer(new FakeDecoder(21), transcriber).TranscribeAsync("abcdefghijk", default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        using var learnerWav = AudioTranscriptImporter.CreateWav(new byte[32000]);
        var learner = transcriber.TranscribeAsync(learnerWav, default); // queued behind first import chunk
        resume.SetResult();
        Assert.Equal(SpeechTranscriptionStatus.Ok, (await learner).Status);
        Assert.True((await import).Success);
        Assert.Equal(new[] { 320044, 32044, 320044, 32044 }, lengths);
    }

    private static AudioTranscriptImporter Importer(FakeDecoder decoder, ISpeechTranscriber transcriber,
        IYoutubeAudioSource? source = null, bool configured = true, int timeout = 300) => new(
        source ?? new FakeSource(() => new MemoryStream(new byte[] { 1, 2, 3 })), decoder, transcriber,
        Options.Create(new SpeechProcessingOptions { WhisperHttpBaseUrl = configured ? "http://127.0.0.1:8092" : "" }),
        Options.Create(new AudioImportOptions { TotalTimeoutSeconds = timeout }), NullLogger<AudioTranscriptImporter>.Instance);

    private static SpeechTranscriptionResult Ok(double start, double end) => new()
    {
        Status = SpeechTranscriptionStatus.Ok, Transcript = "We study every day.",
        Segments = [new() { StartSeconds = start, EndSeconds = end, Text = "We study every day." }]
    };

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
            if (Failure != null) throw new AudioImportException(Failure);
            await File.WriteAllBytesAsync(pcmPath, new byte[seconds * AudioImportOptions.PcmBytesPerSecond], ct);
        }
    }
    private sealed class FakeTranscriber(Func<int, CancellationToken, Task<SpeechTranscriptionResult>> action) : ISpeechTranscriber
    {
        public int Calls { get; private set; }
        public Task<SpeechTranscriptionResult> TranscribeAsync(Stream wav, CancellationToken ct)
        { Assert.InRange(wav.Length, 44, 320044); return action(++Calls, ct); }
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
}
