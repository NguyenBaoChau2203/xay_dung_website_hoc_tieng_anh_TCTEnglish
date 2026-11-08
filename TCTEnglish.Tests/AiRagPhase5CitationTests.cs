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

public sealed class AiRagPhase5CitationTests
{
    [Fact]
    public async Task SendReloadAndOlderPages_KeepAuthorizedSourceOutcomeAndOrder()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SetCardDefinitionAsync(factory, "predict-citation-token");

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/AI/Chat/Send")
        {
            Content = JsonContent.Create(new { conversationId = (Guid?)null, message = "what does forecast mean" })
        };
        request.Headers.Add("RequestVerificationToken", token);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var sent = JsonDocument.Parse(body);
        var conversationId = sent.RootElement.GetProperty("conversationId").GetGuid();
        var grounding = sent.RootElement.GetProperty("grounding");
        Assert.Equal("answered", grounding.GetProperty("outcome").GetString());
        Assert.Equal("card:401", grounding.GetProperty("sources")[0].GetProperty("sourceKey").GetString());
        Assert.Equal("/Home/Study/301", grounding.GetProperty("sources")[0].GetProperty("route").GetString());
        Assert.Contains("predict-citation-token", sent.RootElement.GetProperty("text").GetString());
        Assert.DoesNotContain("https://", body, StringComparison.OrdinalIgnoreCase);

        string? stored;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            stored = await db.AiMessages.Where(message => message.Role == AiMessageRole.Assistant)
                .Select(message => message.GroundingMetadata)
                .SingleAsync();
        }

        Assert.NotNull(stored);
        Assert.DoesNotContain("predict-citation-token", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("/Home/", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("http", stored, StringComparison.OrdinalIgnoreCase);

        using var page = await client.GetAsync($"/AI/Chat?conversationId={conversationId}");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("data-source-key=\"card:401\"", html, StringComparison.Ordinal);
        Assert.Contains("predict-citation-token", html, StringComparison.Ordinal);

        using var messages = await client.GetAsync($"/AI/Chat/Messages?conversationId={conversationId}&pageSize=20");
        var messageJson = await messages.Content.ReadAsStringAsync();
        Assert.Contains("card:401", messageJson, StringComparison.Ordinal);
        Assert.Contains("predict-citation-token", messageJson, StringComparison.Ordinal);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var card = await db.Cards.SingleAsync(item => item.CardId == TestDataIds.UserCardId);
            db.Cards.Remove(card);
            await db.SaveChangesAsync();
        }

        using var after = await client.GetAsync($"/AI/Chat/Messages?conversationId={conversationId}&pageSize=20");
        var hidden = await after.Content.ReadAsStringAsync();
        using var hiddenDoc = JsonDocument.Parse(hidden);
        var hiddenItems = hiddenDoc.RootElement.GetProperty("items").EnumerateArray().ToList();
        var hiddenAssistant = hiddenItems.Single(item => item.GetProperty("role").GetString() == "assistant");
        Assert.Equal(GroundingNotices.Unavailable, hiddenAssistant.GetProperty("content").GetString());
        Assert.Equal("unavailable", hiddenAssistant.GetProperty("grounding").GetProperty("status").GetString());
        Assert.Empty(hiddenAssistant.GetProperty("grounding").GetProperty("sources").EnumerateArray());
        Assert.DoesNotContain("predict-citation-token", hidden, StringComparison.Ordinal);

        using var outsider = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.OutsiderUserId, Roles.Standard);
        using var denied = await outsider.GetAsync($"/AI/Chat/Messages?conversationId={conversationId}&pageSize=20");
        var deniedBody = await denied.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.DoesNotContain("predict-citation-token", deniedBody, StringComparison.Ordinal);
        Assert.DoesNotContain(GroundingNotices.Unavailable, deniedBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StreamCompletion_ReturnsTheSameAuthorizedSource()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        await SetCardDefinitionAsync(factory, "predict-citation-token");
        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);

        using var scope = factory.Services.CreateScope();
        var streaming = scope.ServiceProvider.GetRequiredService<IAiStreamingService>();
        await using var session = await streaming.StartStreamAsync(
            TestDataIds.UserId,
            conversationId,
            "what does forecast mean",
            "connection-phase5",
            "127.0.0.1",
            CancellationToken.None);
        var streamed = string.Concat(session.Chunks);
        Assert.Contains("predict-citation-token", streamed, StringComparison.Ordinal);
        Assert.NotNull(session.Grounding);
        Assert.Equal("answered", session.Grounding.Outcome);
        Assert.Equal("card:401", Assert.Single(session.Grounding.Sources).SourceKey);
    }

    [Fact]
    public async Task OlderPages_StayChronological_AndSuppressRevokedBodies()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        var baseTime = DateTime.UtcNow.AddHours(-2);
        string currentMetadata;
        string revokedMetadata;
        using (var scope = factory.Services.CreateScope())
        {
            var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
            currentMetadata = citations.Capture(Answer("card:401", GroundingHashes.Sha256("not-the-live-card")));
            revokedMetadata = citations.Capture(Answer("card:99999", GroundingHashes.Sha256("missing")));
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.AiMessages.AddRange(
                Message(conversationId, baseTime, "legacy-body", null),
                Message(conversationId, baseTime.AddMinutes(1), "SECRET_REVOKED_BODY", revokedMetadata),
                Message(conversationId, baseTime.AddMinutes(2), "question", null, AiMessageRole.User),
                Message(conversationId, baseTime.AddMinutes(3), "still-visible", currentMetadata));
            await db.SaveChangesAsync();
        }

        using var scopeRead = factory.Services.CreateScope();
        var history = scopeRead.ServiceProvider.GetRequiredService<IAiConversationService>();
        var newest = await history.GetMessagesPageAsync(TestDataIds.UserId, conversationId, 2, null, null, default);
        var older = await history.GetMessagesPageAsync(TestDataIds.UserId, conversationId, 2, newest.Items[0].CreatedAtUtc, newest.Items[0].Id, default);
        var again = await history.GetMessagesPageAsync(TestDataIds.UserId, conversationId, 2, newest.Items[0].CreatedAtUtc, newest.Items[0].Id, default);

        Assert.Equal(new[] { "question", "still-visible" }, newest.Items.Select(item => item.Content).ToArray());
        Assert.Equal("card:401", Assert.Single(newest.Items[1].Grounding!.Sources).SourceKey);
        Assert.Equal("changed", newest.Items[1].Grounding.Status);
        Assert.Equal(new[] { "legacy-body", GroundingNotices.Unavailable }, older.Items.Select(item => item.Content).ToArray());
        Assert.Null(older.Items[0].Grounding);
        Assert.Empty(older.Items[1].Grounding!.Sources);
        Assert.DoesNotContain("SECRET_REVOKED_BODY", older.Items.Select(item => item.Content));
        Assert.Equal(older.Items.Select(item => item.Id), again.Items.Select(item => item.Id));
        Assert.Equal(older.Items.Select(item => item.Content), again.Items.Select(item => item.Content));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => history.GetMessagesPageAsync(TestDataIds.OutsiderUserId, conversationId, 2, null, null, default));
    }

    [Fact]
    public async Task CorruptFutureAndDirectCalls_DoNotLeakRoutesOrForeignCards()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        var now = DateTime.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.AiMessages.AddRange(
                Message(conversationId, now, "corrupt-body", "{\"schemaVersion\":1,\"outcome\":\"answered\",\"sources\":[{\"sourceKey\":\"guide:change-password\",\"route\":\"https://evil.example/phish\",\"contentHash\":\"<script>\"}]}"),
                Message(conversationId, now.AddMinutes(1), "future-body", "{\"schemaVersion\":9,\"outcome\":\"answered\",\"sources\":[{\"sourceKey\":\"card:401\",\"route\":\"/Admin/Users\"}]}"));
            await db.SaveChangesAsync();
        }

        using var scopeRead = factory.Services.CreateScope();
        var history = scopeRead.ServiceProvider.GetRequiredService<IAiConversationService>();
        var page = await history.GetMessagesPageAsync(TestDataIds.UserId, conversationId, 10, null, null, default);
        Assert.Equal(new[] { "corrupt-body", "future-body" }, page.Items.Select(item => item.Content).ToArray());
        Assert.All(page.Items, item => Assert.Equal("unverified", item.Grounding!.Status));
        Assert.All(page.Items, item => Assert.Empty(item.Grounding!.Sources));

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var htmlResponse = await client.GetAsync($"/AI/Chat?conversationId={conversationId}");
        var html = await htmlResponse.Content.ReadAsStringAsync();
        Assert.Contains("corrupt-body", html, StringComparison.Ordinal);
        Assert.DoesNotContain("evil.example", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/Admin/Users", html, StringComparison.Ordinal);

        using var direct = factory.Services.CreateScope();
        var citations = direct.ServiceProvider.GetRequiredService<IGroundingCitationService>();
        var secret = Message(conversationId, now, "SECRET_CARD_BODY", citations.Capture(Answer("card:401", GroundingHashes.Sha256("x"))));
        await citations.ProjectAsync(TestDataIds.OutsiderUserId, [secret], GroundingProjectionMode.Display, default);
        Assert.Equal(GroundingNotices.Unavailable, secret.Content);
        Assert.DoesNotContain("SECRET_CARD_BODY", secret.Content, StringComparison.Ordinal);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => citations.ProjectAsync(
            TestDataIds.UserId,
            [Message(conversationId, now, "body", citations.Capture(Answer("card:401", GroundingHashes.Sha256("x"))))],
            GroundingProjectionMode.Display,
            cancelled.Token));
        Assert.Throws<GroundingCaptureException>(() => citations.Capture(AnswerMany()));
    }

    [Fact]
    public async Task PublishedEdit_IsLabeled_AndLeavesProviderContext()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        int passageId;
        string metadata;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var passage = new ReadingPassage
            {
                Title = "Harbor note",
                Content = "The tidal harbor marker stays fixed.",
                Level = "B1",
                Topic = "nature",
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            };
            db.ReadingPassages.Add(passage);
            await db.SaveChangesAsync();
            passageId = passage.Id;
            var corpus = scope.ServiceProvider.GetRequiredService<RetrievalCorpusService>();
            var live = await corpus.ReadVisibleDocumentAsync(TestDataIds.UserId, "reading:" + passageId, default);
            Assert.NotNull(live);
            var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
            metadata = citations.Capture(new AiChatAnswer(
                HybridRagOutcome.Answered,
                "full",
                "OLD_FACT_SENTENCE",
                [new HybridRagEvidence("reading:" + passageId, "reading-passage", "reading:" + passageId + "#s1", live.ContentHash, null, null, null, "https://evil.example", "authenticated", null, DateTime.UtcNow, live.Body)
                {
                    DocumentContentHash = live.ContentHash,
                    EntityKey = "reading:" + passageId
                }],
                1, 1, 2, "internal", "req"));
            Assert.DoesNotContain("evil.example", metadata, StringComparison.Ordinal);
        }

        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.AiMessages.Add(Message(conversationId, DateTime.UtcNow, "OLD_FACT_SENTENCE", metadata));
            await db.SaveChangesAsync();
            var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
            var messages = await db.AiMessages.AsNoTracking().Where(item => item.ConversationId == conversationId).ToListAsync();
            await citations.ProjectAsync(TestDataIds.UserId, messages, GroundingProjectionMode.Display, default);
            Assert.Equal("current", Assert.Single(messages).Grounding!.Status);
            Assert.Equal("/Reading/Study/" + passageId, Assert.Single(messages[0].Grounding!.Sources).Route);
            Assert.Equal("OLD_FACT_SENTENCE", messages[0].Content);

            var passage = await db.ReadingPassages.SingleAsync(item => item.Id == passageId);
            passage.Content = "The marker was revised after capture.";
            await db.SaveChangesAsync();
            var reread = await db.AiMessages.AsNoTracking().Where(item => item.ConversationId == conversationId).ToListAsync();
            await citations.ProjectAsync(TestDataIds.UserId, reread, GroundingProjectionMode.Display, default);
            Assert.Equal("changed", reread[0].Grounding!.Status);
            Assert.Contains("UTC", reread[0].Grounding.Notice, StringComparison.Ordinal);
            Assert.Equal("OLD_FACT_SENTENCE", reread[0].Content);
            var provider = await db.AiMessages.AsNoTracking().Where(item => item.ConversationId == conversationId).ToListAsync();
            await citations.ProjectAsync(TestDataIds.UserId, provider, GroundingProjectionMode.ProviderContext, default);
            Assert.DoesNotContain("OLD_FACT_SENTENCE", provider[0].Content, StringComparison.Ordinal);
            Assert.Equal(GroundingNotices.StaleContext, provider[0].Content);

            passage.IsPublished = false;
            await db.SaveChangesAsync();
            var hidden = await db.AiMessages.AsNoTracking().Where(item => item.ConversationId == conversationId).ToListAsync();
            await citations.ProjectAsync(TestDataIds.UserId, hidden, GroundingProjectionMode.Display, default);
            Assert.Equal(GroundingNotices.Unavailable, hidden[0].Content);
            Assert.Empty(hidden[0].Grounding!.Sources);
        }
    }

    [Fact]
    public async Task ClassRemovalAndPrivateDowngrade_HideHistoricalBodies()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var memberConversation = await SeedConversationAsync(factory, TestDataIds.MemberUserId);
        using (var scope = factory.Services.CreateScope())
        {
            var chat = scope.ServiceProvider.GetRequiredService<IAiChatService>();
            var reply = await chat.SendAsync(TestDataIds.MemberUserId, memberConversation, "lop hoc cua toi", CancellationToken.None);
            Assert.Equal("class:" + TestDataIds.ClassId, Assert.Single(reply.Grounding!.Sources).SourceKey);
            Assert.Contains("Sprint One Class", reply.Text, StringComparison.Ordinal);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var membership = await db.ClassMembers.SingleAsync(member => member.UserId == TestDataIds.MemberUserId && member.ClassId == TestDataIds.ClassId);
            db.ClassMembers.Remove(membership);
            await db.SaveChangesAsync();
            var history = scope.ServiceProvider.GetRequiredService<IAiConversationService>();
            var page = await history.GetMessagesPageAsync(TestDataIds.MemberUserId, memberConversation, 10, null, null, default);
            var assistant = Assert.Single(page.Items, item => item.Role == AiMessageRole.Assistant);
            Assert.Equal(GroundingNotices.Unavailable, assistant.Content);
            Assert.DoesNotContain("Sprint One Class", assistant.Content, StringComparison.Ordinal);
            Assert.Empty(assistant.Grounding!.Sources);
        }

        var ownerConversation = await SeedConversationAsync(factory, TestDataIds.UserId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var user = await db.Users.SingleAsync(item => item.UserId == TestDataIds.UserId);
            user.Role = Roles.Premium;
            db.SpeakingVideos.Add(new SpeakingVideo
            {
                Id = 7099,
                PlaylistId = TestDataIds.SpeakingPlaylistId,
                OwnerUserId = TestDataIds.UserId,
                Title = "PRIVATE_SHADOW_CLIP",
                YoutubeId = "privateshadow01",
                Level = "A1",
                Topic = "PRIVATE_SHADOW_CLIP",
                ImportStatus = "ready",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            var chat = scope.ServiceProvider.GetRequiredService<IAiChatService>();
            var reply = await chat.SendAsync(TestDataIds.UserId, ownerConversation, "goi y video speaking", CancellationToken.None);
            Assert.Contains(reply.Grounding!.Sources, source => source.SourceKey == "speaking:7099");
            user.Role = Roles.Standard;
            await db.SaveChangesAsync();
            var history = scope.ServiceProvider.GetRequiredService<IAiConversationService>();
            var page = await history.GetMessagesPageAsync(TestDataIds.UserId, ownerConversation, 10, null, null, default);
            var assistant = Assert.Single(page.Items, item => item.Role == AiMessageRole.Assistant);
            Assert.Equal(GroundingNotices.Unavailable, assistant.Content);
            Assert.DoesNotContain("PRIVATE_SHADOW_CLIP", assistant.Content, StringComparison.Ordinal);
            Assert.Empty(assistant.Grounding!.Sources);
        }
    }

    [Fact]
    public async Task MetadataFailure_DoesNotSaveMismatchedSuccess_AndKeepsKnownUsage()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IGroundingCitationService>();
            services.AddScoped<IGroundingCitationService, ThrowingCitationService>();
        });
        await factory.InitializeAsync();
        await SetCardDefinitionAsync(factory, "predict-citation-token");

        using var scope = factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IAiChatService>();
        var error = await Assert.ThrowsAsync<AiProviderException>(() => chat.SendAsync(TestDataIds.UserId, null, "what does forecast mean", CancellationToken.None));
        Assert.Equal("reply_persistence_failed", error.ErrorCode);
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.DoesNotContain(await db.AiMessages.ToListAsync(), message => message.Role == AiMessageRole.Assistant);
        Assert.DoesNotContain(await db.AiMessages.Select(message => message.Content).ToListAsync(), content => content.Contains("predict-citation-token", StringComparison.Ordinal));
        var ledger = await db.AiQuotaReservationLedgers.SingleAsync();
        Assert.Equal(AiQuotaReservationStatus.Pending, ledger.Status);
        Assert.True(ledger.ActualTokens > 0);
    }

    [Fact]
    public async Task ProviderHistory_OmitsRevokedSentences()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IGroundedChatPipeline>();
            services.AddSingleton<RecordingPipeline>();
            services.AddSingleton<IGroundedChatPipeline>(provider => provider.GetRequiredService<RecordingPipeline>());
        });
        await factory.InitializeAsync();
        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using (var scope = factory.Services.CreateScope())
        {
            var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.AiMessages.Add(Message(conversationId, DateTime.UtcNow.AddMinutes(-1), "SECRET_OLD_SENTENCE", citations.Capture(Answer("card:99999", GroundingHashes.Sha256("gone")))));
            await db.SaveChangesAsync();
            var chat = scope.ServiceProvider.GetRequiredService<IAiChatService>();
            await chat.SendAsync(TestDataIds.UserId, conversationId, "hello", CancellationToken.None);
            var recorder = scope.ServiceProvider.GetRequiredService<RecordingPipeline>();
            Assert.Contains(GroundingNotices.Unavailable, recorder.History);
            Assert.DoesNotContain("SECRET_OLD_SENTENCE", recorder.History);
        }
    }

    private static async Task SetCardDefinitionAsync(TestWebApplicationFactory factory, string definition)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var card = await db.Cards.SingleAsync(item => item.CardId == TestDataIds.UserCardId);
        card.Definition = definition;
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedConversationAsync(TestWebApplicationFactory factory, int userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var id = Guid.NewGuid();
        db.AiConversations.Add(new AiConversation
        {
            Id = id,
            UserId = userId,
            Title = "Phase 5",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static AiMessage Message(Guid conversationId, DateTime createdAt, string content, string? metadata, AiMessageRole role = AiMessageRole.Assistant)
        => new()
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role,
            Content = content,
            GroundingMetadata = metadata,
            CreatedAtUtc = createdAt
        };

    private static AiChatAnswer Answer(string sourceKey, string documentHash)
        => new(
            HybridRagOutcome.Answered,
            "full",
            "answer",
            [new HybridRagEvidence(sourceKey, "card", sourceKey + "#s1", GroundingHashes.Sha256("body"), null, null, null, "https://evil.example/phish", "owner", TestDataIds.UserId, DateTime.UtcNow, "body")
            {
                DocumentContentHash = documentHash,
                EntityKey = sourceKey
            }],
            1, 1, 2, "internal", "req");

    private static AiChatAnswer AnswerMany()
    {
        var evidence = Enumerable.Range(1, 13).Select(id => new HybridRagEvidence(
            "card:" + id,
            "card",
            "card:" + id + "#s1",
            GroundingHashes.Sha256("body"),
            null, null, null, null, "owner", TestDataIds.UserId, DateTime.UtcNow, "body")).ToList();
        return new AiChatAnswer(HybridRagOutcome.Answered, "full", "answer", evidence, 1, 1, 2, "internal", "req");
    }

    private sealed class ThrowingCitationService : IGroundingCitationService
    {
        public string Capture(AiChatAnswer answer) => throw new GroundingCaptureException("metadata rejected");

        public Task ProjectAsync(int userId, IList<AiMessage> messages, GroundingProjectionMode mode, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class RecordingPipeline : IGroundedChatPipeline
    {
        public List<string> History { get; } = [];

        public Task<GroundedTurnExecution> ExecuteAsync(GroundedTurnInput input, CancellationToken ct)
        {
            History.AddRange(input.History.Select(turn => turn.Content));
            return Task.FromResult(new GroundedTurnExecution(new AiChatAnswer(
                HybridRagOutcome.OutOfScope, null, "follow-up", [], 1, 1, 2, "recorder", "req")));
        }
    }
}
