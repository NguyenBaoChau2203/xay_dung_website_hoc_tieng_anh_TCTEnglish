using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Internal;
using TCTEnglish.Services.AI.Rag;
using TCTEnglish.Services.AI.Rag.Corpus;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;
using Xunit.Abstractions;

namespace TCTEnglish.Tests;

public sealed class AiRagHybridRetrievalTests
{
    private readonly ITestOutputHelper _output;

    public AiRagHybridRetrievalTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task EmbeddingsClient_MapsIndexesAndRejectsInvalidVectorsWithoutSubstitutingModel()
    {
        var seen = new List<string>();
        var handler = new RecordingHandler(async (request, ct) =>
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            seen.Add(body);
            Assert.Equal("https://api.openai.com/v1/embeddings", request.RequestUri!.GetLeftPart(UriPartial.Path));
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.DoesNotContain("reasoning", body, StringComparison.Ordinal);
            Assert.DoesNotContain("temperature", body, StringComparison.Ordinal);
            Assert.Contains("\"dimensions\":1536", body, StringComparison.Ordinal);
            Assert.Contains("text-embedding-3-small", body, StringComparison.Ordinal);
            return Json(ResponseJson([1, 0], 4, 4));
        });
        var batch = await Client(handler).EmbedAsync(["alpha", "beta"], CancellationToken.None);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(4, batch.PromptTokens);
        Assert.Equal(1f, batch.Vectors[0][0]);
        Assert.Equal(2f, batch.Vectors[1][0]);

        await AssertCode(HttpStatusCode.Unauthorized, "{\"error\":{\"code\":\"invalid_api_key\",\"message\":\"fixture-key-secret\"}}", AiProviderException.ErrorCodeAuthentication, 1);
        await AssertCode(HttpStatusCode.Forbidden, "{\"error\":{\"type\":\"permission\"}}", AiProviderException.ErrorCodeAuthentication, 1);
        await AssertCode(HttpStatusCode.NotFound, "{\"error\":{\"code\":\"model_not_found\"}}", AiProviderException.ErrorCodeModelAccess, 1);
        await AssertCode(HttpStatusCode.TooManyRequests, "{\"error\":{\"code\":\"insufficient_quota\"}}", AiProviderException.ErrorCodeQuota, 1);
        await AssertCode(HttpStatusCode.BadRequest, "{\"error\":{\"code\":\"invalid\"}}", AiProviderException.ErrorCodeInvalidConfiguration, 1);
        await AssertCode(HttpStatusCode.OK, "{", AiProviderException.ErrorCodeInvalidResponse, 1);
        await AssertCode(HttpStatusCode.OK, ResponseJson([0], 1, 2), AiProviderException.ErrorCodeInvalidResponse, 1);
        await AssertCode(HttpStatusCode.OK, ResponseJson([0, 0], 1, 1), AiProviderException.ErrorCodeInvalidResponse, 1);
        await AssertCode(HttpStatusCode.OK, ResponseJson([0], 1, 1, model: "text-embedding-3-large"), AiProviderException.ErrorCodeInvalidResponse, 1);
        await AssertCode(HttpStatusCode.OK, ResponseJson([0], 1, 1, dimensions: 3), AiProviderException.ErrorCodeInvalidResponse, 1);
        await AssertCode(HttpStatusCode.OK, ResponseJson([0], 1, 1, zero: true), AiProviderException.ErrorCodeInvalidResponse, 1);
        await AssertCode(HttpStatusCode.OK, ResponseJson([0], 1, 1, huge: true), AiProviderException.ErrorCodeInvalidResponse, 1);
        await AssertCode(HttpStatusCode.OK, ResponseJson([0], 0, 0), AiProviderException.ErrorCodeInvalidResponse, 1);
        await AssertCode(HttpStatusCode.OK, ResponseJson([0], 2, 3), AiProviderException.ErrorCodeInvalidResponse, 1);
        await AssertCode(HttpStatusCode.OK, ResponseJson([0], 2, 2, outputTokens: true), AiProviderException.ErrorCodeInvalidResponse, 1);
        Assert.Contains("\"dimensions\":1536", Assert.Single(seen), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmbeddingsClient_BoundsTransportAndDoesNotFollowRedirectsOrLogSecrets()
    {
        var logger = new ListLogger();
        var redirect = new RecordingHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.RedirectKeepVerb);
            response.Headers.Location = new Uri("https://evil.example/embeddings");
            return Task.FromResult(response);
        });
        var redirected = await Assert.ThrowsAsync<AiProviderException>(() => Client(redirect, logger: logger).EmbedAsync(["alpha"], CancellationToken.None));
        Assert.Equal(AiProviderException.ErrorCodeRedirectRefused, redirected.ErrorCode);
        Assert.Equal(1, redirect.Calls);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("fixture-key", StringComparison.Ordinal) || message.Contains("evil.example", StringComparison.Ordinal));

        RecordingHandler? rateThenOk = null;
        rateThenOk = new RecordingHandler((_, _) =>
        {
            if (rateThenOk!.Calls == 1)
            {
                var limited = Json("{\"error\":{\"code\":\"rate_limit\"}}", HttpStatusCode.TooManyRequests);
                limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                return Task.FromResult(limited);
            }

            return Task.FromResult(Json(ResponseJson([0], 2, 2)));
        });
        var recovered = await Client(rateThenOk, retries: 1).EmbedAsync(["alpha"], CancellationToken.None);
        Assert.Equal(2, rateThenOk.Calls);
        Assert.Equal(2, recovered.PromptTokens);

        var unavailable = new RecordingHandler((_, _) => Task.FromResult(Json("{}", HttpStatusCode.ServiceUnavailable)));
        var down = await Assert.ThrowsAsync<AiProviderException>(() => Client(unavailable, retries: 1).EmbedAsync(["alpha"], CancellationToken.None));
        Assert.Equal(AiProviderException.ErrorCodeProviderUnavailable, down.ErrorCode);
        Assert.Equal(2, unavailable.Calls);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var blocked = new RecordingHandler((_, _) => throw new InvalidOperationException("must not send"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(blocked).EmbedAsync(["alpha"], cancelled.Token));
        Assert.Equal(0, blocked.Calls);

        var missingKey = new RecordingHandler((_, _) => throw new InvalidOperationException("must not send"));
        var key = await Assert.ThrowsAsync<AiProviderException>(() => Client(missingKey, key: " ").EmbedAsync(["alpha"], CancellationToken.None));
        Assert.Equal(AiProviderException.ErrorCodeInvalidConfiguration, key.ErrorCode);
        Assert.Equal(0, missingKey.Calls);

        var insecure = new RecordingHandler((_, _) => throw new InvalidOperationException("must not send"));
        var endpoint = await Assert.ThrowsAsync<AiProviderException>(() => Client(insecure, baseUrl: "http://example.com/v1").EmbedAsync(["alpha"], CancellationToken.None));
        Assert.Equal(AiProviderException.ErrorCodeInvalidConfiguration, endpoint.ErrorCode);
        Assert.Equal(0, insecure.Calls);

        var slow = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new SlowContent() }));
        var timeout = await Assert.ThrowsAsync<AiProviderException>(() => Client(slow, timeoutSeconds: 1).EmbedAsync(["alpha"], CancellationToken.None));
        Assert.Equal(AiProviderException.ErrorCodeTimeout, timeout.ErrorCode);

        var huge = new RecordingHandler((_, _) => Task.FromResult(Json(new string('x', 80))));
        var bounded = await Assert.ThrowsAsync<AiProviderException>(() => Client(huge, maxBytes: 32).EmbedAsync(["alpha"], CancellationToken.None));
        Assert.Equal(AiProviderException.ErrorCodeInvalidResponse, bounded.ErrorCode);
        Assert.Equal(1, huge.Calls);

        var responsesBase = new RecordingHandler((request, _) =>
        {
            Assert.Equal("https://api.openai.com/v1/embeddings", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(Json(ResponseJson([0], 1, 1)));
        });
        await Client(responsesBase, baseUrl: "https://api.openai.com/v1/responses").EmbedAsync(["alpha"], CancellationToken.None);
    }

    [Fact]
    public void CosineAndRrf_OrderFixturesWithoutTreatingFusionAsProbability()
    {
        var query = new[] { 1f, 0f, 0f };
        Assert.True(CosineRanker.TryTopK(
            [
                new DenseCandidate("c", "reading:c", [1f, 1f, 0f]),
                new DenseCandidate("b", "reading:b", [0f, 1f, 0f]),
                new DenseCandidate("a", "reading:a", [1f, 0f, 0f])
            ],
            query,
            3,
            out var ranked,
            out _));
        Assert.Equal("a,c,b", string.Join(',', ranked.Select(item => item.ChunkId)));
        Assert.True(CosineRanker.ScoresTie(ranked[0].Score, 1d));
        Assert.True(ranked[1].Score > ranked[2].Score);

        Assert.True(CosineRanker.TryTopK(
            [
                new DenseCandidate("right", "reading:b", [1f, 0f, 0f]),
                new DenseCandidate("left", "reading:a", [1f, 0f, 0f])
            ],
            query,
            2,
            out var tied,
            out _));
        Assert.Equal("left,right", string.Join(',', tied.Select(item => item.ChunkId)));
        Assert.True(CosineRanker.ScoresTie(tied[0].Score, tied[1].Score));
        Assert.True(CosineRanker.TryTopK([new DenseCandidate("a", "reading:a", [1f, 0f, 0f])], query, 1, out var top, out _));
        Assert.Equal("a", Assert.Single(top).ChunkId);
        Assert.False(CosineRanker.TryTopK([], new[] { 0f, 0f, 0f }, 1, out _, out var invalid));
        Assert.Equal("query-vector-invalid", invalid);
        Assert.False(CosineRanker.TryTopK([new DenseCandidate("a", "reading:a", [1f])], query, 1, out _, out var mismatch));
        Assert.Equal("candidate-vector-invalid", mismatch);

        var fused = ReciprocalRankFusion.Fuse(
            [new RankedChunk("a", "reading:a"), new RankedChunk("b", "reading:b")],
            [new RankedChunk("b", "reading:b"), new RankedChunk("c", "reading:c")]);
        Assert.Equal("b,a,c", string.Join(',', fused.Select(item => item.ChunkId)));
        Assert.Equal(1d / 61 + 1d / 62, fused[0].Score, 12);
        Assert.Equal(1d / 61, fused[1].Score, 12);
        Assert.Equal(1d / 62, fused[2].Score, 12);
        Assert.All(fused, item => Assert.True(item.Score is > 0 and < 1));

        var lexicalWinner = ReciprocalRankFusion.Fuse(
            [new RankedChunk("exact", "reading:exact"), new RankedChunk("other", "reading:other")],
            [new RankedChunk("exact", "reading:exact"), new RankedChunk("other", "reading:other")]);
        Assert.Equal("exact", lexicalWinner[0].ChunkId);

        var semanticWinner = ReciprocalRankFusion.Fuse(
            [new RankedChunk("keyword", "reading:keyword"), new RankedChunk("semantic", "reading:semantic")],
            [new RankedChunk("semantic", "reading:semantic")]);
        Assert.Equal("semantic", semanticWinner[0].ChunkId);
        Assert.True(semanticWinner[0].Score > semanticWinner[1].Score);

        var overlap = ReciprocalRankFusion.DedupSources(ReciprocalRankFusion.Fuse(
            [new RankedChunk("s1", "reading:same"), new RankedChunk("s2", "reading:same"), new RankedChunk("o", "reading:other")],
            [new RankedChunk("s2", "reading:same")]));
        Assert.Equal("reading:same,reading:other", string.Join(',', overlap.Select(item => item.SourceKey)));
        Assert.Equal("s2", overlap[0].ChunkId);

        var noOverlap = ReciprocalRankFusion.Fuse([new RankedChunk("only-lexical", "reading:b")], [new RankedChunk("only-dense", "reading:a")]);
        Assert.Equal(2, noOverlap.Count);
        Assert.Equal("only-dense,only-lexical", string.Join(',', noOverlap.Select(item => item.ChunkId)));
        Assert.Equal("only-lexical", string.Join(',', ReciprocalRankFusion.Fuse([new RankedChunk("only-lexical", "reading:l")], []).Select(item => item.ChunkId)));
        Assert.Equal("only-dense", string.Join(',', ReciprocalRankFusion.Fuse([], [new RankedChunk("only-dense", "reading:d")]).Select(item => item.ChunkId)));

        var tiedFusion = ReciprocalRankFusion.Fuse(
            [new RankedChunk("c", "reading:b")],
            [new RankedChunk("d", "reading:a")]);
        Assert.Equal("d,c", string.Join(',', tiedFusion.Select(item => item.ChunkId)));
        Assert.Equal(tiedFusion[0].Score, tiedFusion[1].Score);
    }

    [Fact]
    public async Task VectorRebuild_ReusesCache_RejectsBudgetDriftAndPromotionFailure()
    {
        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        context.ReadingPassages.AddRange(Alpha(), Beta());
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var clock = Stopwatch.StartNew();
            var snapshots = new CorpusSnapshotStore(root);
            var corpus = new RetrievalCorpusService(Path.Combine(root, "missing-guides.json"), context, snapshots);
            var built = await corpus.BuildAsync(null, CancellationToken.None);
            Assert.Equal("promoted", built.Status);
            var probe = new OnceProbe();
            var store = new VectorIndexStore(root, probe);
            var embeddings = new FakeEmbeddings();
            var service = new VectorIndexService(corpus, snapshots, store, embeddings, NullLogger<VectorIndexService>.Instance, TimeSpan.FromMilliseconds(50));
            var hybrid = new HybridRetrievalService(corpus, snapshots, store, embeddings);
            Assert.True(EmbeddingBudget.TryCreate(1, 100, out var budget, out _));
            Assert.NotNull(budget);

            var preview = await service.DryRunAsync(CancellationToken.None);
            Assert.Equal("embed-dry-run", preview.Status);
            Assert.Equal(0, embeddings.Calls);
            Assert.True(preview.CacheMisses >= 2);
            Assert.True(preview.EstimatedInputTokens > 0);
            var previewLine = $"dry-run chunks={preview.ChunkCount} misses={preview.CacheMisses} estimatedTokens={preview.EstimatedInputTokens} estimatedRequests={preview.EstimatedRequests} elapsedMs={clock.ElapsedMilliseconds}";
            _output.WriteLine(previewLine);
            Console.WriteLine(previewLine);

            Assert.False(EmbeddingBudget.TryCreate(13, 100, out _, out var cap));
            Assert.Equal("request-cap", cap);
            Assert.True(EmbeddingBudget.TryCreate(1, 1, out var tiny, out _));
            var tooSmall = await service.BuildAsync(tiny!, CancellationToken.None);
            Assert.Equal("rejected", tooSmall.Status);
            Assert.Equal("budget-exceeded", tooSmall.Message);
            Assert.Equal(0, embeddings.Calls);

            var failedPromotion = await service.BuildAsync(budget!, CancellationToken.None);
            Assert.Equal("failed", failedPromotion.Status);
            Assert.Equal(1, embeddings.Calls);
            Assert.Equal(VectorIndexAvailability.Missing, store.LoadCurrent(built.SnapshotId).Availability);

            var promoted = await service.BuildAsync(budget!, CancellationToken.None);
            Assert.True(promoted.Status == "promoted", promoted.Status + ":" + promoted.Message);
            Assert.Equal(1, embeddings.Calls);
            Assert.True(promoted.IndexBytes > 0);
            var ready = store.LoadCurrent(built.SnapshotId);
            Assert.Equal(VectorIndexAvailability.Ready, ready.Availability);
            Assert.Equal(EmbeddingOptions.ModelName, ready.Index!.Model);
            Assert.Equal(EmbeddingOptions.Dimensions, ready.Index.Dimensions);

            var reused = await service.DryRunAsync(CancellationToken.None);
            Assert.Equal(0, reused.CacheMisses);
            Assert.Equal(1, embeddings.Calls);

            embeddings.Query = "alpha marker";
            embeddings.QueryAxis = 0;
            var beforeLexical = embeddings.Calls;
            var lexical = await hybrid.RetrieveAsync(1, "alpha marker", new HybridRetrievalRequest(true), CancellationToken.None);
            Assert.Equal(HybridRetrievalModes.Hybrid, lexical.Mode);
            Assert.False(lexical.AllowsUnconstrainedGeneration);
            Assert.Equal("Alpha note", lexical.Evidence[0].Title);
            Assert.Equal(beforeLexical + 1, embeddings.Calls);

            embeddings.QueryAxis = 1;
            var semantic = await hybrid.RetrieveAsync(1, "alpha marker", new HybridRetrievalRequest(true), CancellationToken.None);
            Assert.Equal(HybridRetrievalModes.Hybrid, semantic.Mode);
            Assert.Equal("Beta note", semantic.Evidence[0].Title);
            Assert.True(semantic.Evidence[0].DenseRank is 1);

            embeddings.Query = "zzzz";
            embeddings.QueryAxis = 1;
            var lexicalEmpty = await hybrid.RetrieveAsync(1, "zzzz", new HybridRetrievalRequest(true), CancellationToken.None);
            Assert.Equal(HybridRetrievalModes.Hybrid, lexicalEmpty.Mode);
            Assert.Equal("Beta note", Assert.Single(lexicalEmpty.Evidence).Title);

            var callsBeforeDeferred = embeddings.Calls;
            var deferred = await hybrid.RetrieveAsync(1, "alpha marker", CancellationToken.None);
            Assert.Equal(HybridRetrievalModes.LexicalDegraded, deferred.Mode);
            Assert.Equal("query-embedding-deferred", deferred.Reason);
            Assert.False(deferred.AllowsUnconstrainedGeneration);
            Assert.Equal(callsBeforeDeferred, embeddings.Calls);

            var beta = context.ReadingPassages.Single(item => item.Title == "Beta note");
            beta.IsPublished = false;
            await context.SaveChangesAsync();
            var callsBeforeDrift = embeddings.Calls;
            var drifted = await service.BuildAsync(budget!, CancellationToken.None);
            Assert.Equal("rejected", drifted.Status);
            Assert.Equal("corpus-drift", drifted.Message);
            Assert.Equal(callsBeforeDrift, embeddings.Calls);
            var revoked = await hybrid.RetrieveAsync(1, "alpha marker", new HybridRetrievalRequest(true), CancellationToken.None);
            Assert.DoesNotContain(revoked.Evidence, item => item.Title == "Beta note");
            Assert.Equal(callsBeforeDrift + 1, embeddings.Calls);

            context.ReadingPassages.Single(item => item.Title == "Alpha note").IsPublished = false;
            await context.SaveChangesAsync();
            var callsBeforeBlock = embeddings.Calls;
            var blocked = await hybrid.RetrieveAsync(1, "alpha marker", new HybridRetrievalRequest(true), CancellationToken.None);
            Assert.Equal("source-revoked", blocked.Reason);
            Assert.Empty(blocked.Evidence);
            Assert.True(blocked.RecheckRejected);
            Assert.False(blocked.AllowsUnconstrainedGeneration);
            Assert.Equal(callsBeforeBlock, embeddings.Calls);
            var promotedLine = $"promoted bytes={promoted.IndexBytes} chunks={promoted.ChunkCount} apiCalls={embeddings.Calls} elapsedMs={clock.ElapsedMilliseconds}";
            _output.WriteLine(promotedLine);
            Console.WriteLine(promotedLine);
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    [Fact]
    public async Task VectorIndex_ModelMismatchCorruptAndSnapshotChangeDoNotCallEmbeddings()
    {
        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        context.ReadingPassages.Add(Alpha());
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var snapshots = new CorpusSnapshotStore(root);
            var corpus = new RetrievalCorpusService(Path.Combine(root, "missing-guides.json"), context, snapshots);
            var built = await corpus.BuildAsync(null, CancellationToken.None);
            var store = new VectorIndexStore(root);
            var embeddings = new FakeEmbeddings();
            var service = new VectorIndexService(corpus, snapshots, store, embeddings, NullLogger<VectorIndexService>.Instance);
            var hybrid = new HybridRetrievalService(corpus, snapshots, store, embeddings);
            Assert.True(EmbeddingBudget.TryCreate(2, 1000, out var budget, out _));
            Assert.Equal("promoted", (await service.BuildAsync(budget!, CancellationToken.None)).Status);
            var calls = embeddings.Calls;

            embeddings.Handle = inputs => throw new AiProviderException("down", AiProviderException.ErrorCodeProviderUnavailable, true);
            var failedQuery = await hybrid.RetrieveAsync(1, "alpha marker", new HybridRetrievalRequest(true), CancellationToken.None);
            Assert.Equal(HybridRetrievalModes.LexicalDegraded, failedQuery.Mode);
            Assert.StartsWith("query-embedding-failed:", failedQuery.Reason, StringComparison.Ordinal);
            Assert.False(failedQuery.AllowsUnconstrainedGeneration);
            Assert.Contains("alpha marker", failedQuery.Evidence[0].Text, StringComparison.Ordinal);

            TamperModel(store);
            var mismatch = await hybrid.RetrieveAsync(1, "alpha marker", new HybridRetrievalRequest(true), CancellationToken.None);
            Assert.Equal(HybridRetrievalModes.LexicalDegraded, mismatch.Mode);
            Assert.Equal("vector-model-mismatch", mismatch.Reason);
            Assert.Equal(calls + 1, embeddings.Calls);

            var published = Directory.GetDirectories(Path.Combine(store.ApprovedRoot!, "vectors", "published")).Single();
            File.WriteAllText(Path.Combine(published, "vector-index.json"), "{");
            var corrupt = await hybrid.RetrieveAsync(1, "alpha marker", new HybridRetrievalRequest(true), CancellationToken.None);
            Assert.Equal(HybridRetrievalModes.LexicalDegraded, corrupt.Mode);
            Assert.Equal(calls + 1, embeddings.Calls);
            Assert.False(corrupt.AllowsUnconstrainedGeneration);

            var freshRoot = CreateContentRoot();
            try
            {
                var freshSnapshots = new CorpusSnapshotStore(freshRoot);
                var freshCorpus = new RetrievalCorpusService(Path.Combine(freshRoot, "missing-guides.json"), context, freshSnapshots);
                Assert.Equal("promoted", (await freshCorpus.BuildAsync(null, CancellationToken.None)).Status);
                var changed = new FakeEmbeddings
                {
                    Handle = _ =>
                    {
                        freshCorpus.BuildAsync(null, CancellationToken.None).GetAwaiter().GetResult();
                        return new EmbeddingBatch([Axis(0)], 1, 1);
                    }
                };
                var freshStore = new VectorIndexStore(freshRoot);
                var freshService = new VectorIndexService(freshCorpus, freshSnapshots, freshStore, changed, NullLogger<VectorIndexService>.Instance);
                var raced = await freshService.BuildAsync(budget!, CancellationToken.None);
                Assert.Equal("failed", raced.Status);
                Assert.Equal("lexical-snapshot-changed", raced.Message);
                Assert.Equal(VectorIndexAvailability.Missing, freshStore.LoadCurrent(null).Availability);
            }
            finally
            {
                DeleteContentRoot(freshRoot);
            }
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    [Fact]
    public void OrdinaryChatAndStartupRegistrationsDoNotInvokeEmbeddings()
    {
        var answer = File.ReadAllText(Path.Combine(RepoRoot(), "TCTEnglish", "Services", "AI", "Rag", "AiChatAnswerService.cs"));
        Assert.DoesNotContain("IOpenAiEmbeddingClient", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("EmbedAsync", answer, StringComparison.Ordinal);
        var parameters = typeof(AiChatAnswerService).GetConstructors().Single().GetParameters();
        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType == typeof(IOpenAiEmbeddingClient) || parameter.ParameterType == typeof(IHybridRetrievalService));

        var program = File.ReadAllText(Path.Combine(RepoRoot(), "TCTEnglish", "Program.cs"));
        Assert.Contains("AddScoped<IAiProviderClient, OpenAiProviderClient>()", program, StringComparison.Ordinal);
        Assert.Contains("OpenAiEmbeddingClient.HttpClientName", program, StringComparison.Ordinal);
        Assert.Contains("AllowAutoRedirect = false", program, StringComparison.Ordinal);
        Assert.DoesNotContain("appsettings.json", File.ReadAllText(Path.Combine(RepoRoot(), "TCTEnglish", "Services", "AI", "EmbeddingOptions.cs")), StringComparison.Ordinal);
        Assert.Equal(1536, EmbeddingOptions.Dimensions);
        Assert.Equal("text-embedding-3-small", EmbeddingOptions.ModelName);
        Assert.Equal(60, ReciprocalRankFusion.K);
    }

    [Fact]
    public async Task AdminEmbeddingMaintenance_RejectsUnauthorizedAndOverBudgetWithZeroApiCalls()
    {
        var root = CreateContentRoot();
        var embeddings = new FakeEmbeddings();
        try
        {
            await using var factory = new TestWebApplicationFactory(services =>
            {
                services.RemoveAll<ICorpusSnapshotStore>();
                services.AddSingleton<ICorpusSnapshotStore>(_ => new CorpusSnapshotStore(root));
                services.RemoveAll<IVectorIndexStore>();
                services.AddSingleton<IVectorIndexStore>(_ => new VectorIndexStore(root));
                services.RemoveAll<IOpenAiEmbeddingClient>();
                services.AddSingleton<IOpenAiEmbeddingClient>(embeddings);
                services.RemoveAll<MlNetAiQueryClassifier>();
                services.AddSingleton<MlNetAiQueryClassifier>();
            });
            await factory.InitializeAsync();
            using var ready = factory.CreateClient();
            var readyStatus = await ready.GetAsync("/health/ready");
            Assert.True(readyStatus.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable);
            Assert.Equal(0, embeddings.Calls);

            using var anonymous = IntegrationTestClientHelper.CreateAnonymousClient(factory);
            using var anonymousEmbed = Form(string.Empty, "embed", "1", "10");
            Assert.NotEqual(HttpStatusCode.OK, (await anonymous.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", anonymousEmbed)).StatusCode);

            using var standard = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
            Assert.True((await standard.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", Form(string.Empty, "embed", "12", "40000"))).StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest);

            using var admin = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.AdminUserId, Roles.Admin);
            var page = await admin.GetStringAsync("/Admin/AiManagement");
            Assert.Contains("id=\"vectorIndexForm\"", page, StringComparison.Ordinal);
            Assert.Contains("value=\"embed\"", page, StringComparison.Ordinal);
            Assert.Contains("value=\"build\"", page, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"root\"", page, StringComparison.Ordinal);
            Assert.Equal(0, embeddings.Calls);

            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", Form(string.Empty, "embed", "1", "10"))).StatusCode);
            var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(admin, "/Admin/AiManagement");
            Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", Form(token, "mutate", "1", "10"))).StatusCode);
            token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(admin, "/Admin/AiManagement");
            Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", Form(token, "embed", "13", "10"))).StatusCode);
            token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(admin, "/Admin/AiManagement");
            using var overTokens = Form(token, "embed", "1", "40001", "root", "../secret");
            Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", overTokens)).StatusCode);
            token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(admin, "/Admin/AiManagement");
            Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", Form(token, "embed-dry-run", null, null))).StatusCode);
            token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(admin, "/Admin/AiManagement");
            Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync("/Admin/AiManagement/RebuildRetrievalIndex", Form(token, "build", null, null))).StatusCode);

            using var scope = factory.Services.CreateScope();
            var answers = scope.ServiceProvider.GetRequiredService<IAiChatAnswerService>();
            var price = await answers.AnswerAsync(TestDataIds.AdminUserId, "Giá vàng hôm nay là bao nhiêu?", [], CancellationToken.None);
            Assert.Equal(HybridRagOutcome.OutOfScope, price.Outcome);
            Assert.Equal(0, embeddings.Calls);
            Assert.False(Directory.Exists(Path.Combine(root, "secret")));
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    private static async Task AssertCode(HttpStatusCode status, string body, string code, int calls)
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(Json(body, status)));
        var error = await Assert.ThrowsAsync<AiProviderException>(() => Client(handler).EmbedAsync(["alpha"], CancellationToken.None));
        Assert.Equal(code, error.ErrorCode);
        Assert.Equal(calls, handler.Calls);
        Assert.DoesNotContain("fixture-key", error.Message, StringComparison.Ordinal);
    }

    private static OpenAiEmbeddingClient Client(
        RecordingHandler handler,
        int retries = 0,
        string key = "fixture-key",
        string baseUrl = "https://api.openai.com/v1",
        int timeoutSeconds = 5,
        int maxBytes = 8 * 1024 * 1024,
        ListLogger? logger = null)
    {
        return new OpenAiEmbeddingClient(
            new HandlerFactory(handler),
            Options.Create(new AiOptions { ApiKey = key, BaseUrl = baseUrl, Model = "gpt-6-luna", ReasoningEffort = "low" }),
            Options.Create(new EmbeddingOptions { RetryMaxAttempts = retries, RequestTimeoutSeconds = timeoutSeconds, MaxResponseBytes = maxBytes }),
            logger ?? new ListLogger());
    }

    private static string ResponseJson(int[] indexes, int prompt, int total, string model = "text-embedding-3-small", int dimensions = 1536, bool zero = false, bool huge = false, bool outputTokens = false)
    {
        var data = string.Join(',', indexes.Select(index =>
        {
            var first = zero ? "0" : (index + 1).ToString(CultureInfo.InvariantCulture);
            if (huge)
            {
                first = "1e39";
            }

            var rest = string.Join(',', Enumerable.Repeat("0", Math.Max(0, dimensions - 1)));
            var values = dimensions == 0 ? string.Empty : dimensions == 1 ? first : first + "," + rest;
            return "{\"object\":\"embedding\",\"index\":" + index.ToString(CultureInfo.InvariantCulture) + ",\"embedding\":[" + values + "]}";
        }));
        var usage = outputTokens
            ? "{\"prompt_tokens\":" + prompt.ToString(CultureInfo.InvariantCulture) + ",\"total_tokens\":" + total.ToString(CultureInfo.InvariantCulture) + ",\"output_tokens\":1}"
            : "{\"prompt_tokens\":" + prompt.ToString(CultureInfo.InvariantCulture) + ",\"total_tokens\":" + total.ToString(CultureInfo.InvariantCulture) + "}";
        return "{\"object\":\"list\",\"model\":\"" + model + "\",\"data\":[" + data + "],\"usage\":" + usage + "}";
    }

    private static float[] Axis(int axis)
    {
        var vector = new float[EmbeddingOptions.Dimensions];
        vector[axis] = 1f;
        return vector;
    }

    [Fact]
    public async Task MissingVectorIndex_ReturnsLexicalDegradedWithoutQueryEmbedding()
    {
        await using var context = CreateSqlite();
        context.Users.Add(User(1));
        context.ReadingPassages.Add(Alpha());
        await context.SaveChangesAsync();
        var root = CreateContentRoot();
        try
        {
            var snapshots = new CorpusSnapshotStore(root);
            var corpus = new RetrievalCorpusService(Path.Combine(root, "missing-guides.json"), context, snapshots);
            Assert.Equal("promoted", (await corpus.BuildAsync(null, CancellationToken.None)).Status);
            var embeddings = new FakeEmbeddings();
            var hybrid = new HybridRetrievalService(corpus, snapshots, new VectorIndexStore(root), embeddings);
            var result = await hybrid.RetrieveAsync(1, "alpha marker", new HybridRetrievalRequest(true), CancellationToken.None);
            Assert.Equal(HybridRetrievalModes.LexicalDegraded, result.Mode);
            Assert.Equal("manifest-missing", result.Reason);
            Assert.Equal(0, embeddings.Calls);
            Assert.NotEmpty(result.Evidence);
        }
        finally
        {
            DeleteContentRoot(root);
        }
    }

    private static void TamperModel(VectorIndexStore store)
    {
        var root = store.ApprovedRoot!;
        var manifestPath = Path.Combine(root, "vector-manifest.json");
        var manifest = JsonSerializer.Deserialize<VectorManifestFile>(File.ReadAllText(manifestPath), SnapshotJson.Options)!;
        var indexPath = Path.Combine(root, "vectors", "published", manifest.IndexId, "vector-index.json");
        var json = File.ReadAllText(indexPath).Replace(EmbeddingOptions.ModelName, "text-embedding-3-large", StringComparison.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(json);
        File.WriteAllBytes(indexPath, bytes);
        var checksum = CorpusHash.Sha256(bytes);
        File.WriteAllText(Path.Combine(root, "vectors", "published", manifest.IndexId, "vector-index.sha256"), checksum + "\n");
        manifest.Model = "text-embedding-3-large";
        manifest.ChecksumSha256 = checksum;
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, SnapshotJson.Options));
    }

    private static ReadingPassage Alpha()
    {
        return new ReadingPassage
        {
            Title = "Alpha note",
            Content = "alpha marker alpha marker alpha marker.",
            Level = "A1",
            Topic = "Sea",
            IsPublished = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static ReadingPassage Beta()
    {
        return new ReadingPassage
        {
            Title = "Beta note",
            Content = "alpha beta harbor.",
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
            FullName = $"User {id}",
            Role = Roles.Standard
        };
    }

    private static string RepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static string CreateContentRoot()
    {
        var root = Path.Combine(RepoRoot(), ".appdata", "local", "corpus-phase3-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteContentRoot(string root)
    {
        var parent = Path.Combine(RepoRoot(), ".appdata", "local", "corpus-phase3-tests");
        if (CorpusPathGuard.IsContained(parent, root) && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Microsoft.Data.Sqlite.SqliteConnection> SqliteKeepers = new();

    private static DbflashcardContext CreateSqlite()
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
        return context;
    }

    private static FormUrlEncodedContent Form(string token, string? operation, string? requests, string? tokens, string? extraName = null, string? extraValue = null)
    {
        var fields = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(token))
        {
            fields["__RequestVerificationToken"] = token;
        }

        if (operation is not null)
        {
            fields["operation"] = operation;
        }

        if (requests is not null)
        {
            fields["maxRequests"] = requests;
        }

        if (tokens is not null)
        {
            fields["maxInputTokens"] = tokens;
        }

        if (extraName is not null && extraValue is not null)
        {
            fields[extraName] = extraValue;
        }

        return new FormUrlEncodedContent(fields);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private sealed class OnceProbe : IVectorWriteProbe
    {
        private int _hits;

        public void BeforeManifestCommit(string manifestPath)
        {
            if (System.Threading.Interlocked.Increment(ref _hits) == 1)
            {
                throw new InvalidOperationException("probe");
            }
        }
    }

    private sealed class FakeEmbeddings : IOpenAiEmbeddingClient
    {
        public int Calls { get; private set; }
        public string? Query { get; set; }
        public int QueryAxis { get; set; }
        public Func<IReadOnlyList<string>, EmbeddingBatch>? Handle { get; set; }

        public Task<EmbeddingBatch> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct)
        {
            Calls++;
            ct.ThrowIfCancellationRequested();
            if (Handle is not null)
            {
                return Task.FromResult(Handle(inputs));
            }

            if (Query is not null && inputs.Count == 1 && inputs[0] == Query)
            {
                return Task.FromResult(new EmbeddingBatch([Axis(QueryAxis)], 1, 1));
            }

            return Task.FromResult(new EmbeddingBatch(inputs.Select(VectorFor).ToList(), Math.Max(1, inputs.Count), Math.Max(1, inputs.Count)));
        }

        private static float[] VectorFor(string text)
        {
            return text.Contains("beta harbor", StringComparison.Ordinal) ? Axis(1) : Axis(0);
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

    private sealed class ListLogger : ILogger<OpenAiEmbeddingClient>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }

    private sealed class SlowContent : HttpContent
    {
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            throw new NotSupportedException();
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }
}
