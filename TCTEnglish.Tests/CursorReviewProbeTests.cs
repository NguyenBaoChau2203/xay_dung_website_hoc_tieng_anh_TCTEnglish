using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using Xunit;
using Xunit.Abstractions;

namespace TCTEnglish.Tests;

public sealed class CursorReviewProbes(ITestOutputHelper output)
{
    [Fact]
    public async Task Quota_OverlappingScopes_DoNotLoseSettledOrPendingCounts()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var a = scopeA.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>();
        var b = scopeB.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>();
        var ra = await a.ReserveQuestionAsync(TestDataIds.UserId, 100, 15, 60000, default);
        var rb = await b.ReserveQuestionAsync(TestDataIds.UserId, 100, 15, 60000, default);
        await a.SettleAsync(ra, 20, default);
        await b.SettleAsync(rb, 20, default);
        using var verify = factory.Services.CreateScope();
        var row = await verify.ServiceProvider.GetRequiredService<DbflashcardContext>()
            .AiUserDailyQuotas.AsNoTracking().SingleAsync();
        output.WriteLine($"actual settled={row.SettledQuestions}, pending={row.PendingQuestions}, tokens={row.SettledTokens}/{row.PendingTokens}; expected 2/0/40/0");
        Assert.Equal(2, row.SettledQuestions);
        Assert.Equal(0, row.PendingQuestions);
    }

    [Fact]
    public async Task Quota_ExistingSuccessfulLogsStillConsumeDailyLimit()
    {
        int calls = 0;
        await using var factory = CreateFactory(_ =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new AiProviderReply("ok", 10, 10, 20, "fake", "probe"));
        });
        await factory.InitializeAsync();
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var conversation = new AiConversation { Id=Guid.NewGuid(), UserId=TestDataIds.UserId, Title="historical", CreatedAtUtc=DateTime.UtcNow, UpdatedAtUtc=DateTime.UtcNow };
            db.AiConversations.Add(conversation);
            for (int i = 0; i < 15; i++) db.AiRequestLogs.Add(new AiRequestLog
            {
                Id = Guid.NewGuid(), UserId = TestDataIds.UserId, ConversationId=conversation.Id, IsSuccess = true,
                RequestedAtUtc = DateTime.UtcNow, TotalTokens = 20
            });
            await db.SaveChangesAsync();
        }
        using var scope = factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IAiChatService>();
        var before = await chat.GetDailyUsageAsync(TestDataIds.UserId, default);
        var error = await Record.ExceptionAsync(() => chat.SendAsync(TestDataIds.UserId, null, "sixteenth request", default));
        output.WriteLine($"UI usage before={before.RequestedToday}; provider calls={calls}; error={error?.GetType().Name ?? "none"}");
        Assert.IsType<AiRateLimitException>(error);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Cancel_AfterPromptCommitBeforeProvider_RemovesOrphan()
    {
        using var cts = new CancellationTokenSource();
        await using var factory = CreateFactory(_ => Task.FromResult(new AiProviderReply("fake", 1, 1, 2, "fake", "probe")));
        factory.SaveInterceptors.Add(new CancelPromptInterceptor(cts));
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<IAiChatService>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chat.SendAsync(TestDataIds.UserId, null, "cancel after commit", cts.Token));
        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var prompts = await db.AiMessages.AsNoTracking().CountAsync();
        output.WriteLine($"persisted prompts={prompts}; conversations={await db.AiConversations.CountAsync()}; provider was never entered");
        Assert.Equal(0, prompts);
    }

    [Fact]
    public async Task Login_MalformedBcryptWithRecognizedPrefixAndLength_FailsWithout500()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var user = await db.Users.SingleAsync(x => x.UserId == TestDataIds.UserId);
            user.PasswordHash = "$2a$99$" + new string('a', 53);
            await db.SaveChangesAsync();
        }
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Account/Login");
        using var content = new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["email"]="student@test.local", ["password"]="irrelevant", ["__RequestVerificationToken"]=token
        });
        using var response = await client.PostAsync("/Account/Login", content);
        var body = await response.Content.ReadAsStringAsync();
        output.WriteLine($"status={(int)response.StatusCode}; salt error={body.Contains("SaltParse")}; cost error={body.Contains("work factor", StringComparison.OrdinalIgnoreCase)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmailDoesNotPersistUser()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client = IntegrationTestClientHelper.CreateAnonymousClient(factory);
        var token = await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client, "/Account/Login");
        using var content = new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["email"]="not-an-email", ["password"]="123456", ["fullName"]="Review Fixture", ["__RequestVerificationToken"]=token
        });
        using var response = await client.PostAsync("/Account/Register", content);
        using var verify = factory.Services.CreateScope();
        var saved = await verify.ServiceProvider.GetRequiredService<DbflashcardContext>().Users.AnyAsync(x => x.Email == "not-an-email");
        output.WriteLine($"status={(int)response.StatusCode}; invalid email persisted={saved}");
        Assert.False(saved);
    }

    [Fact]
    public async Task Quota_FinalizationReplayDoesNotDoubleCount()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var quota = scope.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>();
        var reservation = await quota.ReserveQuestionAsync(TestDataIds.UserId, 100, 15, 60000, default);
        await quota.SettleAsync(reservation, 20, default);
        await quota.SettleAsync(reservation, 20, default);
        using var verify = factory.Services.CreateScope();
        var row = await verify.ServiceProvider.GetRequiredService<DbflashcardContext>().AiUserDailyQuotas.AsNoTracking().SingleAsync();
        output.WriteLine($"replaying same reservation: settled={row.SettledQuestions}, settledTokens={row.SettledTokens}; expected 1/20");
        Assert.Equal(1, row.SettledQuestions);
    }

    [Fact]
    public async Task History_HttpPageProvidesAPathToOlderData()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.InitializeAsync();
        var conversationId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(-1);
        using(var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.AiConversations.Add(new AiConversation { Id=conversationId, UserId=TestDataIds.UserId, Title="Review newest", CreatedAtUtc=start.AddHours(1), UpdatedAtUtc=start.AddHours(1) });
            for(int i=0;i<35;i++) db.AiConversations.Add(new AiConversation { Id=Guid.NewGuid(), UserId=TestDataIds.UserId, Title=$"Review old conversation {i:D3}", CreatedAtUtc=start.AddMinutes(i), UpdatedAtUtc=start.AddMinutes(i) });
            for(int i=0;i<120;i++) db.AiMessages.Add(new AiMessage { Id=Guid.NewGuid(), ConversationId=conversationId, Role=i%2==0?AiMessageRole.User:AiMessageRole.Assistant, Content=$"review-history-content-{i:D3}", CreatedAtUtc=start.AddMinutes(i) });
            await db.SaveChangesAsync();
        }
        using var client=IntegrationTestClientHelper.CreateAuthenticatedClient(factory,TestDataIds.UserId,Roles.Standard);
        using var response=await client.GetAsync($"/AI/Chat?conversationId={conversationId}");
        var html=await response.Content.ReadAsStringAsync();
        bool oldestMessage=html.Contains("review-history-content-000");
        bool oldestConversation=html.Contains("Review old conversation 000");
        bool canLoadOlder=html.Contains("data-ai-load-older",StringComparison.OrdinalIgnoreCase)||html.Contains("Load older",StringComparison.OrdinalIgnoreCase)||html.Contains("Tải tin nhắn cũ",StringComparison.OrdinalIgnoreCase);
        output.WriteLine($"status={(int)response.StatusCode}; oldest message={oldestMessage}; oldest conversation={oldestConversation}; load-older control={canLoadOlder}");
        Assert.True(oldestMessage||canLoadOlder,"When first window omits old history, UI must expose a load-older path.");
    }

    [Fact]
    public async Task ChangePassword_MalformedHashDoesNotBypassCurrentPasswordAndLengthValidation()
    {
        await using var factory=new TestWebApplicationFactory();
        await factory.InitializeAsync();
        using var client=IntegrationTestClientHelper.CreateAuthenticatedClient(factory,TestDataIds.UserId,Roles.Standard);
        var token=await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client,"/Account/Settings");
        using var content=new FormUrlEncodedContent(new Dictionary<string,string> { ["CurrentPassword"]="wrong",["NewPassword"]="x",["ConfirmPassword"]="x",["__RequestVerificationToken"]=token });
        using var response=await client.PostAsync("/Account/ChangePassword",content);
        using var verify=factory.Services.CreateScope();
        var user=await verify.ServiceProvider.GetRequiredService<DbflashcardContext>().Users.AsNoTracking().SingleAsync(x=>x.UserId==TestDataIds.UserId);
        bool overwritten=user.PasswordHash!="hash";
        output.WriteLine($"status={(int)response.StatusCode}; malformed stored hash changed with wrong current password and one-char new password={overwritten}");
        Assert.False(overwritten);
    }

    [Fact]
    public async Task SignalR_StopOnSameConnection_CancelsWaitingProviderBeforeProviderCompletes()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var factory = CreateFactory(async ct =>
        {
            entered.TrySetResult();
            try { await release.Task.WaitAsync(ct); }
            catch (OperationCanceledException) { cancelled.TrySetResult(); throw; }
            return new AiProviderReply("fake", 1, 1, 2, "fake", "probe");
        });
        await factory.InitializeAsync();
        Guid conversationId = Guid.NewGuid();
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.AiConversations.Add(new AiConversation { Id=conversationId, UserId=TestDataIds.UserId, Title="probe", CreatedAtUtc=DateTime.UtcNow, UpdatedAtUtc=DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        output.WriteLine($"MaximumParallelInvocationsPerClient={factory.Services.GetRequiredService<IOptions<HubOptions>>().Value.MaximumParallelInvocationsPerClient}");
        var client = factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers.Add(TestAuthenticationHandler.UserIdHeaderName, TestDataIds.UserId.ToString());
        using var socket = await client.ConnectAsync(new Uri("ws://localhost/classChatHub"), default);
        var frames = new Queue<JsonElement>();
        string buffered = "";
        async Task Send(object data) => await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data)+"\u001e")), WebSocketMessageType.Text, true, default);
        async Task<JsonElement> Read()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while(frames.Count==0)
            {
                var bytes=new byte[8192];
                var result=await socket.ReceiveAsync(new ArraySegment<byte>(bytes),timeout.Token);
                buffered+=Encoding.UTF8.GetString(bytes,0,result.Count);
                int end;
                while((end=buffered.IndexOf('\u001e'))>=0)
                {
                    var frame=buffered[..end]; buffered=buffered[(end+1)..];
                    if(frame.Length>0) frames.Enqueue(JsonDocument.Parse(frame).RootElement.Clone());
                }
            }
            return frames.Dequeue();
        }
        bool didCancel;
        try
        {
            await Send(new { protocol="json", version=1 });
            _=await Read();
            await Send(new { type=1, invocationId="start-probe", target="StartAiStream", arguments=new object[]{conversationId,"stop probe"} });
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            string? streamId=null;
            for(int i=0;i<10 && streamId is null;i++)
            {
                var frame=await Read();
                if(frame.TryGetProperty("target",out var target) && target.GetString()=="AiStreamStarted")
                    streamId=frame.GetProperty("arguments")[0].GetProperty("streamId").GetString();
            }
            Assert.NotNull(streamId);
            await Send(new { type=1, invocationId="stop-probe", target="StopAiStream", arguments=new[]{streamId} });
            var finished = await Task.WhenAny(cancelled.Task, Task.Delay(1000));
            didCancel = finished==cancelled.Task;
            output.WriteLine($"AiStreamStarted received; StopAiStream sent while provider blocked; provider cancelled within 1s={didCancel}");
        }
        finally { release.TrySetResult(); socket.Abort(); }
        Assert.True(didCancel, "Stop invocation on the same SignalR connection must run before StartAiStream finishes.");
    }

    private static TestWebApplicationFactory CreateFactory(Func<CancellationToken,Task<AiProviderReply>> reply) => new(services =>
    {
        services.RemoveAll<IAiProviderClient>();
        services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, _, ct) => reply(ct)));
        services.KeepChatLifecycleOnProviderStub();
        services.PostConfigure<AiOptions>(o => { o.DailyTokenBudgetPerUser=60000; o.RequestTokenBudget=4600; o.MaxInputChars=1000; });
    });

    private sealed class CancelPromptInterceptor(CancellationTokenSource cts) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken=default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AiMessage>().Any(x=>x.Entity.Role==AiMessageRole.User)) cts.Cancel();
            return ValueTask.FromResult(result);
        }
    }
}
