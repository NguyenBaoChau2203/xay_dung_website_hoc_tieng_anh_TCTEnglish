using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Speaking;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class OpenAiSpeechTranscriberTests
{
    [Theory]
    [InlineData("{\"text\":123}")]
    [InlineData("{\"text\":{\"value\":\"hi\"}}")]
    [InlineData("{\"text\":null}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"error\":\"engine failure\"}")]
    public async Task MalformedOrInvalidSchema_ReturnsUnavailableInvalidSchema(string body)
    {
        var result = await TranscribeBodyAsync(body);
        Assert.Equal(SpeechTranscriptionStatus.Unavailable, result.Status);
        Assert.Equal("invalid_schema", result.ErrorCode);
        Assert.Equal(OpenAiSpeechTranscriber.EngineName, result.EngineName);
        Assert.Equal("gpt-transcribe", result.ModelIdentity);
    }

    [Fact]
    public async Task ValidEmptyText_IsNoSpeech()
    {
        var result = await TranscribeBodyAsync("{\"text\":\"\"}");
        Assert.Equal(SpeechTranscriptionStatus.NoSpeech, result.Status);
        Assert.Equal("no_speech", result.ErrorCode);
        Assert.Empty(result.Segments);
    }

    [Fact]
    public async Task ValidText_IsOk_WithoutProviderSegments()
    {
        var result = await TranscribeBodyAsync("{\"text\":\"Good morning\",\"languages\":[{\"code\":\"en\"}]}");
        Assert.Equal(SpeechTranscriptionStatus.Ok, result.Status);
        Assert.Equal("Good morning", result.Transcript);
        Assert.Empty(result.Segments);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task Multipart_SendsWavFilenameModelAndBearer_Only()
    {
        HttpRequestMessage? captured = null;
        var handler = new ScriptedHandler(async (request, _) =>
        {
            captured = request;
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
            var parts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in multipart)
            {
                var name = part.Headers.ContentDisposition?.Name?.Trim('"');
                Assert.False(string.IsNullOrWhiteSpace(name));
                if (name == "file")
                {
                    Assert.Equal("speech.wav", part.Headers.ContentDisposition?.FileName?.Trim('"'));
                    Assert.Equal("audio/wav", part.Headers.ContentType?.MediaType);
                    parts[name!] = "wav";
                }
                else
                {
                    parts[name!] = await part.ReadAsStringAsync();
                }
            }

            Assert.Equal("gpt-transcribe", parts["model"]);
            Assert.Equal("wav", parts["file"]);
            Assert.DoesNotContain(parts.Keys, k => k is "prompt" or "language" or "reasoning" or "referenceText");
            return OkJson("{\"text\":\"hi\"}");
        });

        var result = await CreateTranscriber(handler).TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal(SpeechTranscriptionStatus.Ok, result.Status);
        Assert.NotNull(captured);
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("test-speech-key", captured.Headers.Authorization?.Parameter);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task MissingKey_DoesNotCallHttp()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(OkJson("{\"text\":\"no\"}")));
        var transcriber = CreateTranscriber(handler, configured: false);
        var result = await transcriber.TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal(SpeechTranscriptionStatus.NotReady, result.Status);
        Assert.Equal("engine_not_ready", result.ErrorCode);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "engine_auth")]
    [InlineData(HttpStatusCode.Forbidden, "engine_auth")]
    [InlineData(HttpStatusCode.NotFound, "engine_auth")]
    [InlineData(HttpStatusCode.TooManyRequests, "engine_busy")]
    [InlineData(HttpStatusCode.InternalServerError, "engine_http_error")]
    public async Task ProviderStatus_MapsCategoryWithoutRetry(HttpStatusCode status, string code)
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent("{\"error\":\"x\"}", Encoding.UTF8, "application/json")
        }));
        var result = await CreateTranscriber(handler).TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal(SpeechTranscriptionStatus.Unavailable, result.Status);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task OversizedResponse_IsUnavailable_AndReleasesLease()
    {
        var huge = "{\"text\":\"" + new string('a', 300_000) + "\"}";
        var handler = new ScriptedHandler((_, _) => Task.FromResult(OkJson(huge)));
        var limiter = new SpeechInferenceLimiter(Options.Create(new SpeechProcessingOptions
        {
            MaxConcurrentInferences = 1,
            MaxQueuedInferences = 4
        }));
        var first = await CreateTranscriber(handler, limiter: limiter, maxResponseBytes: 1024)
            .TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal("response_too_large", first.ErrorCode);

        handler.Enqueue(OkJson("{\"text\":\"ok\"}"));
        var second = await CreateTranscriber(handler, limiter: limiter, maxResponseBytes: 1024)
            .TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal(SpeechTranscriptionStatus.Ok, second.Status);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task AfterInvalidSchema_NextValidRequest_StillAdmitted()
    {
        var handler = new ScriptedHandler(
            OkJson("{\"text\":123}"),
            OkJson("{\"text\":\"ok\"}"));
        var limiter = new SpeechInferenceLimiter(Options.Create(new SpeechProcessingOptions
        {
            MaxConcurrentInferences = 1,
            MaxQueuedInferences = 4
        }));
        var first = await CreateTranscriber(handler, limiter: limiter).TranscribeAsync(EmptyWav(), CancellationToken.None);
        var second = await CreateTranscriber(handler, limiter: limiter).TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal("invalid_schema", first.ErrorCode);
        Assert.Equal(SpeechTranscriptionStatus.Ok, second.Status);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task ResponseBodyIoFailureAfterOkHeaders_IsUnavailable_AndReleasesLease()
    {
        var handler = new ScriptedHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new ThrowingReadStream())
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        });
        var limiter = new SpeechInferenceLimiter(Options.Create(new SpeechProcessingOptions
        {
            MaxConcurrentInferences = 1,
            MaxQueuedInferences = 4
        }));
        var first = await CreateTranscriber(handler, limiter: limiter).TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal(SpeechTranscriptionStatus.Unavailable, first.Status);
        Assert.Equal("engine_unavailable", first.ErrorCode);
        Assert.Equal(1, handler.Calls);

        handler.Enqueue(OkJson("{\"text\":\"recovered\"}"));
        var second = await CreateTranscriber(handler, limiter: limiter).TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal(SpeechTranscriptionStatus.Ok, second.Status);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task CallerCancel_PropagatesAndReleasesLease()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new ScriptedHandler(async (_, ct) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return OkJson("{\"text\":\"late\"}");
        });
        var limiter = new SpeechInferenceLimiter(Options.Create(new SpeechProcessingOptions
        {
            MaxConcurrentInferences = 1,
            MaxQueuedInferences = 4
        }));
        using var cts = new CancellationTokenSource();
        var pending = CreateTranscriber(handler, limiter: limiter).TranscribeAsync(EmptyWav(), cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        handler.Enqueue(OkJson("{\"text\":\"next\"}"));
        var next = await CreateTranscriber(handler, limiter: limiter).TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal(SpeechTranscriptionStatus.Ok, next.Status);
    }

    private static async Task<SpeechTranscriptionResult> TranscribeBodyAsync(string body)
        => await CreateTranscriber(new ScriptedHandler(OkJson(body))).TranscribeAsync(EmptyWav(), CancellationToken.None);

    private static OpenAiSpeechTranscriber CreateTranscriber(
        ScriptedHandler handler,
        bool configured = true,
        ISpeechInferenceLimiter? limiter = null,
        int maxResponseBytes = 262144)
    {
        limiter ??= new SpeechInferenceLimiter(Options.Create(new SpeechProcessingOptions
        {
            MaxConcurrentInferences = 1,
            MaxQueuedInferences = 4
        }));
        var client = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        return new OpenAiSpeechTranscriber(
            client,
            Options.Create(new SpeechProcessingOptions
            {
                InferenceTimeoutSeconds = 5,
                MaxConcurrentInferences = 1,
                MaxQueuedInferences = 4,
                OpenAI = new OpenAiSpeechOptions
                {
                    Model = "gpt-transcribe",
                    BaseUrl = "https://api.openai.com/v1/",
                    MaxResponseBytes = maxResponseBytes
                }
            }),
            new FixedSpeechCredentials(configured),
            limiter,
            NullLogger<OpenAiSpeechTranscriber>.Instance);
    }

    private static HttpResponseMessage OkJson(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static MemoryStream EmptyWav() => new(new byte[] { 0, 1, 2, 3 });

    private sealed class FixedSpeechCredentials(bool configured) : IOpenAiSpeechCredentialResolver
    {
        public bool TryResolve(out OpenAiSpeechCredentials credentials)
        {
            if (!configured)
            {
                credentials = default!;
                return false;
            }

            credentials = new OpenAiSpeechCredentials(
                "test-speech-key",
                "test",
                "gpt-transcribe",
                new Uri("https://api.openai.com/v1/"));
            return true;
        }
    }

    private sealed class ThrowingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("reset");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(new IOException("reset"));
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses = new();

        public int Calls;

        public ScriptedHandler(params HttpResponseMessage[] responses)
        {
            foreach (var response in responses)
            {
                var captured = response;
                _responses.Enqueue((_, _) => Task.FromResult(captured));
            }
        }

        public ScriptedHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response)
        {
            _responses.Enqueue(response);
        }

        public void Enqueue(HttpResponseMessage response)
        {
            var captured = response;
            _responses.Enqueue((_, _) => Task.FromResult(captured));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No scripted response left.");
            }

            return _responses.Dequeue()(request, cancellationToken);
        }
    }
}
