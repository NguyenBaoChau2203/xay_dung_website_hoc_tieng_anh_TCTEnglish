using System.Net;
using System.Net.Http.Headers;
using System.Text;
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

public sealed class AiRagPhase3FixRound1Tests
{
    [Fact]
    public async Task F01_OneRequestBudget_DoesNotSendTheRetry()
    {
        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        context.ReadingPassages.Add(Passage("alpha marker."));
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var snapshots = new CorpusSnapshotStore(root);
            var corpus = new RetrievalCorpusService(Path.Combine(root, "missing-guides.json"), context, snapshots);
            Assert.Equal("promoted", (await corpus.BuildAsync(null, CancellationToken.None)).Status);
            RecordingHandler? handler = null;
            handler = new RecordingHandler((_, _) =>
            {
                if (handler!.Calls == 1)
                {
                    var limited = Json("{\"error\":{\"code\":\"rate_limit\"}}", HttpStatusCode.TooManyRequests);
                    limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                    return Task.FromResult(limited);
                }

                return Task.FromResult(Json(EmbeddingJson(1, 1)));
            });
            var service = Service(corpus, snapshots, new VectorIndexStore(root), Client(handler, retries: 1), TimeSpan.FromSeconds(2));
            Assert.True(EmbeddingBudget.TryCreate(1, 1000, out var budget, out _));

            var result = await service.BuildAsync(budget!, CancellationToken.None);

            Assert.Equal(1, handler.Calls);
            Assert.Equal(1, result.ApiCalls);
            Assert.NotEqual("promoted", result.Status);
            Assert.Equal(VectorIndexAvailability.Missing, new VectorIndexStore(root).LoadCurrent(result.LexicalSnapshotId).Availability);
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    [Fact]
    public async Task F02_CharacterEstimateDoesNotAdmitOverCeilingAndKnownUsageIsKept()
    {
        const string vietnamese = "Tôi học tiếng Việt mỗi ngày.";
        Assert.Equal(7, ApproximateTokenCounter.Count(vietnamese));
        Assert.True(Encoding.UTF8.GetByteCount(vietnamese) > 10);

        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        context.ReadingPassages.Add(Passage(vietnamese));
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var snapshots = new CorpusSnapshotStore(root);
            var corpus = new RetrievalCorpusService(Path.Combine(root, "missing-guides.json"), context, snapshots);
            Assert.Equal("promoted", (await corpus.BuildAsync(null, CancellationToken.None)).Status);
            var embeddings = new UsageFake(20);
            var service = Service(corpus, snapshots, new VectorIndexStore(root), embeddings, TimeSpan.FromSeconds(2));
            Assert.True(EmbeddingBudget.TryCreate(1, 10, out var budget, out _));

            var blocked = await service.BuildAsync(budget!, CancellationToken.None);

            Assert.Equal(0, embeddings.Calls);
            Assert.Equal("rejected", blocked.Status);
            Assert.Equal("budget-exceeded", blocked.Message);
            Assert.NotEqual("promoted", blocked.Status);
        }
        finally
        {
            DeleteContentRoot(root);
        }

        await using var charged = CreateSqlite();
        charged.Users.Add(User(1));
        charged.ReadingPassages.Add(Passage("alpha"));
        await charged.SaveChangesAsync();
        var chargedRoot = CreateContentRoot();
        try
        {
            var snapshots = new CorpusSnapshotStore(chargedRoot);
            var corpus = new RetrievalCorpusService(Path.Combine(chargedRoot, "missing-guides.json"), charged, snapshots);
            Assert.Equal("promoted", (await corpus.BuildAsync(null, CancellationToken.None)).Status);
            var embeddings = new UsageFake(20);
            var service = Service(corpus, snapshots, new VectorIndexStore(chargedRoot), embeddings, TimeSpan.FromSeconds(2));
            Assert.True(EmbeddingBudget.TryCreate(1, 10, out var budget, out _));
            Assert.True(Encoding.UTF8.GetByteCount("alpha") <= 10);

            var overage = await service.BuildAsync(budget!, CancellationToken.None);

            Assert.Equal(1, embeddings.Calls);
            Assert.Equal("failed", overage.Status);
            Assert.Equal("usage-exceeded", overage.Message);
            Assert.Equal(20, overage.ActualPromptTokens);
            Assert.Equal(VectorIndexAvailability.Missing, new VectorIndexStore(chargedRoot).LoadCurrent(overage.LexicalSnapshotId).Availability);
        }
        finally
        {
            DeleteContentRoot(chargedRoot);
        }
    }

    [Fact]
    public async Task F03_SeparateScopes_DoNotOverlapTheSameIndex()
    {
        var opened = OpenSqlite();
        await using var firstContext = opened.Context;
        await using var secondContext = new DbflashcardContext(opened.Options);
        firstContext.Users.Add(User(1));
        firstContext.ReadingPassages.Add(Passage("alpha marker."));
        await firstContext.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var snapshots = new CorpusSnapshotStore(root);
            var store = new VectorIndexStore(root);
            var firstCorpus = new RetrievalCorpusService(Path.Combine(root, "missing-guides.json"), firstContext, snapshots);
            Assert.Equal("promoted", (await firstCorpus.BuildAsync(null, CancellationToken.None)).Status);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var embeddings = new GatedEmbeddings(entered, gate);
            var first = Service(firstCorpus, snapshots, store, embeddings, TimeSpan.FromMilliseconds(80));
            var secondCorpus = new RetrievalCorpusService(Path.Combine(root, "missing-guides.json"), secondContext, snapshots);
            var second = Service(secondCorpus, snapshots, store, embeddings, TimeSpan.FromMilliseconds(80));
            Assert.True(EmbeddingBudget.TryCreate(2, 1000, out var budget, out _));

            var running = first.BuildAsync(budget!, CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var overlapped = await second.BuildAsync(budget!, CancellationToken.None);

            Assert.Equal("busy", overlapped.Status);
            Assert.Equal(1, embeddings.Calls);
            gate.TrySetResult();
            var completed = await running.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("promoted", completed.Status);
            Assert.Equal(1, embeddings.Calls);

            var reused = await second.BuildAsync(budget!, CancellationToken.None);
            Assert.Equal("promoted", reused.Status);
            Assert.Equal(1, embeddings.Calls);
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    [Fact]
    public async Task F04_UnpublishDuringBatch_DoesNotSendOrPromoteRevokedText()
    {
        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        var passages = Enumerable.Range(0, 65).Select(index => Passage($"note {index} marker.")).ToList();
        context.ReadingPassages.AddRange(passages);
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var snapshots = new CorpusSnapshotStore(root);
            var corpus = new RetrievalCorpusService(Path.Combine(root, "missing-guides.json"), context, snapshots);
            var lexical = await corpus.BuildAsync(null, CancellationToken.None);
            Assert.Equal("promoted", lexical.Status);
            Assert.Equal(65, lexical.ChunkCount);
            var embeddings = new MutatingFake(async () =>
            {
                foreach (var passage in passages)
                {
                    passage.IsPublished = false;
                }

                await context.SaveChangesAsync();
            });
            var service = Service(corpus, snapshots, new VectorIndexStore(root), embeddings, TimeSpan.FromSeconds(2));
            Assert.True(EmbeddingBudget.TryCreate(12, 40_000, out var budget, out _));

            var result = await service.BuildAsync(budget!, CancellationToken.None);

            Assert.Equal(new[] { 64 }, embeddings.BatchSizes);
            Assert.NotEqual("promoted", result.Status);
            Assert.Equal(VectorIndexAvailability.Missing, new VectorIndexStore(root).LoadCurrent(lexical.SnapshotId).Availability);
            Assert.True(result.ApiCalls >= 1);
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    [Fact]
    public async Task F04_ContentHashAndPrivateSourceAndSnapshotChange_StopTheNextSend()
    {
        await AssertStopped(async (context, passages) =>
        {
            passages[0].Content = "changed body after the first batch.";
            await context.SaveChangesAsync();
        });

        await AssertStopped(async (context, _) =>
        {
            var exercise = context.WritingExercises.Single();
            exercise.UserId = 1;
            await context.SaveChangesAsync();
        }, includeWriting: true);

        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        var passages = Enumerable.Range(0, 65).Select(index => Passage($"snap {index} marker.")).ToList();
        context.ReadingPassages.AddRange(passages);
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var snapshots = new CorpusSnapshotStore(root);
            var corpus = new RetrievalCorpusService(Path.Combine(root, "missing-guides.json"), context, snapshots);
            Assert.Equal("promoted", (await corpus.BuildAsync(null, CancellationToken.None)).Status);
            var embeddings = new MutatingFake(async () =>
            {
                await using var fresh = new DbflashcardContext(ContextOptions(context));
                var rebuilt = new RetrievalCorpusService(Path.Combine(root, "missing-guides.json"), fresh, snapshots);
                Assert.Equal("promoted", (await rebuilt.BuildAsync(null, CancellationToken.None)).Status);
            });
            var service = Service(corpus, snapshots, new VectorIndexStore(root), embeddings, TimeSpan.FromSeconds(2));
            Assert.True(EmbeddingBudget.TryCreate(12, 40_000, out var budget, out _));

            var result = await service.BuildAsync(budget!, CancellationToken.None);

            Assert.Equal(new[] { 64 }, embeddings.BatchSizes);
            Assert.NotEqual("promoted", result.Status);
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    [Fact]
    public async Task CancellationAfterPaidSuccess_KeepsKnownUsageAndThePreviousIndex()
    {
        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        context.ReadingPassages.Add(Passage("alpha marker."));
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var snapshots = new CorpusSnapshotStore(root);
            var corpus = new RetrievalCorpusService(Path.Combine(root, "missing-guides.json"), context, snapshots);
            var store = new VectorIndexStore(root);
            Assert.Equal("promoted", (await corpus.BuildAsync(null, CancellationToken.None)).Status);
            var firstEmbeddings = new UsageFake(4);
            var first = Service(corpus, snapshots, store, firstEmbeddings, TimeSpan.FromSeconds(2));
            Assert.True(EmbeddingBudget.TryCreate(2, 1000, out var firstBudget, out _));
            var promoted = await first.BuildAsync(firstBudget!, CancellationToken.None);
            Assert.True(promoted.Status == "promoted", promoted.Status + ":" + promoted.Message);
            var previous = store.LoadCurrent(promoted.LexicalSnapshotId);
            Assert.Equal(VectorIndexAvailability.Ready, previous.Availability);

            context.ReadingPassages.Add(Passage("second marker."));
            await context.SaveChangesAsync();
            Assert.Equal("promoted", (await corpus.BuildAsync(null, CancellationToken.None)).Status);
            using var cts = new CancellationTokenSource();
            var embeddings = new CancelAfter(cts, 9);
            var service = Service(corpus, snapshots, store, embeddings, TimeSpan.FromSeconds(2));
            Assert.True(EmbeddingBudget.TryCreate(2, 1000, out var budget, out _));

            var cancelled = await service.BuildAsync(budget!, cts.Token);

            Assert.Equal("cancelled", cancelled.Status);
            Assert.Equal(1, embeddings.Calls);
            Assert.Equal(9, cancelled.ActualPromptTokens);
            Assert.Equal(previous.IndexId, store.LoadCurrent(previous.LexicalSnapshotId).IndexId);
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    [Fact]
    public async Task F04_RetryDoesNotResendAfterTheSourceIsRevoked()
    {
        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        var passage = Passage("retry marker.");
        context.ReadingPassages.Add(passage);
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var snapshots = new CorpusSnapshotStore(root);
            var corpus = new RetrievalCorpusService(Path.Combine(root, "missing-guides.json"), context, snapshots);
            var lexical = await corpus.BuildAsync(null, CancellationToken.None);
            Assert.Equal("promoted", lexical.Status);
            RecordingHandler? handler = null;
            handler = new RecordingHandler(async (_, _) =>
            {
                if (handler!.Calls == 1)
                {
                    passage.IsPublished = false;
                    await context.SaveChangesAsync();
                    var limited = Json("{\"error\":{\"code\":\"rate_limit\"}}", HttpStatusCode.TooManyRequests);
                    limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                    return limited;
                }

                return Json(EmbeddingJson(1, 1));
            });
            var service = Service(corpus, snapshots, new VectorIndexStore(root), Client(handler, retries: 1), TimeSpan.FromSeconds(2));
            Assert.True(EmbeddingBudget.TryCreate(12, 1000, out var budget, out _));

            var result = await service.BuildAsync(budget!, CancellationToken.None);

            Assert.Equal(1, handler.Calls);
            Assert.NotEqual("promoted", result.Status);
            Assert.Equal(VectorIndexAvailability.Missing, new VectorIndexStore(root).LoadCurrent(lexical.SnapshotId).Availability);
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    private static async Task AssertStopped(Func<DbflashcardContext, List<ReadingPassage>, Task> mutate, bool includeWriting = false)
    {
        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        var count = includeWriting ? 64 : 65;
        var passages = Enumerable.Range(0, count).Select(index => Passage($"row {index} marker.")).ToList();
        context.ReadingPassages.AddRange(passages);
        if (includeWriting)
        {
            context.WritingExercises.Add(new WritingExercise
            {
                Title = "Public drill",
                Level = "A1",
                ContentType = "sentence",
                Topic = "Sea",
                SourceType = "original",
                PreviewText = "public line",
                IsPublished = true,
                CreatedAt = DateTime.UtcNow,
                WritingExerciseSentences =
                {
                    new WritingExerciseSentence { SortOrder = 1, VietnameseText = "xin chao", EnglishMeaning = "hello" }
                }
            });
        }

        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var snapshots = new CorpusSnapshotStore(root);
            var corpus = new RetrievalCorpusService(Path.Combine(root, "missing-guides.json"), context, snapshots);
            var lexical = await corpus.BuildAsync(null, CancellationToken.None);
            Assert.Equal("promoted", lexical.Status);
            Assert.Equal(65, lexical.ChunkCount);
            var embeddings = new MutatingFake(() => mutate(context, passages));
            var service = Service(corpus, snapshots, new VectorIndexStore(root), embeddings, TimeSpan.FromSeconds(2));
            Assert.True(EmbeddingBudget.TryCreate(12, 40_000, out var budget, out _));

            var result = await service.BuildAsync(budget!, CancellationToken.None);

            Assert.Equal(new[] { 64 }, embeddings.BatchSizes);
            Assert.NotEqual("promoted", result.Status);
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    private static VectorIndexService Service(
        RetrievalCorpusService corpus,
        CorpusSnapshotStore snapshots,
        VectorIndexStore store,
        IOpenAiEmbeddingClient embeddings,
        TimeSpan wait)
    {
        return new VectorIndexService(corpus, snapshots, store, embeddings, NullLogger<VectorIndexService>.Instance, wait);
    }

    private static OpenAiEmbeddingClient Client(RecordingHandler handler, int retries)
    {
        return new OpenAiEmbeddingClient(
            new HandlerFactory(handler),
            Options.Create(new AiOptions { ApiKey = "fixture-key", BaseUrl = "https://api.openai.com/v1", Model = "gpt-6-luna", ReasoningEffort = "low" }),
            Options.Create(new EmbeddingOptions { RetryMaxAttempts = retries, RequestTimeoutSeconds = 5 }),
            NullLogger<OpenAiEmbeddingClient>.Instance);
    }

    private static string EmbeddingJson(int prompt, int total)
    {
        var rest = string.Join(',', Enumerable.Repeat("0", EmbeddingOptions.Dimensions - 1));
        return "{\"object\":\"list\",\"model\":\"text-embedding-3-small\",\"data\":[{\"object\":\"embedding\",\"index\":0,\"embedding\":[1," + rest + "]}],\"usage\":{\"prompt_tokens\":" + prompt.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"total_tokens\":" + total.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}}";
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private static ReadingPassage Passage(string content)
    {
        return new ReadingPassage
        {
            Title = "Note",
            Content = content,
            Level = "A1",
            Topic = "Sea",
            IsPublished = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static User User(int id)
    {
        return new User
        {
            UserId = id,
            Email = $"user{id}@test.local",
            PasswordHash = "hash",
            FullName = "User",
            Role = Roles.Standard
        };
    }

    private static string CreateContentRoot()
    {
        var root = Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")), ".appdata", "local", "corpus-phase3-fix", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteContentRoot(string root)
    {
        var parent = Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")), ".appdata", "local", "corpus-phase3-fix");
        if (CorpusPathGuard.IsContained(parent, root) && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Microsoft.Data.Sqlite.SqliteConnection> SqliteKeepers = new();

    private static DbflashcardContext CreateSqlite()
    {
        var opened = OpenSqlite();
        OpenOptions.Add(opened.Context, opened.Options);
        return opened.Context;
    }

    private static (DbflashcardContext Context, DbContextOptions<DbflashcardContext> Options) OpenSqlite()
    {
        var connection = SqliteKeepers.GetOrAdd(Guid.NewGuid().ToString("N"), static name =>
        {
            var keeper = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source=file:{name}?mode=memory&cache=shared");
            keeper.Open();
            return keeper;
        });
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>()
            .Options;
        var context = new DbflashcardContext(options);
        context.Database.EnsureCreated();
        return (context, options);
    }

    private static DbContextOptions<DbflashcardContext> ContextOptions(DbflashcardContext context)
    {
        if (!OpenOptions.TryGetValue(context, out var options))
        {
            throw new InvalidOperationException("The SQLite options for this fixture were not retained.");
        }

        return options;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DbflashcardContext, DbContextOptions<DbflashcardContext>> OpenOptions = new();

    private sealed class UsageFake(int promptTokens) : IOpenAiEmbeddingClient
    {
        public int Calls { get; private set; }

        public Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct)
        {
            Calls++;
            var vectors = inputs.Select(_ => Axis()).ToList();
            return Task.FromResult(new EmbeddingBatch(vectors, promptTokens, promptTokens));
        }

        private static float[] Axis()
        {
            var vector = new float[EmbeddingOptions.Dimensions];
            vector[0] = 1f;
            return vector;
        }
    }

    private sealed class GatedEmbeddings(TaskCompletionSource entered, TaskCompletionSource release) : IOpenAiEmbeddingClient
    {
        public int Calls { get; private set; }

        public async Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct)
        {
            var call = Interlocked.Increment(ref _calls);
            Calls = call;
            if (call == 1)
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(ct);
            }

            return new EmbeddingBatch(inputs.Select(_ => Axis()).ToList(), Math.Max(1, inputs.Count), Math.Max(1, inputs.Count));
        }

        private int _calls;

        private static float[] Axis()
        {
            var vector = new float[EmbeddingOptions.Dimensions];
            vector[0] = 1f;
            return vector;
        }
    }

    private sealed class CancelAfter(CancellationTokenSource cancel, int promptTokens) : IOpenAiEmbeddingClient
    {
        public int Calls { get; private set; }

        public Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct)
        {
            Calls++;
            cancel.Cancel();
            return Task.FromResult(new EmbeddingBatch(inputs.Select(_ => Axis()).ToList(), promptTokens, promptTokens));
        }

        private static float[] Axis()
        {
            var vector = new float[EmbeddingOptions.Dimensions];
            vector[0] = 1f;
            return vector;
        }
    }

    private sealed class MutatingFake(Func<Task> mutate) : IOpenAiEmbeddingClient
    {
        public List<int> BatchSizes { get; } = [];

        public async Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct)
        {
            BatchSizes.Add(inputs.Count);
            if (BatchSizes.Count == 1)
            {
                await mutate();
            }

            return new EmbeddingBatch(inputs.Select(_ => Axis()).ToList(), Math.Max(1, inputs.Count), Math.Max(1, inputs.Count));
        }

        private static float[] Axis()
        {
            var vector = new float[EmbeddingOptions.Dimensions];
            vector[0] = 1f;
            return vector;
        }
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
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
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        }
    }
}
