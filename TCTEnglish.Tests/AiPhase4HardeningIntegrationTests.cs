using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Internal;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTEnglish.ViewModels.AI;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiPhase4HardeningIntegrationTests
{
    [Fact]
    public async Task Chat_AnonymousUser_IsUnauthorizedOrRedirectedToLogin()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-anon")));
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        using var response = await client.GetAsync("/AI/Chat");

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Redirect });
    }

    [Fact]
    public async Task Send_WhenConversationOwnedByAnotherUser_ReturnsNotFound()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("Xin chào từ AI", 10, 15, 25, "test-model", "req-owned")));
        await factory.InitializeAsync();

        var outsiderConversationId = await SeedConversationAsync(factory, TestDataIds.OutsiderUserId);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");

        using var request = CreateSendRequest(outsiderConversationId, "Xin chào", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Send_WhenStandardUserReachedDailyQuestionLimit_Returns429WithFriendlyMessage()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 1, 1, 2, "test-model", "req-limit")));
        await factory.InitializeAsync();

        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        await SeedSuccessfulRequestLogsAsync(factory, TestDataIds.UserId, conversationId, count: 15);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/AI/Chat?conversationId={conversationId}");

        using var request = CreateSendRequest(conversationId, "quota check", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("15 câu hỏi AI mỗi ngày", body);
    }

    [Fact]
    public async Task Chat_WithoutConversationId_DoesNotCreateConversation()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-chat-draft")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var response = await client.GetAsync("/AI/Chat");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var conversationCount = await context.AiConversations
            .AsNoTracking()
            .CountAsync(x => x.UserId == TestDataIds.UserId);

        Assert.Equal(0, conversationCount);
    }

    [Fact]
    public async Task Chat_FullPage_RendersUserConversationHistoryAndNewChatLink()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-history")));
        await factory.InitializeAsync();

        var myConversationId = await SeedConversationAsync(factory, TestDataIds.UserId, "My phase4 conversation");
        await SeedConversationAsync(factory, TestDataIds.OutsiderUserId, "Outsider conversation");

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync($"/AI/Chat?conversationId={myConversationId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-ai-new-chat-link", html);
        Assert.Contains("My phase4 conversation", html);
        Assert.DoesNotContain("Outsider conversation", html);
        Assert.Contains($"/AI/Chat?conversationId={myConversationId}", html);
        Assert.Contains($"data-ai-history-delete=\"{myConversationId}\"", html);
        Assert.Contains("data-ai-chat-base-url=\"/AI/Chat\"", html);
        Assert.Contains("id=\"conversationIdInput\"", html);
        Assert.Contains("id=\"deleteConversationModal\"", html);
        Assert.Contains("id=\"deleteSuccessToast\"", html);
        Assert.Contains("bootstrap.bundle.min.js", html);
    }

    [Fact]
    public async Task AiChatScript_DeleteFlow_DoesNotUseBrowserNativeDialogs()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-delete-ui-runtime")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var response = await client.GetAsync("/js/ai-chat.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var js = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("confirm(", js);
        Assert.DoesNotContain("alert(", js);
    }

    [Fact]
    public async Task AiChatScript_EmbeddedSend_PostsLauncherQuotaSyncMessage()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-quota-sync-js")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var response = await client.GetAsync("/js/ai-chat.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var js = await response.Content.ReadAsStringAsync();
        Assert.Contains("tct-ai-launcher-quota-consumed", js);
        Assert.Contains("window.parent.postMessage", js);
    }

    [Fact]
    public async Task AiChatScript_QuickActions_AreScopedAndGuardedAgainstDuplicateSubmit()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-quick-action-hardening-js")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var response = await client.GetAsync("/js/ai-chat.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var js = await response.Content.ReadAsStringAsync();
        Assert.Contains("elForm.closest('[data-ai-chat-shell]')", js);
        Assert.Contains("elChatShell?.querySelector('[data-ai-quick-actions]')", js);
        Assert.Contains("elForm.dataset.quickActionPending === 'true'", js);
        Assert.Contains("setQuickActionsBusy(elQuickActions, true)", js);
        Assert.Contains("markQuickActionsAccepted(elQuickActions)", js);
    }

    [Fact]
    public async Task AiLauncherScript_HandlesQuotaSyncMessageForStandardPlan()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-launcher-quota-sync-js")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        using var response = await client.GetAsync("/js/ai-chat-launcher.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var js = await response.Content.ReadAsStringAsync();
        Assert.Contains("tct-ai-launcher-quota-consumed", js);
        Assert.Contains("window.addEventListener('message'", js);
        Assert.Contains("fetch(usageUrl", js);
        Assert.Contains("data-ai-plan-quota-used", js);
    }

    [Fact]
    public async Task Usage_WhenStandardUser_ReturnsAuthoritativeDailyQuotaSnapshot()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-usage-snapshot")));
        await factory.InitializeAsync();

        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId, "Usage endpoint seed");
        await SeedSuccessfulRequestLogsAsync(factory, TestDataIds.UserId, conversationId, count: 4);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/AI/Chat/Usage");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AiUsageInfo>();
        Assert.NotNull(payload);
        Assert.False(payload!.IsUnlimited);
        Assert.Equal(15, payload.DailyLimit);
        Assert.Equal(4, payload.RequestedToday);
    }

    [Fact]
    public async Task Chat_WhenConversationSelected_MarksHistoryItemAsActive()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-history-active")));
        await factory.InitializeAsync();

        var selectedConversationId = await SeedConversationAsync(factory, TestDataIds.UserId, "Selected conversation");
        await SeedConversationAsync(factory, TestDataIds.UserId, "Another conversation");

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync($"/AI/Chat?conversationId={selectedConversationId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains($"data-conversation-id=\"{selectedConversationId}\"", html);
        Assert.Contains("ai-chat-history-item active", html);
    }

    [Fact]
    public async Task Chat_Embed_DoesNotRenderHistoryPanel()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-embed")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/AI/Chat?embed=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("data-ai-history-root", html);
        Assert.DoesNotContain("data-ai-new-chat-link", html);
        Assert.Contains("data-ai-scroll-bottom", html);
        Assert.Contains("id=\"scrollToBottomBtn\"", html);
        Assert.Contains("data-user-avatar=\"\"", html);
        Assert.Contains("data-user-initial=\"U\"", html);
        Assert.Contains("data-ai-avatar=\"/images/ai/tct-ai-launcher.png\"", html);
    }

    [Fact]
    public async Task Delete_WhenConversationOwnedByAnotherUser_ReturnsNotFound()
    {
        await using var factory = CreateFactory(_ => Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-delete-outsider")));
        await factory.InitializeAsync();

        var outsiderConversationId = await SeedConversationAsync(factory, TestDataIds.OutsiderUserId);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");

        using var request = CreateDeleteRequest(outsiderConversationId, antiForgeryToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var stillExists = await context.AiConversations.AnyAsync(c => c.Id == outsiderConversationId);
        Assert.True(stillExists);
    }

    [Fact]
    public async Task AiLauncher_StandardUser_RendersStandardPlanAndDailyQuotaCountdown()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-launcher-std")));
        await factory.InitializeAsync();

        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId, "Launcher quota seed");
        await SeedSuccessfulRequestLogsAsync(factory, TestDataIds.UserId, conversationId, count: 3);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        // Fetch a page that uses the shared _Layout containing _AiChatLauncher
        using var response = await client.GetAsync("/Home/Index");

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-ai-plan-summary", html);
        Assert.Contains("Gói Standard: 15 câu/ngày", html);
        Assert.Contains("data-ai-plan-quota", html);
        Assert.Contains("data-ai-plan-quota-used=\"3\"", html);
        Assert.Contains("data-ai-plan-quota-limit=\"15\"", html);
        Assert.Contains("data-ai-usage-url=\"/AI/Chat/Usage\"", html);
        Assert.Contains("Đã dùng 3/15", html);
        Assert.DoesNotContain("Gói Premium: Không giới hạn", html);
    }

    [Fact]
    public async Task AiLauncher_PremiumUser_RendersUnlimitedPlanWithoutStandardQuotaCountdown()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-launcher-prem")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);

        using var response = await client.GetAsync("/Home/Index");

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-ai-plan-summary", html);
        Assert.Contains("Gói Premium: Không giới hạn", html);
        Assert.DoesNotContain("Gói Standard: 15 câu/ngày", html);
        Assert.DoesNotContain("data-ai-plan-quota", html);
    }

    [Fact]
    public async Task Delete_WhenConversationOwnedByUser_DeletesAndReturnsSuccess()
    {
        await using var factory = CreateFactory(_ => Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-delete-owner")));
        await factory.InitializeAsync();

        var myConversationId = await SeedConversationAsync(factory, TestDataIds.UserId, "To Be Deleted");
        await SeedMessageAndRequestLogAsync(factory, myConversationId, TestDataIds.UserId);

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");

        using var request = CreateDeleteRequest(myConversationId, antiForgeryToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var stillExists = await context.AiConversations.AnyAsync(c => c.Id == myConversationId);
        var messageCount = await context.AiMessages.CountAsync(x => x.ConversationId == myConversationId);
        var requestLogCount = await context.AiRequestLogs.CountAsync(x => x.ConversationId == myConversationId);

        Assert.False(stillExists);
        Assert.Equal(0, messageCount);
        Assert.Equal(0, requestLogCount);
    }

    [Fact]
    public async Task Delete_WithoutAntiForgeryToken_ReturnsBadRequest()
    {
        await using var factory = CreateFactory(_ => Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-delete-csrf")));
        await factory.InitializeAsync();

        var myConversationId = await SeedConversationAsync(factory, TestDataIds.UserId, "To Be Deleted");
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);

        using var request = CreateDeleteRequest(myConversationId, antiForgeryToken: null);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Chat_FullPage_RendersAccessibilityHooksForComposerAndStatus()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-a11y-hooks")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/AI/Chat");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<label for=\"messageInput\" class=\"visually-hidden\">", html);
        Assert.Contains("id=\"messageInput\"", html);
        Assert.Contains("id=\"chatWindow\"", html);
        Assert.Contains("tabindex=\"0\"", html);
        Assert.Contains("id=\"scrollToBottomBtn\"", html);
        Assert.Contains("data-ai-scroll-bottom", html);
        Assert.Contains("aria-controls=\"chatWindow\"", html);
        Assert.Contains("id=\"typing\" class=\"ai-chat-message ai-chat-message-assistant d-none\"", html);
        Assert.Contains("ai-chat-typing-bubble", html);
        Assert.Contains("data-user-initial=\"U\"", html);
        Assert.Contains("data-ai-avatar=\"/images/ai/tct-ai-launcher.png\"", html);
        Assert.Contains("data-ai-quick-actions", html);
        Assert.Contains("data-ai-quick-action", html);
        Assert.Contains("aria-label=\"Gợi ý câu hỏi nhanh\"", html);
        Assert.Contains("aria-describedby=\"chatStatus\"", html);
        Assert.Contains("id=\"chatStatus\" class=\"alert alert-warning ai-chat-status d-none mb-0\" role=\"status\" aria-live=\"assertive\"", html);
    }

    [Fact]
    public async Task Chat_FullPage_StandardPlan_RendersPlanHintAndDeleteFeedbackHooks()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-standard-hint")));
        await factory.InitializeAsync();

        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId, "Standard plan conversation");
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync($"/AI/Chat?conversationId={conversationId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Gói Standard: 15 câu/ngày", html);
        Assert.Contains("id=\"deleteConversationError\"", html);
        Assert.Contains("id=\"confirmDeleteConversationBtn\"", html);
    }

    [Fact]
    public async Task Chat_FullPage_RendersBoundedShellAndScrollAffordanceHooks()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-layout-hooks")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/AI/Chat");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-ai-chat-layout", html);
        Assert.Contains("data-ai-chat-surface", html);
        Assert.Contains("data-ai-chat-shell=\"page\"", html);
        Assert.Contains("data-ai-chat-panel", html);
        Assert.Contains("data-ai-chat-scroll-region", html);
        Assert.Contains("data-ai-chat-composer", html);
        Assert.Contains("data-ai-scroll-bottom", html);
        Assert.Contains("role=\"log\"", html);
        Assert.Contains("aria-controls=\"chatWindow\"", html);
    }

    [Fact]
    public async Task Chat_Embed_RendersEmbeddedScrollShellHooks()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-embed-layout-hooks")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/AI/Chat?embed=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-ai-chat-shell=\"embedded\"", html);
        Assert.Contains("data-ai-chat-panel", html);
        Assert.Contains("data-ai-chat-scroll-region", html);
        Assert.Contains("data-ai-chat-composer", html);
        Assert.Contains("data-ai-scroll-bottom", html);
    }

    [Fact]
    public async Task HomeIndex_RendersAiLauncherWithDialogAccessibilitySemantics()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-launcher-a11y")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/Home/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-ai-launcher", html);
        Assert.Contains("data-ai-panel", html);
        Assert.Contains("role=\"dialog\"", html);
        Assert.Contains("aria-modal=\"true\"", html);
        Assert.Contains("data-ai-panel-body", html);
        Assert.Contains("aria-haspopup=\"dialog\"", html);
    }

    [Fact]
    public async Task HomeIndex_StandardUser_RendersLauncherPlanSummary()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-launcher-standard-plan")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/Home/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-ai-plan-summary", html);
        Assert.Contains("Gói Standard: 15 câu/ngày", html);
    }

    [Fact]
    public async Task HomeIndex_PremiumUser_RendersLauncherUnlimitedPlanSummary()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", "req-launcher-premium-plan")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);
        using var response = await client.GetAsync("/Home/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-ai-plan-summary", html);
        Assert.Contains("Gói Premium: Không giới hạn", html);
    }

    [Fact]
    public async Task Send_HappyPath_ReturnsOkAndPersistsAssistantUsage()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("**hello** learner", 10, 15, 25, "test-model", "req-success")));
        await factory.InitializeAsync();

        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/AI/Chat?conversationId={conversationId}");

        using var request = CreateSendRequest(conversationId, "Please explain present perfect", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("**hello** learner", responseBody);
        Assert.Contains("test-model", responseBody);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var messages = await context.AiMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(2, messages.Count);
        Assert.Equal(AiMessageRole.User, messages[0].Role);
        Assert.Equal(AiMessageRole.Assistant, messages[1].Role);
        Assert.Equal(10, messages[1].PromptTokens);
        Assert.Equal(15, messages[1].CompletionTokens);
    }

    [Fact]
    public async Task DependencyInjection_ResolvesInternalKnowledgeProvider()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.PostConfigure<AiOptions>(options =>
            {
                options.ApiKey = "test-gemini-key";
                options.Model = "gemini-2.5-flash-lite";
            });
        });

        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var providerClient = scope.ServiceProvider.GetRequiredService<IAiProviderClient>();

        Assert.IsType<InternalKnowledgeProvider>(providerClient);
    }

    [Fact]
    public async Task Send_WhenProviderConfiguredAsGemini_AndFakeProviderReturnsReply_ReturnsOk()
    {
        await using var factory = CreateFactory(
            _ => Task.FromResult(new AiProviderReply("Gemini success", 8, 12, 20, "gemini-2.5-flash-lite", "req-gemini-ok")));
        await factory.InitializeAsync();

        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/AI/Chat?conversationId={conversationId}");

        using var request = CreateSendRequest(conversationId, "gemini happy path", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("Gemini success", responseBody);
        Assert.Contains("gemini-2.5-flash-lite", responseBody);
    }

    [Fact]
    public async Task Send_WhenProviderConfiguredAsGemini_AndFakeProviderFails_ReturnsServiceUnavailable()
    {
        await using var factory = CreateFactory(
            _ => Task.FromException<AiProviderReply>(new AiProviderException("AI provider request failed.", AiProviderException.ErrorCodeProviderUnavailable, true)));
        await factory.InitializeAsync();

        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/AI/Chat?conversationId={conversationId}");

        using var request = CreateSendRequest(conversationId, "gemini fail path", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Send_WithoutConversationId_CreatesConversationAndReturnsConversationId()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("reply", 4, 5, 9, "test-model", "req-first-send")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");

        using var request = CreateSendRequest(null, "Need help with IELTS speaking part 2", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ChatReplyDto>();
        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload!.ConversationId);
        Assert.Equal("Need help with IELTS speaking part 2", payload.ConversationTitle);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var conversation = await context.AiConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == payload.ConversationId && x.UserId == TestDataIds.UserId);

        Assert.NotNull(conversation);
        Assert.Equal("Need help with IELTS speaking part 2", conversation!.Title);
    }

    [Fact]
    public async Task Send_WhenProviderTimesOut_ReturnsServiceUnavailable()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromException<AiProviderReply>(new AiProviderException("AI provider timeout.", AiProviderException.ErrorCodeTimeout, true)));
        await factory.InitializeAsync();

        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/AI/Chat?conversationId={conversationId}");

        using var request = CreateSendRequest(conversationId, "timeout case", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Send_ExistingConversation_ProviderFailureThenRetry_DoesNotDuplicateUserPrompt()
    {
        var attempt = 0;
        await using var factory = CreateFactory(_ =>
        {
            attempt++;
            return attempt == 1
                ? Task.FromException<AiProviderReply>(new AiProviderException("AI provider timeout.", AiProviderException.ErrorCodeTimeout, true))
                : Task.FromResult(new AiProviderReply("retry-success", 6, 7, 13, "test-model", "req-retry-success"));
        });
        await factory.InitializeAsync();

        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/AI/Chat?conversationId={conversationId}");

        using var firstRequest = CreateSendRequest(conversationId, "retry me", antiForgeryToken);
        using var firstResponse = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, firstResponse.StatusCode);

        using var retryRequest = CreateSendRequest(conversationId, "retry me", antiForgeryToken);
        using var retryResponse = await client.SendAsync(retryRequest);
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var messages = await context.AiMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(2, messages.Count);
        Assert.Equal(AiMessageRole.User, messages[0].Role);
        Assert.Equal("retry me", messages[0].Content);
        Assert.Equal(AiMessageRole.Assistant, messages[1].Role);
        Assert.Equal("retry-success", messages[1].Content);
    }

    [Fact]
    public async Task Send_DraftConversation_WhenProviderTimesOut_ReturnsServiceUnavailableWithoutPersistingDraft()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromException<AiProviderReply>(new AiProviderException("AI provider timeout.", AiProviderException.ErrorCodeTimeout, true)));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");

        using var request = CreateSendRequest(null, "timeout case", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var conversationCount = await context.AiConversations
            .AsNoTracking()
            .CountAsync(x => x.UserId == TestDataIds.UserId);

        var messageCount = await context.AiMessages
            .AsNoTracking()
            .CountAsync();

        Assert.Equal(0, conversationCount);
        Assert.Equal(0, messageCount);
    }

    [Fact]
    public async Task Send_WhenConversationAlreadyProcessing_ReturnsConflict()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var factory = CreateFactory(async _ =>
        {
            await gate.Task;
            return new AiProviderReply("done", 1, 1, 2, "test-model", "req-conflict");
        });
        await factory.InitializeAsync();

        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using var clientOne = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var clientTwo = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryTokenOne = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(clientOne, $"/AI/Chat?conversationId={conversationId}");
        var antiForgeryTokenTwo = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(clientTwo, $"/AI/Chat?conversationId={conversationId}");

        using var requestOne = CreateSendRequest(conversationId, "first", antiForgeryTokenOne);
        var firstResponseTask = clientOne.SendAsync(requestOne);

        await Task.Delay(50);

        using var requestTwo = CreateSendRequest(conversationId, "second", antiForgeryTokenTwo);
        using var secondResponse = await clientTwo.SendAsync(requestTwo);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        gate.SetResult();
        using var firstResponse = await firstResponseTask;
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
    }

    private static TestWebApplicationFactory CreateFactory(
        Func<CancellationToken, Task<AiProviderReply>> replyFactory)
    {
        return new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiProviderClient>();

            services.PostConfigure<AiOptions>(options =>
            {
                options.ApiKey = "test-api-key";
                options.BaseUrl = "https://test-ai-provider.local/v1";
                options.RequestTimeoutSeconds = 1;
                options.MaxInputChars = 1000;
                options.RequestTokenBudget = 4600;
                options.DailyTokenBudgetPerUser = 60000;
            });

            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, _, ct) => replyFactory(ct)));
        });
    }

    private static HttpRequestMessage CreateSendRequest(Guid? conversationId, string message, string antiForgeryToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/AI/Chat/Send")
        {
            Content = JsonContent.Create(new { conversationId, message })
        };

        request.Headers.Add("RequestVerificationToken", antiForgeryToken);
        return request;
    }

    private static HttpRequestMessage CreateDeleteRequest(Guid conversationId, string? antiForgeryToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/AI/Chat/Delete")
        {
            Content = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("conversationId", conversationId.ToString())
            ])
        };

        if (!string.IsNullOrWhiteSpace(antiForgeryToken))
        {
            request.Headers.Add("RequestVerificationToken", antiForgeryToken);
        }

        return request;
    }

    private static async Task SeedSuccessfulRequestLogsAsync(
        TestWebApplicationFactory factory,
        int userId,
        Guid conversationId,
        int count)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var now = DateTime.UtcNow;

        for (var index = 0; index < count; index++)
        {
            context.AiRequestLogs.Add(new AiRequestLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ConversationId = conversationId,
                IsSuccess = true,
                RequestedAtUtc = now.AddMinutes(-index)
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedMessageAndRequestLogAsync(
        TestWebApplicationFactory factory,
        Guid conversationId,
        int userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var now = DateTime.UtcNow;

        context.AiMessages.AddRange(
            new AiMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                Role = AiMessageRole.User,
                Content = "hello",
                CreatedAtUtc = now.AddMinutes(-1)
            },
            new AiMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                Role = AiMessageRole.Assistant,
                Content = "Xin chào",
                PromptTokens = 3,
                CompletionTokens = 4,
                ModelName = "test-model",
                CreatedAtUtc = now
            });

        context.AiRequestLogs.Add(new AiRequestLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ConversationId = conversationId,
            IsSuccess = true,
            PromptTokens = 3,
            CompletionTokens = 4,
            TotalTokens = 7,
            ModelName = "test-model",
            RequestedAtUtc = now
        });

        await context.SaveChangesAsync();
    }

    private static async Task<Guid> SeedConversationAsync(TestWebApplicationFactory factory, int userId, string title = "Integration")
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var conversationId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        context.AiConversations.Add(new AiConversation
        {
            Id = conversationId,
            UserId = userId,
            Title = title,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await context.SaveChangesAsync();
        return conversationId;
    }

    [Fact]
    public async Task LauncherHeader_StandardUser_DisplaysDailyUsage()
    {
        await using var factory = CreateFactory(_ => Task.FromResult(new AiProviderReply("success", 0, 0, 0, "test-model", "req-1")));
        await factory.InitializeAsync();
        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId, "Launcher usage");

        // Perform requests to increase usage
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.AiRequestLogs.Add(new AiRequestLog
            {
                Id = Guid.NewGuid(),
                UserId = TestDataIds.UserId,
                ConversationId = conversationId,
                IsSuccess = true,
                RequestedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        using var response = await client.GetAsync("/"); // Home displays launcher

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Gói Standard: 15 câu/ngày", html);
        Assert.Contains("data-ai-plan-quota", html);
        Assert.Contains("Đã dùng 1/15", html);
    }

    [Fact]
    public async Task LauncherHeader_PremiumUser_DisplaysUnlimited()
    {
        await using var factory = CreateFactory(_ => Task.FromResult(new AiProviderReply("success", 0, 0, 0, "test-model", "req-1")));
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Premium);
        using var response = await client.GetAsync("/"); // Home displays launcher

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Gói Premium: Không giới hạn", html);
        Assert.DoesNotContain("data-ai-plan-quota", html);
    }
}

