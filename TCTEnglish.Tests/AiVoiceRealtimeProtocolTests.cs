using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Moq;
using TCTEnglish.Services.AI.Voice;
using TCTEnglish.Services.Speaking;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiVoiceRealtimeProtocolTests
{
    public const string Sdp = "v=0\r\no=- 1 2 IN IP4 127.0.0.1\r\ns=-\r\nt=0 0\r\nm=audio 9 UDP/TLS/RTP/SAVPF 111\r\n";

    [Theory]
    [InlineData("m=application 9 UDP/DTLS/SCTP webrtc-datachannel")]
    [InlineData("  M = application 9 UDP/DTLS/SCTP webrtc-datachannel")]
    [InlineData("m=video 9 UDP/TLS/RTP/SAVPF 96")]
    [InlineData("m=audio 9 RTP/AVP 0")]
    [InlineData("m=garbage")]
    public void RejectsAdditionalMedia(string media) => Assert.Throws<VoiceException>(() => VoiceSdpPolicy.Validate(Sdp + media));

    [Theory]
    [InlineData("v=0\nm=audio 0 RTP/AVP 0")]
    [InlineData("v=0\nm=audio -1 RTP/AVP 0")]
    [InlineData("v=0\nm=audio 65536 RTP/AVP 0")]
    [InlineData("v=0\nm=audio 9evil RTP/AVP 0")]
    [InlineData("m=audio 9 RTP/AVP 0")]
    [InlineData("v=0")]
    public void RejectsMalformed(string sdp) => Assert.Throws<VoiceException>(() => VoiceSdpPolicy.Validate(sdp));

    [Fact]
    public void AudioAndByteBoundary()
    {
        VoiceSdpPolicy.Validate(Sdp);
        Assert.Throws<VoiceException>(() => VoiceSdpPolicy.Validate(Sdp + new string('é', VoiceSdpPolicy.MaxBytes / 2)));
    }

    [Theory]
    [InlineData("https://evil.test/v1/realtime/calls/rtc_x")]
    [InlineData("https://api.openai.com.evil.test/v1/realtime/calls/rtc_x")]
    [InlineData("https://user@api.openai.com/v1/realtime/calls/rtc_x")]
    [InlineData("/v1/realtime/calls/rtc_x?key=x")]
    [InlineData("/v1/realtime/calls/rtc_x#fragment")]
    [InlineData("/v1/realtime/calls/../rtc_x")]
    public void RejectsUntrustedLocation(string location) => Assert.Throws<VoiceException>(() => OpenAiRealtimeVoiceClient.ParseCallId(location));

    [Fact]
    public async Task InvalidOfferNeverDispatches()
    {
        var handler = new Handler();
        var client = Client(handler);
        await Assert.ThrowsAsync<VoiceException>(() => client.CreateCallAsync("bad", "bounded instructions", "user-hash", _ => Task.CompletedTask, default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task PersistsCallBeforeReadingInvalidAnswer()
    {
        var handler = new Handler { Answer = "invalid" };
        string? persisted = null;
        await Assert.ThrowsAsync<VoiceException>(() => Client(handler).CreateCallAsync(Sdp, "instructions", "user-hash", id => { persisted = id; return Task.CompletedTask; }, default));
        Assert.Equal("rtc_test-123", persisted);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void SequentialTurnsRequireProviderToFinishWithoutVadInterruption()
    {
        var config = VoiceSessionConfiguration.Build(new AiVoiceOptions(), "instructions");
        var vad = config.GetProperty("audio").GetProperty("input").GetProperty("turn_detection");
        Assert.True(vad.GetProperty("create_response").GetBoolean());
        Assert.False(vad.GetProperty("interrupt_response").GetBoolean());
        var actual = System.Text.Json.Nodes.JsonNode.Parse(config.GetRawText())!;
        actual["audio"]!["input"]!["turn_detection"]!["interrupt_response"] = true;
        Assert.False(VoiceSessionConfiguration.IsReady(JsonSerializer.SerializeToElement(new { type = "session.updated", session = actual }), config));
    }

    [Fact]
    public async Task MultipartCallUsesServerSettings()
    {
        var handler = new Handler();
        var result = await Client(handler).CreateCallAsync(Sdp, "instructions", "user-hash", _ => Task.CompletedTask, default);
        Assert.Equal(Sdp, result.AnswerSdp);
        Assert.Contains("instructions", handler.Body);
        Assert.Contains("server_vad", handler.Body);
        Assert.Contains("interrupt_response", handler.Body);
        Assert.Equal("https://api.openai.com/v1/realtime/calls", handler.Url);
    }

    [Fact]
    public async Task ExplicitProviderRejectionIsDistinctFromUnknownServerFailure()
    {
        var rejected = new Handler { Status = HttpStatusCode.TooManyRequests };
        await Assert.ThrowsAsync<VoiceProviderRejectedException>(() => Client(rejected).CreateCallAsync(Sdp, "instructions", "hash", _ => throw new Exception("must not allocate"), default));
        var unknown = new Handler { Status = HttpStatusCode.InternalServerError };
        var failure = await Assert.ThrowsAsync<VoiceException>(() => Client(unknown).CreateCallAsync(Sdp, "instructions", "hash", _ => throw new Exception("must not allocate"), default));
        Assert.IsNotType<VoiceProviderRejectedException>(failure);
    }

    [Fact]
    public void ReadinessRequiresEffectiveConfiguration()
    {
        var config = VoiceSessionConfiguration.Build(new AiVoiceOptions(), "instructions");
        var ack = JsonSerializer.SerializeToElement(new { type = "session.updated", session = config });
        Assert.True(VoiceSessionConfiguration.IsReady(ack, config));
        Assert.False(VoiceSessionConfiguration.IsReady(JsonSerializer.SerializeToElement(new { type = "session.created", session = config }), config));
        Assert.False(VoiceSessionConfiguration.IsReady(JsonSerializer.SerializeToElement(new { type = "session.updated", session = new { instructions = "provider default", tools = Array.Empty<object>() } }), config));
        var modified = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(config.GetRawText())!;
        modified["tools"] = JsonSerializer.SerializeToElement(new[] { new { type = "function", name = "privileged" } });
        Assert.False(VoiceSessionConfiguration.IsReady(JsonSerializer.SerializeToElement(new { type = "session.updated", session = modified }), config));
    }

    [Fact]
    public void ReadinessDoesNotDependOnEchoOfOptionalTranscriptionLanguageHint()
    {
        var config = VoiceSessionConfiguration.Build(new AiVoiceOptions(), "instructions");
        var actual = System.Text.Json.Nodes.JsonNode.Parse(config.GetRawText())!;
        actual["audio"]!["input"]!["transcription"]!.AsObject().Remove("language");
        var ack = JsonSerializer.SerializeToElement(new { type = "session.updated", session = actual });
        Assert.True(VoiceSessionConfiguration.IsReady(ack, config));
        Assert.Equal("gpt-transcribe", config.GetProperty("audio").GetProperty("input").GetProperty("transcription").GetProperty("model").GetString());
    }

    [Fact]
    public void GenerationDoneDoesNotEndPlaybackOrLeakProviderPayload()
    {
        Assert.Null(VoicePublicEvent.Project(JsonSerializer.SerializeToElement(new { type = "response.done", response = new { status = "completed" } })));
        Assert.Null(VoicePublicEvent.Project(JsonSerializer.SerializeToElement(new { type = "session.updated", session = new { instructions = "secret" } })));
        var projected = VoicePublicEvent.Project(JsonSerializer.SerializeToElement(new { type = "output_audio_buffer.started", delta = "private" }));
        Assert.Equal("speaking", projected!.State);
        Assert.DoesNotContain("private", JsonSerializer.Serialize(projected));
    }

    [Fact]
    public async Task ReadinessDiagnosticsExposeOnlyRequestedFieldNamesAndPublicModel()
    {
        var config = VoiceSessionConfiguration.Build(new AiVoiceOptions(), "private instructions");
        await using var socket = new FakeSideband();
        var actual = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(config.GetRawText())!;
        actual["model"] = JsonSerializer.SerializeToElement("gpt-realtime-2.1-2026-05-21");
        actual["unexpected_private"] = JsonSerializer.SerializeToElement("private transcript");
        await socket.Events.Writer.WriteAsync(JsonSerializer.SerializeToElement(new { type = "session.updated", session = actual }));
        await socket.Events.Writer.WriteAsync(JsonSerializer.SerializeToElement(new { type = "session.updated", session = config }));
        var diagnostics = new List<string>();
        await OpenAiRealtimeVoiceClient.WaitForReadyAsync(socket, config, default, d => diagnostics.Add(string.Join(",", d.Fields) + ":" + d.Model));
        Assert.Equal(new[] { "model:gpt-realtime-2.1-2026-05-21" }, diagnostics);
        Assert.DoesNotContain("private", string.Join("", diagnostics));
        Assert.NotEmpty(VoiceSessionConfiguration.MismatchedFields(JsonSerializer.SerializeToElement("bad session"), config));
    }

    [Fact]
    public async Task SidebandIgnoresUnrelatedAckAndTimesOutWithoutRequiredConfig()
    {
        var config = VoiceSessionConfiguration.Build(new AiVoiceOptions(), "instructions");
        await using var socket = new FakeSideband();
        await socket.Events.Writer.WriteAsync(JsonSerializer.SerializeToElement(new { type = "session.updated", session = new { instructions = "unrelated", tools = Array.Empty<object>() } }));
        using var ct = new CancellationTokenSource(TimeSpan.FromMilliseconds(80));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => OpenAiRealtimeVoiceClient.WaitForReadyAsync(socket, config, ct.Token));
        await socket.Events.Writer.WriteAsync(JsonSerializer.SerializeToElement(new { type = "session.updated", session = config }));
        await OpenAiRealtimeVoiceClient.WaitForReadyAsync(socket, config, default);
    }

    [Fact]
    public async Task SidebandConnectsWithPrivateCallIdAndSendsOnlyMutableUpdateBeforeEffectiveAck()
    {
        var socket = new ProtocolSideband();
        var factory = new Mock<IVoiceSidebandFactory>();
        factory.Setup(x => x.ConnectAsync(new Uri("wss://api.openai.com/v1/realtime?call_id=rtc_test"), "offline-fake-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(socket);
        await using var attached = await Client(new Handler(), factory.Object).AttachAsync("rtc_test", "instructions", default);
        Assert.Equal("session.update", socket.Sent.GetProperty("type").GetString());
        Assert.False(socket.Sent.GetProperty("session").TryGetProperty("model", out _));
        Assert.Equal(0, socket.Sent.GetProperty("session").GetProperty("tools").GetArrayLength());
        Assert.Equal(2, socket.Received);
        factory.VerifyAll();
    }

    private sealed class ProtocolSideband : IVoiceSideband
    {
        public JsonElement Sent;
        public int Received;
        public Task SendAsync(JsonElement message, CancellationToken ct) { Sent = message; return Task.CompletedTask; }
        public Task<JsonElement> ReceiveAsync(CancellationToken ct)
        {
            Received++;
            return Task.FromResult(Received == 1 ? JsonSerializer.SerializeToElement(new { type = "session.updated", session = new { instructions = "unrelated" } })
                : JsonSerializer.SerializeToElement(new { type = "session.updated", session = VoiceSessionConfiguration.Build(new AiVoiceOptions(), "instructions") }));
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static OpenAiRealtimeVoiceClient Client(Handler handler, IVoiceSidebandFactory? sockets = null)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(OpenAiRealtimeVoiceClient.HttpClientName)).Returns(new HttpClient(handler));
        var credentials = new Mock<IOpenAiSpeechCredentialResolver>();
        var resolved = new OpenAiSpeechCredentials("offline-fake-key", "test", "gpt-transcribe", new Uri("https://api.openai.com/v1/"));
        credentials.Setup(x => x.TryResolve(out resolved)).Returns(true);
        return new(factory.Object, credentials.Object, Options.Create(new AiVoiceOptions { Enabled = true }), sockets ?? new ClientVoiceSidebandFactory());
    }

    private sealed class Handler : HttpMessageHandler
    {
        public string Answer = Sdp;
        public string? Body, Url;
        public int Calls;
        public HttpStatusCode Status = HttpStatusCode.Created;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Url = request.RequestUri!.ToString();
            Assert.Equal("offline-fake-key", request.Headers.Authorization!.Parameter);
            Body = await request.Content!.ReadAsStringAsync(ct);
            var response = new HttpResponseMessage(Status) { Content = new StringContent(Answer) };
            response.Headers.Location = new Uri("/v1/realtime/calls/rtc_test-123", UriKind.Relative);
            return response;
        }
    }
}
