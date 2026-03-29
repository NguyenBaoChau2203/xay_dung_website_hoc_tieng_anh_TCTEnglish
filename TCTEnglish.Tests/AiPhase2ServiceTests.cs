using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiChatServiceTests
{
    [Fact]
    public async Task SendAsync_ValidRequest_SavesUserAndAssistantMessages()
    {
        var dbName = $"ai-phase2-success-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var conversationId = await SeedUsersAndConversationAsync(context, ownerUserId: 1);

        var service = CreateService(context, new StubAiProviderClient(_ =>
            Task.FromResult(new AiProviderReply("Xin chào", 12, 18, 30, "gpt-4o-mini", "req-1"))));

        var result = await service.SendAsync(1, conversationId, "Hello teacher", CancellationToken.None);

        Assert.Equal(conversationId, result.ConversationId);
        Assert.Equal("Xin chào", result.Text);
        Assert.Equal(12, result.Usage.PromptTokens);
        Assert.Equal(18, result.Usage.CompletionTokens);
        Assert.Equal(30, result.Usage.TotalTokens);
        Assert.Equal("gpt-4o-mini", result.Usage.Model);

        var savedMessages = await context.AiMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(2, savedMessages.Count);
        Assert.Equal(AiMessageRole.User, savedMessages[0].Role);
        Assert.Equal("Hello teacher", savedMessages[0].Content);
        Assert.Equal(AiMessageRole.Assistant, savedMessages[1].Role);
        Assert.Equal("Xin chào", savedMessages[1].Content);
        Assert.Equal(12, savedMessages[1].PromptTokens);
        Assert.Equal(18, savedMessages[1].CompletionTokens);

        var requestLogs = await context.AiRequestLogs
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .ToListAsync();

        Assert.Single(requestLogs);
        Assert.True(requestLogs[0].IsSuccess);
        Assert.Equal(30, requestLogs[0].TotalTokens);
    }

    [Fact]
    public async Task SendAsync_InvalidInput_ThrowsArgumentException()
    {
        var dbName = $"ai-phase2-invalid-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var conversationId = await SeedUsersAndConversationAsync(context, ownerUserId: 1);

        var service = CreateService(context, new StubAiProviderClient(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "gpt-4o-mini", null))));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendAsync(1, conversationId, "   ", CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_OwnershipViolation_ThrowsKeyNotFoundException()
    {
        var dbName = $"ai-phase2-ownership-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var conversationId = await SeedUsersAndConversationAsync(context, ownerUserId: 1);

        var service = CreateService(context, new StubAiProviderClient(_ =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "gpt-4o-mini", null))));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SendAsync(2, conversationId, "hello", CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_ProviderFailure_ThrowsAndDoesNotSaveAssistantMessage()
    {
        var dbName = $"ai-phase2-provider-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var conversationId = await SeedUsersAndConversationAsync(context, ownerUserId: 1);

        var service = CreateService(context, new StubAiProviderClient(_ =>
            throw new AiProviderException("AI provider request failed.", "http_503", true)));

        await Assert.ThrowsAsync<AiProviderException>(() =>
            service.SendAsync(1, conversationId, "hello", CancellationToken.None));

        var messages = await context.AiMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .ToListAsync();

        Assert.Single(messages);
        Assert.Equal(AiMessageRole.User, messages[0].Role);

        var requestLogs = await context.AiRequestLogs
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .ToListAsync();

        Assert.Single(requestLogs);
        Assert.False(requestLogs[0].IsSuccess);
        Assert.Equal("http_503", requestLogs[0].ErrorCode);
    }

    [Fact]
    public async Task SendAsync_ConcurrentConversationRequest_ThrowsAiConcurrentRequestException()
    {
        var dbName = $"ai-phase2-concurrency-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var conversationId = await SeedUsersAndConversationAsync(context, ownerUserId: 1);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new StubAiProviderClient(async _ =>
        {
            await gate.Task;
            return new AiProviderReply("done", 1, 1, 2, "gpt-4o-mini", "req-2");
        });

        var guard = new AiConversationExecutionGuard();
        var firstService = CreateService(context, provider, guard);
        var secondService = CreateService(context, provider, guard);

        var firstRequest = firstService.SendAsync(1, conversationId, "first", CancellationToken.None);
        await Task.Delay(50);

        await Assert.ThrowsAsync<AiConcurrentRequestException>(() =>
            secondService.SendAsync(1, conversationId, "second", CancellationToken.None));

        gate.SetResult();
        await firstRequest;
    }

    private static DbflashcardContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new DbflashcardContext(options);
    }

    private static async Task<Guid> SeedUsersAndConversationAsync(DbflashcardContext context, int ownerUserId)
    {
        context.Users.AddRange(
            new User
            {
                UserId = 1,
                Email = "owner@example.com",
                PasswordHash = "hash",
                Role = Roles.Standard
            },
            new User
            {
                UserId = 2,
                Email = "other@example.com",
                PasswordHash = "hash",
                Role = Roles.Standard
            });

        var conversationId = Guid.NewGuid();
        context.AiConversations.Add(new AiConversation
        {
            Id = conversationId,
            UserId = ownerUserId,
            Title = "Phase2",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return conversationId;
    }

    private static AiChatService CreateService(
        DbflashcardContext context,
        IAiProviderClient providerClient,
        IAiConversationExecutionGuard? guard = null)
    {
        var aiOptions = Options.Create(new AiOptions
        {
            MaxInputChars = 1000,
            MaxOutputTokens = 600,
            RequestTokenBudget = 4600,
            ModelContextLimit = 128000
        });

        return new AiChatService(
            context,
            new AiContextBuilder(new SimpleAiTokenCounter()),
            providerClient,
            guard ?? new AiConversationExecutionGuard(),
            aiOptions,
            NullLogger<AiChatService>.Instance);
    }

    private sealed class StubAiProviderClient : IAiProviderClient
    {
        private readonly Func<IReadOnlyList<AiContextMessage>, Task<AiProviderReply>> _handler;

        public StubAiProviderClient(Func<IReadOnlyList<AiContextMessage>, Task<AiProviderReply>> handler)
        {
            _handler = handler;
        }

        public Task<AiProviderReply> GenerateReplyAsync(IReadOnlyList<AiContextMessage> messages, CancellationToken ct)
        {
            return _handler(messages);
        }
    }
}
