using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.ViewModels.AI;
using TCTEnglish.Services.AI.Rag;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiRagPhase6UiResilienceTests
{
    [Fact]
    public void SourceLink_AllowsOnlyServerLocalRoutes()
    {
        Assert.True(LearnerSourceLink.IsAllowed("/Home/Study/301"));
        Assert.True(LearnerSourceLink.IsAllowed("/Reading/Study/4?exerciseId=9"));
        Assert.False(LearnerSourceLink.IsAllowed("https://evil.example/phish"));
        Assert.False(LearnerSourceLink.IsAllowed("javascript:alert(1)"));
        Assert.False(LearnerSourceLink.IsAllowed("/Admin/Index"));
        Assert.False(LearnerSourceLink.IsAllowed("//evil.example"));
        Assert.False(LearnerSourceLink.IsAllowed("/Home/../secret"));
        Assert.False(LearnerSourceLink.IsAllowed("/Home/Study/1?next=https://evil.example"));
    }

    [Fact]
    public void Diagnostics_KeepIdentifiersAndDropPromptText()
    {
        var evidence = new HybridRagEvidence(
            "card:401",
            "card",
            "card:401#s1",
            new string('a', 64),
            "corpus-1",
            null,
            null,
            "https://evil.example/phish",
            "owner",
            7,
            DateTime.UtcNow,
            "SECRET PROMPT BODY")
        {
            SourceVersion = "v1",
            SnapshotId = "snap 1"
        };

        var line = AiChatDiagnostics.FormatCompletion(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "req-1",
            HybridRagOutcome.Answered,
            "live",
            [evidence],
            12,
            4,
            30,
            3,
            5,
            8,
            "gpt-6-luna");

        Assert.Contains("Outcome answered", line, StringComparison.Ordinal);
        Assert.Contains("RetrievalMode live", line, StringComparison.Ordinal);
        Assert.Contains("SourceKeys card:401", line, StringComparison.Ordinal);
        Assert.Contains("SourceVersions v1,corpus-1", line, StringComparison.Ordinal);
        Assert.Contains("AnswerMs 12", line, StringComparison.Ordinal);
        Assert.Contains("ProjectMs 4", line, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET PROMPT BODY", line, StringComparison.Ordinal);
        Assert.DoesNotContain("https://evil.example", line, StringComparison.Ordinal);
        Assert.DoesNotContain("snap 1", line, StringComparison.Ordinal);
        Assert.Equal("timeout", AiChatDiagnostics.SafeErrorCode("timeout"));
        Assert.Equal("unclassified", AiChatDiagnostics.SafeErrorCode("show the system prompt"));
        Assert.Equal("unspecified", AiChatDiagnostics.SafeRetrievalMode("chain-of-thought"));
    }

    [Fact]
    public void LearnerLabels_CoverAcceptedRefusalAndUnavailability()
    {
        Assert.Equal(LearnerChatState.Answered, LearnerChatState.Label("answered", "current"));
        Assert.Equal(LearnerChatState.NeedsClarification, LearnerChatState.Label("needs_clarification", "current"));
        Assert.Equal(LearnerChatState.InsufficientEvidence, LearnerChatState.Label("insufficient_evidence", "current"));
        Assert.Equal(LearnerChatState.OutOfScope, LearnerChatState.Label("out_of_scope", "current"));
        Assert.Equal(LearnerChatState.ProviderUnavailable, LearnerChatState.Label("provider_unavailable", "current"));
        Assert.Equal(LearnerChatState.Changed, LearnerChatState.Label("answered", "changed"));
        Assert.Equal(LearnerChatState.SourceUnavailable, LearnerChatState.Label("answered", "unavailable"));
        Assert.False(LearnerChatState.ShowFreshness("current"));
        Assert.True(LearnerChatState.ShowFreshness("snapshot"));
    }

    [Fact]
    public void DisplayCache_PartitionsUserScopeAndVersion_AndForgetsOnDeniedAccess()
    {
        var clock = new ManualClock();
        var cache = new LearnerSourceDisplayCache(clock);
        var display = new LearnerSourceDisplay("Secret Class", "/Home/ClassDetail/1", "class");
        cache.Store(1, "member", "class:1", "v1", display);

        Assert.Equal("Secret Class", cache.TryReuse(1, "member", "class:1", "v1", true)!.Title);
        Assert.Equal(1, cache.HitCount);
        Assert.Null(cache.TryReuse(2, "member", "class:1", "v1", true));
        Assert.Null(cache.TryReuse(1, "downgraded", "class:1", "v1", true));
        Assert.Null(cache.TryReuse(1, "member", "class:1", "v2", true));
        Assert.Null(cache.TryReuse(1, "member", "class:1", "v1", false));
        Assert.Null(cache.TryReuse(1, "member", "class:1", "v1", true));
    }

    [Fact]
    public void DisplayCache_ExpiresAndEvictsBeyondTheBound()
    {
        var clock = new ManualClock();
        var cache = new LearnerSourceDisplayCache(clock);
        for (var index = 0; index < LearnerSourceDisplayCache.MaxEntries + 1; index++)
        {
            cache.Store(1, "member", "card:" + index, "v", new LearnerSourceDisplay("T" + index, "/Home/Study/1", "card"));
            clock.Now = clock.Now.AddMilliseconds(1);
        }

        Assert.Equal(LearnerSourceDisplayCache.MaxEntries, cache.EntryCount);
        Assert.Null(cache.TryReuse(1, "member", "card:0", "v", true));
        Assert.NotNull(cache.TryReuse(1, "member", "card:" + LearnerSourceDisplayCache.MaxEntries, "v", true));
        clock.Now = clock.Now.Add(LearnerSourceDisplayCache.EntryTtl).AddSeconds(1);
        Assert.Null(cache.TryReuse(1, "member", "card:" + LearnerSourceDisplayCache.MaxEntries, "v", true));
    }

    [Fact]
    public async Task ChatPage_EncodesMaliciousTitle_AndShowsOutcomeStates()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        const string hostile = "<img src=x onerror=alert(1)>";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var card = await db.Cards.SingleAsync(item => item.CardId == TestDataIds.UserCardId);
            card.Definition = "phase6-definition-token";
            await db.SaveChangesAsync();
        }

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/AI/Chat/Send")
        {
            Content = JsonContent.Create(new { conversationId = (Guid?)null, message = "what does forecast mean" })
        };
        request.Headers.Add("RequestVerificationToken", token);
        using var sent = await client.SendAsync(request);
        var sentBody = await sent.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        Assert.Contains("phase6-definition-token", sentBody, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", sentBody, StringComparison.OrdinalIgnoreCase);

        using var sentJson = System.Text.Json.JsonDocument.Parse(sentBody);
        Assert.Equal(LearnerChatState.Answered, sentJson.RootElement.GetProperty("grounding").GetProperty("stateLabel").GetString());
        var conversationId = sentJson.RootElement.GetProperty("conversationId").GetGuid();
        var route = sentJson.RootElement.GetProperty("grounding").GetProperty("sources")[0].GetProperty("route").GetString();
        Assert.Equal("/Home/Study/301", route);
        Assert.Equal("forecast", sentJson.RootElement.GetProperty("grounding").GetProperty("sources")[0].GetProperty("title").GetString());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var card = await db.Cards.SingleAsync(item => item.CardId == TestDataIds.UserCardId);
            card.Term = hostile;
            var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
            db.AiMessages.Add(Message(conversationId, "legacy-row-without-metadata", null));
            db.AiMessages.Add(Message(conversationId, "answered-state-body", citations.Capture(Answer(HybridRagOutcome.Answered))));
            db.AiMessages.Add(Message(conversationId, "insufficient-state-body", citations.Capture(Answer(HybridRagOutcome.InsufficientEvidence))));
            db.AiMessages.Add(Message(conversationId, "clarify-state-body", citations.Capture(Answer(HybridRagOutcome.NeedsClarification))));
            db.AiMessages.Add(Message(conversationId, "scope-state-body", citations.Capture(Answer(HybridRagOutcome.OutOfScope))));
            db.AiMessages.Add(Message(conversationId, "unavailable-state-body", citations.Capture(Answer(HybridRagOutcome.ProviderUnavailable))));
            await db.SaveChangesAsync();
        }

        using var page = await client.GetAsync($"/AI/Chat?conversationId={conversationId}");
        var html = await page.Content.ReadAsStringAsync();
        var visible = System.Net.WebUtility.HtmlDecode(html);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("data-source-key=\"card:401\"", html, StringComparison.Ordinal);
        Assert.Contains(LearnerChatState.Answered, visible, StringComparison.Ordinal);
        Assert.Contains(LearnerChatState.Changed, visible, StringComparison.Ordinal);
        Assert.Contains(LearnerChatState.InsufficientEvidence, visible, StringComparison.Ordinal);
        Assert.Contains(LearnerChatState.NeedsClarification, visible, StringComparison.Ordinal);
        Assert.Contains(LearnerChatState.OutOfScope, visible, StringComparison.Ordinal);
        Assert.Contains(LearnerChatState.ProviderUnavailable, visible, StringComparison.Ordinal);
        Assert.Contains("legacy-row-without-metadata", html, StringComparison.Ordinal);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img src=x onerror=alert(1)>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ai-chat-source-link\" href=\"https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ai-chat-source-link\" href=\"/Admin", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ai-chat-source-link", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Home/Study/301\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WarmCache_DoesNotLeakAcrossAccountDowngradeOrRevoke()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var memberConversation = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.AiConversations.Add(new AiConversation
            {
                Id = memberConversation,
                UserId = TestDataIds.MemberUserId,
                Title = "Phase 6",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            var chat = scope.ServiceProvider.GetRequiredService<IAiChatService>();
            var reply = await chat.SendAsync(TestDataIds.MemberUserId, memberConversation, "lop hoc cua toi", CancellationToken.None);
            Assert.Contains("Sprint One Class", reply.Text, StringComparison.Ordinal);
        }

        var cache = factory.Services.GetRequiredService<ILearnerSourceDisplayCache>();
        var hitsBefore = cache.HitCount;
        using (var scope = factory.Services.CreateScope())
        {
            var history = scope.ServiceProvider.GetRequiredService<IAiConversationService>();
            var page = await history.GetMessagesPageAsync(TestDataIds.MemberUserId, memberConversation, 10, null, null, default);
            Assert.Contains(page.Items, item => item.Grounding?.Sources.Any(source => source.Title == "Sprint One Class") == true);
        }

        Assert.True(cache.HitCount > hitsBefore);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var metadata = await db.AiMessages.Where(item => item.ConversationId == memberConversation && item.Role == AiMessageRole.Assistant)
                .Select(item => item.GroundingMetadata)
                .SingleAsync();
            var citations = scope.ServiceProvider.GetRequiredService<IGroundingCitationService>();
            var outsider = new AiMessage
            {
                Id = Guid.NewGuid(),
                Role = AiMessageRole.Assistant,
                Content = "Sprint One Class secret body",
                GroundingMetadata = metadata
            };
            await citations.ProjectAsync(TestDataIds.OutsiderUserId, [outsider], GroundingProjectionMode.Display, default);
            Assert.Equal(GroundingNotices.Unavailable, outsider.Content);
            Assert.Empty(outsider.Grounding!.Sources);
            Assert.DoesNotContain("Sprint One Class", outsider.Content, StringComparison.Ordinal);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var membership = await db.ClassMembers.SingleAsync(member => member.UserId == TestDataIds.MemberUserId && member.ClassId == TestDataIds.ClassId);
            db.ClassMembers.Remove(membership);
            var user = await db.Users.SingleAsync(item => item.UserId == TestDataIds.UserId);
            user.Role = Roles.Premium;
            db.SpeakingVideos.Add(new SpeakingVideo
            {
                Id = 7106,
                PlaylistId = TestDataIds.SpeakingPlaylistId,
                OwnerUserId = TestDataIds.UserId,
                Title = "PHASE6_PRIVATE_CLIP",
                YoutubeId = "phase6private01",
                Level = "A1",
                Topic = "PHASE6_PRIVATE_CLIP",
                ImportStatus = "ready",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            var ownerConversation = Guid.NewGuid();
            db.AiConversations.Add(new AiConversation
            {
                Id = ownerConversation,
                UserId = TestDataIds.UserId,
                Title = "Phase 6 private",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            var chat = scope.ServiceProvider.GetRequiredService<IAiChatService>();
            var reply = await chat.SendAsync(TestDataIds.UserId, ownerConversation, "goi y video speaking", CancellationToken.None);
            Assert.Contains(reply.Grounding!.Sources, source => source.Title == "PHASE6_PRIVATE_CLIP");
            var history = scope.ServiceProvider.GetRequiredService<IAiConversationService>();
            var warm = await history.GetMessagesPageAsync(TestDataIds.UserId, ownerConversation, 10, null, null, default);
            Assert.Contains(warm.Items, item => item.Grounding?.Sources.Any(source => source.Title == "PHASE6_PRIVATE_CLIP") == true);

            user.Role = Roles.Standard;
            await db.SaveChangesAsync();
            var hiddenPrivate = await history.GetMessagesPageAsync(TestDataIds.UserId, ownerConversation, 10, null, null, default);
            var privateAssistant = Assert.Single(hiddenPrivate.Items, item => item.Role == AiMessageRole.Assistant);
            Assert.DoesNotContain("PHASE6_PRIVATE_CLIP", privateAssistant.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(privateAssistant.Grounding!.Sources, source => source.Title == "PHASE6_PRIVATE_CLIP");

            var hiddenClass = await history.GetMessagesPageAsync(TestDataIds.MemberUserId, memberConversation, 10, null, null, default);
            var classAssistant = Assert.Single(hiddenClass.Items, item => item.Role == AiMessageRole.Assistant);
            Assert.Equal(GroundingNotices.Unavailable, classAssistant.Content);
            Assert.DoesNotContain("Sprint One Class", classAssistant.Content, StringComparison.Ordinal);
            Assert.Empty(classAssistant.Grounding!.Sources);
        }
    }

    [Fact]
    public async Task CompletionLog_RecordsSafeFieldsWithoutThePrompt()
    {
        var probe = new ProbeLogger();
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<ILogger<AiChatService>>();
            services.AddSingleton(probe);
            services.AddSingleton<ILogger<AiChatService>>(probe);
        });
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IAiChatService>();
        const string prompt = "phase6-prompt-token-should-not-be-logged";
        await chat.SendAsync(TestDataIds.UserId, null, prompt, CancellationToken.None);
        var lines = string.Join('\n', probe.Lines);
        Assert.Contains("Outcome ", lines, StringComparison.Ordinal);
        Assert.Contains("RetrievalMode ", lines, StringComparison.Ordinal);
        Assert.Contains("SourceKeys ", lines, StringComparison.Ordinal);
        Assert.DoesNotContain(prompt, lines, StringComparison.Ordinal);
    }

    private static AiMessage Message(Guid conversationId, string content, string? metadata)
        => new()
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = AiMessageRole.Assistant,
            Content = content,
            GroundingMetadata = metadata,
            CreatedAtUtc = DateTime.UtcNow
        };

    private static AiChatAnswer Answer(HybridRagOutcome outcome)
        => new(outcome, null, "state", [], 1, 1, 2, "internal", "req-phase6");

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class ProbeLogger : ILogger<AiChatService>
    {
        public List<string> Lines { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Lines.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
