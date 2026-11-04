using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiHistoryPagingIntegrationTests
{
    [Fact]
    public async Task ConversationsPage_DefaultsToThirty_AndSupportsStableCursor()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var baseTime = DateTime.UtcNow.AddDays(-1);
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            for (var i = 0; i < 45; i++)
            {
                db.AiConversations.Add(new AiConversation
                {
                    Id = Guid.NewGuid(),
                    UserId = TestDataIds.UserId,
                    Title = $"Conversation {i:D2}",
                    CreatedAtUtc = baseTime.AddMinutes(i),
                    UpdatedAtUtc = baseTime.AddMinutes(i)
                });
            }

            // Outsider noise
            db.AiConversations.Add(new AiConversation
            {
                Id = Guid.NewGuid(),
                UserId = TestDataIds.OutsiderUserId,
                Title = "Outsider",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAiConversationService>();

        var first = await service.GetConversationsPageAsync(TestDataIds.UserId, pageSize: 30, null, null, default);
        Assert.Equal(30, first.Items.Count);
        Assert.True(first.HasMore);
        Assert.DoesNotContain(first.Items, x => x.Title == "Outsider");

        var cursor = first.Items[^1];
        var second = await service.GetConversationsPageAsync(
            TestDataIds.UserId,
            pageSize: 30,
            cursor.UpdatedAtUtc,
            cursor.Id,
            default);

        Assert.Equal(15, second.Items.Count);
        Assert.False(second.HasMore);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
    }

    [Fact]
    public async Task MessagesPage_ReturnsNewestWindowChronologically_WithoutLoadingEntireHistory()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var conversationId = Guid.NewGuid();
        var baseTime = DateTime.UtcNow.AddHours(-2);
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.AiConversations.Add(new AiConversation
            {
                Id = conversationId,
                UserId = TestDataIds.UserId,
                Title = "Long history",
                CreatedAtUtc = baseTime,
                UpdatedAtUtc = baseTime
            });

            for (var i = 0; i < 120; i++)
            {
                db.AiMessages.Add(new AiMessage
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    Role = i % 2 == 0 ? AiMessageRole.User : AiMessageRole.Assistant,
                    Content = $"msg-{i:D3}",
                    CreatedAtUtc = baseTime.AddMinutes(i)
                });
            }

            await db.SaveChangesAsync();
        }

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAiConversationService>();
        var page = await service.GetMessagesPageAsync(TestDataIds.UserId, conversationId, pageSize: 50, null, null, default);

        Assert.Equal(50, page.Items.Count);
        Assert.True(page.HasMore);
        Assert.Equal("msg-070", page.Items[0].Content);
        Assert.Equal("msg-119", page.Items[^1].Content);
        for (var i = 1; i < page.Items.Count; i++)
        {
            Assert.True(page.Items[i - 1].CreatedAtUtc <= page.Items[i].CreatedAtUtc);
        }

        var older = await service.GetMessagesPageAsync(
            TestDataIds.UserId,
            conversationId,
            pageSize: 50,
            page.Items[0].CreatedAtUtc,
            page.Items[0].Id,
            default);

        Assert.Equal(50, older.Items.Count);
        Assert.True(older.HasMore);
        Assert.Equal("msg-020", older.Items[0].Content);
        Assert.Equal("msg-069", older.Items[^1].Content);
        Assert.Empty(page.Items.Select(x => x.Id).Intersect(older.Items.Select(x => x.Id)));
    }

    [Fact]
    public async Task MessagesPage_Outsider_ReturnsNotFound()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();

        var conversationId = Guid.NewGuid();
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.AiConversations.Add(new AiConversation
            {
                Id = conversationId,
                UserId = TestDataIds.UserId,
                Title = "Owned",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAiConversationService>();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetMessagesPageAsync(TestDataIds.OutsiderUserId, conversationId, 20, null, null, default));
    }
}
