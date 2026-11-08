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

public sealed class AiRagPhase4RepairRound1Tests
{
    [Fact]
    public void CompletedEmptySelection_DoesNotAnswerFromCatalog()
    {
        var bell = new GroundedSentence("bell-1", "reading:1", "reading", "The bell is red.", "/Reading/Study/1", "1");
        var composed = GroundedAnswerComposer.Compose(new GroundedComposeRequest(
            "what color is the bell",
            [],
            [bell],
            """{"sentenceIds":[]}""",
            GroundedSelectorStatus.Completed,
            false));
        Assert.NotEqual(HybridRagOutcome.Answered, composed.Outcome);
        Assert.DoesNotContain("The bell is red.", composed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RealTwoSentenceChunk_FollowUpCopiesOriginalSentenceAndProvenance()
    {
        await using var corpus = await CorpusLab.CreateAsync("The bell is red. The door is blue.");
        var hybrid = corpus.Hybrid(new ScriptEmbeddings());
        var pipeline = new GroundedChatPipeline(new RefusalAnswer(), hybrid, new ScriptedSelector("""{"sentenceIds":["unused"]}"""), new SimpleAiTokenCounter());
        var decision = Budget();
        var execution = await pipeline.ExecuteAsync(new GroundedTurnInput(
            corpus.UserId,
            "is that true?",
            [],
            [new GroundedHistoryTurn("user", "what color is the bell")],
            decision,
            null), CancellationToken.None);
        Assert.Equal(HybridRagOutcome.Answered, execution.Answer.Outcome);
        Assert.Contains("The bell is red.", execution.Answer.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("The door is blue.", execution.Answer.Text, StringComparison.Ordinal);
        var evidence = Assert.Single(execution.Answer.Evidence);
        Assert.Equal("authenticated", evidence.Visibility);
        Assert.StartsWith("/Reading/Study/", evidence.AuthorizedRoute, StringComparison.Ordinal);
        Assert.Equal(CorpusHash.Sha256("The bell is red."), evidence.ContentHash);
        Assert.False(string.IsNullOrWhiteSpace(evidence.SentenceId));
        Assert.DoesNotContain("\n", evidence.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LostEmbeddingResponse_RetainsUnknownBoundAndKnownSeven()
    {
        await using var corpus = await CorpusLab.CreateAsync("The bell is red.");
        var calls = 0;
        var handler = new RecordingHandler((_, _) =>
        {
            calls++;
            if (calls == 1)
            {
                throw new IOException("Synthetic response lost after send");
            }

            return Task.FromResult(EmbeddingResponse(7, validVector: true));
        });
        var hybrid = corpus.Hybrid(HttpEmbeddings(handler, retries: 1));
        var result = await hybrid.RetrieveAsync(corpus.UserId, "what color is the bell", new HybridRetrievalRequest(true), CancellationToken.None);
        Assert.Equal(2, handler.Calls);
        Assert.Equal(7, result.EmbeddingPromptTokens);
        Assert.True(result.EmbeddingUnknownTokens > 0);
    }

    [Fact]
    public async Task InvalidQueryVector_RetainsKnownSeven()
    {
        await using var corpus = await CorpusLab.CreateAsync("The bell is red.");
        var handler = new RecordingHandler((_, _) => Task.FromResult(EmbeddingResponse(7, validVector: false)));
        var hybrid = corpus.Hybrid(HttpEmbeddings(handler, retries: 0));
        var result = await hybrid.RetrieveAsync(corpus.UserId, "what color is the bell", new HybridRetrievalRequest(true), CancellationToken.None);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(7, result.EmbeddingPromptTokens);
        Assert.False(result.EmbeddingUsageUnknown);
    }

    [Fact]
    public async Task IncompleteGeneration_RetainsKnownOneHundredFive()
    {
        var selectorCalls = 0;
        var handler = new RecordingHandler((_, _) =>
        {
            selectorCalls++;
            var body = """{"status":"incomplete","usage":{"input_tokens":100,"output_tokens":5,"total_tokens":105},"output":[]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });
        var clock = new ManualClock();
        await using var factory = ChatFactory(clock, services =>
        {
            services.PostConfigure<AiOptions>(options =>
            {
                options.MaxOutputTokens = 64;
                options.RequestTokenBudget = 4600;
                options.RetryMaxAttempts = 0;
                options.ApiKey = "synthetic-review-key";
                options.BaseUrl = "https://api.openai.com/v1";
                options.Model = "gpt-6-luna";
                options.ReasoningEffort = "low";
            });
            services.RemoveAll<IAiProviderClient>();
            services.AddSingleton<IAiProviderClient>(_ => TextClient(handler));
            services.RemoveAll<IHybridRetrievalService>();
            services.AddSingleton<IHybridRetrievalService>(new ScriptedHybrid(TwoBells()));
        });
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var reply = await scope.ServiceProvider.GetRequiredService<IAiChatService>()
            .SendAsync(TestDataIds.UserId, null, "what color is the bell", default);
        Assert.Equal(1, selectorCalls);
        Assert.DoesNotContain("The bell is red.", reply.Text, StringComparison.Ordinal);
        using var verify = factory.Services.CreateScope();
        var ledger = await verify.ServiceProvider.GetRequiredService<DbflashcardContext>().AiQuotaReservationLedgers.AsNoTracking().SingleAsync();
        Assert.Equal(105, ledger.KnownGenerationTokens);
        Assert.True(ledger.UnknownGenerationTokens is null or 0);
        Assert.Equal(
            105 + (ledger.KnownEmbeddingTokens ?? 0) + (ledger.UnknownEmbeddingTokens ?? 0),
            ledger.ActualTokens);
    }

    [Fact]
    public async Task CjkHistory_DoesNotSendAnUnboundedSelectorPayload()
    {
        string? body = null;
        var handler = new RecordingHandler(async (request, _) =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"status":"completed","output":[{"type":"message","role":"assistant","status":"completed","content":[{"type":"output_text","text":"{\"sentenceIds\":[\"bell-1\"]}"}]}]}""", Encoding.UTF8, "application/json")
            };
        });
        var clock = new ManualClock();
        var cjk = new string('你', 1000);
        await using var factory = ChatFactory(clock, services =>
        {
            services.PostConfigure<AiOptions>(options =>
            {
                options.MaxOutputTokens = 64;
                options.RequestTokenBudget = 700;
                options.MaxInputChars = 4000;
                options.RetryMaxAttempts = 0;
                options.ApiKey = "synthetic-review-key";
                options.BaseUrl = "https://api.openai.com/v1";
                options.Model = "gpt-6-luna";
                options.ReasoningEffort = "low";
            });
            services.RemoveAll<IAiProviderClient>();
            services.AddSingleton<IAiProviderClient>(_ => TextClient(handler));
            services.RemoveAll<IHybridRetrievalService>();
            services.AddSingleton<IHybridRetrievalService>(new ScriptedHybrid(TwoBells()));
        });
        await factory.InitializeAsync();
        Guid conversationId;
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            conversationId = Guid.NewGuid();
            db.AiConversations.Add(new AiConversation
            {
                Id = conversationId,
                UserId = TestDataIds.UserId,
                Title = "history",
                CreatedAtUtc = clock.Utc.UtcDateTime,
                UpdatedAtUtc = clock.Utc.UtcDateTime
            });
            db.AiMessages.Add(new AiMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                Role = AiMessageRole.User,
                Content = cjk,
                CreatedAtUtc = clock.Utc.UtcDateTime
            });
            await db.SaveChangesAsync();
        }

        using var scope = factory.Services.CreateScope();
        try
        {
            await scope.ServiceProvider.GetRequiredService<IAiChatService>()
                .SendAsync(TestDataIds.UserId, conversationId, "what color is the bell", default);
        }
        catch (ArgumentException ex)
        {
            Assert.Contains("giới hạn token", ex.Message, StringComparison.Ordinal);
        }

        Assert.True(handler.Calls == 0 || (body is not null && Encoding.UTF8.GetByteCount(body) <= 700));
    }

    [Fact]
    public async Task AdmittedCjkHistory_StaysInsideTheSelectorCeiling_AndNarrowBudgetRefuses()
    {
        var bodies = new List<string>();
        var handler = new RecordingHandler(async (request, _) =>
        {
            bodies.Add(await request.Content!.ReadAsStringAsync());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"status":"completed","usage":{"input_tokens":20,"output_tokens":4,"total_tokens":24},"output":[{"type":"message","role":"assistant","status":"completed","content":[{"type":"output_text","text":"{\"sentenceIds\":[\"bell-red\"]}"}]}]}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var narrowAdmitted = ChatTurnBudget.TryCreate("what color is the bell", 328, 64, 700, out var narrow);
        Assert.True(!narrowAdmitted || narrow.GenerationInputUpper < 3228);
        Assert.True(ChatTurnBudget.TryCreate("what color is the bell", 200, 64, 4600, out var decision, generationAttempts: 1));
        var selector = new OpenAiChatSentenceSelector(TextClient(handler), Options.Create(new AiOptions
        {
            ApiKey = "synthetic-review-key",
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-6-luna",
            ReasoningEffort = "low",
            RetryMaxAttempts = 0,
            MaxOutputTokens = 64
        }));
        var pipeline = new GroundedChatPipeline(
            new RefusalAnswer(),
            new ScriptedHybrid(TwoBells()),
            selector,
            new SimpleAiTokenCounter());
        var cjk = new string('你', 4000);
        await pipeline.ExecuteAsync(new GroundedTurnInput(
            TestDataIds.UserId,
            "what color is the bell",
            [],
            [new GroundedHistoryTurn("user", cjk)],
            decision,
            null), CancellationToken.None);
        Assert.NotEmpty(bodies);
        Assert.All(bodies, payload => Assert.True(Encoding.UTF8.GetByteCount(payload) <= decision.GenerationInputUpper));
        Assert.DoesNotContain(cjk, bodies[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnpublishDuringSelection_DoesNotAcceptTheOldSentence()
    {
        await using var corpus = await CorpusLab.CreateAsync("The bell is red.", "The bell is blue.");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var selector = new GatedSelector(release);
        var pipeline = new GroundedChatPipeline(new RefusalAnswer(), corpus.Hybrid(new ScriptEmbeddings()), selector, new SimpleAiTokenCounter());
        var send = pipeline.ExecuteAsync(new GroundedTurnInput(
            corpus.UserId,
            "what color is the bell",
            [],
            [],
            Budget(),
            null), CancellationToken.None);
        await selector.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        corpus.UnpublishAll();
        release.TrySetResult();
        var execution = await send;
        Assert.NotEqual(HybridRagOutcome.Answered, execution.Answer.Outcome);
        Assert.DoesNotContain("The bell is red.", execution.Answer.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("The bell is blue.", execution.Answer.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpiredOwner_DoesNotSendGenerationAfterRecovery()
    {
        var clock = new ManualClock();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hybrid = new ScriptedHybrid(TwoBells()) { Release = release };
        var selector = new CountingSelector();
        await using var factory = ChatFactory(clock, services =>
        {
            services.RemoveAll<IHybridRetrievalService>();
            services.AddSingleton<IHybridRetrievalService>(hybrid);
            services.RemoveAll<IChatSentenceSelector>();
            services.AddSingleton<IChatSentenceSelector>(selector);
        });
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var send = scope.ServiceProvider.GetRequiredService<IAiChatService>()
            .SendAsync(TestDataIds.UserId, null, "what color is the bell", default);
        await hybrid.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Utc = clock.Utc.AddMinutes(4);
        using (var other = factory.Services.CreateScope())
        {
            await other.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>()
                .ReserveQuestionAsync(TestDataIds.UserId, 40, 15, 60000, default);
        }

        release.TrySetResult();
        var reply = await send;
        Assert.Equal(0, selector.Calls);
        Assert.DoesNotContain("The bell is red.", reply.Text, StringComparison.Ordinal);
        using var verify = factory.Services.CreateScope();
        var ledgers = await verify.ServiceProvider.GetRequiredService<DbflashcardContext>().AiQuotaReservationLedgers.AsNoTracking().ToListAsync();
        var first = ledgers.OrderBy(row => row.CreatedAtUtc).First();
        Assert.Equal(AiQuotaReservationStatus.Settled, first.Status);
        Assert.Null(first.KnownGenerationTokens);
    }

    private static ChatTurnBudgetDecision Budget()
    {
        Assert.True(ChatTurnBudget.TryCreate("what color is the bell", 200, 64, 4600, out var decision));
        return decision;
    }

    private static TestWebApplicationFactory ChatFactory(ManualClock clock, Action<IServiceCollection> configure)
        => new(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
            configure(services);
        });

    private static HybridRetrievalResult TwoBells()
        => new(HybridRetrievalModes.Hybrid, "test",
        [
            new HybridEvidenceItem("bell-red", "reading:1", "reading", "hash", "The bell is red.", "Reading", 1, 1, null),
            new HybridEvidenceItem("bell-blue", "reading:2", "reading", "hash", "The bell is blue.", "Reading", 1, 2, null)
        ], false)
        { EmbeddingAttempted = true, EmbeddingPromptTokens = 17 };

    private static OpenAiProviderClient TextClient(RecordingHandler handler)
        => new(new HandlerFactory(handler), Options.Create(new AiOptions
        {
            ApiKey = "synthetic-review-key",
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-6-luna",
            ReasoningEffort = "low",
            RetryMaxAttempts = 0,
            MaxOutputTokens = 64
        }), NullLogger<OpenAiProviderClient>.Instance);

    private static OpenAiEmbeddingClient HttpEmbeddings(RecordingHandler handler, int retries)
        => new(new HandlerFactory(handler),
            Options.Create(new AiOptions { ApiKey = "synthetic-review-key", BaseUrl = "https://api.openai.com/v1", Model = "gpt-6-luna", ReasoningEffort = "low" }),
            Options.Create(new EmbeddingOptions { RetryMaxAttempts = retries, RequestTimeoutSeconds = 5 }),
            NullLogger<OpenAiEmbeddingClient>.Instance);

    private static HttpResponseMessage EmbeddingResponse(int usage, bool validVector)
    {
        var vector = new float[validVector ? EmbeddingOptions.Dimensions : 3];
        vector[0] = 1;
        var json = JsonSerializer.Serialize(new
        {
            @object = "list",
            model = EmbeddingOptions.ModelName,
            data = new[] { new { @object = "embedding", index = 0, embedding = vector } },
            usage = new { prompt_tokens = usage, total_tokens = usage }
        });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Utc { get; set; } = new(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Utc;
    }

    private sealed class RefusalAnswer : IAiChatAnswerService
    {
        public Task<AiChatAnswer> AnswerAsync(int userId, string userMessage, IReadOnlyList<AiContextMessage> contextMessages, CancellationToken ct)
            => Task.FromResult(new AiChatAnswer(HybridRagOutcome.OutOfScope, null, "deterministic", [], 1, 1, 2, "fixed", "req"));
    }

    private sealed class ScriptedSelector(string json) : IChatSentenceSelector
    {
        public Task<ChatSentenceSelection> SelectAsync(ChatSentenceSelectionRequest request, CancellationToken ct)
            => Task.FromResult(new ChatSentenceSelection(json, 1, 1, 2, true));
    }

    private sealed class CountingSelector : IChatSentenceSelector
    {
        public int Calls { get; private set; }

        public Task<ChatSentenceSelection> SelectAsync(ChatSentenceSelectionRequest request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new ChatSentenceSelection("""{"sentenceIds":["bell-red"]}""", 1, 1, 2, true));
        }
    }

    private sealed class GatedSelector : IChatSentenceSelector
    {
        private readonly TaskCompletionSource _release;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public GatedSelector(TaskCompletionSource release) => _release = release;

        public async Task<ChatSentenceSelection> SelectAsync(ChatSentenceSelectionRequest request, CancellationToken ct)
        {
            Entered.TrySetResult();
            await _release.Task.WaitAsync(ct);
            var id = request.Sentences.FirstOrDefault()?.SentenceId ?? "missing";
            return new ChatSentenceSelection("{\"sentenceIds\":[\"" + id + "\"]}", 1, 1, 2, true);
        }
    }

    private sealed class ScriptedHybrid : IHybridRetrievalService
    {
        private readonly HybridRetrievalResult _result;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? Release { get; init; }
        public ScriptedHybrid(HybridRetrievalResult result) => _result = result;

        public Task<HybridRetrievalResult> RetrieveAsync(int userId, string query, CancellationToken ct)
            => RetrieveAsync(userId, query, HybridRetrievalRequest.Deferred, ct);

        public async Task<HybridRetrievalResult> RetrieveAsync(int userId, string query, HybridRetrievalRequest request, CancellationToken ct)
        {
            Entered.TrySetResult();
            if (Release is not null)
            {
                await Release.Task.WaitAsync(ct);
            }

            return _result;
        }
    }

    private sealed class ScriptEmbeddings : IOpenAiEmbeddingClient
    {
        public Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct)
        {
            var vectors = inputs.Select(_ =>
            {
                var vector = new float[EmbeddingOptions.Dimensions];
                vector[0] = 1;
                return vector;
            }).ToList();
            var usage = EmbeddingTokenBound.Sum(inputs);
            return Task.FromResult(new EmbeddingBatch(vectors, usage, usage));
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
        public HttpClient CreateClient(string name) => new(handler, false) { Timeout = Timeout.InfiniteTimeSpan };
    }

    private sealed class CorpusLab : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public int UserId { get; } = TestDataIds.UserId;
        public RetrievalCorpusService Corpus { get; }
        public CorpusSnapshotStore Snapshots { get; }
        public VectorIndexStore Vectors { get; }
        private readonly string _root;
        private readonly DbflashcardContext _context;

        private CorpusLab(SqliteConnection connection, DbflashcardContext context, RetrievalCorpusService corpus, CorpusSnapshotStore snapshots, VectorIndexStore vectors, string root)
        {
            _connection = connection;
            _context = context;
            Corpus = corpus;
            Snapshots = snapshots;
            Vectors = vectors;
            _root = root;
        }

        public static async Task<CorpusLab> CreateAsync(params string[] bodies)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<DbflashcardContext>().UseSqlite(connection)
                .ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>().Options;
            var context = new DbflashcardContext(options);
            await context.Database.EnsureCreatedAsync();
            context.Users.Add(new User
            {
                UserId = TestDataIds.UserId,
                Email = "phase4-repair@example.invalid",
                PasswordHash = "synthetic-only",
                Role = Roles.Standard
            });
            foreach (var body in bodies)
            {
                context.ReadingPassages.Add(new ReadingPassage
                {
                    Title = "Bell",
                    Content = body,
                    IsPublished = true,
                    Level = "A1",
                    Topic = "Sea",
                    CreatedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync();
            var parent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".appdata", "local", "corpus-phase4-repair"));
            var root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var snapshots = new CorpusSnapshotStore(root);
            var corpus = new RetrievalCorpusService(Path.Combine(root, "no-guides.json"), context, snapshots);
            Assert.Equal("promoted", (await corpus.BuildAsync(null, CancellationToken.None)).Status);
            var vectors = new VectorIndexStore(root);
            var indexer = new VectorIndexService(corpus, snapshots, vectors, new ScriptEmbeddings(), NullLogger<VectorIndexService>.Instance);
            Assert.True(EmbeddingBudget.TryCreate(2, 4000, out var budget, out _));
            Assert.Equal("promoted", (await indexer.BuildAsync(budget!, CancellationToken.None)).Status);
            return new CorpusLab(connection, context, corpus, snapshots, vectors, root);
        }

        public HybridRetrievalService Hybrid(IOpenAiEmbeddingClient embeddings)
            => new(Corpus, Snapshots, Vectors, embeddings);

        public void UnpublishAll()
        {
            foreach (var passage in _context.ReadingPassages)
            {
                passage.IsPublished = false;
            }

            _context.SaveChanges();
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
