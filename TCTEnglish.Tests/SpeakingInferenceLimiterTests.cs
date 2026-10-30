using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Speaking;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class SpeakingInferenceLimiterTests
{
    [Fact]
    public async Task QueueAndInference_ShareOneDeadline()
    {
        var handler = new HoldHandler();
        var limiter = new SpeechInferenceLimiter(Options.Create(new SpeechProcessingOptions
        {
            MaxConcurrentInferences = 1,
            MaxQueuedInferences = 4
        }));
        var holder = CreateTranscriber(handler, limiter, timeoutSeconds: 30);
        var queued = CreateTranscriber(handler, limiter, timeoutSeconds: 1);

        var holding = holder.TranscribeAsync(EmptyWav(), CancellationToken.None);
        await WaitUntilAsync(() => handler.Calls == 1);

        var started = DateTime.UtcNow;
        var result = await queued.TranscribeAsync(EmptyWav(), CancellationToken.None);
        var elapsed = DateTime.UtcNow - started;

        Assert.Equal(SpeechTranscriptionStatus.Timeout, result.Status);
        Assert.Equal("engine_timeout", result.ErrorCode);
        Assert.True(elapsed > TimeSpan.FromMilliseconds(700), $"Returned too quickly: {elapsed}.");
        Assert.True(elapsed < TimeSpan.FromSeconds(8), $"Queue wait was not inside the 1s budget: {elapsed}.");
        Assert.Equal(1, handler.Calls);

        handler.Release.TrySetResult();
        var held = await holding;
        Assert.Equal(SpeechTranscriptionStatus.Ok, held.Status);

        var after = CreateTranscriber(handler, limiter, timeoutSeconds: 5);
        var recovered = await after.TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal(SpeechTranscriptionStatus.Ok, recovered.Status);
    }

    [Fact]
    public async Task SixthAdmission_IsBusy_AndDoesNotCallHttp()
    {
        var handler = new HoldHandler();
        var limiter = new SpeechInferenceLimiter(Options.Create(new SpeechProcessingOptions
        {
            MaxConcurrentInferences = 1,
            MaxQueuedInferences = 4
        }));
        var workers = Enumerable.Range(0, 5)
            .Select(_ => CreateTranscriber(handler, limiter, 30).TranscribeAsync(EmptyWav(), CancellationToken.None))
            .ToArray();
        await WaitUntilAsync(() => handler.Calls == 1);

        var busy = await CreateTranscriber(handler, limiter, 30).TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal("engine_busy", busy.ErrorCode);
        Assert.Equal(1, handler.Calls);

        handler.Release.TrySetResult();
        await Task.WhenAll(workers);
    }

    [Fact]
    public async Task CallerCancel_WhileQueued_ReleasesTheSlot()
    {
        var handler = new HoldHandler();
        var limiter = new SpeechInferenceLimiter(Options.Create(new SpeechProcessingOptions
        {
            MaxConcurrentInferences = 1,
            MaxQueuedInferences = 4
        }));
        var holding = CreateTranscriber(handler, limiter, 30).TranscribeAsync(EmptyWav(), CancellationToken.None);
        await WaitUntilAsync(() => handler.Calls == 1);

        using var cts = new CancellationTokenSource();
        var queued = CreateTranscriber(handler, limiter, 30).TranscribeAsync(EmptyWav(), cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);

        handler.Release.TrySetResult();
        await holding;
        var next = await CreateTranscriber(handler, limiter, 5).TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal(SpeechTranscriptionStatus.Ok, next.Status);
    }

    [Fact]
    public async Task UnconfiguredEngine_DoesNotCallHttp_AcrossScopes()
    {
        var handler = new HoldHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<SpeechProcessingOptions>(options =>
        {
            options.WhisperHttpBaseUrl = string.Empty;
            options.MaxConcurrentInferences = 1;
            options.MaxQueuedInferences = 4;
        });
        services.AddSingleton<ISpeechInferenceLimiter, SpeechInferenceLimiter>();
        services.AddHttpClient<ISpeechTranscriber, WhisperHttpTranscriber>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        Assert.Same(
            first.ServiceProvider.GetRequiredService<ISpeechInferenceLimiter>(),
            second.ServiceProvider.GetRequiredService<ISpeechInferenceLimiter>());

        var result = await first.ServiceProvider.GetRequiredService<ISpeechTranscriber>()
            .TranscribeAsync(EmptyWav(), CancellationToken.None);
        Assert.Equal(SpeechTranscriptionStatus.NotReady, result.Status);
        Assert.Equal(0, handler.Calls);
    }

    private static WhisperHttpTranscriber CreateTranscriber(HoldHandler handler, ISpeechInferenceLimiter limiter, int timeoutSeconds)
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
                InferenceTimeoutSeconds = timeoutSeconds,
                MaxConcurrentInferences = 1,
                MaxQueuedInferences = 4
            }),
            limiter,
            NullLogger<WhisperHttpTranscriber>.Instance);
    }

    private static MemoryStream EmptyWav() => new(new byte[] { 0, 1, 2, 3 });

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not reached.");
            }

            await Task.Delay(20);
        }
    }

    private sealed class HoldHandler : HttpMessageHandler
    {
        public int Calls;
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            await Release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"text\":\"hello\",\"segments\":[]}")
            };
        }
    }
}
