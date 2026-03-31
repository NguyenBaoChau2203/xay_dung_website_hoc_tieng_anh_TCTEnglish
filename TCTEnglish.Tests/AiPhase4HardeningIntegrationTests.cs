using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
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
        Assert.Contains("id=\"typing\" class=\"ai-chat-typing d-none\" role=\"status\" aria-live=\"polite\"", html);
        Assert.Contains("id=\"chatStatus\" class=\"alert alert-warning ai-chat-status d-none mb-0\" role=\"status\" aria-live=\"assertive\"", html);
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
    public async Task DependencyInjection_ResolvesGeminiProviderClient()
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

        Assert.IsType<GeminiProviderClient>(providerClient);
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

            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, ct) => replyFactory(ct)));
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

}

