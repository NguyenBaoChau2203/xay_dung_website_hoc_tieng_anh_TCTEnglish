using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI;
using ProviderClient = TCTEnglish.Services.AI.OpenAiProviderClient;

namespace TCTEnglish.Tests;

public class OpenAiProviderClientTests
{
    private static readonly AiContextMessage[] Messages =
        [new("system", "Help learners."), new("user", "Hello"), new("assistant", "Hi"), new("user", "Explain")];

    public static string Completed(string text = "Hello", bool usage = true) => JsonSerializer.Serialize(new
    {
        status = "completed", model = "gpt-6-luna", output = new object[]
        {
            new { type = "reasoning", summary = new[] { new { type = "summary_text", text = "private reasoning" } } },
            new { type = "message", role = "assistant", status = "completed", content = new[] { new { type = "output_text", text } } }
        },
        usage = usage ? new { input_tokens = 12, output_tokens = 20, total_tokens = 32, output_tokens_details = new { reasoning_tokens = 15 } } : null
    });

    [Fact]
    public async Task Request_UsesResponsesBearerOrderedRolesLowAndWorkloadCap_WithoutSamplingOrAudio()
    {
        var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("fixture-key", request.Headers.Authorization.Parameter);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var root = body.RootElement;
            Assert.Equal("gpt-6-luna", root.GetProperty("model").GetString());
            Assert.Equal("low", root.GetProperty("reasoning").GetProperty("effort").GetString());
            Assert.False(root.GetProperty("store").GetBoolean());
            Assert.Equal(4096, root.GetProperty("max_output_tokens").GetInt32());
            Assert.Equal(new[] { "system", "user", "assistant", "user" }, root.GetProperty("input").EnumerateArray().Select(x => x.GetProperty("role").GetString()));
            Assert.False(root.TryGetProperty("temperature", out _));
            Assert.False(root.TryGetProperty("top_p", out _));
            Assert.False(root.TryGetProperty("text", out _));
            Assert.DoesNotContain("audio", root.GetRawText());
            return Response(Completed());
        });
        await Client(handler).GenerateReplyAsync(1, Messages, default,
            new() { MaxOutputTokens = 4096, Temperature = 0.2, RequestTimeoutSeconds = 2 });
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ReasoningFirst_CollectsOnlyAssistantOutputText_AndMapsUsageOnce()
    {
        var handler = new Handler((_, _) => Task.FromResult(Response(Completed("{\"valid\":true}"))));
        var reply = await Client(handler).GenerateReplyAsync(1, Messages, default);
        Assert.Equal("{\"valid\":true}", reply.Text);
        Assert.Equal((12, 20, 32), (reply.PromptTokens, reply.CompletionTokens, reply.TotalTokens));
        Assert.Equal("gpt-6-luna", reply.Model);
        Assert.Equal("fixture-request", reply.RequestId);
        Assert.Equal(15, reply.ReasoningTokens);
    }

    [Fact]
    public async Task MultipleMessagesAndParts_AreConcatenatedInOrder_MissingUsageIsZero()
    {
        const string body = """{"status":"completed","output":[{"type":"message","role":"assistant","status":"completed","content":[{"type":"output_text","text":"one"},{"type":"output_text","text":"two"}]},{"type":"message","role":"assistant","status":"completed","content":[{"type":"output_text","text":"three"}]}]}""";
        var reply = await Client(new Handler((_, _) => Task.FromResult(Response(body)))).GenerateReplyAsync(1, Messages, default);
        Assert.Equal("onetwothree", reply.Text);
        Assert.Equal((0, 0, 0), (reply.PromptTokens, reply.CompletionTokens, reply.TotalTokens));
    }

    [Theory]
    [InlineData("{\"status\":\"incomplete\",\"output\":[]}")]
    [InlineData("{\"status\":\"failed\",\"error\":{\"message\":\"private\"}}")]
    [InlineData("{\"status\":\"queued\",\"output\":[]}")]
    [InlineData("{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"role\":\"assistant\",\"content\":[{\"type\":\"refusal\",\"refusal\":\"private\"}]}]}")]
    [InlineData("{\"status\":\"completed\",\"output\":[]}")]
    [InlineData("{\"status\":\"completed\",\"output\":[1]}")]
    [InlineData("{\"status\":\"completed\",\"output\":{}}")]
    [InlineData("{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"role\":\"assistant\",\"status\":\"incomplete\",\"content\":[{\"type\":\"output_text\",\"text\":\"{partial\"}]}]}")]
    [InlineData("{\"status\":\"completed\",\"output\":[{\"type\":\"reasoning\",\"text\":\"private\"}]}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("not-json-private")]
    public async Task InvalidRefusedOrPartialResponse_IsNeverSuccessAndNeverLeaksBody(string body)
    {
        var handler = new Handler((_, _) => Task.FromResult(Response(body)));
        var error = await Assert.ThrowsAsync<AiProviderException>(() => Client(handler).GenerateReplyAsync(1, Messages, default));
        Assert.False(error.IsTransient);
        Assert.DoesNotContain("private", error.ToString());
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(400, "invalid_request_error", false, 1)]
    [InlineData(401, "invalid_api_key", false, 1)]
    [InlineData(403, "permission_denied", false, 1)]
    [InlineData(404, "model_not_found", false, 1)]
    [InlineData(429, "insufficient_quota", false, 1)]
    [InlineData(429, "rate_limit_exceeded", true, 2)]
    [InlineData(500, "server_error", true, 2)]
    [InlineData(503, "server_error", true, 2)]
    public async Task HttpErrors_RetryOnlyTransient_AndDoNotExposeProviderBody(int status, string code, bool transient, int calls)
    {
        var handler = new Handler((_, _) => Task.FromResult(Response($"{{\"error\":{{\"code\":\"{code}\",\"message\":\"private fixture-key\"}}}}", (HttpStatusCode)status)));
        var error = await Assert.ThrowsAsync<AiProviderException>(() => Client(handler, retries: 1).GenerateReplyAsync(1, Messages, default));
        Assert.Equal(transient, error.IsTransient);
        Assert.Equal(calls, handler.Calls);
        Assert.DoesNotContain("private", error.ToString());
        Assert.DoesNotContain("fixture-key", error.ToString());
    }

    [Fact]
    public async Task RetryAfterLongerThanBudget_DoesNotSendAgain()
    {
        var handler = new Handler((_, _) =>
        {
            var response = Response("{}", HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(60));
            return Task.FromResult(response);
        });
        await Assert.ThrowsAsync<AiProviderException>(() => Client(handler, retries: 2).GenerateReplyAsync(1, Messages, default, new() { RequestTimeoutSeconds = 1 }));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CallerCancellation_DuringSend_PropagatesWithoutRetry()
    {
        var handler = new Handler(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Response(Completed()); });
        using var cancel = new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).GenerateReplyAsync(1, Messages, cancel.Token));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CallerCancellation_DuringBackoff_Propagates()
    {
        var handler = new Handler((_, _) => Task.FromResult(Response("{}", HttpStatusCode.ServiceUnavailable)));
        var clock = new CoordinatedClock { HoldDelays = true };
        using var cancel = new CancellationTokenSource();
        var pending = Client(handler, retries: 2, clock: clock).GenerateReplyAsync(1, Messages, cancel.Token);
        await clock.DelayEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, handler.Calls);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(1, clock.Starts);
    }

    [Fact]
    public async Task Deadline_CoversBodyRead_AndDoesNotResetForRetry()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new GatedContent(entered) }));
        var clock = new CoordinatedClock();
        var pending = Client(handler, retries: 2, clock: clock).GenerateReplyAsync(1, Messages, default, new() { RequestTimeoutSeconds = 1 });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Active!.ReachOriginalDeadline();
        var error = await Assert.ThrowsAsync<AiProviderException>(() => pending);
        Assert.Equal(AiProviderException.ErrorCodeTimeout, error.ErrorCode);
        Assert.InRange(clock.Active.Elapsed.TotalSeconds, 0.8, 2.5);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(1, clock.Starts);
    }

    [Fact]
    public async Task Deadline_OneOriginalWindowCovers503BackoffAndNextBody()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Handler? handler = null;
        handler = new Handler((_, _) => handler!.Calls == 1
            ? Task.FromResult(Response("{}", HttpStatusCode.ServiceUnavailable))
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new GatedContent(entered) }));
        var clock = new CoordinatedClock();
        var pending = Client(handler!, retries: 2, clock: clock).GenerateReplyAsync(1, Messages, default, new() { RequestTimeoutSeconds = 1 });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var budget = clock.Active!;
        budget.Advance(TimeSpan.FromMilliseconds(750));
        var error = await Assert.ThrowsAsync<AiProviderException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(AiProviderException.ErrorCodeTimeout, error.ErrorCode);
        Assert.InRange(budget.Elapsed.TotalSeconds, 0.8, 2.5);
        Assert.Equal(2, handler!.Calls);
        Assert.Equal(1, clock.Starts);
        Assert.Equal(TimeSpan.FromMilliseconds(250), budget.RecordedDelay);
    }

    [Fact]
    public async Task MissingKey_IsActionableWithoutNetwork()
    {
        var handler = new Handler((_, _) => throw new Exception("must not send"));
        var error = await Assert.ThrowsAsync<AiProviderException>(() => Client(handler, key: "").GenerateReplyAsync(1, Messages, default));
        Assert.Equal(AiProviderException.ErrorCodeInvalidConfiguration, error.ErrorCode);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task NetworkErrors_AreBoundedAndRedacted()
    {
        var handler = new Handler((_, _) => throw new HttpRequestException("private fixture-key"));
        var error = await Assert.ThrowsAsync<AiProviderException>(() => Client(handler, retries: 1).GenerateReplyAsync(1, Messages, default));
        Assert.Equal(AiProviderException.ErrorCodeNetwork, error.ErrorCode);
        Assert.Equal(2, handler.Calls);
        Assert.DoesNotContain("private", error.ToString());
    }

    [Fact]
    public async Task CallerCancellation_DuringResponseBody_Propagates()
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new SlowContent() }));
        using var cancel = new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).GenerateReplyAsync(1, Messages, cancel.Token));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("https://generativelanguage.googleapis.com/v1beta", "gpt-6-luna", "low", "fixture-key")]
    [InlineData("https://api.openai.com/v1?key=fixture", "gpt-6-luna", "low", "fixture-key")]
    [InlineData("http://example.test/v1", "gpt-6-luna", "low", "fixture-key")]
    [InlineData("https://user:private@api.openai.com/v1", "gpt-6-luna", "low", "fixture-key")]
    [InlineData("", "gpt-6-luna", "low", "fixture-key")]
    [InlineData("https://api.openai.com/v1", "another-model", "low", "fixture-key")]
    [InlineData("https://api.openai.com/v1", "gpt-6-luna", "none", "fixture-key")]
    [InlineData("https://api.openai.com/v1", "gpt-6-luna", "low", "AIza-fixture")]
    public async Task UnsafeOrOffTargetConfiguration_IsRejectedBeforeSending(string url, string model, string effort, string key)
    {
        var handler = new Handler((_, _) => throw new Exception("must not send"));
        var provider = new ProviderClient(new Factory(handler), Options.Create(new AiOptions { BaseUrl = url, Model = model, ReasoningEffort = effort, ApiKey = key }), NullLogger<ProviderClient>.Instance);
        var error = await Assert.ThrowsAsync<AiProviderException>(() => provider.GenerateReplyAsync(1, Messages, default));
        Assert.Equal(AiProviderException.ErrorCodeInvalidConfiguration, error.ErrorCode);
        Assert.Equal(0, handler.Calls);
        Assert.DoesNotContain("private", error.ToString());
    }

    [Fact]
    public async Task EndpointAlreadyEndsInResponses_IsNotDuplicated()
    {
        var handler = new Handler((request, _) =>
        {
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(Response(Completed()));
        });
        var provider = new ProviderClient(new Factory(handler), Options.Create(new AiOptions { BaseUrl = "https://api.openai.com/v1/responses/", ApiKey = "fixture-key" }), NullLogger<ProviderClient>.Instance);
        await provider.GenerateReplyAsync(1, Messages, default);
    }

    [Fact]
    public async Task ChatConstrainedOutput_AddsResponsesJsonSchema_AndFakeBodyStillParses()
    {
        var handler = new Handler(async (request, ct) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var format = body.RootElement.GetProperty("text").GetProperty("format");
            Assert.Equal("json_schema", format.GetProperty("type").GetString());
            Assert.Equal("chat_sentence_selection", format.GetProperty("name").GetString());
            Assert.True(format.GetProperty("strict").GetBoolean());
            Assert.Equal("object", format.GetProperty("schema").GetProperty("type").GetString());
            Assert.False(format.GetProperty("schema").GetProperty("additionalProperties").GetBoolean());
            return Response(Completed("{\"sentenceIds\":[\"bell-1\"]}"));
        });
        var schema = """{"type":"object","additionalProperties":false,"properties":{"sentenceIds":{"type":"array","items":{"type":"string"}}},"required":["sentenceIds"]}""";
        var reply = await Client(handler).GenerateReplyAsync(1, Messages, default, new AiProviderRequestOptions
        {
            ConstrainedOutput = new ChatConstrainedOutput("chat_sentence_selection", schema)
        });
        Assert.Equal("{\"sentenceIds\":[\"bell-1\"]}", reply.Text);
        Assert.Equal(1, handler.Calls);
    }

    private static ProviderClient Client(Handler handler, int retries = 0, string key = "fixture-key", IAiTransportClock? clock = null) =>
        new(new Factory(handler), Options.Create(new AiOptions { ApiKey = key, RetryMaxAttempts = retries, BaseUrl = "https://api.openai.com/v1", Model = "gpt-6-luna" }), NullLogger<ProviderClient>.Instance, clock);

    public static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        response.Headers.Add("x-request-id", "fixture-request");
        return response;
    }

    public sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return send(request, cancellationToken); }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
    }

    private sealed class SlowContent : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new NotSupportedException();
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) => await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private sealed class GatedContent(TaskCompletionSource entered) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new NotSupportedException();
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    private sealed class CoordinatedClock : IAiTransportClock
    {
        public int Starts { get; private set; }
        public CoordinatedBudget? Active { get; private set; }
        public bool HoldDelays { get; init; }
        public TaskCompletionSource DelayEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IAiTransportBudget Start(CancellationToken caller, TimeSpan timeout)
        {
            Starts++;
            Active = new CoordinatedBudget(caller, timeout, HoldDelays, DelayEntered);
            return Active;
        }
    }

    private sealed class CoordinatedBudget : IAiTransportBudget
    {
        private readonly CancellationTokenSource _budget;
        private readonly TimeSpan _timeout;
        private readonly bool _holdDelays;
        private readonly TaskCompletionSource _delayEntered;

        public CoordinatedBudget(CancellationToken caller, TimeSpan timeout, bool holdDelays, TaskCompletionSource delayEntered)
        {
            _timeout = timeout;
            _holdDelays = holdDelays;
            _delayEntered = delayEntered;
            _budget = CancellationTokenSource.CreateLinkedTokenSource(caller);
        }

        public TimeSpan Elapsed { get; private set; }
        public TimeSpan RecordedDelay { get; private set; }
        public CancellationToken Token => _budget.Token;

        public void ReachOriginalDeadline()
        {
            Elapsed = _timeout;
            _budget.Cancel();
        }

        public void Advance(TimeSpan step)
        {
            var next = Elapsed + step;
            if (next >= _timeout)
            {
                Elapsed = _timeout;
                _budget.Cancel();
                return;
            }

            Elapsed = next;
        }

        public async Task DelayAsync(TimeSpan delay)
        {
            RecordedDelay = delay;
            _delayEntered.TrySetResult();
            if (!_holdDelays)
            {
                Advance(delay);
                if (_budget.IsCancellationRequested)
                {
                    throw new OperationCanceledException(Token);
                }

                return;
            }

            await Task.Delay(Timeout.Infinite, Token);
        }

        public void Dispose() => _budget.Dispose();
    }
}
