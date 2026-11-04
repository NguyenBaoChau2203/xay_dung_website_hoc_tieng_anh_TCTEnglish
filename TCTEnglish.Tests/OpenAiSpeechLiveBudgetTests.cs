using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.Speaking;
using TCTEnglish.Tests.Infrastructure;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class OpenAiSpeechLiveBudgetTests
{
    [Theory]
    [InlineData("wrong_rate")]
    [InlineData("wrong_container")]
    [InlineData("truncated_data")]
    [InlineData("duplicate_data")]
    [InlineData("wrong_byte_rate")]
    public async Task UnverifiedWav_IsRejectedBeforeAnyPaidSend(string mutation)
    {
        var budget = new OpenAiSpeechLiveBudget();
        var inner = new CountingHandler();
        using var client = CreateClient(inner, budget);
        using var wav = SilenceWav(1);
        var bytes = wav.ToArray();
        switch (mutation)
        {
            case "wrong_rate": BitConverter.GetBytes(8_000).CopyTo(bytes, 24); break;
            case "wrong_container": bytes[0] = (byte)'X'; break;
            case "truncated_data": BitConverter.GetBytes(bytes.Length * 2).CopyTo(bytes, 40); break;
            case "wrong_byte_rate": BitConverter.GetBytes(1).CopyTo(bytes, 28); break;
            case "duplicate_data":
                bytes = bytes.Concat(bytes.AsSpan(36).ToArray()).ToArray();
                BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
                break;
        }

        using var request = CreateRequest(bytes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));
        Assert.Equal(0, inner.Calls);
        Assert.Equal(0, budget.Posts);
    }

    [Fact]
    public async Task DuplicateFileParts_AreRejectedBeforeAnyPaidSend()
    {
        var budget = new OpenAiSpeechLiveBudget();
        var inner = new CountingHandler();
        using var client = CreateClient(inner, budget);
        using var wav = SilenceWav(1);
        using var request = CreateRequest(wav.ToArray());
        ((MultipartFormDataContent)request.Content!).Add(new ByteArrayContent(wav.ToArray()), "file", "second.wav");

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));
        Assert.Equal(0, inner.Calls);
        Assert.Equal(0, budget.Posts);
    }

    [Fact]
    public async Task ValidStreamPayload_ReachesTransportUnchanged()
    {
        var budget = new OpenAiSpeechLiveBudget();
        using var wav = SilenceWav(1);
        var expected = wav.ToArray();
        var inner = new PayloadHandler(expected);
        using var client = CreateClient(inner, budget);
        using var request = new HttpRequestMessage(HttpMethod.Post, "audio/transcriptions");
        var multipart = new MultipartFormDataContent();
        multipart.Add(new StreamContent(wav), "file", "speech.wav");
        multipart.Add(new StringContent("gpt-transcribe"), "model");
        request.Content = multipart;

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, budget.Posts);
        Assert.Equal(1d, budget.AudioSeconds);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Exact120SecondBudget_IsSharedAcrossClients_ThenBlocksNextSend()
    {
        var budget = new OpenAiSpeechLiveBudget(maxPosts: 16, maxAudioSeconds: 120);
        var inner = new CountingHandler();
        using var firstClient = CreateClient(inner, budget);
        using var otherClient = CreateClient(inner, budget);
        for (var i = 0; i < 8; i++)
        {
            using var wav = SilenceWav(15);
            using var request = CreateRequest(wav.ToArray());
            using var response = await (i % 2 == 0 ? firstClient : otherClient).SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal(120d, budget.AudioSeconds);
        using var extraWav = SilenceWav(1);
        using var extra = CreateRequest(extraWav.ToArray());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => otherClient.SendAsync(extra));
        Assert.Contains("audio_cap", error.Message, StringComparison.Ordinal);
        Assert.Equal(8, inner.Calls);
    }

    private static HttpRequestMessage CreateRequest(byte[] wav)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(wav), "file", "speech.wav");
        return new HttpRequestMessage(HttpMethod.Post, "audio/transcriptions") { Content = content };
    }

    private sealed class PayloadHandler(byte[] expected) : HttpMessageHandler
    {
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var content = Assert.IsType<MultipartFormDataContent>(request.Content);
            var file = Assert.Single(content.Where(p => p.Headers.ContentDisposition?.Name?.Trim('"') == "file"));
            Assert.Equal(expected, await file.ReadAsByteArrayAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task NinthPost_IsBlocked_AtHttpBoundary()
    {
        var budget = new OpenAiSpeechLiveBudget(maxPosts: 8, maxAudioSeconds: 120);
        var inner = new CountingHandler();
        var client = CreateClient(inner, budget);
        var transcriber = CreateTranscriber(client);

        for (var i = 0; i < 8; i++)
        {
            using var wav = SilenceWav(1);
            Assert.Equal(SpeechTranscriptionStatus.Ok, (await transcriber.TranscribeAsync(wav, CancellationToken.None)).Status);
        }

        using var ninth = SilenceWav(1);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => transcriber.TranscribeAsync(ninth, CancellationToken.None));
        Assert.Contains("post_cap", error.Message, StringComparison.Ordinal);
        Assert.Equal(8, inner.Calls);
        Assert.Equal(8, budget.Posts);
    }

    [Fact]
    public async Task AudioCap_BlocksBeforeSend_UsingMeasuredWavSeconds()
    {
        var budget = new OpenAiSpeechLiveBudget(maxPosts: 8, maxAudioSeconds: 5);
        var inner = new CountingHandler();
        var client = CreateClient(inner, budget);
        var transcriber = CreateTranscriber(client);

        using var first = SilenceWav(4);
        Assert.Equal(SpeechTranscriptionStatus.Ok, (await transcriber.TranscribeAsync(first, CancellationToken.None)).Status);

        using var second = SilenceWav(2);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => transcriber.TranscribeAsync(second, CancellationToken.None));
        Assert.Contains("audio_cap", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, inner.Calls);
        Assert.InRange(budget.AudioSeconds, 3.9, 4.1);
    }

    [Fact]
    public async Task ImportAndDirect_ShareSameBudgetInstance()
    {
        var budget = new OpenAiSpeechLiveBudget(maxPosts: 3, maxAudioSeconds: 120);
        var inner = new CountingHandler();
        var client = CreateClient(inner, budget);
        var transcriber = CreateTranscriber(client);
        var planner = new FixedPlanner(
            new PcmSpeechWindow(0, 16_000),
            new PcmSpeechWindow(16_000, 32_000));
        var importer = new AudioTranscriptImporter(
            new FixedSource(),
            new FixedDecoder(SilencePcm(3)),
            transcriber,
            planner,
            new AlwaysReadyCredentials(),
            Options.Create(new SpeechProcessingOptions { Enabled = true }),
            Options.Create(new AudioImportOptions()),
            NullLogger<AudioTranscriptImporter>.Instance);

        Assert.True((await importer.TranscribeAsync("abcdefghijk", default)).Success);
        Assert.Equal(2, budget.Posts);

        using var direct = SilenceWav(1);
        Assert.Equal(SpeechTranscriptionStatus.Ok, (await transcriber.TranscribeAsync(direct, CancellationToken.None)).Status);
        Assert.Equal(3, budget.Posts);

        using var blocked = SilenceWav(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => transcriber.TranscribeAsync(blocked, CancellationToken.None));
        Assert.Equal(3, inner.Calls);
    }

    [Fact]
    public async Task FailedHttpAfterReserve_StillConsumesBudget()
    {
        var budget = new OpenAiSpeechLiveBudget(maxPosts: 2, maxAudioSeconds: 120);
        var inner = new CountingHandler(failFirst: true);
        var client = CreateClient(inner, budget);
        var transcriber = CreateTranscriber(client);

        using var wav = SilenceWav(1);
        var failed = await transcriber.TranscribeAsync(wav, CancellationToken.None);
        Assert.Equal(SpeechTranscriptionStatus.Unavailable, failed.Status);
        Assert.Equal(1, budget.Posts);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task ParallelReserves_DoNotOvershootPostCap()
    {
        var budget = new OpenAiSpeechLiveBudget(maxPosts: 8, maxAudioSeconds: 120);
        var inner = new HoldHandler();
        var client = CreateClient(inner, budget);
        var workers = Enumerable.Range(0, 12)
            .Select(async _ =>
            {
                var transcriber = CreateTranscriber(client);
                using var wav = SilenceWav(1);
                try
                {
                    await transcriber.TranscribeAsync(wav, CancellationToken.None);
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            })
            .ToArray();

        await Task.Delay(50);
        inner.Release.TrySetResult();
        var outcomes = await Task.WhenAll(workers);
        Assert.Equal(8, outcomes.Count(x => x));
        Assert.Equal(8, budget.Posts);
        Assert.Equal(8, inner.Calls);
    }

    private static HttpClient CreateClient(HttpMessageHandler inner, OpenAiSpeechLiveBudget budget)
        => new(new OpenAiSpeechLiveBudgetHandler(inner, budget), disposeHandler: false)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };

    private static OpenAiSpeechTranscriber CreateTranscriber(HttpClient client)
        => new(
            client,
            Options.Create(new SpeechProcessingOptions
            {
                InferenceTimeoutSeconds = 5,
                OpenAI = new OpenAiSpeechOptions { Model = "gpt-transcribe", BaseUrl = "https://api.openai.com/v1/" }
            }),
            new AlwaysReadyCredentials(),
            new SpeechInferenceLimiter(Options.Create(new SpeechProcessingOptions
            {
                MaxConcurrentInferences = 4,
                MaxQueuedInferences = 16
            })),
            NullLogger<OpenAiSpeechTranscriber>.Instance);

    private static MemoryStream SilenceWav(int seconds)
    {
        var pcm = SilencePcm(seconds);
        return AudioTranscriptImporter.CreateWav(pcm);
    }

    private static byte[] SilencePcm(int seconds) => new byte[seconds * AudioImportOptions.PcmBytesPerSecond];

    private sealed class CountingHandler(bool failFirst = false) : HttpMessageHandler
    {
        public int Calls;
        private int _failures;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            if (failFirst && Interlocked.Increment(ref _failures) == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{\"error\":\"x\"}", Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"text\":\"ok\"}", Encoding.UTF8, "application/json")
            });
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
                Content = new StringContent("{\"text\":\"ok\"}", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class AlwaysReadyCredentials : IOpenAiSpeechCredentialResolver
    {
        public bool TryResolve(out OpenAiSpeechCredentials credentials)
        {
            credentials = new OpenAiSpeechCredentials(
                "test-key",
                "test",
                "gpt-transcribe",
                new Uri("https://api.openai.com/v1/"));
            return true;
        }
    }

    private sealed class FixedPlanner(params PcmSpeechWindow[] windows) : IPcmSpeechWindowPlanner
    {
        public IReadOnlyList<PcmSpeechWindow> Plan(ReadOnlySpan<byte> pcm16Mono16kHz) => windows;
    }

    private sealed class FixedSource : IYoutubeAudioSource
    {
        public Task<Stream> OpenAudioAsync(string youtubeId, CancellationToken ct)
            => Task.FromResult<Stream>(new MemoryStream(new byte[] { 1, 2, 3 }));
    }

    private sealed class FixedDecoder(byte[] pcm) : IAudioPcmDecoder
    {
        public Task EnsureAvailableAsync(CancellationToken ct) => Task.CompletedTask;
        public Task DecodeAsync(string sourcePath, string pcmPath, CancellationToken ct)
            => File.WriteAllBytesAsync(pcmPath, pcm, ct);
    }
}
