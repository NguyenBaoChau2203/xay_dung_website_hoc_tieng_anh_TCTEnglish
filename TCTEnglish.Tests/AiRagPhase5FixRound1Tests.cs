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

public sealed class AiRagPhase5FixRound1Tests
{
    private const string UnverifiedContext = "Không xác minh được nguồn của câu trả lời này. Không dùng nội dung cũ làm dữ liệu hiện tại.";

    [Fact]
    public async Task HistoryBudgetEarlyReturn_RemovesRevokedChangedAndUnverifiedBodies()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IGroundedChatPipeline>();
            services.AddSingleton<ContextRecorder>();
            services.AddSingleton<IGroundedChatPipeline>(provider => provider.GetRequiredService<ContextRecorder>());
        });
        await factory.InitializeAsync();
        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        var now = DateTime.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
            db.AiMessages.Add(Message(conversationId, now, "REVOKED_HISTORY_SECRET", citations.Capture(CardAnswer("card:99999"))));
            db.AiMessages.Add(Message(conversationId, now.AddSeconds(-1), "KEEP_RECENT_TURN", null, AiMessageRole.User));
            for (var index = 0; index < 120; index++)
            {
                db.AiMessages.Add(Message(
                    conversationId,
                    now.AddMinutes(-2).AddSeconds(-index),
                    new string('x', 6000),
                    null,
                    AiMessageRole.User));
            }

            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<IAiChatService>()
                .SendAsync(TestDataIds.UserId, conversationId, "hello", CancellationToken.None);
            var recorder = scope.ServiceProvider.GetRequiredService<ContextRecorder>();
            Assert.Contains("KEEP_RECENT_TURN", recorder.History);
            Assert.Contains("KEEP_RECENT_TURN", recorder.Context);
            Assert.DoesNotContain("REVOKED_HISTORY_SECRET", recorder.History);
            Assert.DoesNotContain("REVOKED_HISTORY_SECRET", recorder.Context);
            Assert.Contains(GroundingNotices.Unavailable, recorder.History);
        }
    }

    [Fact]
    public async Task HistorySecondBatch_StillProjectsTheIncludedRevokedAssistant()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.PostConfigure<AiOptions>(options =>
            {
                options.RequestTokenBudget = 2950;
                options.MaxOutputTokens = 2048;
                options.ModelContextLimit = 2950;
            });
            services.RemoveAll<IGroundedChatPipeline>();
            services.AddSingleton<ContextRecorder>();
            services.AddSingleton<IGroundedChatPipeline>(provider => provider.GetRequiredService<ContextRecorder>());
        });
        await factory.InitializeAsync();
        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        var now = DateTime.UtcNow;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
        for (var index = 1; index <= 49; index++)
        {
            db.AiMessages.Add(Message(conversationId, now.AddSeconds(-index), "edge-" + index, null, AiMessageRole.User));
        }

        db.AiMessages.Add(Message(conversationId, now.AddSeconds(-50), "BATCH_BOUNDARY_SECRET", citations.Capture(CardAnswer("card:99999"))));
        db.AiMessages.Add(Message(conversationId, now.AddSeconds(-51), new string('y', 6000), null, AiMessageRole.User));
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IAiChatService>()
            .SendAsync(TestDataIds.UserId, conversationId, "hello", CancellationToken.None);
        var recorder = scope.ServiceProvider.GetRequiredService<ContextRecorder>();
        Assert.Contains("edge-1", recorder.History);
        Assert.DoesNotContain("BATCH_BOUNDARY_SECRET", recorder.History);
        Assert.DoesNotContain("BATCH_BOUNDARY_SECRET", recorder.Context);
        Assert.Contains(GroundingNotices.Unavailable, recorder.History);
    }

    [Fact]
    public async Task ZeroHistoryBudget_OmitsTheRevokedAssistant()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.PostConfigure<AiOptions>(options =>
            {
                options.RequestTokenBudget = 100;
                options.MaxOutputTokens = 100;
                options.ModelContextLimit = 100;
            });
            services.RemoveAll<IGroundedChatPipeline>();
            services.AddSingleton<ContextRecorder>();
            services.AddSingleton<IGroundedChatPipeline>(provider => provider.GetRequiredService<ContextRecorder>());
        });
        await factory.InitializeAsync();
        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
        db.AiMessages.Add(Message(conversationId, DateTime.UtcNow, "ZERO_BUDGET_SECRET", citations.Capture(CardAnswer("card:99999"))));
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IAiChatService>()
            .SendAsync(TestDataIds.UserId, conversationId, "hello", CancellationToken.None);
        var recorder = scope.ServiceProvider.GetRequiredService<ContextRecorder>();
        Assert.DoesNotContain("ZERO_BUDGET_SECRET", recorder.History);
        Assert.DoesNotContain("ZERO_BUDGET_SECRET", recorder.Context);
    }

    [Fact]
    public async Task InvalidMetadata_KeepsDisplayBody_AndBlocksProviderReplay()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
        var broken = Message(conversationId, DateTime.UtcNow.AddMinutes(-3), "UNVERIFIABLE_PRIVATE_SECRET", "{broken");
        var future = Message(conversationId, DateTime.UtcNow.AddMinutes(-2), "FUTURE_SCHEMA_SECRET", """{"schemaVersion":9,"outcome":"answered","sources":[{"sourceKey":"card:99999"}]}""");
        var legacy = Message(conversationId, DateTime.UtcNow.AddMinutes(-1), "LEGACY_PLAIN_BODY", null);
        db.AiMessages.AddRange(broken, future, legacy);
        await db.SaveChangesAsync();

        await citations.ProjectAsync(TestDataIds.UserId, [broken], GroundingProjectionMode.Display, CancellationToken.None);
        await citations.ProjectAsync(TestDataIds.UserId, [future], GroundingProjectionMode.ProviderContext, CancellationToken.None);
        var providerBroken = Message(conversationId, DateTime.UtcNow, "UNVERIFIABLE_PRIVATE_SECRET", "{broken");
        await citations.ProjectAsync(TestDataIds.UserId, [providerBroken], GroundingProjectionMode.ProviderContext, CancellationToken.None);
        var providerLegacy = Message(conversationId, DateTime.UtcNow, "LEGACY_PLAIN_BODY", null);
        await citations.ProjectAsync(TestDataIds.UserId, [providerLegacy], GroundingProjectionMode.ProviderContext, CancellationToken.None);

        Assert.Equal("UNVERIFIABLE_PRIVATE_SECRET", broken.Content);
        Assert.Equal("unverified", broken.Grounding!.Status);
        Assert.Empty(broken.Grounding.Sources);
        Assert.Equal(UnverifiedContext, future.Content);
        Assert.DoesNotContain("FUTURE_SCHEMA_SECRET", future.Content, StringComparison.Ordinal);
        Assert.Equal(UnverifiedContext, providerBroken.Content);
        Assert.Equal("LEGACY_PLAIN_BODY", providerLegacy.Content);
        Assert.Null(providerLegacy.Grounding);
    }

    [Fact]
    public async Task NullAndMalformedSources_DoNotCrashHttpHistoryOrSend()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IGroundedChatPipeline>();
            services.AddSingleton<ContextRecorder>();
            services.AddSingleton<IGroundedChatPipeline>(provider => provider.GetRequiredService<ContextRecorder>());
        });
        await factory.InitializeAsync();
        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.AiMessages.Add(Message(
                conversationId,
                DateTime.UtcNow,
                "NULL_SOURCE_SECRET",
                """{"schemaVersion":1,"outcome":"answered","sources":[null]}"""));
            db.AiMessages.Add(Message(
                conversationId,
                DateTime.UtcNow.AddMinutes(-1),
                "DEEP_SECRET",
                "{\"schemaVersion\":1,\"outcome\":\"answered\",\"sources\":[{\"sourceKey\":{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"f\":{\"g\":{\"h\":\"x\"}}}}}}}}]}"));
            await db.SaveChangesAsync();
        }

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var messages = await client.GetAsync($"/AI/Chat/Messages?conversationId={conversationId}&pageSize=20");
        var json = await messages.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, messages.StatusCode);
        using var document = JsonDocument.Parse(json);
        var nullItem = document.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("content").GetString() == "NULL_SOURCE_SECRET");
        Assert.Equal("unverified", nullItem.GetProperty("grounding").GetProperty("status").GetString());
        Assert.Empty(nullItem.GetProperty("grounding").GetProperty("sources").EnumerateArray());
        Assert.DoesNotContain("evil.example", json, StringComparison.Ordinal);

        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/AI/Chat/Send")
        {
            Content = JsonContent.Create(new { conversationId, message = "hello" })
        };
        request.Headers.Add("RequestVerificationToken", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var recorder = factory.Services.GetRequiredService<ContextRecorder>();
        Assert.DoesNotContain("NULL_SOURCE_SECRET", recorder.History);
        Assert.DoesNotContain("NULL_SOURCE_SECRET", recorder.Context);
        Assert.DoesNotContain("DEEP_SECRET", recorder.History);
    }

    [Fact]
    public async Task TwoCitedSentences_EditSecond_LabelsChangedAndStripsProviderContext()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.ReadingPassages.Add(new ReadingPassage
        {
            Id = 1,
            Title = "Harbor",
            Content = "The harbor lamp is red. The harbor bell is blue.",
            Level = "A1",
            Topic = "Sea",
            IsPublished = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var corpus = scope.ServiceProvider.GetRequiredService<RetrievalCorpusService>();
        var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
        var live = await corpus.ReadVisibleDocumentAsync(TestDataIds.UserId, "reading:1", CancellationToken.None);
        Assert.NotNull(live);
        var prepared = CorpusChunker.Prepare(live);
        Assert.Equal(2, prepared.Sentences.Count);
        var answer = new AiChatAnswer(
            HybridRagOutcome.Answered,
            "excerpt",
            "The harbor lamp is red. The harbor bell is blue.",
            prepared.Sentences.Select(sentence => new HybridRagEvidence(
                "reading:1",
                "reading",
                sentence.SentenceId,
                sentence.ContentHash,
                null,
                null,
                null,
                "/Reading/Study/1",
                "authenticated",
                null,
                DateTime.UtcNow,
                sentence.Text)
            {
                DocumentContentHash = live.ContentHash,
                EntityKey = "reading:1"
            }).ToList(),
            1, 1, 2, "internal", "req");
        var metadata = citations.Capture(answer);
        Assert.Contains(prepared.Sentences[0].SentenceId, metadata, StringComparison.Ordinal);
        Assert.Contains(prepared.Sentences[1].SentenceId, metadata, StringComparison.Ordinal);

        var saved = Message(conversationId, DateTime.UtcNow.AddMinutes(-1), answer.Text, metadata);
        var incomplete = Message(
            conversationId,
            DateTime.UtcNow.AddMinutes(-2),
            "The harbor lamp is red. The harbor bell is blue.",
            $$"""{"schemaVersion":1,"outcome":"answered","sources":[{"sourceKey":"reading:1","sentenceId":"{{prepared.Sentences[0].SentenceId}}","contentHash":"{{prepared.Sentences[0].ContentHash}}"}]}""");
        db.AiMessages.AddRange(saved, incomplete);
        var passage = await db.ReadingPassages.SingleAsync(item => item.Id == 1);
        passage.Content = "The harbor lamp is red. The harbor bell is green.";
        await db.SaveChangesAsync();

        var display = Message(conversationId, saved.CreatedAtUtc, saved.Content, metadata);
        var provider = Message(conversationId, saved.CreatedAtUtc, saved.Content, metadata);
        var oldProvider = Message(conversationId, incomplete.CreatedAtUtc, incomplete.Content, incomplete.GroundingMetadata);
        await citations.ProjectAsync(TestDataIds.UserId, [display, provider, oldProvider], GroundingProjectionMode.Display, CancellationToken.None);
        Assert.Equal("changed", display.Grounding!.Status);
        Assert.Contains("The harbor bell is blue.", display.Content, StringComparison.Ordinal);
        Assert.Contains("UTC", display.Grounding.Notice, StringComparison.Ordinal);
        Assert.Single(display.Grounding.Sources);
        Assert.Equal("changed", oldProvider.Grounding!.Status);

        provider.Content = saved.Content;
        oldProvider.Content = incomplete.Content;
        provider.Grounding = null;
        oldProvider.Grounding = null;
        await citations.ProjectAsync(TestDataIds.UserId, [provider, oldProvider], GroundingProjectionMode.ProviderContext, CancellationToken.None);
        Assert.Equal(GroundingNotices.StaleContext, provider.Content);
        Assert.DoesNotContain("The harbor bell is blue.", provider.Content, StringComparison.Ordinal);
        Assert.Equal(GroundingNotices.StaleContext, oldProvider.Content);
        Assert.DoesNotContain("The harbor bell is blue.", oldProvider.Content, StringComparison.Ordinal);
        Assert.Equal("The harbor lamp is red. The harbor bell is blue.", display.Content);
    }

    [Fact]
    public async Task SharedProjection_DoesNotReuseAnotherSentenceOrDropARepeatedOne()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.ReadingPassages.Add(new ReadingPassage
        {
            Id = 1,
            Title = "Harbor",
            Content = "The harbor lamp is red. The harbor bell is blue.",
            Level = "A1",
            Topic = "Sea",
            IsPublished = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var corpus = scope.ServiceProvider.GetRequiredService<RetrievalCorpusService>();
        var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
        var live = await corpus.ReadVisibleDocumentAsync(TestDataIds.UserId, "reading:1", CancellationToken.None);
        Assert.NotNull(live);
        var prepared = CorpusChunker.Prepare(live);
        var first = prepared.Sentences[0];
        var second = prepared.Sentences[1];
        var repeated = citations.Capture(new AiChatAnswer(
            HybridRagOutcome.Answered,
            "excerpt",
            first.Text,
            [
                Evidence("reading:1", first, live.ContentHash),
                Evidence("reading:1", first, live.ContentHash)
            ],
            1, 1, 2, "internal", "req"));
        Assert.Equal(1, Count(repeated, first.SentenceId));

        var current = Message(conversationId, DateTime.UtcNow.AddMinutes(-2), first.Text, Envelope(first, live.ContentHash));
        var staleSentence = "The harbor bell is blue.";
        var changed = Message(
            conversationId,
            DateTime.UtcNow.AddMinutes(-1),
            staleSentence,
            Envelope(second, live.ContentHash, new string('a', 64)));
        var missing = Message(
            conversationId,
            DateTime.UtcNow,
            "MIXED_DOCUMENT_SECRET",
            $$"""{"schemaVersion":1,"outcome":"answered","sources":[{"sourceKey":"reading:1","sentenceId":"{{first.SentenceId}}","contentHash":"{{first.ContentHash}}","documentHash":"{{live.ContentHash}}"},{"sourceKey":"reading:2","sentenceId":"reading:2#s1","contentHash":"{{first.ContentHash}}","documentHash":"{{live.ContentHash}}"}]}""");
        await citations.ProjectAsync(TestDataIds.UserId, [current, changed, missing], GroundingProjectionMode.Display, CancellationToken.None);
        Assert.Equal("current", current.Grounding!.Status);
        Assert.Equal(first.Text, current.Content);
        Assert.Equal("changed", changed.Grounding!.Status);
        Assert.Equal(staleSentence, changed.Content);
        Assert.Equal(GroundingNotices.Unavailable, missing.Content);
        Assert.Empty(missing.Grounding!.Sources);

        current.Content = first.Text;
        changed.Content = staleSentence;
        current.Grounding = null;
        changed.Grounding = null;
        await citations.ProjectAsync(TestDataIds.UserId, [current, changed], GroundingProjectionMode.ProviderContext, CancellationToken.None);
        Assert.Equal(first.Text, current.Content);
        Assert.Equal(GroundingNotices.StaleContext, changed.Content);
        Assert.DoesNotContain(staleSentence, changed.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MixedSourceRevocation_SuppressesTheWholeAnswerOnReloadAndContext()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IGroundedChatPipeline>();
            services.AddSingleton<ContextRecorder>();
            services.AddSingleton<IGroundedChatPipeline>(provider => provider.GetRequiredService<ContextRecorder>());
        });
        await factory.InitializeAsync();
        await SetCardDefinitionAsync(factory, "visible-card-fact");
        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using var scope = factory.Services.CreateScope();
        var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
        var evidence = new List<HybridRagEvidence>
        {
            new("card:401", "card", "card:401#s1", GroundingHashes.Sha256("visible-card-fact"), null, null, null, null, "owner", TestDataIds.UserId, DateTime.UtcNow, "visible-card-fact")
            {
                DocumentContentHash = GroundingHashes.TitleBody("forecast", "setName=|definition=visible-card-fact|phonetic=|example="),
                EntityKey = "card:401"
            },
            new("card:99999", "card", "card:99999#s1", GroundingHashes.Sha256("missing"), null, null, null, null, "owner", TestDataIds.UserId, DateTime.UtcNow, "missing")
            {
                EntityKey = "card:99999"
            }
        };
        var metadata = citations.Capture(new AiChatAnswer(HybridRagOutcome.Answered, "excerpt", "MIXED_SOURCE_SECRET visible-card-fact", evidence, 1, 1, 2, "internal", "req"));
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.AiMessages.Add(Message(conversationId, DateTime.UtcNow, "MIXED_SOURCE_SECRET visible-card-fact", metadata));
        await db.SaveChangesAsync();
        var history = scope.ServiceProvider.GetRequiredService<IAiConversationService>();
        var page = await history.GetMessagesPageAsync(TestDataIds.UserId, conversationId, 10, null, null, CancellationToken.None);
        var assistant = Assert.Single(page.Items, item => item.Role == AiMessageRole.Assistant);
        Assert.Equal(GroundingNotices.Unavailable, assistant.Content);
        Assert.Empty(assistant.Grounding!.Sources);
        await scope.ServiceProvider.GetRequiredService<IAiChatService>()
            .SendAsync(TestDataIds.UserId, conversationId, "hello", CancellationToken.None);
        var recorder = scope.ServiceProvider.GetRequiredService<ContextRecorder>();
        Assert.DoesNotContain("MIXED_SOURCE_SECRET", recorder.History);
        Assert.DoesNotContain("MIXED_SOURCE_SECRET", recorder.Context);
    }

    [Fact]
    public async Task ProjectionFaultAfterCommit_ReturnsSafeReplyWithoutDuplicatingSettlement()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IGroundingCitationService>();
            services.AddScoped<FaultAfterCaptureCitationService>();
            services.AddScoped<IGroundingCitationService>(provider => provider.GetRequiredService<FaultAfterCaptureCitationService>());
        });
        await factory.InitializeAsync();
        await SetCardDefinitionAsync(factory, "predict-citation-token");
        using var scope = factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IAiChatService>();
        var reply = await chat.SendAsync(TestDataIds.UserId, null, "what does forecast mean", CancellationToken.None);
        Assert.Equal(UnverifiedContext, reply.Text);
        Assert.DoesNotContain("predict-citation-token", reply.Text, StringComparison.Ordinal);
        Assert.Equal("unverified", reply.Grounding!.Status);
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var assistants = await db.AiMessages.Where(message => message.Role == AiMessageRole.Assistant).ToListAsync();
        var assistant = Assert.Single(assistants);
        Assert.Contains("predict-citation-token", assistant.Content, StringComparison.Ordinal);
        Assert.NotNull(assistant.GroundingMetadata);
        var ledger = await db.AiQuotaReservationLedgers.SingleAsync();
        Assert.Equal(AiQuotaReservationStatus.Settled, ledger.Status);
        Assert.True(ledger.ActualTokens > 0);
        Assert.Equal(1, (await db.AiUserDailyQuotas.SingleAsync()).SettledQuestions);

        var reloaded = await scope.ServiceProvider.GetRequiredService<IAiConversationService>()
            .GetMessagesPageAsync(TestDataIds.UserId, reply.ConversationId, 10, null, null, CancellationToken.None);
        Assert.Contains("predict-citation-token", Assert.Single(reloaded.Items, item => item.Role == AiMessageRole.Assistant).Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProjectionCancelAfterCommit_DoesNotFailTheCommittedTurn()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IGroundingCitationService>();
            services.AddScoped<CancelAfterCaptureCitationService>();
            services.AddScoped<IGroundingCitationService>(provider => provider.GetRequiredService<CancelAfterCaptureCitationService>());
        });
        await factory.InitializeAsync();
        await SetCardDefinitionAsync(factory, "predict-citation-token");
        using var scope = factory.Services.CreateScope();
        var reply = await scope.ServiceProvider.GetRequiredService<IAiChatService>()
            .SendAsync(TestDataIds.UserId, null, "what does forecast mean", CancellationToken.None);
        Assert.Equal(UnverifiedContext, reply.Text);
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Single(await db.AiMessages.Where(message => message.Role == AiMessageRole.Assistant).ToListAsync());
        Assert.Equal(1, (await db.AiUserDailyQuotas.SingleAsync()).SettledQuestions);
        Assert.Equal(AiQuotaReservationStatus.Settled, (await db.AiQuotaReservationLedgers.SingleAsync()).Status);
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
            Title = "Phase 5 repair",
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

    private static HybridRagEvidence Evidence(string sourceKey, CorpusSentence sentence, string documentHash)
        => new(
            sourceKey,
            "reading",
            sentence.SentenceId,
            sentence.ContentHash,
            null,
            null,
            null,
            "/Reading/Study/1",
            "authenticated",
            null,
            DateTime.UtcNow,
            sentence.Text)
        {
            DocumentContentHash = documentHash,
            EntityKey = sourceKey
        };

    private static string Envelope(CorpusSentence sentence, string documentHash, string? contentHash = null)
        => $$"""{"schemaVersion":1,"outcome":"answered","sources":[{"sourceKey":"reading:1","sentenceId":"{{sentence.SentenceId}}","contentHash":"{{contentHash ?? sentence.ContentHash}}","documentHash":"{{documentHash}}"}]}""";

    private static int Count(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static AiChatAnswer CardAnswer(string sourceKey)
        => new(
            HybridRagOutcome.Answered,
            "full",
            "answer",
            [new HybridRagEvidence(sourceKey, "card", sourceKey + "#s1", GroundingHashes.Sha256("body"), null, null, null, null, "owner", TestDataIds.UserId, DateTime.UtcNow, "body")
            {
                EntityKey = sourceKey
            }],
            1, 1, 2, "internal", "req");

    private sealed class ContextRecorder : IGroundedChatPipeline
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

    private sealed class FaultAfterCaptureCitationService : IGroundingCitationService
    {
        private readonly GroundingCitationService _inner;
        private int _displayCalls;

        public FaultAfterCaptureCitationService(DbflashcardContext context, RetrievalCorpusService corpus)
        {
            _inner = new GroundingCitationService(context, corpus);
        }

        public string Capture(AiChatAnswer answer) => _inner.Capture(answer);

        public Task ProjectAsync(int userId, IList<AiMessage> messages, GroundingProjectionMode mode, CancellationToken ct)
        {
            if (mode == GroundingProjectionMode.Display && ++_displayCalls == 1)
            {
                throw new InvalidOperationException("projection fault");
            }

            return _inner.ProjectAsync(userId, messages, mode, ct);
        }
    }

    private sealed class CancelAfterCaptureCitationService : IGroundingCitationService
    {
        private readonly GroundingCitationService _inner;
        private int _displayCalls;

        public CancelAfterCaptureCitationService(DbflashcardContext context, RetrievalCorpusService corpus)
        {
            _inner = new GroundingCitationService(context, corpus);
        }

        public string Capture(AiChatAnswer answer) => _inner.Capture(answer);

        public Task ProjectAsync(int userId, IList<AiMessage> messages, GroundingProjectionMode mode, CancellationToken ct)
        {
            if (mode == GroundingProjectionMode.Display && ++_displayCalls == 1)
            {
                throw new OperationCanceledException("projection cancelled");
            }

            return _inner.ProjectAsync(userId, messages, mode, ct);
        }
    }
}
