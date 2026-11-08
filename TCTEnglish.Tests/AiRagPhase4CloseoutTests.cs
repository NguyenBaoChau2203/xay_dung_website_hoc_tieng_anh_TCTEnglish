using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Rag;
using TCTEnglish.Services.AI.Rag.Corpus;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiRagPhase4CloseoutTests
{
    private const string Question = "what color is the bell";

    [Fact]
    public async Task CancelAfterGenerationBody_KeepsReceiptAndNoAssistant()
    {
        using var caller = new CancellationTokenSource();
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new EofContent(TextBody, caller) }));
        await using var host = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiChatAnswerService>(); services.AddSingleton<IAiChatAnswerService>(new Refusal());
            services.RemoveAll<IHybridRetrievalService>(); services.AddSingleton<IHybridRetrievalService>(new MutableHybrid());
            services.RemoveAll<IAiProviderClient>(); services.AddSingleton<IAiProviderClient>(TextClient(handler));
            services.PostConfigure<AiOptions>(o => { o.MaxOutputTokens = 64; o.RetryMaxAttempts = 0; });
        });
        await host.InitializeAsync();
        using var scope = host.Services.CreateScope();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.ServiceProvider.GetRequiredService<IAiChatService>()
            .SendAsync(TestDataIds.UserId, null, Question, caller.Token));
        using var verify = host.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var ledger = await db.AiQuotaReservationLedgers.AsNoTracking().SingleAsync();
        Assert.Equal(105, ledger.KnownGenerationTokens);
        Assert.Equal(122, ledger.ActualTokens);
        Assert.Equal(0, await db.AiMessages.CountAsync(x => x.Role == AiMessageRole.Assistant));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CancelAfterEmbeddingBody_KeepsKnownReceipt()
    {
        await using var lab = await Lab.Open("The bell is red.");
        using var caller = new CancellationTokenSource();
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new EofContent(EmbeddingBody, caller) }));
        var journal = new Journal();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Pipeline(lab.Hybrid(EmbeddingClient(handler)), new Selector())
            .ExecuteAsync(Input(journal), caller.Token));
        Assert.Equal(7, journal.KnownEmbedding);
        Assert.Equal(0, journal.UnknownEmbedding);
    }

    [Fact]
    public async Task EmbeddingHttpConfigurationFailure_RemainsASentUnknownAttempt()
    {
        await using var lab = await Lab.Open("The bell is red.");
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{}") }));
        var journal = new Journal();
        await Pipeline(lab.Hybrid(EmbeddingClient(handler)), new Selector()).ExecuteAsync(Input(journal), default);
        Assert.Equal(1, handler.Calls);
        Assert.True(journal.UnknownEmbedding > 0);
    }

    [Fact]
    public async Task EmbeddingReceipt_IsRecordedBeforeRetry()
    {
        await using var lab = await Lab.Open("The bell is red.");
        var journal = new Journal();
        Handler? handler = null;
        handler = new Handler((_, _) =>
        {
            if (handler!.Calls == 2) Assert.Equal(7, journal.KnownEmbedding);
            return Task.FromResult(new HttpResponseMessage(handler.Calls == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            { Content = new StringContent(EmbeddingBody, Encoding.UTF8, "application/json") });
        });
        await Pipeline(lab.Hybrid(EmbeddingClient(handler, retries: 1)), new Selector()).ExecuteAsync(Input(journal), default);
        Assert.Equal(2, handler.Calls);
        Assert.Equal(14, journal.KnownEmbedding);
        Assert.Equal(0, journal.UnknownEmbedding);
    }

    [Fact]
    public async Task GenerationRetry_DoesNotResendRevokedEvidence()
    {
        var hybrid = new MutableHybrid();
        var journal = new Journal();
        var sentBound = 0;
        var handler = new Handler(async (request, ct) =>
        {
            sentBound = (await request.Content!.ReadAsByteArrayAsync(ct)).Length + 64;
            hybrid.Current = false;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{}") };
            response.Headers.RetryAfter = new(TimeSpan.Zero);
            return response;
        });
        var result = await Pipeline(hybrid, new OpenAiChatSentenceSelector(TextClient(handler, retries: 1), Options.Create(new AiOptions())))
            .ExecuteAsync(Input(journal), default);
        Assert.Equal(1, handler.Calls);
        Assert.NotEqual(HybridRagOutcome.Answered, result.Answer.Outcome);
        Assert.Equal(sentBound, journal.UnknownGeneration);
    }

    [Fact]
    public async Task EmbeddingRetry_DoesNotSendAfterOwnershipLost()
    {
        await using var lab = await Lab.Open("The bell is red.");
        var journal = new Journal();
        var handler = new Handler((_, _) =>
        {
            journal.Owns = false;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{}") };
            response.Headers.RetryAfter = new(TimeSpan.Zero);
            return Task.FromResult(response);
        });
        await Pipeline(lab.Hybrid(EmbeddingClient(handler, retries: 1)), new Selector()).ExecuteAsync(Input(journal), default);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SingleSentence_RechecksAfterReceiptWrite()
    {
        var hybrid = new MutableHybrid { Single = true };
        var journal = new Journal { AfterEmbedding = () => hybrid.Current = false };
        var result = await Pipeline(hybrid, new Selector()).ExecuteAsync(Input(journal), default);
        Assert.NotEqual(HybridRagOutcome.Answered, result.Answer.Outcome);
        Assert.DoesNotContain("The bell is red.", result.Answer.Text);
    }

    [Fact]
    public async Task AnswerEvidence_KeepsDocumentIdentity()
    {
        await using var lab = await Lab.Open("The bell is red.");
        var source = Assert.Single(await lab.Corpus.ListCurrentChunksAsync(default));
        var result = await Pipeline(lab.Hybrid(new FakeEmbedding()), new Selector()).ExecuteAsync(Input(null), default);
        var evidence = Assert.Single(result.Answer.Evidence);
        Assert.Equal(source.DocumentContentHash, evidence.DocumentContentHash);
        Assert.Equal(lab.Snapshots.LoadCurrent().SnapshotId, evidence.SnapshotId);
    }

    [Fact]
    public async Task ReplacedSnapshot_RejectsSelectionFromOldSnapshot()
    {
        await using var lab = await Lab.Open("The bell is red.", "The bell is blue.");
        var selector = new Selector { BeforeReply = async () => Assert.Equal("promoted", (await lab.Corpus.BuildAsync(null, default)).Status) };
        var result = await Pipeline(lab.Hybrid(new FakeEmbedding()), selector).ExecuteAsync(Input(null), default);
        Assert.Equal(1, selector.Calls);
        Assert.NotEqual(HybridRagOutcome.Answered, result.Answer.Outcome);
    }

    [Fact]
    public async Task GenerationPreflight_LeavesNoUnknownCharge()
    {
        var handler = new Handler((_, _) => throw new InvalidOperationException("must not send"));
        var journal = new Journal();
        await Pipeline(new MutableHybrid(), new OpenAiChatSentenceSelector(TextClient(handler, key: ""), Options.Create(new AiOptions())))
            .ExecuteAsync(Input(journal), default);
        Assert.Equal(0, handler.Calls);
        Assert.Equal(0, journal.UnknownGeneration);
        Assert.Equal(17, journal.KnownEmbedding);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LexicalManifestSharingConflict_RetainsAtomicPublication(bool lockDirectory)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var lab = await Lab.Open("The bell is red.");
        var current = lab.Snapshots.LoadCurrent();
        var probe = new LockCorpusManifest(lockDirectory);
        var store = new CorpusSnapshotStore(lab.Root, probe);
        var result = await store.CommitAsync(new CorpusSnapshotPayload { Json = current.Json!, DocumentCount = current.DocumentCount, ChunkCount = current.ChunkCount }, default);
        await probe.Released.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(result.Status == "promoted", result.Message);
        Assert.Equal(LexicalAvailability.Ready, store.LoadCurrent().Availability);
    }

    [Fact]
    public async Task ManifestSharingConflict_RetriesWithoutReembeddingOrLosingOldIndex()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var lab = await Lab.Open("The bell is red.");
        var current = lab.Vectors.LoadCurrent(lab.Snapshots.LoadCurrent().SnapshotId);
        var probe = new LockManifest();
        var store = new VectorIndexStore(lab.Root, probe);
        var result = await store.CommitAsync(new VectorIndexPayload { Json = current.Json!, LexicalSnapshotId = current.LexicalSnapshotId!, ChunkCount = current.Index!.Entries.Count }, default);
        await probe.Released.Task;
        Assert.True(result.Status == "promoted", result.Message);
        Assert.Equal(VectorIndexAvailability.Ready, store.LoadCurrent(current.LexicalSnapshotId).Availability);
    }

    private static GroundedChatPipeline Pipeline(IHybridRetrievalService hybrid, IChatSentenceSelector selector) => new(new Refusal(), hybrid, selector, new SimpleAiTokenCounter());
    private static GroundedTurnInput Input(IAiTurnStageJournal? journal)
    {
        Assert.True(ChatTurnBudget.TryCreate(Question, 200, 64, 4600, out var budget, generationAttempts: 2, embeddingAttempts: 2));
        return new(TestDataIds.UserId, Question, [], [], budget, journal);
    }
    internal static OpenAiProviderClient TextClient(Handler handler, int retries = 0, string key = "synthetic-review-key")
        => new(new HttpFactory(handler), Options.Create(new AiOptions { ApiKey = key, RetryMaxAttempts = retries, MaxOutputTokens = 64 }), NullLogger<OpenAiProviderClient>.Instance);
    private static OpenAiEmbeddingClient EmbeddingClient(Handler handler, int retries = 0)
        => new(new HttpFactory(handler), Options.Create(new AiOptions { ApiKey = "synthetic-review-key" }), Options.Create(new EmbeddingOptions { RetryMaxAttempts = retries }), NullLogger<OpenAiEmbeddingClient>.Instance);
    internal const string TextBody = """{"status":"completed","usage":{"input_tokens":100,"output_tokens":5,"total_tokens":105},"output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"{\"sentenceIds\":[\"red\"]}"}]}]}""";
    private static string EmbeddingBody => JsonSerializer.Serialize(new { @object = "list", model = EmbeddingOptions.ModelName,
        data = new[] { new { @object = "embedding", index = 0, embedding = Axis() } }, usage = new { prompt_tokens = 7, total_tokens = 7 } });
    private static float[] Axis() { var v = new float[1536]; v[0] = 1; return v; }
    private sealed class Refusal : IAiChatAnswerService
    { public Task<AiChatAnswer> AnswerAsync(int id, string q, IReadOnlyList<AiContextMessage> context, CancellationToken ct) => Task.FromResult(new AiChatAnswer(HybridRagOutcome.OutOfScope, null, "fixed", [], 1, 1, 2, "fixed", "req")); }
    private sealed class MutableHybrid : IHybridRetrievalService
    {
        public bool Current = true, Single;
        public Task<bool> SourcesStillCurrentAsync(int id, IReadOnlyList<GroundedSourceIdentity> sources, CancellationToken ct) => Task.FromResult(Current);
        public Task<HybridRetrievalResult> RetrieveAsync(int id, string q, CancellationToken ct) => RetrieveAsync(id, q, HybridRetrievalRequest.Deferred, ct);
        public Task<HybridRetrievalResult> RetrieveAsync(int id, string q, HybridRetrievalRequest request, CancellationToken ct)
        {
            var items = new[] { ("red", "The bell is red."), ("blue", "The bell is blue.") }.Take(Single ? 1 : 2)
                .Select(x => new HybridEvidenceItem(x.Item1, "reading:" + x.Item1, "reading", "doc", x.Item2, "Bell", 1, 1, null)
                { SentenceId = x.Item1, SentenceHash = CorpusHash.Sha256(x.Item2) }).ToArray();
            return Task.FromResult(new HybridRetrievalResult(HybridRetrievalModes.Hybrid, "test", items, false) { EmbeddingAttempted = true, EmbeddingPromptTokens = 17 });
        }
    }
    private sealed class Selector : IChatSentenceSelector
    {
        public int Calls; public Func<Task>? BeforeReply;
        public async Task<ChatSentenceSelection> SelectAsync(ChatSentenceSelectionRequest request, CancellationToken ct)
        { Calls++; if (BeforeReply is not null) await BeforeReply(); return new(JsonSerializer.Serialize(new { sentenceIds = new[] { request.Sentences[0].SentenceId } }), 1, 1, 2, true); }
    }
    private sealed class Journal : IAiTurnStageJournal
    {
        public bool Owns = true;
        public int KnownEmbedding, UnknownEmbedding, KnownGeneration, UnknownGeneration;
        public Action? AfterEmbedding;
        public bool HasCharge => Charge > 0;
        public int Charge => KnownEmbedding + UnknownEmbedding + KnownGeneration + UnknownGeneration;
        public Task<bool> OwnsAsync(CancellationToken ct) => Task.FromResult(Owns);
        public Task<bool> BeginAsync(AiQuotaTurnStage stage, int unknown, CancellationToken ct) => RecordUnknownAsync(stage, unknown, ct);
        public Task<bool> RecordKnownAsync(AiQuotaTurnStage stage, int known, CancellationToken ct) => RecordUsageAsync(stage, known, 0, ct);
        public Task<bool> RecordUsageAsync(AiQuotaTurnStage stage, int known, int unknown, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); if (!Owns) return Task.FromResult(false); if (stage == AiQuotaTurnStage.Embedding) { KnownEmbedding = known; UnknownEmbedding = unknown; AfterEmbedding?.Invoke(); } else { KnownGeneration = known; UnknownGeneration = unknown; } return Task.FromResult(true); }
        public Task<bool> RecordUnknownAsync(AiQuotaTurnStage stage, int unknown, CancellationToken ct)
        { if (stage == AiQuotaTurnStage.Embedding) UnknownEmbedding = unknown; else UnknownGeneration = unknown; return Task.FromResult(Owns); }
        public Task<bool> ClearUnknownAsync(AiQuotaTurnStage stage, CancellationToken ct) => RecordUnknownAsync(stage, 0, ct);
    }
    internal sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { public int Calls; protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { Calls++; return send(r, ct); } }
    private sealed class HttpFactory(HttpMessageHandler handler) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(handler, false) { Timeout = Timeout.InfiniteTimeSpan }; }
    internal sealed class EofContent(string text, CancellationTokenSource caller) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(Encoding.UTF8.GetBytes(text)).AsTask();
        protected override bool TryComputeLength(out long length) { length = Encoding.UTF8.GetByteCount(text); return true; }
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new EofStream(Encoding.UTF8.GetBytes(text), caller));
    }
    private sealed class EofStream(byte[] bytes, CancellationTokenSource caller) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        { var n = Read(buffer.Span); if (n == 0) caller.Cancel(); return ValueTask.FromResult(n); }
    }
    private sealed class LockManifest : IVectorWriteProbe
    {
        public TaskCompletionSource Released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void BeforeManifestCommit(string path)
        { var hold = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); _ = Task.Run(async () => { await Task.Delay(120); hold.Dispose(); Released.TrySetResult(); }); }
    }
    private sealed class LockCorpusManifest(bool lockDirectory) : ICorpusWriteProbe
    {
        public TaskCompletionSource Released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void BeforeWrite(string path)
        {
            if (!path.EndsWith(lockDirectory ? "corpus.sha256" : "manifest.json.writing", StringComparison.Ordinal)) return;
            var target = lockDirectory ? Path.Combine(Path.GetDirectoryName(path)!, "corpus.json") : path[..^".writing".Length];
            var hold = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
            _ = Task.Run(async () => { await Task.Delay(120); hold.Dispose(); Released.TrySetResult(); });
        }
    }
    private sealed class Lab : IAsyncDisposable
    {
        public string Root = null!; public DbflashcardContext Db = null!; public SqliteConnection Connection = null!;
        public RetrievalCorpusService Corpus = null!; public CorpusSnapshotStore Snapshots = null!; public VectorIndexStore Vectors = null!;
        public static async Task<Lab> Open(params string[] bodies)
        {
            var c = new SqliteConnection("Data Source=:memory:"); await c.OpenAsync();
            var db = new DbflashcardContext(new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(c).ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>().Options);
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new User { UserId = TestDataIds.UserId, Email = "closeout@test.invalid", PasswordHash = "synthetic", Role = Roles.Standard });
            foreach (var body in bodies) db.ReadingPassages.Add(new ReadingPassage { Title = "Bell", Content = body, IsPublished = true, Level = "A1", Topic = "Sea", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.appdata/local/corpus-phase4-closeout", Guid.NewGuid().ToString("N")));
            var snapshots = new CorpusSnapshotStore(root); var corpus = new RetrievalCorpusService(Path.Combine(root, "no-guides.json"), db, snapshots);
            Assert.Equal("promoted", (await corpus.BuildAsync(null, default)).Status);
            var store = new VectorIndexStore(root); EmbeddingBudget.TryCreate(2, 4000, out var budget, out _);
            var result = await new VectorIndexService(corpus, snapshots, store, new FakeEmbedding(), NullLogger<VectorIndexService>.Instance).BuildAsync(budget!, default);
            Assert.True(result.Status == "promoted", result.Message);
            return new() { Root = root, Db = db, Connection = c, Corpus = corpus, Snapshots = snapshots, Vectors = store };
        }
        public HybridRetrievalService Hybrid(IOpenAiEmbeddingClient embedding) => new(Corpus, Snapshots, Vectors, embedding);
        public async ValueTask DisposeAsync()
        { await Db.DisposeAsync(); await Connection.DisposeAsync(); var parent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.appdata/local/corpus-phase4-closeout")); if (CorpusPathGuard.IsContained(parent, Root) && Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    private sealed class FakeEmbedding : IOpenAiEmbeddingClient
    { public Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> input, CancellationToken ct) => Task.FromResult(new EmbeddingBatch(input.Select(_ => Axis()).ToArray(), 1, 1)); }
}
