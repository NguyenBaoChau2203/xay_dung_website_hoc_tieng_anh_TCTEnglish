using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiAcceptanceHttpHandlerTests
{
    [Fact]
    public async Task SharedRun_AdmitsRealEmbeddingAndTextTransportsAndStopsNextPost()
    {
        using var run = new AiAcceptanceRunGuard(2, 10_000, TimeSpan.FromMinutes(1));
        var wire = new Wire((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/embeddings")
            ? EmbeddingReply() : TextReply()));
        using var guarded = new AiAcceptanceHttpHandler(run, wire);
        var factory = new Factory(guarded);
        await Embeddings(factory).EmbedAsync(["alpha"], CancellationToken.None);
        await new OpenAiProviderClient(factory, TextOptions(), NullLogger<OpenAiProviderClient>.Instance)
            .GenerateReplyAsync(1, [new AiContextMessage("user", "hello")], CancellationToken.None);
        var failure = await Assert.ThrowsAsync<AiProviderException>(() => Embeddings(factory).EmbedAsync(["alpha"], CancellationToken.None));
        Assert.Equal(AiProviderException.ErrorCodeBudgetExhausted, failure.ErrorCode);
        Assert.Equal(2, wire.Calls);
        Assert.Equal(2, run.Posts);
        Assert.Equal(4, run.KnownTokens);
        Assert.Equal(0, run.UnknownTokens);
    }

    [Fact]
    public async Task EveryRetryTraversesTheRunGuardAndKeepsLostUsage()
    {
        using var run = new AiAcceptanceRunGuard(2, 20, TimeSpan.FromMinutes(1));
        Wire? wire = null;
        wire = new Wire((_, _) => Task.FromResult(wire!.Calls == 1 ? Transient() : EmbeddingReply()));
        using var guarded = new AiAcceptanceHttpHandler(run, wire);
        var ledger = new EmbeddingOperationBudget(2, 20);
        var reply = await Embeddings(new Factory(guarded), retries: 1).EmbedAsync(["alpha"], new EmbeddingInvocation { Budget = ledger }, CancellationToken.None);
        Assert.Single(reply.Vectors);
        Assert.Equal(2, wire.Calls);
        Assert.Equal(2, run.Posts);
        Assert.Equal(5, run.UnknownTokens);
        Assert.Equal(1, run.KnownTokens);
        Assert.False(run.TryPromote(1, 1));
    }

    [Fact]
    public async Task RunCapRejectsTheRetryBeforeItReachesHttp()
    {
        using var run = new AiAcceptanceRunGuard(1, 20, TimeSpan.FromMinutes(1));
        var wire = new Wire((_, _) => Task.FromResult(Transient()));
        using var guarded = new AiAcceptanceHttpHandler(run, wire);
        await Assert.ThrowsAsync<AiProviderException>(() => Embeddings(new Factory(guarded), retries: 1)
            .EmbedAsync(["alpha"], new EmbeddingInvocation { Budget = new EmbeddingOperationBudget(2, 20) }, CancellationToken.None));
        Assert.Equal(1, wire.Calls);
        Assert.Equal(1, run.Posts);
        Assert.Equal(5, run.UnknownTokens);
    }

    [Fact]
    public async Task RunExpiryCancelsInFlightBodyAndKeepsItsReservation()
    {
        var elapsed = TimeSpan.Zero;
        using var run = new AiAcceptanceRunGuard(2, 20, TimeSpan.FromMinutes(1), () => elapsed);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wire = new Wire((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new WaitingContent(entered) }));
        using var guarded = new AiAcceptanceHttpHandler(run, wire);
        var pending = Embeddings(new Factory(guarded)).EmbedAsync(["alpha"], CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        elapsed = TimeSpan.FromMinutes(1);
        Assert.False(run.TryPromote(0, 1));
        var failure = await Assert.ThrowsAsync<AiProviderException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(AiProviderException.ErrorCodeTimeout, failure.ErrorCode);
        Assert.Equal(1, wire.Calls);
        Assert.Equal(5, run.UnknownTokens);
    }

    [Fact]
    public async Task MalformedReceiptRemainsUnknownThroughTheRealClient()
    {
        using var run = new AiAcceptanceRunGuard(1, 10, TimeSpan.FromMinutes(1));
        var wire = new Wire((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("{\"usage\":{\"prompt_tokens\":-1,\"total_tokens\":-1}}", Encoding.UTF8, "application/json") }));
        using var guarded = new AiAcceptanceHttpHandler(run, wire);
        await Assert.ThrowsAsync<AiProviderException>(() => Embeddings(new Factory(guarded)).EmbedAsync(["alpha"], CancellationToken.None));
        Assert.Equal(5, run.UnknownTokens);
        Assert.Equal(0, run.KnownTokens);
        Assert.False(run.TryPromote(1, 1));
    }

    private static IOptions<AiOptions> TextOptions() => Options.Create(new AiOptions
    { ApiKey = "nonpaid-fixture", BaseUrl = "https://api.openai.com/v1", Model = "gpt-6-luna", ReasoningEffort = "low" });

    private static OpenAiEmbeddingClient Embeddings(IHttpClientFactory factory, int retries = 0)
        => new(factory, TextOptions(), Options.Create(new EmbeddingOptions { RetryMaxAttempts = retries }), NullLogger<OpenAiEmbeddingClient>.Instance);

    private static HttpResponseMessage EmbeddingReply()
    {
        var vector = new float[EmbeddingOptions.Dimensions]; vector[0] = 1;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                @object = "list",
                model = EmbeddingOptions.ModelName,
                data = new[] { new { @object = "embedding", index = 0, embedding = vector } },
                usage = new { prompt_tokens = 1, total_tokens = 1 }
            }), Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage TextReply() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
        "{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"role\":\"assistant\",\"status\":\"completed\",\"content\":[{\"type\":\"output_text\",\"text\":\"ok\"}]}],\"usage\":{\"input_tokens\":2,\"output_tokens\":1,\"total_tokens\":3}}",
        Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage Transient()
    {
        var reply = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        reply.Headers.RetryAfter = new(TimeSpan.Zero);
        return reply;
    }

    private sealed class Wire(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return send(request, ct); }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
    }

    private sealed class WaitingContent(TaskCompletionSource entered) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new NotSupportedException();
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken ct)
        { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); }
    }
}
