using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingBriefTransportTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Interlocked.Increment(ref Calls); return send(request, ct); }
    }
    private sealed class Clients(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private static OpenAiProviderClient Provider(HttpClient client) => new(new Clients(client), Options.Create(new AiOptions
    { ApiKey = "synthetic-test-key", RetryMaxAttempts = 2 }), NullLogger<OpenAiProviderClient>.Instance);

    [Fact]
    public async Task ActualTransportDoesNotResendUnknownDispatchAndRecordsUncertainUsageWithoutChatSchema()
    {
        var handler = new Handler((_, _) => throw new HttpRequestException("synthetic"));
        using var client = new HttpClient(handler); var receipts = new List<(int Known, int Unknown)>();
        var error = await Assert.ThrowsAsync<AiProviderException>(() => Provider(client).GenerateReplyAsync(1,
            [new("system", "Compose original bilingual content."), new("user", "synthetic practice brief")], CancellationToken.None,
            new AiProviderRequestOptions { StopOnUncertainOutcome = true,
                RecordUsage = (known, unknown, _) => { receipts.Add((known, unknown)); return Task.FromResult(true); } }));
        Assert.Equal("network", error.ErrorCode); Assert.Equal(1, handler.Calls);
        Assert.True(error.RecordedUnknownTokens > 0); Assert.Contains(receipts, r => r.Known == 0 && r.Unknown > 0);
    }

    [Theory]
    [InlineData("completed", "output_text", "original bilingual JSON", null)]
    [InlineData("completed", "refusal", "", "refusal")]
    [InlineData("incomplete", "output_text", "partial", "incomplete_response")]
    public async Task ActualTransportPreservesKnownUsageAndOpenAiPayload(string status, string type, string text, string? expectedError)
    {
        string? sent = null;
        var body = System.Text.Json.JsonSerializer.Serialize(new { status, model = "gpt-6-luna", usage = new { input_tokens = 10, output_tokens = 20, total_tokens = 30 },
            output = new[] { new { type = "message", role = "assistant", content = new[] { new { type, text } } } } });
        var handler = new Handler(async (request, ct) => { sent = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }; });
        using var client = new HttpClient(handler); var receipts = new List<int>();
        var call = Provider(client).GenerateReplyAsync(1, [new("user", "synthetic")], CancellationToken.None,
            new AiProviderRequestOptions { StopOnUncertainOutcome = true,
                RecordUsage = (known, _, _) => { receipts.Add(known); return Task.FromResult(true); } });
        if (expectedError == null) Assert.Equal(30, (await call).TotalTokens);
        else { var ex = await Assert.ThrowsAsync<AiProviderException>(() => call); Assert.Equal(expectedError, ex.ErrorCode); Assert.Equal(30, ex.RecordedTotalTokens); }
        Assert.Contains(30, receipts); Assert.Equal(1, handler.Calls);
        Assert.Contains("\"store\":false", sent); Assert.DoesNotContain("json_schema", sent);
    }
}
