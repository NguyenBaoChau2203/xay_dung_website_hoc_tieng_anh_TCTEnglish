using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.TestHelpers;
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

        var service = CreateService(context, new StubAiProviderClient((_, _, _) =>
            Task.FromResult(new AiProviderReply("Xin chào", 12, 18, 30, "test-model", "req-1"))));

        var result = await service.SendAsync(1, conversationId, "Hello teacher", CancellationToken.None);

        Assert.Equal(conversationId, result.ConversationId);
        Assert.Equal("Xin chào", result.Text);
        Assert.Equal(12, result.Usage.PromptTokens);
        Assert.Equal(18, result.Usage.CompletionTokens);
        Assert.Equal(30, result.Usage.TotalTokens);
        Assert.Equal("test-model", result.Usage.Model);

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

        var service = CreateService(context, new StubAiProviderClient((_, _, _) =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", null))));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendAsync(1, conversationId, "   ", CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_OwnershipViolation_ThrowsKeyNotFoundException()
    {
        var dbName = $"ai-phase2-ownership-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var conversationId = await SeedUsersAndConversationAsync(context, ownerUserId: 1);

        var service = CreateService(context, new StubAiProviderClient((_, _, _) =>
            Task.FromResult(new AiProviderReply("ignored", 0, 0, 0, "test-model", null))));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SendAsync(2, conversationId, "hello", CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_ProviderFailure_ThrowsAndRollsBackUserPromptInExistingConversation()
    {
        var dbName = $"ai-phase2-provider-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var conversationId = await SeedUsersAndConversationAsync(context, ownerUserId: 1);

        var service = CreateService(context, new StubAiProviderClient((_, _, _) =>
            throw new AiProviderException("AI provider request failed.", "http_503", true)));

        await Assert.ThrowsAsync<AiProviderException>(() =>
            service.SendAsync(1, conversationId, "hello", CancellationToken.None));

        var messages = await context.AiMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .ToListAsync();

        Assert.Empty(messages);

        var requestLogs = await context.AiRequestLogs
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .ToListAsync();

        Assert.Single(requestLogs);
        Assert.False(requestLogs[0].IsSuccess);
        Assert.Equal("http_503", requestLogs[0].ErrorCode);
    }

    [Fact]
    public async Task SendAsync_ExistingConversation_ProviderFailureThenRetry_DoesNotDuplicateUserPrompt()
    {
        var dbName = $"ai-phase2-provider-retry-no-dup-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var conversationId = await SeedUsersAndConversationAsync(context, ownerUserId: 1);

        var attempt = 0;
        var service = CreateService(context, new StubAiProviderClient((_, _, _) =>
        {
            attempt++;
            return attempt == 1
                ? throw new AiProviderException("AI provider request failed.", "http_503", true)
                : Task.FromResult(new AiProviderReply("retry-success", 3, 4, 7, "test-model", "req-retry"));
        }));

        await Assert.ThrowsAsync<AiProviderException>(() =>
            service.SendAsync(1, conversationId, "hello", CancellationToken.None));

        var retryResult = await service.SendAsync(1, conversationId, "hello", CancellationToken.None);
        Assert.Equal("retry-success", retryResult.Text);

        var messages = await context.AiMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(2, messages.Count);
        Assert.Equal(AiMessageRole.User, messages[0].Role);
        Assert.Equal("hello", messages[0].Content);
        Assert.Equal(AiMessageRole.Assistant, messages[1].Role);
        Assert.Equal("retry-success", messages[1].Content);
    }

    [Fact]
    public async Task SendAsync_DraftConversation_ProviderFailure_RollsBackDraftData()
    {
        var dbName = $"ai-phase2-draft-provider-failure-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        await SeedUsersOnlyAsync(context);

        var service = CreateService(context, new StubAiProviderClient((_, _, _) =>
            throw new AiProviderException("AI provider request failed.", "http_503", true)));

        await Assert.ThrowsAsync<AiProviderException>(() =>
            service.SendAsync(1, null, "hello", CancellationToken.None));

        var conversationCount = await context.AiConversations
            .AsNoTracking()
            .CountAsync(x => x.UserId == 1);

        var messageCount = await context.AiMessages
            .AsNoTracking()
            .CountAsync();

        var requestLogCount = await context.AiRequestLogs
            .AsNoTracking()
            .CountAsync();

        Assert.Equal(0, conversationCount);
        Assert.Equal(0, messageCount);
        Assert.Equal(0, requestLogCount);
    }

    [Fact]
    public async Task SendAsync_ConcurrentConversationRequest_ThrowsAiConcurrentRequestException()
    {
        var dbName = $"ai-phase2-concurrency-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var conversationId = await SeedUsersAndConversationAsync(context, ownerUserId: 1);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new StubAiProviderClient(async (_, _, _) =>
        {
            await gate.Task;
            return new AiProviderReply("done", 1, 1, 2, "test-model", "req-2");
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

    [Fact]
    public async Task SendAsync_WithoutConversationId_CreatesConversationWithPromptBasedTitle()
    {
        var dbName = $"ai-phase2-create-on-first-send-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        await SeedUsersOnlyAsync(context);

        var longPrompt = "   Explain the difference between present perfect and past simple with practical examples and common mistakes for Vietnamese learners.   ";
        var service = CreateService(context, new StubAiProviderClient((_, _, _) =>
            Task.FromResult(new AiProviderReply("Sure", 8, 9, 17, "test-model", "req-create"))));

        var result = await service.SendAsync(1, null, longPrompt, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.ConversationId);
        Assert.Equal("Explain the difference between present perfect and past simple with practical ex", result.ConversationTitle);

        var createdConversation = await context.AiConversations
            .AsNoTracking()
            .SingleAsync(x => x.Id == result.ConversationId);

        Assert.Equal("Explain the difference between present perfect and past simple with practical ex", createdConversation.Title);

        var savedMessages = await context.AiMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == result.ConversationId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(2, savedMessages.Count);
        Assert.Equal(AiMessageRole.User, savedMessages[0].Role);
        Assert.Equal(AiMessageRole.Assistant, savedMessages[1].Role);
    }

    [Fact]
    public async Task SendAsync_StandardUser_AtLimit_ThrowsRateLimitException()
    {
        var dbName = $"ai-phase1-limit-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var conversationId = await SeedUsersAndConversationAsync(context, ownerUserId: 1);

        for (int i = 0; i < 15; i++)
        {
            context.AiRequestLogs.Add(new AiRequestLog
            {
                Id = Guid.NewGuid(),
                UserId = 1,
                ConversationId = conversationId,
                IsSuccess = true,
                RequestedAtUtc = DateTime.UtcNow,
                ErrorCode = null
            });
        }
        await context.SaveChangesAsync();

        var service = CreateService(context, new StubAiProviderClient((_, _, _) =>
            Task.FromResult(new AiProviderReply("success", 10, 10, 20, "test-model", null))));

        var exception = await Assert.ThrowsAsync<AiRateLimitException>(() =>
            service.SendAsync(1, conversationId, "hello", CancellationToken.None));

        Assert.Equal("daily_question_limit_exceeded", exception.ErrorCode);
    }

    [Fact]
    public async Task SendAsync_StandardUser_BelowDailyLimit_Succeeds()
    {
        var dbName = $"ai-phase1-standard-below-limit-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var conversationId = await SeedUsersAndConversationAsync(context, ownerUserId: 1);

        for (int i = 0; i < 14; i++)
        {
            context.AiRequestLogs.Add(new AiRequestLog
            {
                Id = Guid.NewGuid(),
                UserId = 1,
                ConversationId = conversationId,
                IsSuccess = true,
                RequestedAtUtc = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();

        var service = CreateService(context, new StubAiProviderClient((_, _, _) =>
            Task.FromResult(new AiProviderReply("success", 10, 10, 20, "test-model", null))));

        var result = await service.SendAsync(1, conversationId, "hello", CancellationToken.None);

        Assert.Equal("success", result.Text);
    }

    [Fact]
    public async Task SendAsync_ProviderFailure_DoesNotConsumeDailyQuestionQuota()
    {
        var dbName = $"ai-phase1-provider-failure-does-not-consume-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var conversationId = await SeedUsersAndConversationAsync(context, ownerUserId: 1);

        for (int i = 0; i < 14; i++)
        {
            context.AiRequestLogs.Add(new AiRequestLog
            {
                Id = Guid.NewGuid(),
                UserId = 1,
                ConversationId = conversationId,
                IsSuccess = true,
                RequestedAtUtc = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();

        var failingService = CreateService(context, new StubAiProviderClient((_, _, _) =>
            throw new AiProviderException("AI provider request failed.", "http_503", true)));

        await Assert.ThrowsAsync<AiProviderException>(() =>
            failingService.SendAsync(1, conversationId, "provider fails", CancellationToken.None));

        var successfulRequestCountAfterFailure = await context.AiRequestLogs
            .AsNoTracking()
            .CountAsync(x => x.UserId == 1 && x.IsSuccess);

        Assert.Equal(14, successfulRequestCountAfterFailure);

        var successService = CreateService(context, new StubAiProviderClient((_, _, _) =>
            Task.FromResult(new AiProviderReply("success", 5, 7, 12, "test-model", null))));

        var result = await successService.SendAsync(1, conversationId, "after failure", CancellationToken.None);

        Assert.Equal("success", result.Text);
    }

    [Fact]
    public async Task SendAsync_PremiumUser_AboveStandardLimit_Succeeds()
    {
        var dbName = $"ai-phase1-premium-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var conversationId = await SeedUsersAndConversationAsync(context, ownerUserId: 1);

        var user = await context.Users.FindAsync(1);
        user!.Role = Roles.Premium;

        for (int i = 0; i < 15; i++)
        {
            context.AiRequestLogs.Add(new AiRequestLog
            {
                Id = Guid.NewGuid(),
                UserId = 1,
                ConversationId = conversationId,
                IsSuccess = true,
                RequestedAtUtc = DateTime.UtcNow,
                ErrorCode = null
            });
        }
        await context.SaveChangesAsync();

        var service = CreateService(context, new StubAiProviderClient((_, _, _) =>
            Task.FromResult(new AiProviderReply("success", 10, 10, 20, "test-model", null))));

        var result = await service.SendAsync(1, conversationId, "hello", CancellationToken.None);
        Assert.Equal("success", result.Text);
    }

    [Fact]
    public async Task SendAsync_FailedRequestsDoNotCountTowardsLimit()
    {
        var dbName = $"ai-phase1-failedreqs-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var conversationId = await SeedUsersAndConversationAsync(context, ownerUserId: 1);

        for (int i = 0; i < 15; i++)
        {
            context.AiRequestLogs.Add(new AiRequestLog
            {
                Id = Guid.NewGuid(),
                UserId = 1,
                ConversationId = conversationId,
                IsSuccess = false,
                RequestedAtUtc = DateTime.UtcNow,
                ErrorCode = "provider_error"
            });
        }
        await context.SaveChangesAsync();

        var service = CreateService(context, new StubAiProviderClient((_, _, _) =>
            Task.FromResult(new AiProviderReply("success", 10, 10, 20, "test-model", null))));

        var result = await service.SendAsync(1, conversationId, "hello", CancellationToken.None);
        Assert.Equal("success", result.Text);
    }

    [Fact]
    public async Task SendAsync_AdminUser_AboveStandardLimit_Succeeds()
    {
        var dbName = $"ai-phase1-admin-{Guid.NewGuid()}";
        await using var context = CreateContext(dbName);
        var conversationId = await SeedUsersAndConversationAsync(context, ownerUserId: 1);

        var user = await context.Users.FindAsync(1);
        user!.Role = Roles.Admin;

        for (int i = 0; i < 15; i++)
        {
            context.AiRequestLogs.Add(new AiRequestLog
            {
                Id = Guid.NewGuid(),
                UserId = 1,
                ConversationId = conversationId,
                IsSuccess = true,
                RequestedAtUtc = DateTime.UtcNow,
                ErrorCode = null
            });
        }
        await context.SaveChangesAsync();

        var service = CreateService(context, new StubAiProviderClient((_, _, _) =>
            Task.FromResult(new AiProviderReply("success", 10, 10, 20, "test-model", null))));

        var result = await service.SendAsync(1, conversationId, "hello", CancellationToken.None);
        Assert.Equal("success", result.Text);
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

    private static async Task SeedUsersOnlyAsync(DbflashcardContext context)
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

        await context.SaveChangesAsync();
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
            new AiConversationService(context),
            new AiContextBuilder(new SimpleAiTokenCounter()),
            providerClient,
            guard ?? new AiConversationExecutionGuard(),
            aiOptions,
            NullLogger<AiChatService>.Instance);
    }

}
