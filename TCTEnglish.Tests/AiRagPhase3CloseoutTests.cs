using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Rag.Corpus;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiRagPhase3CloseoutTests
{
    [Fact]
    public async Task InvalidVectorResponse_RetainsItsValidUsage()
    {
        var handler = new Handler((_, _) => Task.FromResult(Response(3, validVector: false)));
        var ledger = new EmbeddingOperationBudget(2, 20);
        var failure = await Assert.ThrowsAsync<AiProviderException>(() => Client(handler).EmbedAsync(
            ["alpha"], new EmbeddingInvocation { Budget = ledger }, CancellationToken.None));
        Assert.Equal(AiProviderException.ErrorCodeInvalidResponse, failure.ErrorCode);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(3, ledger.KnownPromptTokens);
        Assert.False(ledger.UsageIncomplete);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("cancelled")]
    [InlineData("timeout")]
    public async Task SentRequest_WithoutUsableResponse_ReportsUnknownUsage(string scenario)
    {
        using var caller = new CancellationTokenSource();
        var handler = new Handler(async (_, token) =>
        {
            if (scenario == "cancelled")
            {
                caller.Cancel();
                token.ThrowIfCancellationRequested();
            }
            if (scenario == "timeout") await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{") };
        });
        var ledger = new EmbeddingOperationBudget(2, 20);
        var invocation = new EmbeddingInvocation { Budget = ledger };
        if (scenario == "cancelled")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).EmbedAsync(["alpha"], invocation, caller.Token));
        else
            await Assert.ThrowsAsync<AiProviderException>(() => Client(handler, timeout: 1).EmbedAsync(["alpha"], invocation, caller.Token));
        Assert.Equal(1, handler.Calls);
        Assert.Equal(1, ledger.Attempts);
        Assert.Equal(0, ledger.KnownPromptTokens);
        Assert.True(ledger.UsageIncomplete);
    }

    [Fact]
    public async Task UnknownFirstAttempt_CannotRetryPastTheTokenCeiling()
    {
        Handler? handler = null;
        handler = new Handler((_, _) => handler!.Calls == 1
            ? throw new IOException("Synthetic response lost after send")
            : Task.FromResult(Response(1)));
        var ledger = new EmbeddingOperationBudget(2, 5);
        await Assert.ThrowsAsync<AiProviderException>(() => Client(handler, retries: 1).EmbedAsync(
            ["alpha"], new EmbeddingInvocation { Budget = ledger }, CancellationToken.None));
        Assert.Equal(1, handler.Calls);
        Assert.True(ledger.UsageIncomplete);
    }

    [Fact]
    public async Task UnknownAttempt_RemainsReservedAcrossSuccessfulRetryAndLaterBatches()
    {
        Handler? handler = null;
        handler = new Handler((_, _) => handler!.Calls == 1
            ? throw new IOException("Synthetic response lost after send")
            : Task.FromResult(Response(1)));
        var ledger = new EmbeddingOperationBudget(3, 10);
        var client = Client(handler, retries: 1);
        var invocation = new EmbeddingInvocation { Budget = ledger };
        var recovered = await client.EmbedAsync(["alpha"], invocation, CancellationToken.None);
        Assert.Equal(1, recovered.PromptTokens);
        Assert.Equal(2, handler.Calls);
        // Five possible tokens from the lost response plus one known token leave
        // four, which cannot safely admit another five-byte input.
        await Assert.ThrowsAsync<AiProviderException>(() => client.EmbedAsync(["alpha"], invocation, CancellationToken.None));
        Assert.Equal(2, handler.Calls);
        Assert.Equal(1, ledger.KnownPromptTokens);
        Assert.True(ledger.UsageIncomplete);
    }

    [Fact]
    public async Task CorpusAboveOneJobCeiling_ResumesFromCacheWithoutPromotingPartialIndex()
    {
        await using var keeper = new SqliteConnection("Data Source=:memory:");
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(keeper)
            .ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>().Options;
        await using var context = new DbflashcardContext(options);
        await context.Database.EnsureCreatedAsync();
        for (var index = 0; index < 3; index++) context.ReadingPassages.Add(new ReadingPassage
        {
            Title = "Bounded source " + index,
            Content = "marker " + index + " " + new string((char)('a' + index), 210) + ".",
            IsPublished = true, Level = "A1", Topic = "Sea", CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var parent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".appdata", "local", "corpus-phase3-closeout"));
        var root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var snapshots = new CorpusSnapshotStore(root);
            var corpus = new RetrievalCorpusService(Path.Combine(root, "no-guides.json"), context, snapshots);
            var lexical = await corpus.BuildAsync(null, CancellationToken.None);
            Assert.Equal("promoted", lexical.Status);
            var store = new VectorIndexStore(root);
            var embeddings = new BoundUsageEmbeddings();
            var service = new VectorIndexService(corpus, snapshots, store, embeddings, NullLogger<VectorIndexService>.Instance);
            Assert.True(EmbeddingBudget.TryCreate(1, 250, out var budget, out _));
            Assert.True((await service.DryRunAsync(CancellationToken.None)).SafeInputTokenUpperBound > 250);
            for (var completed = 1; completed <= 3; completed++)
            {
                var result = await service.BuildAsync(budget!, CancellationToken.None);
                Assert.Equal(1, result.ApiCalls);
                Assert.InRange(result.ActualPromptTokens, 1, 250);
                Assert.Equal(completed == 3 ? "promoted" : "partial", result.Status);
                Assert.Equal(completed, embeddings.Calls);
                Assert.Equal(3 - completed, (await service.DryRunAsync(CancellationToken.None)).CacheMisses);
                Assert.Equal(completed == 3 ? VectorIndexAvailability.Ready : VectorIndexAvailability.Missing,
                    store.LoadCurrent(lexical.SnapshotId).Availability);
            }
            Assert.Equal(3, store.LoadCurrent(lexical.SnapshotId).Index!.Entries.Count);
        }
        finally
        {
            if (CorpusPathGuard.IsContained(parent, root) && Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreflightAndCacheFailure_ReportOnlyActualAttemptsAndKeepUsage(bool missingKey)
    {
        await using var keeper = new SqliteConnection("Data Source=:memory:");
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(keeper)
            .ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>().Options;
        await using var context = new DbflashcardContext(options);
        await context.Database.EnsureCreatedAsync();
        context.ReadingPassages.Add(new ReadingPassage { Title = "Cost fixture", Content = "alpha", IsPublished = true,
            Level = "A1", Topic = "Sea", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var parent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".appdata", "local", "corpus-phase3-closeout"));
        var root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var snapshots = new CorpusSnapshotStore(root);
            var corpus = new RetrievalCorpusService(Path.Combine(root, "no-guides.json"), context, snapshots);
            Assert.Equal("promoted", (await corpus.BuildAsync(null, CancellationToken.None)).Status);
            var store = new FailingCache(new VectorIndexStore(root));
            var handler = new Handler((_, _) => Task.FromResult(Response(3)));
            var service = new VectorIndexService(corpus, snapshots, store,
                Client(handler, key: missingKey ? string.Empty : "synthetic-review-key"), NullLogger<VectorIndexService>.Instance);
            Assert.True(EmbeddingBudget.TryCreate(1, 100, out var budget, out _));
            var failed = await service.BuildAsync(budget!, CancellationToken.None);
            Assert.Equal("failed", failed.Status);
            Assert.Equal(missingKey ? 0 : 3, failed.ActualPromptTokens);
            Assert.Equal(missingKey ? 0 : 1, failed.ApiCalls);
            Assert.Equal(missingKey ? 0 : 1, handler.Calls);
            Assert.True(failed.PromptUsageComplete);
            Assert.Equal(VectorIndexAvailability.Missing, store.LoadCurrent(failed.LexicalSnapshotId).Availability);
        }
        finally
        {
            if (CorpusPathGuard.IsContained(parent, root) && Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static OpenAiEmbeddingClient Client(Handler handler, int retries = 0, int timeout = 5, string key = "synthetic-review-key") => new(
        new Factory(handler), Options.Create(new AiOptions { ApiKey = key }),
        Options.Create(new EmbeddingOptions { RetryMaxAttempts = retries, RequestTimeoutSeconds = timeout }),
        NullLogger<OpenAiEmbeddingClient>.Instance);

    private static HttpResponseMessage Response(int usage, bool validVector = true)
    {
        var vector = new float[validVector ? EmbeddingOptions.Dimensions : 3];
        vector[0] = 1;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { @object = "list", model = EmbeddingOptions.ModelName,
                data = new[] { new { @object = "embedding", index = 0, embedding = vector } },
                usage = new { prompt_tokens = usage, total_tokens = usage } }), Encoding.UTF8, "application/json")
        };
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            return send(request, token);
        }
    }
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false) { Timeout = Timeout.InfiniteTimeSpan };
    }
    private sealed class BoundUsageEmbeddings : IOpenAiEmbeddingClient
    {
        public int Calls;
        public Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct)
        {
            Calls++;
            var vectors = inputs.Select(_ => { var vector = new float[1536]; vector[0] = 1; return vector; }).ToList();
            var usage = EmbeddingTokenBound.Sum(inputs);
            return Task.FromResult(new EmbeddingBatch(vectors, usage, usage));
        }
    }

    private sealed class FailingCache(IVectorIndexStore inner) : IVectorIndexStore
    {
        public string? ApprovedRoot => inner.ApprovedRoot;
        public VectorIndexLoad LoadCurrent(string? snapshot) => inner.LoadCurrent(snapshot);
        public Task<VectorIndexCommit> CommitAsync(VectorIndexPayload payload, CancellationToken ct) => inner.CommitAsync(payload, ct);
        public bool TryReadCache(string hash, out float[] vector) => inner.TryReadCache(hash, out vector);
        public Task WriteCacheAsync(string hash, float[] vector, CancellationToken ct) => throw new IOException("Synthetic cache disk failure");
    }
}
