using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiContextBuilderTests
{
    private readonly AiContextBuilder _builder = new(new SimpleAiTokenCounter());

    [Fact]
    public void BuildContextMessages_DoesNotExceedTokenBudget()
    {
        var options = new AiOptions
        {
            ModelContextLimit = 40,
            RequestTokenBudget = 40,
            MaxOutputTokens = 10
        };

        var history = new List<AiMessage>
        {
            CreateMessage(AiMessageRole.User, new string('a', 32), DateTime.UtcNow.AddMinutes(-2)),
            CreateMessage(AiMessageRole.Assistant, new string('b', 32), DateTime.UtcNow.AddMinutes(-1))
        };

        var result = _builder.BuildContextMessages(
            systemPrompt: new string('s', 20),
            currentUserMessage: new string('u', 40),
            history,
            options);

        Assert.True(result.PlannedTotalTokens <= options.RequestTokenBudget);
    }

    [Fact]
    public void BuildContextMessages_TrimsCurrentMessage_WhenInputIsTooLong()
    {
        var options = new AiOptions
        {
            ModelContextLimit = 60,
            RequestTokenBudget = 60,
            MaxOutputTokens = 20
        };

        var result = _builder.BuildContextMessages(
            systemPrompt: new string('s', 80),
            currentUserMessage: new string('u', 280),
            history: [],
            options);

        var userMessage = result.Messages.Last(x => x.Role == "user");
        var tokenCounter = new SimpleAiTokenCounter();
        var maxCurrentTokens = options.RequestTokenBudget - options.MaxOutputTokens - tokenCounter.CountTokens(new string('s', 80));

        Assert.True(result.WasCurrentMessageTrimmed);
        Assert.True(tokenCounter.CountTokens(userMessage.Content) <= Math.Max(0, maxCurrentTokens));
        Assert.True(result.PlannedTotalTokens <= options.RequestTokenBudget);
    }

    [Fact]
    public void BuildContextMessages_WithEmptyHistory_ReturnsSystemAndUserMessages()
    {
        var options = new AiOptions();

        var result = _builder.BuildContextMessages(
            systemPrompt: "system prompt",
            currentUserMessage: "hello",
            history: [],
            options);

        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("system", result.Messages[0].Role);
        Assert.Equal("user", result.Messages[1].Role);
    }

    private static AiMessage CreateMessage(AiMessageRole role, string content, DateTime createdAtUtc)
    {
        return new AiMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            Role = role,
            Content = content,
            CreatedAtUtc = createdAtUtc
        };
    }
}

public sealed class AiConversationServiceTests
{
    [Fact]
    public async Task GetMessagesByConversationAsync_Throws_WhenConversationDoesNotBelongToUser()
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase($"ai-ownership-{Guid.NewGuid()}")
            .Options;

        await using var context = new DbflashcardContext(options);
        await SeedUsersAsync(context);

        var conversation = new AiConversation
        {
            Id = Guid.NewGuid(),
            UserId = 1,
            Title = "Owned",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        context.AiConversations.Add(conversation);
        context.AiMessages.Add(new AiMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = AiMessageRole.User,
            Content = "hello",
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new AiConversationService(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetMessagesByConversationAsync(userId: 2, conversation.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetConversationsByUserAsync_ReturnsOnlyOwnedConversations()
    {
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase($"ai-list-{Guid.NewGuid()}")
            .Options;

        await using var context = new DbflashcardContext(options);
        await SeedUsersAsync(context);

        context.AiConversations.AddRange(
            new AiConversation
            {
                Id = Guid.NewGuid(),
                UserId = 1,
                Title = "Mine",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new AiConversation
            {
                Id = Guid.NewGuid(),
                UserId = 2,
                Title = "Not mine",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

        await context.SaveChangesAsync();

        var service = new AiConversationService(context);
        var conversations = await service.GetConversationsByUserAsync(1, CancellationToken.None);

        Assert.Single(conversations);
        Assert.Equal("Mine", conversations[0].Title);
    }

    [Fact]
    public async Task DeleteConversationAsync_WhenCalledByOwner_ReturnsTrueAndCascades()
    {
        var dbName = $"ai-phase2-delete-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var context = new DbflashcardContext(options);
        await SeedUsersAsync(context);

        var conversationId = Guid.NewGuid();
        context.AiConversations.Add(new AiConversation
        {
            Id = conversationId,
            UserId = 1,
            Title = "Mine",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        context.AiMessages.Add(new AiMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = AiMessageRole.User,
            Content = "test",
            CreatedAtUtc = DateTime.UtcNow
        });

        context.AiRequestLogs.Add(new AiRequestLog
        {
            Id = Guid.NewGuid(),
            UserId = 1,
            ConversationId = conversationId,
            IsSuccess = true,
            RequestedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = new AiConversationService(context);
        var result = await service.DeleteConversationAsync(1, conversationId, CancellationToken.None);

        Assert.True(result);
        Assert.Empty(context.AiConversations);
        Assert.Empty(context.AiMessages);
        Assert.Empty(context.AiRequestLogs);
    }

    [Fact]
    public async Task DeleteConversationAsync_WhenCalledByOutsider_ReturnsFalseAndLeavesDataIntact()
    {
        var dbName = $"ai-phase2-deloutsider-{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<DbflashcardContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var context = new DbflashcardContext(options);
        await SeedUsersAsync(context);

        var conversationId = Guid.NewGuid();
        context.AiConversations.Add(new AiConversation
        {
            Id = conversationId,
            UserId = 1,
            Title = "Mine",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = new AiConversationService(context);
        var result = await service.DeleteConversationAsync(2, conversationId, CancellationToken.None);

        Assert.False(result);
        Assert.Single(context.AiConversations);
    }

    private static async Task SeedUsersAsync(DbflashcardContext context)
    {
        context.Users.AddRange(
            new User
            {
                UserId = 1,
                Email = "u1@example.com",
                PasswordHash = "hash",
                Role = Roles.Standard
            },
            new User
            {
                UserId = 2,
                Email = "u2@example.com",
                PasswordHash = "hash",
                Role = Roles.Standard
            });

        await context.SaveChangesAsync();
    }
}
