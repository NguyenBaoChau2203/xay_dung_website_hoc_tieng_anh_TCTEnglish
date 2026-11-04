using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class AiCancelAndStopIntegrationTests
{
    [Fact]
    public async Task SendAsync_CallerCancelAfterUserMessageSave_RollsBackDraftAndDoesNotLeaveAssistant()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var factory = CreateFactory(async ct =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new AiProviderReply("should-not-persist", 1, 1, 2, "test-model", "req-cancel-draft");
        });
        await factory.InitializeAsync();

        using var scope = factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IAiChatService>();
        using var cts = new CancellationTokenSource();

        var sendTask = chat.SendAsync(TestDataIds.UserId, conversationId: null, "cancel me draft", cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sendTask.WaitAsync(TimeSpan.FromSeconds(5)));

        await using var assertScope = factory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(0, await db.AiConversations.AsNoTracking().CountAsync(x => x.UserId == TestDataIds.UserId));
        Assert.Equal(0, await db.AiMessages.AsNoTracking().CountAsync());
        Assert.Equal(
            0,
            await db.AiRequestLogs.AsNoTracking().CountAsync(x => x.UserId == TestDataIds.UserId && x.IsSuccess));
    }

    [Fact]
    public async Task SendAsync_CallerCancelOnExistingConversation_RemovesOrphanUserPrompt()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var factory = CreateFactory(async ct =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new AiProviderReply("should-not-persist", 1, 1, 2, "test-model", "req-cancel-existing");
        });
        await factory.InitializeAsync();

        Guid conversationId;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            conversationId = Guid.NewGuid();
            db.AiConversations.Add(new AiConversation
            {
                Id = conversationId,
                UserId = TestDataIds.UserId,
                Title = "Existing",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var scope = factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IAiChatService>();
        using var cts = new CancellationTokenSource();

        var sendTask = chat.SendAsync(TestDataIds.UserId, conversationId, "orphan prompt", cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sendTask.WaitAsync(TimeSpan.FromSeconds(5)));

        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.True(await assertDb.AiConversations.AsNoTracking().AnyAsync(x => x.Id == conversationId));
        Assert.Equal(0, await assertDb.AiMessages.AsNoTracking().CountAsync(x => x.ConversationId == conversationId));
        Assert.Equal(
            0,
            await assertDb.AiRequestLogs.AsNoTracking().CountAsync(x => x.ConversationId == conversationId && x.IsSuccess));
    }

    [Fact]
    public async Task Streaming_StopDuringProviderWait_CancelsBeforeOutput()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var factory = CreateFactory(async ct =>
        {
            entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }

            return new AiProviderReply("late", 1, 1, 2, "test-model", "req-stop");
        });
        await factory.InitializeAsync();

        Guid conversationId;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            conversationId = Guid.NewGuid();
            db.AiConversations.Add(new AiConversation
            {
                Id = conversationId,
                UserId = TestDataIds.UserId,
                Title = "Stream stop",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var scope = factory.Services.CreateScope();
        var streaming = scope.ServiceProvider.GetRequiredService<IAiStreamingService>();
        var startedIds = new List<string>();
        using var connectionAborted = new CancellationTokenSource();

        var startTask = streaming.StartStreamAsync(
            TestDataIds.UserId,
            conversationId,
            "stop while waiting",
            connectionId: "conn-stop-1",
            ipAddress: "127.0.0.1",
            connectionAborted.Token,
            onStreamStarted: streamId =>
            {
                startedIds.Add(streamId);
                return Task.CompletedTask;
            });

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(startedIds);

        await streaming.StopStreamAsync("conn-stop-1", startedIds[0]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startTask.WaitAsync(TimeSpan.FromSeconds(5)));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(0, await assertDb.AiMessages.AsNoTracking().CountAsync(x => x.ConversationId == conversationId));
    }

    [Fact]
    public async Task HttpSend_AfterCancelCleanup_AllowsNextTurn()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var call = 0;
        await using var factory = CreateFactory(async ct =>
        {
            var n = Interlocked.Increment(ref call);
            if (n == 1)
            {
                await release.Task.WaitAsync(ct);
                ct.ThrowIfCancellationRequested();
            }

            return new AiProviderReply($"reply-{n}", 1, 1, 2, "test-model", $"req-{n}");
        });
        await factory.InitializeAsync();

        using var client = IntegrationTestClientHelper.CreateAuthenticatedClient(factory, TestDataIds.UserId, Roles.Standard);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/AI/Chat");

        using var firstCts = new CancellationTokenSource();
        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/AI/Chat/Send")
        {
            Content = JsonContent.Create(new { conversationId = (Guid?)null, message = "first cancelled" })
        };
        firstRequest.Headers.Add("RequestVerificationToken", token);

        var firstTask = client.SendAsync(firstRequest, firstCts.Token);
        await Task.Delay(200);
        await firstCts.CancelAsync();
        release.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstTask.WaitAsync(TimeSpan.FromSeconds(5)));

        using var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/AI/Chat/Send")
        {
            Content = JsonContent.Create(new { conversationId = (Guid?)null, message = "second ok" })
        };
        secondRequest.Headers.Add("RequestVerificationToken", token);
        using var secondResponse = await client.SendAsync(secondRequest);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
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
                options.RequestTimeoutSeconds = 5;
                options.MaxInputChars = 1000;
                options.RequestTokenBudget = 4600;
                options.DailyTokenBudgetPerUser = 60000;
            });
            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, _, ct) => replyFactory(ct)));
        });
    }
}
