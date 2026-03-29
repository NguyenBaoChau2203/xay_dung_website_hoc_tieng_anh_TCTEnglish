using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiPhase4HardeningIntegrationTests
{
    [Fact]
    public async Task Chat_AnonymousUser_IsUnauthorizedOrRedirectedToLogin()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "gpt-4o-mini", "req-anon")));
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);

        using var response = await client.GetAsync("/AI/Chat");

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Redirect });
    }

    [Fact]
    public async Task Send_WhenConversationOwnedByAnotherUser_ReturnsNotFound()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("Xin chào từ AI", 10, 15, 25, "gpt-4o-mini", "req-owned")));
        await factory.InitializeAsync();

        var outsiderConversationId = await SeedConversationAsync(factory, TestDataIds.OutsiderUserId);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");

        using var request = CreateSendRequest(outsiderConversationId, "Xin chào", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Send_HappyPath_ReturnsOkAndPersistsAssistantUsage()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromResult(new AiProviderReply("**hello** learner", 10, 15, 25, "gpt-4o-mini", "req-success")));
        await factory.InitializeAsync();

        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/AI/Chat?conversationId={conversationId}");

        using var request = CreateSendRequest(conversationId, "Please explain present perfect", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("**hello** learner", responseBody);
        Assert.Contains("gpt-4o-mini", responseBody);

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
    public async Task Send_WhenProviderTimesOut_ReturnsServiceUnavailable()
    {
        await using var factory = CreateFactory(_ =>
            Task.FromException<AiProviderReply>(new AiProviderException("AI provider timeout.", "timeout", true)));
        await factory.InitializeAsync();

        var conversationId = await SeedConversationAsync(factory, TestDataIds.UserId);
        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var antiForgeryToken = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, $"/AI/Chat?conversationId={conversationId}");

        using var request = CreateSendRequest(conversationId, "timeout case", antiForgeryToken);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Send_WhenConversationAlreadyProcessing_ReturnsConflict()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var factory = CreateFactory(async _ =>
        {
            await gate.Task;
            return new AiProviderReply("done", 1, 1, 2, "gpt-4o-mini", "req-conflict");
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
                options.BaseUrl = "https://test.openai.local/v1";
                options.RequestTimeoutSeconds = 1;
                options.MaxInputChars = 1000;
                options.RequestTokenBudget = 4600;
                options.DailyTokenBudgetPerUser = 60000;
            });

            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient(replyFactory));
        });
    }

    private static HttpRequestMessage CreateSendRequest(Guid conversationId, string message, string antiForgeryToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/AI/Chat/Send")
        {
            Content = JsonContent.Create(new { conversationId, message })
        };

        request.Headers.Add("RequestVerificationToken", antiForgeryToken);
        return request;
    }

    private static async Task<Guid> SeedConversationAsync(TestWebApplicationFactory factory, int userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();

        var conversationId = Guid.NewGuid();
        context.AiConversations.Add(new AiConversation
        {
            Id = conversationId,
            UserId = userId,
            Title = "Integration",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return conversationId;
    }

    private sealed class StubAiProviderClient : IAiProviderClient
    {
        private readonly Func<CancellationToken, Task<AiProviderReply>> _replyFactory;

        public StubAiProviderClient(Func<CancellationToken, Task<AiProviderReply>> replyFactory)
        {
            _replyFactory = replyFactory;
        }

        public Task<AiProviderReply> GenerateReplyAsync(IReadOnlyList<AiContextMessage> messages, CancellationToken ct)
        {
            return _replyFactory(ct);
        }
    }
}

