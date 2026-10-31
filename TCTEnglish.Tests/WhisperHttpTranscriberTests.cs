using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Speaking;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WhisperHttpTranscriberTests
{
    [Theory]
    [InlineData("{\"text\":123}")]
    [InlineData("{\"text\":{\"value\":\"hi\"}}")]
    [InlineData("{\"text\":null}")]
    [InlineData("{\"text\":\"Good morning\",\"segments\":[{\"start\":\"bad\",\"end\":1,\"text\":\"Good morning\"}]}")]
    [InlineData("{\"text\":\"Good morning\",\"segments\":[{\"start\":1,\"end\":\"2\",\"text\":\"Good morning\"}]}")]
    [InlineData("{\"text\":\"hi\",\"segments\":\"not-array\"}")]
    [InlineData("{\"text\":\"hi\",\"segments\":[\"bad\"]}")]
    [InlineData("{\"text\":\"hi\",\"segments\":[{\"start\":2,\"end\":1,\"text\":\"hi\"}]}")]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task MalformedOrInvalidSchema_ReturnsUnavailableInvalidSchema(string body)
    {
        var result = await TranscribeBodyAsync(body);

        Assert.Equal(SpeechTranscriptionStatus.Unavailable, result.Status);
        Assert.Equal("invalid_schema", result.ErrorCode);
        Assert.NotEqual(SpeechTranscriptionStatus.NoSpeech, result.Status);
    }

    [Theory]
    [InlineData("{\"error\":\"engine failure\"}")]
    [InlineData("{}")]
    [InlineData("{\"segments\":[]}")]
    public async Task ErrorObjectOrEmptyContractWithoutSpeech_IsInvalidSchema_NotNoSpeech(string body)
    {
        var result = await TranscribeBodyAsync(body);

        Assert.Equal(SpeechTranscriptionStatus.Unavailable, result.Status);
        Assert.Equal("invalid_schema", result.ErrorCode);
    }

    [Fact]
    public async Task ValidEmptyTextAndEmptySegments_IsNoSpeech()
    {
        var result = await TranscribeBodyAsync("{\"text\":\"\",\"segments\":[]}");

        Assert.Equal(SpeechTranscriptionStatus.NoSpeech, result.Status);
        Assert.Equal("no_speech", result.ErrorCode);
    }

    [Theory]
    [InlineData("{\"text\":\"[BLANK_AUDIO]\",\"segments\":[],\"no_speech_prob\":0.905}")]
    [InlineData("{\"text\":\" [BLANK_AUDIO] \",\"segments\":[]}")]
    [InlineData("{\"text\":\"\",\"segments\":[{\"start\":0,\"end\":1,\"text\":\"[BLANK_AUDIO]\"}]}")]
    [InlineData("{\"text\":\"[BLANK_AUDIO]\",\"segments\":[{\"start\":0,\"end\":1,\"text\":\"[BLANK_AUDIO]\"}]}")]
    public async Task EngineBlankAudioMarkerOnly_IsNoSpeech(string body)
    {
        var result = await TranscribeBodyAsync(body);

        Assert.Equal(SpeechTranscriptionStatus.NoSpeech, result.Status);
        Assert.Equal("no_speech", result.ErrorCode);
        Assert.Equal(string.Empty, result.Transcript);
        Assert.DoesNotContain("BLANK_AUDIO", result.Transcript, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MixedSpeechAndBlankAudioMarker_KeepsSpeechOk()
    {
        var result = await TranscribeBodyAsync(
            "{\"text\":\"Hello [BLANK_AUDIO]\",\"segments\":[{\"start\":0,\"end\":0.5,\"text\":\"Hello\"},{\"start\":0.5,\"end\":1,\"text\":\"[BLANK_AUDIO]\"}]}");

        Assert.Equal(SpeechTranscriptionStatus.Ok, result.Status);
        Assert.Contains("Hello", result.Transcript, StringComparison.Ordinal);
        Assert.DoesNotContain("BLANK_AUDIO", result.Transcript, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidText_IsOk()
    {
        var result = await TranscribeBodyAsync(
            "{\"text\":\"Good morning\",\"segments\":[{\"start\":0.0,\"end\":1.2,\"text\":\"Good morning\"}]}");

        Assert.Equal(SpeechTranscriptionStatus.Ok, result.Status);
        Assert.Equal("Good morning", result.Transcript);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task SegmentOnlyValidContract_IsOk()
    {
        var result = await TranscribeBodyAsync(
            "{\"text\":\"\",\"segments\":[{\"start\":0,\"end\":0.5,\"text\":\"Hello\"}]}");

        Assert.Equal(SpeechTranscriptionStatus.Ok, result.Status);
        Assert.Equal("Hello", result.Transcript);
    }

    [Fact]
    public async Task AfterInvalidSchema_NextValidRequest_StillAdmitted()
    {
        var handler = new ScriptedHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"text\":123}", Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"text\":\"ok\",\"segments\":[]}", Encoding.UTF8, "application/json")
            });
        var limiter = new SpeechInferenceLimiter(Options.Create(new SpeechProcessingOptions
        {
            MaxConcurrentInferences = 1,
            MaxQueuedInferences = 4
        }));
        var transcriber = CreateTranscriber(handler, limiter);

        var first = await transcriber.TranscribeAsync(EmptyWav(), CancellationToken.None);
        var second = await transcriber.TranscribeAsync(EmptyWav(), CancellationToken.None);

        Assert.Equal("invalid_schema", first.ErrorCode);
        Assert.Equal(SpeechTranscriptionStatus.Ok, second.Status);
        Assert.Equal(2, handler.Calls);
    }

    private static async Task<SpeechTranscriptionResult> TranscribeBodyAsync(string body)
    {
        var handler = new ScriptedHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
        var limiter = new SpeechInferenceLimiter(Options.Create(new SpeechProcessingOptions
        {
            MaxConcurrentInferences = 1,
            MaxQueuedInferences = 4
        }));
        return await CreateTranscriber(handler, limiter).TranscribeAsync(EmptyWav(), CancellationToken.None);
    }

    private static WhisperHttpTranscriber CreateTranscriber(HttpMessageHandler handler, ISpeechInferenceLimiter limiter)
    {
        var client = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://127.0.0.1")
        };
        return new WhisperHttpTranscriber(
            client,
            Options.Create(new SpeechProcessingOptions
            {
                WhisperHttpBaseUrl = "http://127.0.0.1",
                InferenceTimeoutSeconds = 5,
                MaxConcurrentInferences = 1,
                MaxQueuedInferences = 4,
                ModelIdentity = "test-whisper"
            }),
            limiter,
            NullLogger<WhisperHttpTranscriber>.Instance);
    }

    private static MemoryStream EmptyWav() => new(new byte[] { 0, 1, 2, 3 });

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public int Calls;

        public ScriptedHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No scripted response left.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
