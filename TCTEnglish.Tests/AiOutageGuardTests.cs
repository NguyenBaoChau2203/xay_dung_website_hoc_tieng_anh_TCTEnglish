using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Rag;
using TCTEnglish.Services.AI.Rag.Corpus;
using TCTEnglish.Services.AI.Resilience;
using TCTEnglish.Services.Speaking;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiOutageGuardTests
{
    private static readonly AiContextMessage[] Messages = [new("user", "Hello")];

    [Fact]
    public void ThreeTransientFailures_OpenAndBlockUntilClockAdvances()
    {
        var clock = new ManualAiClock();
        var circuit = new AiOutageCircuit(clock);
        Fail(circuit);
        Fail(circuit);
        Assert.Equal(AiOutageAdmission.Allowed, circuit.TryAdmit().Admission);
        Fail(circuit);
        Assert.Equal(AiOutageAdmission.Rejected, circuit.TryAdmit().Admission);
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(AiOutageAdmission.Rejected, circuit.TryAdmit().Admission);
        clock.Advance(TimeSpan.FromSeconds(1));
        var probe = circuit.TryAdmit();
        Assert.Equal(AiOutageAdmission.Probe, probe.Admission);
        Assert.Equal(AiOutageAdmission.Rejected, circuit.TryAdmit().Admission);
    }

    [Fact]
    public void RetryAfter_ExtendsOpenInterval_AndIsCapped()
    {
        var clock = new ManualAiClock();
        var circuit = new AiOutageCircuit(clock);
        Fail(circuit);
        Fail(circuit);
        Fail(circuit, TimeSpan.FromSeconds(90));
        clock.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(AiOutageAdmission.Rejected, circuit.TryAdmit().Admission);
        clock.Advance(TimeSpan.FromSeconds(59));
        var probe = circuit.TryAdmit();
        Assert.Equal(AiOutageAdmission.Probe, probe.Admission);
        circuit.Release(probe);

        var capped = new AiOutageCircuit(clock);
        Fail(capped);
        Fail(capped);
        Fail(capped, TimeSpan.FromSeconds(500));
        clock.Advance(TimeSpan.FromSeconds(119));
        Assert.Equal(AiOutageAdmission.Rejected, capped.TryAdmit().Admission);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(AiOutageAdmission.Probe, capped.TryAdmit().Admission);
    }

    [Fact]
    public void SuccessAndNonTransient_Reset_CancellationReleasesProbe()
    {
        var clock = new ManualAiClock();
        var circuit = new AiOutageCircuit(clock);
        Fail(circuit);
        Fail(circuit);
        circuit.CompleteSuccess(circuit.TryAdmit());
        Fail(circuit);
        Fail(circuit);
        Assert.Equal(AiOutageAdmission.Allowed, circuit.TryAdmit().Admission);

        Fail(circuit);
        Assert.Equal(AiOutageAdmission.Rejected, circuit.TryAdmit().Admission);
        clock.Advance(AiOutageCircuit.OpenDuration);
        var probe = circuit.TryAdmit();
        Assert.Equal(AiOutageAdmission.Probe, probe.Admission);
        circuit.CompleteNonTransient(probe);
        Assert.Equal(AiOutageAdmission.Allowed, circuit.TryAdmit().Admission);

        Assert.Equal(AiOutageAdmission.Allowed, circuit.TryAdmit().Admission);
        Fail(circuit);
        Fail(circuit);
        Fail(circuit);
        clock.Advance(AiOutageCircuit.OpenDuration);
        var released = circuit.TryAdmit();
        Assert.Equal(AiOutageAdmission.Probe, released.Admission);
        circuit.Release(released);
        Assert.Equal(AiOutageAdmission.Probe, circuit.TryAdmit().Admission);
    }

    [Fact]
    public void Authorities_DoNotShareRawCredentialsOrState()
    {
        var left = AiProviderAuthority.Create(AiProviderFamily.Text, "https://api.openai.com/v1/responses", "gpt-6-luna", "secret-left");
        var right = AiProviderAuthority.Create(AiProviderFamily.Text, "https://api.openai.com/v1/responses", "gpt-6-luna", "secret-right");
        var speech = AiProviderAuthority.Create(AiProviderFamily.Speech, "https://api.openai.com/v1/audio/transcriptions", "gpt-transcribe", "secret-left");
        Assert.NotEqual(left, right);
        Assert.NotEqual(left, speech);
        Assert.DoesNotContain("secret-left", left, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-right", right, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Passthrough_DoesNotHideProviderCalls()
    {
        var handler = new CountingHandler((_, _) => Task.FromResult(Status(HttpStatusCode.InternalServerError)));
        var client = TextClient(handler);
        for (var i = 0; i < 4; i++)
        {
            await Assert.ThrowsAsync<AiProviderException>(() => client.GenerateReplyAsync(1, Messages, CancellationToken.None));
        }

        Assert.Equal(4, handler.Calls);
    }

    [Fact]
    public async Task TextCircuit_FailsFastWithoutHttp_AndIsolatesKeyAndFamily()
    {
        var clock = new ManualAiClock();
        var registry = new AiOutageRegistry(clock);
        using var scope = AiOutageAmbient.Push(registry);
        var handler = new CountingHandler((_, _) => Task.FromResult(Status(HttpStatusCode.InternalServerError)));
        var client = TextClient(handler, key: "key-a");
        for (var i = 0; i < 3; i++)
        {
            var failed = await Assert.ThrowsAsync<AiProviderException>(() => client.GenerateReplyAsync(1, Messages, CancellationToken.None));
            Assert.NotEqual(AiProviderException.ErrorCodeCircuitOpen, failed.ErrorCode);
        }

        var blocked = await Assert.ThrowsAsync<AiProviderException>(() => client.GenerateReplyAsync(1, Messages, CancellationToken.None));
        Assert.Equal(AiProviderException.ErrorCodeCircuitOpen, blocked.ErrorCode);
        Assert.False(blocked.HasRecordedUsage);
        Assert.Equal(0, blocked.RecordedUnknownTokens);
        Assert.Equal(3, handler.Calls);
        Assert.DoesNotContain("key-a", blocked.ToString(), StringComparison.Ordinal);

        var otherKey = TextClient(handler, key: "key-b");
        await Assert.ThrowsAsync<AiProviderException>(() => otherKey.GenerateReplyAsync(1, Messages, CancellationToken.None));
        Assert.Equal(4, handler.Calls);

        var embeddings = EmbeddingClient(handler, key: "key-a");
        await Assert.ThrowsAsync<AiProviderException>(() => embeddings.EmbedAsync(["hello"], CancellationToken.None));
        Assert.Equal(5, handler.Calls);
    }

    [Fact]
    public async Task TextRetries_CountAsOneLogicalFailure()
    {
        var clock = new ManualAiClock();
        using var scope = AiOutageAmbient.Push(new AiOutageRegistry(clock));
        var handler = new CountingHandler((_, _) => Task.FromResult(Status(HttpStatusCode.InternalServerError)));
        var client = TextClient(handler, retries: 2, clock: new NoDelayClock());
        await Assert.ThrowsAsync<AiProviderException>(() => client.GenerateReplyAsync(1, Messages, CancellationToken.None));
        Assert.Equal(3, handler.Calls);
        await Assert.ThrowsAsync<AiProviderException>(() => client.GenerateReplyAsync(1, Messages, CancellationToken.None));
        Assert.Equal(6, handler.Calls);
    }

    [Fact]
    public async Task TextProbe_OneCallerSends_OtherFailsFast_SuccessCloses()
    {
        var clock = new ManualAiClock();
        using var scope = AiOutageAmbient.Push(new AiOutageRegistry(clock));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CountingHandler handler = null!;
        handler = new CountingHandler(async (_, ct) =>
        {
            if (handler.Calls > 3)
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(ct);
                return Status(HttpStatusCode.OK, Completed());
            }

            return Status(HttpStatusCode.InternalServerError);
        });
        var client = TextClient(handler);
        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<AiProviderException>(() => client.GenerateReplyAsync(1, Messages, CancellationToken.None));
        }

        clock.Advance(AiOutageCircuit.OpenDuration);
        var probe = client.GenerateReplyAsync(1, Messages, CancellationToken.None);
        var rejected = client.GenerateReplyAsync(1, Messages, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var blocked = await Assert.ThrowsAsync<AiProviderException>(() => rejected);
        Assert.Equal(AiProviderException.ErrorCodeCircuitOpen, blocked.ErrorCode);
        Assert.Equal(4, handler.Calls);
        release.TrySetResult();
        var reply = await probe.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("Hello", reply.Text);
        Assert.Equal(4, handler.Calls);
        var recovered = await client.GenerateReplyAsync(1, Messages, CancellationToken.None);
        Assert.Equal("Hello", recovered.Text);
        Assert.Equal(5, handler.Calls);
    }

    [Fact]
    public async Task CancelledProbe_ReleasesLease_WithoutCounting()
    {
        var clock = new ManualAiClock();
        using var scope = AiOutageAmbient.Push(new AiOutageRegistry(clock));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CountingHandler handler = null!;
        handler = new CountingHandler(async (_, ct) =>
        {
            if (handler.Calls == 4)
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
            }

            return Status(HttpStatusCode.InternalServerError);
        });
        var client = TextClient(handler, timeoutSeconds: 30);
        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<AiProviderException>(() => client.GenerateReplyAsync(1, Messages, CancellationToken.None));
        }

        clock.Advance(AiOutageCircuit.OpenDuration);
        using var cts = new CancellationTokenSource();
        var probe = client.GenerateReplyAsync(1, Messages, cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe);
        await Assert.ThrowsAsync<AiProviderException>(() => client.GenerateReplyAsync(1, Messages, CancellationToken.None));
        Assert.Equal(5, handler.Calls);
    }

    [Fact]
    public async Task MissingKey_DoesNotOpenCircuitOrSend()
    {
        var clock = new ManualAiClock();
        using var scope = AiOutageAmbient.Push(new AiOutageRegistry(clock));
        var handler = new CountingHandler((_, _) => Task.FromResult(Status(HttpStatusCode.OK, Completed())));
        var missing = TextClient(handler, key: "");
        var error = await Assert.ThrowsAsync<AiProviderException>(() => missing.GenerateReplyAsync(1, Messages, CancellationToken.None));
        Assert.Equal(AiProviderException.ErrorCodeInvalidConfiguration, error.ErrorCode);
        Assert.Equal(0, handler.Calls);
        var ready = await TextClient(handler).GenerateReplyAsync(1, Messages, CancellationToken.None);
        Assert.Equal("Hello", ready.Text);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task EmbeddingCircuit_DoesNotReserveUnsentAttempt()
    {
        var clock = new ManualAiClock();
        using var scope = AiOutageAmbient.Push(new AiOutageRegistry(clock));
        var handler = new CountingHandler((_, _) => Task.FromResult(Status(HttpStatusCode.InternalServerError)));
        var client = EmbeddingClient(handler);
        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<AiProviderException>(() => client.EmbedAsync(["hello"], CancellationToken.None));
        }

        var budget = new EmbeddingOperationBudget(2, 1000);
        var blocked = await Assert.ThrowsAsync<AiProviderException>(() => client.EmbedAsync(
            ["hello"],
            new EmbeddingInvocation { Budget = budget },
            CancellationToken.None));
        Assert.Equal(AiProviderException.ErrorCodeCircuitOpen, blocked.ErrorCode);
        Assert.Equal(0, budget.Attempts);
        Assert.Equal(0, budget.UncertainPromptTokenUpperBound);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Speech429_StopsReadingAfterTheErrorBodyBound()
    {
        var payload = new byte[2_097_191];
        payload[0] = (byte)'{';
        var body = new CountingContent(payload) { Headers = { ContentLength = 50_000_000 } };
        var handler = new CountingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = body }));
        using var scope = AiOutageAmbient.Push(new AiOutageRegistry(new ManualAiClock()));
        var result = await SpeechClient(handler, key: "bounded-429").TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal("engine_busy", result.ErrorCode);
        Assert.InRange(body.BytesRead, 1, 8193);
    }

    [Theory]
    [InlineData("{\"error\":{\"code\":\"insufficient_quota\"}}", "code")]
    [InlineData("{\"error\":{\"type\":\"billing_hard_limit_reached\"}}", "type")]
    public async Task Speech429_ReadsSmallBillingMetadataCompletely(string json, string kind)
    {
        var body = new CountingContent(Encoding.UTF8.GetBytes(json));
        var handler = new CountingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = body }));
        using var scope = AiOutageAmbient.Push(new AiOutageRegistry(new ManualAiClock()));
        var result = await SpeechClient(handler, key: "billing-" + kind).TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal(json.Length, body.BytesRead);
        Assert.Equal("quota_exhausted", result.ErrorCode);
        var outcome = SpeakingAssessmentOutcome.FromTranscriptionFailure(result);
        Assert.Equal(503, outcome.StatusCode);
        Assert.Equal("quota_exhausted", outcome.ErrorCode);
        Assert.False(outcome.CanCommitProgress);
        Assert.DoesNotContain("quá nhanh", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Speech429_MalformedOrCancelledBody_StaysSafe()
    {
        var malformed = new CountingContent(Encoding.UTF8.GetBytes("{"));
        var handler = new CountingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = malformed }));
        using (AiOutageAmbient.Push(new AiOutageRegistry(new ManualAiClock())))
        {
            var result = await SpeechClient(handler, key: "malformed-429").TranscribeAsync(EmptyWav(), CancellationToken.None);
            Assert.Equal("engine_busy", result.ErrorCode);
            Assert.Equal(1, malformed.BytesRead);
        }

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocking = new BlockingContent(started);
        var blockingHandler = new CountingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = blocking }));
        using var scope = AiOutageAmbient.Push(new AiOutageRegistry(new ManualAiClock()));
        using var cts = new CancellationTokenSource();
        var pending = SpeechClient(blockingHandler, key: "cancel-429").TranscribeAsync(EmptyWav(), cts.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task SpeechCircuit_BlocksHttp_BillingQuotaDoesNotCount_LocalBusyDoesNotCount()
    {
        var clock = new ManualAiClock();
        using var scope = AiOutageAmbient.Push(new AiOutageRegistry(clock));
        var handler = new CountingHandler((_, _) => Task.FromResult(Status(HttpStatusCode.InternalServerError)));
        var transcriber = SpeechClient(handler);
        for (var i = 0; i < 3; i++)
        {
            var failed = await transcriber.TranscribeAsync(EmptyWav(), CancellationToken.None);
            Assert.Equal("engine_http_error", failed.ErrorCode);
        }

        var blocked = await transcriber.TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal("circuit_open", blocked.ErrorCode);
        Assert.Equal(3, handler.Calls);

        var busyHandler = new CountingHandler((_, _) => Task.FromResult(Status(HttpStatusCode.InternalServerError)));
        var busy = SpeechClient(busyHandler, key: "busy-key", limiter: new OnceBusyLimiter());
        var localBusy = await busy.TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal("engine_busy", localBusy.ErrorCode);
        var localOutcome = SpeakingAssessmentOutcome.FromTranscriptionFailure(localBusy);
        Assert.Equal(429, localOutcome.StatusCode);
        Assert.Contains("Hàng đợi", localOutcome.Message, StringComparison.Ordinal);
        Assert.Equal(0, busyHandler.Calls);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal("engine_http_error", (await busy.TranscribeAsync(EmptyWav(), CancellationToken.None)).ErrorCode);
        }

        Assert.Equal("circuit_open", (await busy.TranscribeAsync(EmptyWav(), CancellationToken.None)).ErrorCode);
        Assert.Equal(3, busyHandler.Calls);

        var quotaHandler = new CountingHandler((_, _) => Task.FromResult(Status(HttpStatusCode.TooManyRequests, """{"error":{"code":"insufficient_quota"}}""")));
        var quota = SpeechClient(quotaHandler, key: "quota-key");
        for (var i = 0; i < 4; i++)
        {
            var result = await quota.TranscribeAsync(EmptyWav(), CancellationToken.None);
            Assert.Equal("quota_exhausted", result.ErrorCode);
            Assert.Equal(503, SpeakingAssessmentOutcome.FromTranscriptionFailure(result).StatusCode);
        }

        Assert.Equal(4, quotaHandler.Calls);
    }

    [Fact]
    public void OlderRelease_DoesNotAdmitASecondRecoveryProbe()
    {
        var clock = new ManualAiClock();
        var registry = new AiOutageRegistry(clock);
        var older = Enter(registry);
        OpenThroughOtherFailures(registry);
        clock.Advance(AiOutageCircuit.OpenDuration);
        var probe = Enter(registry);
        Assert.Equal(AiOutageAdmission.Probe, probe.Admission);

        older.Release();

        var competitor = Enter(registry);
        Assert.Equal(AiOutageAdmission.Rejected, competitor.Admission);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("nontransient")]
    [InlineData("stale-transient")]
    public void OlderCompletion_DoesNotResetAHeldRecoveryProbe(string completion)
    {
        var clock = new ManualAiClock();
        var registry = new AiOutageRegistry(clock);
        var older = Enter(registry);
        OpenThroughOtherFailures(registry);
        clock.Advance(AiOutageCircuit.OpenDuration);
        var probe = Enter(registry);
        Assert.Equal(AiOutageAdmission.Probe, probe.Admission);

        if (completion == "success") older.CompleteSuccess();
        else if (completion == "nontransient") older.CompleteNonTransient();
        else older.CompleteTransient(null);
        if (completion == "stale-transient") clock.Advance(AiOutageCircuit.OpenDuration);

        var competitor = Enter(registry);
        Assert.Equal(AiOutageAdmission.Rejected, competitor.Admission);
        probe.CompleteSuccess();
        Assert.Equal(AiOutageAdmission.Allowed, Enter(registry).Admission);
    }

    [Fact]
    public async Task OlderInFlightSuccess_DoesNotAdmitASecondProbeOverHttp()
    {
        var clock = new ManualAiClock();
        using var scope = AiOutageAmbient.Push(new AiOutageRegistry(clock));
        var releaseOlder = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var olderEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CountingHandler handler = null!;
        handler = new CountingHandler(async (_, ct) =>
        {
            if (handler.Calls == 1)
            {
                olderEntered.TrySetResult();
                await releaseOlder.Task.WaitAsync(ct);
                return Status(HttpStatusCode.OK, Completed());
            }

            if (handler.Calls == 5)
            {
                probeEntered.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
            }

            return Status(HttpStatusCode.InternalServerError);
        });
        var client = TextClient(handler, timeoutSeconds: 30);
        var older = client.GenerateReplyAsync(1, Messages, CancellationToken.None);
        await olderEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<AiProviderException>(() => client.GenerateReplyAsync(1, Messages, CancellationToken.None));
        }

        clock.Advance(AiOutageCircuit.OpenDuration);
        using var probeCancel = new CancellationTokenSource();
        var probe = client.GenerateReplyAsync(1, Messages, probeCancel.Token);
        await probeEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        releaseOlder.TrySetResult();
        Assert.Equal("Hello", (await older.WaitAsync(TimeSpan.FromSeconds(3))).Text);
        var blocked = await Assert.ThrowsAsync<AiProviderException>(() => client.GenerateReplyAsync(1, Messages, CancellationToken.None));
        Assert.Equal(AiProviderException.ErrorCodeCircuitOpen, blocked.ErrorCode);
        Assert.Equal(5, handler.Calls);
        probeCancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe);
    }

    private static void Fail(AiOutageCircuit circuit, TimeSpan? retryAfter = null)
    {
        var ticket = circuit.TryAdmit();
        Assert.Equal(AiOutageAdmission.Allowed, ticket.Admission);
        circuit.CompleteTransient(ticket, retryAfter);
    }

    private static AiOutageLease Enter(AiOutageRegistry registry)
        => registry.TryEnter(AiProviderFamily.Text, "https://api.openai.com/v1/responses", "gpt-6-luna", "key");

    private static void OpenThroughOtherFailures(AiOutageRegistry registry)
    {
        for (var i = 0; i < 3; i++)
        {
            var lease = Enter(registry);
            Assert.Equal(AiOutageAdmission.Allowed, lease.Admission);
            lease.CompleteTransient(null);
        }

        Assert.Equal(AiOutageAdmission.Rejected, Enter(registry).Admission);
    }

    [Fact]
    public async Task GenerationCircuitOpen_ClearsUnsentAllowance_AndKeepsEarlierEmbeddingUsage()
    {
        var journal = new RecordingJournal();
        var selector = new ThrowingSelector(new AiProviderException(
            "paused",
            AiProviderException.ErrorCodeCircuitOpen,
            false));
        var pipeline = new GroundedChatPipeline(
            new OutOfScopeAnswer(),
            new ScriptedHybrid(TwoBells()),
            selector,
            new SimpleAiTokenCounter());
        Assert.True(ChatTurnBudget.TryCreate("what color is the bell", 200, 64, 4600, out var budget));
        var execution = await pipeline.ExecuteAsync(new GroundedTurnInput(
            7,
            "what color is the bell",
            [],
            [],
            budget,
            journal), CancellationToken.None);
        Assert.True(selector.Calls > 0);
        Assert.Equal(HybridRagOutcome.ProviderUnavailable, execution.Answer.Outcome);
        Assert.Contains(journal.Events, item => item.Op == "usage" && item.Stage == AiQuotaTurnStage.Embedding && item.Known == 17 && item.Unknown == 0);
        Assert.DoesNotContain(journal.Events, item => item.Op == "unknown" && item.Stage == AiQuotaTurnStage.Generating);
        Assert.Contains(journal.Events, item => item.Op == "clear" && item.Stage == AiQuotaTurnStage.Generating);
    }

    private static OpenAiProviderClient TextClient(
        CountingHandler handler,
        int retries = 0,
        string key = "fixture-key",
        int timeoutSeconds = 5,
        IAiTransportClock? clock = null)
        => new(new HandlerFactory(handler), Options.Create(new AiOptions
        {
            ApiKey = key,
            RetryMaxAttempts = retries,
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-6-luna",
            ReasoningEffort = "low",
            RequestTimeoutSeconds = timeoutSeconds
        }), NullLogger<OpenAiProviderClient>.Instance, clock);

    private static OpenAiEmbeddingClient EmbeddingClient(CountingHandler handler, string key = "fixture-key")
        => new(new HandlerFactory(handler), Options.Create(new AiOptions
        {
            ApiKey = key,
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-6-luna",
            ReasoningEffort = "low"
        }), Options.Create(new EmbeddingOptions
        {
            RetryMaxAttempts = 0,
            RequestTimeoutSeconds = 5
        }), NullLogger<OpenAiEmbeddingClient>.Instance);

    private static OpenAiSpeechTranscriber SpeechClient(CountingHandler handler, string key = "speech-key", ISpeechInferenceLimiter? limiter = null)
    {
        var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        limiter ??= new SpeechInferenceLimiter(Options.Create(new SpeechProcessingOptions
        {
            MaxConcurrentInferences = 1,
            MaxQueuedInferences = 4
        }));
        return new OpenAiSpeechTranscriber(
            http,
            Options.Create(new SpeechProcessingOptions
            {
                InferenceTimeoutSeconds = 5,
                MaxConcurrentInferences = 1,
                MaxQueuedInferences = 4,
                OpenAI = new OpenAiSpeechOptions { Model = "gpt-transcribe", BaseUrl = "https://api.openai.com/v1/" }
            }),
            new FixedCredentials(key),
            limiter,
            NullLogger<OpenAiSpeechTranscriber>.Instance);
    }

    private static HttpResponseMessage Status(HttpStatusCode status, string body = "{}")
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string Completed() => JsonSerializer.Serialize(new
    {
        status = "completed",
        model = "gpt-6-luna",
        output = new object[]
        {
            new
            {
                type = "message",
                role = "assistant",
                status = "completed",
                content = new[] { new { type = "output_text", text = "Hello" } }
            }
        },
        usage = new { input_tokens = 1, output_tokens = 1, total_tokens = 2 }
    });

    private static MemoryStream EmptyWav() => new(new byte[] { 0, 1, 2, 3 });

    private static HybridRetrievalResult TwoBells()
        => new(HybridRetrievalModes.Hybrid, "test",
        [
            new HybridEvidenceItem("bell-red", "reading:1", "reading", "hash", "The bell is red.", "Reading", 1, 1, null),
            new HybridEvidenceItem("bell-blue", "reading:2", "reading", "hash", "The bell is blue.", "Reading", 1, 2, null)
        ], false)
        {
            EmbeddingAttempted = true,
            EmbeddingPromptTokens = 17
        };

    private sealed class NoDelayClock : IAiTransportClock
    {
        public IAiTransportBudget Start(CancellationToken caller, TimeSpan timeout) => new Budget(caller, timeout);

        private sealed class Budget : IAiTransportBudget
        {
            private readonly CancellationTokenSource _budget;

            public Budget(CancellationToken caller, TimeSpan timeout)
            {
                _budget = CancellationTokenSource.CreateLinkedTokenSource(caller);
                _budget.CancelAfter(timeout);
            }

            public TimeSpan Elapsed { get; private set; }

            public CancellationToken Token => _budget.Token;

            public Task DelayAsync(TimeSpan delay)
            {
                Elapsed += delay;
                return Task.CompletedTask;
            }

            public void Dispose() => _budget.Dispose();
        }
    }

    private sealed class ManualAiClock : TimeProvider
    {
        public DateTimeOffset Utc { get; set; } = new(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Utc;

        public void Advance(TimeSpan by) => Utc += by;
    }

    private sealed class CountingContent : HttpContent
    {
        private readonly byte[] _payload;

        public CountingContent(byte[] payload) => _payload = payload;

        public int BytesRead { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => WriteAllAsync(stream, CancellationToken.None);

        protected override bool TryComputeLength(out long length)
        {
            length = _payload.Length;
            return true;
        }

        protected override Task<Stream> CreateContentReadStreamAsync()
            => Task.FromResult<Stream>(new CountingStream(this));

        private async Task WriteAllAsync(Stream stream, CancellationToken cancellationToken)
        {
            await stream.WriteAsync(_payload, cancellationToken);
            BytesRead += _payload.Length;
        }

        private sealed class CountingStream(CountingContent owner) : Stream
        {
            private int _position;

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => owner._payload.Length;
            public override long Position { get => _position; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => ReadCore(buffer.AsSpan(offset, count));
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(ReadCore(buffer.Span));
            }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override int Read(Span<byte> buffer) => ReadCore(buffer);

            private int ReadCore(Span<byte> buffer)
            {
                var count = Math.Min(buffer.Length, owner._payload.Length - _position);
                if (count <= 0) return 0;
                owner._payload.AsSpan(_position, count).CopyTo(buffer);
                _position += count;
                owner.BytesRead += count;
                return count;
            }
        }
    }

    private sealed class BlockingContent(TaskCompletionSource started) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task<Stream> CreateContentReadStreamAsync()
            => Task.FromResult<Stream>(new BlockingStream(started));

        private sealed class BlockingStream(TaskCompletionSource started) : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => 0;
            public override long Position { get => 0; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return 0;
            }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }

    private sealed class CountingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return send(request, cancellationToken);
        }
    }

    private sealed class HandlerFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class OnceBusyLimiter : ISpeechInferenceLimiter
    {
        private int _calls;

        public Task<IAsyncDisposable?> AcquireAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                return Task.FromResult<IAsyncDisposable?>(null);
            }

            return Task.FromResult<IAsyncDisposable?>(new NoopLease());
        }

        private sealed class NoopLease : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class FixedCredentials(string key) : IOpenAiSpeechCredentialResolver
    {
        public bool TryResolve(out OpenAiSpeechCredentials credentials)
        {
            credentials = new OpenAiSpeechCredentials(key, "test", "gpt-transcribe", new Uri("https://api.openai.com/v1/"));
            return true;
        }
    }

    private sealed class OutOfScopeAnswer : IAiChatAnswerService
    {
        public Task<AiChatAnswer> AnswerAsync(int userId, string userMessage, IReadOnlyList<AiContextMessage> contextMessages, CancellationToken ct)
            => Task.FromResult(new AiChatAnswer(HybridRagOutcome.OutOfScope, null, "deterministic", [], 1, 1, 2, "fixed", "req"));
    }

    private sealed class ScriptedHybrid(HybridRetrievalResult result) : IHybridRetrievalService
    {
        public Task<HybridRetrievalResult> RetrieveAsync(int userId, string query, CancellationToken ct)
            => Task.FromResult(result);

        public Task<HybridRetrievalResult> RetrieveAsync(int userId, string query, HybridRetrievalRequest request, CancellationToken ct)
            => Task.FromResult(result);
    }

    private sealed class ThrowingSelector(AiProviderException failure) : IChatSentenceSelector
    {
        public int Calls { get; private set; }

        public Task<ChatSentenceSelection> SelectAsync(ChatSentenceSelectionRequest request, CancellationToken ct)
        {
            Calls++;
            throw failure;
        }
    }

    private sealed class RecordingJournal : IAiTurnStageJournal
    {
        public List<(string Op, AiQuotaTurnStage Stage, int Known, int Unknown)> Events { get; } = [];

        public bool HasCharge => false;

        public int Charge => 0;

        public Task<bool> BeginAsync(AiQuotaTurnStage stage, int unknownBound, CancellationToken ct)
        {
            Events.Add(("begin", stage, 0, unknownBound));
            return Task.FromResult(true);
        }

        public Task<bool> RecordKnownAsync(AiQuotaTurnStage stage, int knownTokens, CancellationToken ct)
        {
            Events.Add(("known", stage, knownTokens, 0));
            return Task.FromResult(true);
        }

        public Task<bool> RecordUsageAsync(AiQuotaTurnStage stage, int knownTokens, int unknownTokens, CancellationToken ct)
        {
            Events.Add(("usage", stage, knownTokens, unknownTokens));
            return Task.FromResult(true);
        }

        public Task<bool> RecordUnknownAsync(AiQuotaTurnStage stage, int unknownBound, CancellationToken ct)
        {
            Events.Add(("unknown", stage, 0, unknownBound));
            return Task.FromResult(true);
        }

        public Task<bool> ClearUnknownAsync(AiQuotaTurnStage stage, CancellationToken ct)
        {
            Events.Add(("clear", stage, 0, 0));
            return Task.FromResult(true);
        }

        public Task<bool> OwnsAsync(CancellationToken ct) => Task.FromResult(true);
    }
}
