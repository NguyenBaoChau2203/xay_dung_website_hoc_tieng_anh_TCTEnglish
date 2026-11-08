using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Rag;
using TCTEnglish.Services.AI.Rag.Corpus;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiRagPhase5FixRound2Tests
{
    private const string PrivateBody = "MISSING_SOURCES_PRIVATE_BODY";

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"outcome\":\"answered\"}")]
    [InlineData("{\"schemaVersion\":1,\"outcome\":\"answered\",\"scope\":\"full\",\"capturedAtUtc\":\"2026-11-07T00:00:00Z\"}")]
    [InlineData("{\"schemaVersion\":1,\"outcome\":\"answered\",\"sources\":null}")]
    [InlineData("{\"schemaVersion\":1,\"outcome\":\"answered\",\"sources\":{}}")]
    public async Task MissingSources_AreUnverified_AndLeaveProviderContext(string metadata)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
        var provider = Row(PrivateBody, metadata);
        var display = Row(PrivateBody, metadata);
        await citations.ProjectAsync(TestDataIds.UserId, [provider], GroundingProjectionMode.ProviderContext, CancellationToken.None);
        await citations.ProjectAsync(TestDataIds.UserId, [display], GroundingProjectionMode.Display, CancellationToken.None);
        Assert.Equal(GroundingNotices.UnverifiedContext, provider.Content);
        Assert.DoesNotContain(PrivateBody, provider.Content, StringComparison.Ordinal);
        Assert.Equal(PrivateBody, display.Content);
        Assert.Equal("unverified", display.Grounding!.Status);
        Assert.Empty(display.Grounding.Sources);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"outcome\":\"out_of_scope\",\"sources\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"outcome\":\"out_of_scope\",\"Sources\":[]}")]
    public async Task ExplicitEmptySources_StayReadable(string metadata)
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
        var provider = Row("EMPTY_SOURCES_OK", metadata);
        var display = Row("EMPTY_SOURCES_OK", metadata);
        await citations.ProjectAsync(TestDataIds.UserId, [provider], GroundingProjectionMode.ProviderContext, CancellationToken.None);
        await citations.ProjectAsync(TestDataIds.UserId, [display], GroundingProjectionMode.Display, CancellationToken.None);
        Assert.Equal("EMPTY_SOURCES_OK", provider.Content);
        Assert.Null(provider.Grounding);
        Assert.Equal("EMPTY_SOURCES_OK", display.Content);
        Assert.Equal("current", display.Grounding!.Status);
        Assert.Empty(display.Grounding.Sources);
    }

    [Fact]
    public async Task LegacyNullMetadata_StaysDistinctFromMissingSources()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
        var legacy = Row("LEGACY_PLAIN_BODY", null);
        await citations.ProjectAsync(TestDataIds.UserId, [legacy], GroundingProjectionMode.ProviderContext, CancellationToken.None);
        Assert.Equal("LEGACY_PLAIN_BODY", legacy.Content);
        Assert.Null(legacy.Grounding);
    }

    [Fact]
    public async Task MissingSources_DoNotEnterHttpHistoryContext_AndOlderPageStaysUnverified()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IGroundedChatPipeline>();
            services.AddSingleton<Round2Recorder>();
            services.AddSingleton<IGroundedChatPipeline>(provider => provider.GetRequiredService<Round2Recorder>());
        });
        await factory.InitializeAsync();
        var conversationId = await SeedConversationAsync(factory);
        var olderId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.AiMessages.Add(new AiMessage
            {
                Id = olderId,
                ConversationId = conversationId,
                Role = AiMessageRole.Assistant,
                Content = PrivateBody,
                GroundingMetadata = """{"schemaVersion":1,"outcome":"answered"}""",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2)
            });
            db.AiMessages.Add(new AiMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                Role = AiMessageRole.User,
                Content = "newer-user-turn",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
            });
            await db.SaveChangesAsync();
        }

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var firstPage = await client.GetAsync($"/AI/Chat/Messages?conversationId={conversationId}&pageSize=1");
        var firstJson = await firstPage.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);
        using var firstDocument = JsonDocument.Parse(firstJson);
        var newest = Assert.Single(firstDocument.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("newer-user-turn", newest.GetProperty("content").GetString());
        Assert.True(firstDocument.RootElement.GetProperty("hasMore").GetBoolean());
        var before = newest.GetProperty("createdAtUtc").GetString() ?? throw new InvalidOperationException("Message cursor is missing.");
        var beforeId = newest.GetProperty("id").GetGuid();
        using var olderPage = await client.GetAsync($"/AI/Chat/Messages?conversationId={conversationId}&pageSize=1&beforeCreatedAtUtc={Uri.EscapeDataString(before)}&beforeId={beforeId}");
        var olderJson = await olderPage.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, olderPage.StatusCode);
        using var olderDocument = JsonDocument.Parse(olderJson);
        var older = Assert.Single(olderDocument.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(PrivateBody, older.GetProperty("content").GetString());
        Assert.Equal("unverified", older.GetProperty("grounding").GetProperty("status").GetString());
        Assert.Empty(older.GetProperty("grounding").GetProperty("sources").EnumerateArray());

        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/AI/Chat/Send")
        {
            Content = JsonContent.Create(new { conversationId, message = "hello" })
        };
        request.Headers.Add("RequestVerificationToken", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var recorder = factory.Services.GetRequiredService<Round2Recorder>();
        Assert.DoesNotContain(PrivateBody, recorder.History);
        Assert.DoesNotContain(PrivateBody, recorder.Context);
    }

    [Fact]
    public async Task ProjectionDeadline_EndsAWaitingDisplayCallWithoutReleasingTheBarrier()
    {
        await using var factory = WaitingFactory();
        await factory.InitializeAsync();
        await SetDefinitionAsync(factory, "predict-round2-token");
        using var scope = factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<ProjectionGate>();
        var chat = scope.ServiceProvider.GetRequiredService<IAiChatService>();
        var sending = chat.SendAsync(TestDataIds.UserId, null, "what does forecast mean", CancellationToken.None);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(gate.ProjectionToken.CanBeCanceled);
            var waited = Stopwatch.StartNew();
            var budget = AiChatService.AcceptedTurnProjectionBudget;
            var completed = await Task.WhenAny(sending, Task.Delay(budget + TimeSpan.FromSeconds(3))) == sending;
            Assert.True(completed);
            Assert.InRange(waited.Elapsed, budget - TimeSpan.FromSeconds(1), budget + TimeSpan.FromSeconds(3));
            var reply = await sending;
            Assert.Equal(GroundingNotices.UnverifiedContext, reply.Text);
            Assert.DoesNotContain("predict-round2-token", reply.Text, StringComparison.Ordinal);
            Assert.Equal("unverified", reply.Grounding!.Status);
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var assistant = Assert.Single(await db.AiMessages.Where(message => message.Role == AiMessageRole.Assistant).ToListAsync());
            Assert.Contains("predict-round2-token", assistant.Content, StringComparison.Ordinal);
            Assert.NotNull(assistant.GroundingMetadata);
            Assert.Equal(AiQuotaReservationStatus.Settled, (await db.AiQuotaReservationLedgers.SingleAsync()).Status);
            Assert.Equal(1, (await db.AiUserDailyQuotas.SingleAsync()).SettledQuestions);
            var history = scope.ServiceProvider.GetRequiredService<IAiConversationService>();
            var page = await history.GetMessagesPageAsync(TestDataIds.UserId, assistant.ConversationId, 10, null, null, CancellationToken.None);
            Assert.Contains("predict-round2-token", Assert.Single(page.Items, item => item.Role == AiMessageRole.Assistant).Content, StringComparison.Ordinal);
        }
        finally
        {
            gate.Release.TrySetResult(true);
            try
            {
                await sending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception)
            {
                // The assertion above is the result. This wait only stops the accepted turn
                // before the factory disposes its database scope.
            }
        }
    }

    [Fact]
    public async Task StreamCompletion_UsesTheSameProjectionDeadline()
    {
        await using var factory = WaitingFactory();
        await factory.InitializeAsync();
        await SetDefinitionAsync(factory, "predict-round2-stream");
        var conversationId = await SeedConversationAsync(factory);
        using var scope = factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<ProjectionGate>();
        var streaming = scope.ServiceProvider.GetRequiredService<IAiStreamingService>();
        var starting = streaming.StartStreamAsync(
            TestDataIds.UserId,
            conversationId,
            "what does forecast mean",
            "connection-round2",
            "127.0.0.1",
            CancellationToken.None);
        AiStreamingSession? session = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(gate.ProjectionToken.CanBeCanceled);
            var budget = AiChatService.AcceptedTurnProjectionBudget;
            var completed = await Task.WhenAny(starting, Task.Delay(budget + TimeSpan.FromSeconds(3))) == starting;
            Assert.True(completed);
            session = await starting;
            var streamed = string.Concat(session.Chunks);
            Assert.Equal(GroundingNotices.UnverifiedContext, streamed);
            Assert.DoesNotContain("predict-round2-stream", streamed, StringComparison.Ordinal);
            Assert.Equal("unverified", session.Grounding!.Status);
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            Assert.Equal(1, await db.AiMessages.CountAsync(message => message.Role == AiMessageRole.Assistant));
            Assert.Equal(1, (await db.AiUserDailyQuotas.SingleAsync()).SettledQuestions);
        }
        finally
        {
            gate.Release.TrySetResult(true);
            try
            {
                session ??= await starting.WaitAsync(TimeSpan.FromSeconds(10));
                await session.DisposeAsync();
            }
            catch (Exception)
            {
                // Cleanup must release the conversation stream even when the deadline assertion fails.
            }
        }
    }

    private static TestWebApplicationFactory WaitingFactory()
        => new(services =>
        {
            services.RemoveAll<IGroundingCitationService>();
            services.AddSingleton<ProjectionGate>();
            services.AddScoped<IGroundingCitationService>(provider => new DeadlineCitationService(
                provider.GetRequiredService<DbflashcardContext>(),
                provider.GetRequiredService<RetrievalCorpusService>(),
                provider.GetRequiredService<ProjectionGate>()));
        });

    private static async Task SetDefinitionAsync(TestWebApplicationFactory factory, string definition)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var card = await db.Cards.SingleAsync(item => item.CardId == TestDataIds.UserCardId);
        card.Definition = definition;
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedConversationAsync(TestWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var id = Guid.NewGuid();
        db.AiConversations.Add(new AiConversation
        {
            Id = id,
            UserId = TestDataIds.UserId,
            Title = "Phase 5 repair round 2",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static AiMessage Row(string content, string? metadata)
        => new()
        {
            Id = Guid.NewGuid(),
            Role = AiMessageRole.Assistant,
            Content = content,
            GroundingMetadata = metadata,
            CreatedAtUtc = DateTime.UtcNow
        };

    private sealed class Round2Recorder : IGroundedChatPipeline
    {
        public List<string> History { get; } = [];

        public List<string> Context { get; } = [];

        public Task<GroundedTurnExecution> ExecuteAsync(GroundedTurnInput input, CancellationToken ct)
        {
            History.AddRange(input.History.Select(turn => turn.Content));
            Context.AddRange(input.Context.Select(turn => turn.Content));
            return Task.FromResult(new GroundedTurnExecution(new AiChatAnswer(
                HybridRagOutcome.OutOfScope, null, "follow-up", [], 1, 1, 2, "recorder", "req")));
        }
    }

    public sealed class ProjectionGate
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken ProjectionToken { get; set; }

        public int DisplayEntries;
    }

    private sealed class DeadlineCitationService(DbflashcardContext db, RetrievalCorpusService corpus, ProjectionGate gate) : IGroundingCitationService
    {
        private readonly GroundingCitationService _inner = new(db, corpus);

        public string Capture(AiChatAnswer answer) => _inner.Capture(answer);

        public async Task ProjectAsync(int userId, IList<AiMessage> messages, GroundingProjectionMode mode, CancellationToken ct)
        {
            if (mode == GroundingProjectionMode.Display && Interlocked.Increment(ref gate.DisplayEntries) == 1)
            {
                gate.ProjectionToken = ct;
                gate.Entered.TrySetResult(true);
                await gate.Release.Task.WaitAsync(ct);
            }

            await _inner.ProjectAsync(userId, messages, mode, ct);
        }
    }
}
