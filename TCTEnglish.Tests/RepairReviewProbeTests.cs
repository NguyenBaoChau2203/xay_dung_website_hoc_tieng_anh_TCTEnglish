using System.Net;
using System.Net.Http.Json;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTVocabulary.Models;
using Xunit;
using Xunit.Abstractions;

namespace TCTEnglish.Tests;

public sealed class RepairReviewTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Send_SuccessAdvancesConversationTimestamp()
    {
        await using var factory=CreateFactory();
        await factory.InitializeAsync();
        var old=DateTime.UtcNow.AddDays(-3);
        var id=await SeedConversation(factory,old);
        using var scope=factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAiChatService>().SendAsync(TestDataIds.UserId,id,"hello again",default);
        using var verify=factory.Services.CreateScope();
        var db=verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var actual=await db.AiConversations.AsNoTracking().Where(x=>x.Id==id).Select(x=>x.UpdatedAtUtc).SingleAsync();
        output.WriteLine($"before={old:O}; after={actual:O}; advanced={actual>old.AddHours(1)}; messages={await db.AiMessages.CountAsync(x=>x.ConversationId==id)}");
        Assert.True(actual>old.AddHours(1),"Successful reply must update history recency after quota reserve.");
    }

    [Fact]
    public async Task CancelAtFinalize_AfterCommittedReply_DoesNotReleaseChargedReservation()
    {
        using var cts=new CancellationTokenSource();
        await using var factory=CreateFactory(services=>
        {
            services.RemoveAll<IAiDailyQuotaReservationService>();
            services.AddScoped<AiDailyQuotaReservationService>();
            services.AddScoped<IAiDailyQuotaReservationService>(sp=>new FinalizeFailure(sp.GetRequiredService<AiDailyQuotaReservationService>(),cts));
        });
        await factory.InitializeAsync();
        using var scope=factory.Services.CreateScope();
        var error=await Record.ExceptionAsync(()=>scope.ServiceProvider.GetRequiredService<IAiChatService>().SendAsync(TestDataIds.UserId,null,"cancel at finalization",cts.Token));
        using var verify=factory.Services.CreateScope();
        var db=verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var ledger=await db.AiQuotaReservationLedgers.AsNoTracking().SingleAsync();
        var quota=await db.AiUserDailyQuotas.AsNoTracking().SingleAsync();
        output.WriteLine($"error={error?.GetType().Name}; assistant={await db.AiMessages.CountAsync(x=>x.Role==AiMessageRole.Assistant)}; successLogs={await db.AiRequestLogs.CountAsync(x=>x.IsSuccess)}; status={ledger.Status}; pending={quota.PendingQuestions}; settled={quota.SettledQuestions}");
        Assert.NotEqual(AiQuotaReservationStatus.Released,ledger.Status);
    }

    [Fact]
    public async Task UpdateEmail_InvalidEmailMustNotPersist()
    {
        await using var factory=CreateFactory();
        await factory.InitializeAsync();
        using var client=IntegrationTestClientHelper.CreateAuthenticatedClient(factory,TestDataIds.UserId,Roles.Standard);
        var token=await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client,"/Account/Settings");
        using var content=new FormUrlEncodedContent(new Dictionary<string,string>{["Email"]="not-an-email",["__RequestVerificationToken"]=token});
        using var response=await client.PostAsync("/Account/UpdateEmail",content);
        using var verify=factory.Services.CreateScope();
        var actual=await verify.ServiceProvider.GetRequiredService<DbflashcardContext>().Users.Where(x=>x.UserId==TestDataIds.UserId).Select(x=>x.Email).SingleAsync();
        output.WriteLine($"HTTP={(int)response.StatusCode}; invalid email persisted={actual=="not-an-email"}");
        Assert.NotEqual("not-an-email",actual);
    }

    [Fact]
    public async Task RetryAfterCommittedReplyFailure_DoesNotCallProviderTwiceForSameIntent()
    {
        int calls=0;
        await using var factory=CreateFactory(services=>
        {
            services.RemoveAll<IAiProviderClient>();
            services.AddScoped<IAiProviderClient>(_=>new StubAiProviderClient((_,_,_)=>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult(new AiProviderReply("ok",10,10,20,"fake","review"));
            }));
            services.RemoveAll<IAiDailyQuotaReservationService>();
            services.AddScoped<AiDailyQuotaReservationService>();
            services.AddScoped<IAiDailyQuotaReservationService>(sp=>new FinalizeFailure(sp.GetRequiredService<AiDailyQuotaReservationService>(),null));
        });
        await factory.InitializeAsync();
        var id=await SeedConversation(factory,DateTime.UtcNow.AddDays(-1));
        using var client=IntegrationTestClientHelper.CreateAuthenticatedClient(factory,TestDataIds.UserId,Roles.Standard);
        var token=await IntegrationTestClientHelper.GetAntiForgeryTokenAsync(client,"/AI/Chat");
        async Task<HttpResponseMessage> Send()
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,"/AI/Chat/Send"){Content=JsonContent.Create(new{conversationId=id,message="same intent same payload"})};
            request.Headers.Add("RequestVerificationToken",token);
            return await client.SendAsync(request);
        }
        using var first=await Send();
        // Production sendWithRetry retries >=500 for an existing conversation.
        using var second=(int)first.StatusCode>=500?await Send():null;
        using var verify=factory.Services.CreateScope();
        var db=verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        output.WriteLine($"HTTP first={(int)first.StatusCode}; retry={(int?)second?.StatusCode}; provider calls={calls}; assistant rows={await db.AiMessages.CountAsync(x=>x.Role==AiMessageRole.Assistant)}");
        Assert.Equal(1,calls);
    }

    private static TestWebApplicationFactory CreateFactory(Action<IServiceCollection>? configure=null)=>new(services=>
    {
        services.RemoveAll<IAiProviderClient>();
        services.AddScoped<IAiProviderClient>(_=>new StubAiProviderClient((_,_,_)=>Task.FromResult(new AiProviderReply("ok",10,10,20,"fake","review"))));
        services.PostConfigure<AiOptions>(o=>{o.RequestTokenBudget=4600;o.DailyTokenBudgetPerUser=60000;o.MaxInputChars=1000;});
        configure?.Invoke(services);
        services.KeepChatLifecycleOnProviderStub();
    });

    private static async Task<Guid> SeedConversation(TestWebApplicationFactory factory,DateTime old)
    {
        using var scope=factory.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var id=Guid.NewGuid();
        db.AiConversations.Add(new AiConversation{Id=id,UserId=TestDataIds.UserId,Title="existing",CreatedAtUtc=old,UpdatedAtUtc=old});
        await db.SaveChangesAsync();
        return id;
    }

    private sealed class FinalizeFailure(IAiDailyQuotaReservationService inner,CancellationTokenSource? cts):IAiDailyQuotaReservationService
    {
        public Task<AiQuotaReservation> ReserveQuestionAsync(int userId,int plannedTokens,int questionLimit,int tokenBudget,CancellationToken ct)=>inner.ReserveQuestionAsync(userId,plannedTokens,questionLimit,tokenBudget,ct);
        public Task SettleAsync(AiQuotaReservation reservation,int actualTokens,CancellationToken ct)
        {
            if(cts is not null){cts.Cancel();ct.ThrowIfCancellationRequested();}
            throw new InvalidOperationException("Synthetic finalization failure after committed provider reply.");
        }
        public Task ReleaseAsync(AiQuotaReservation reservation,CancellationToken ct)=>inner.ReleaseAsync(reservation,ct);
        public Task MarkKnownUsagePendingAsync(AiQuotaReservation reservation,int actualTokens,CancellationToken ct)=>inner.MarkKnownUsagePendingAsync(reservation,actualTokens,ct);
        public Task<bool> RecoverKnownPendingAsync(Guid requestId,CancellationToken ct)=>inner.RecoverKnownPendingAsync(requestId,ct);
    }
}

[Collection("MariaDbAcceptance")]
public sealed class RepairMariaDbConcurrencyReview(ITestOutputHelper output)
{
    [MariaDbAcceptanceFact]
    public async Task SimultaneousReservations_WithTwoAvailableSlots_BothSucceed()
    {
        var connection=Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var runId=Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");
        await using var factory=new MariaDbAcceptanceFactory(connection,runId);
        await factory.InitializeAsync();
        using(var seed=factory.Services.CreateScope())
        {
            var db=seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.AiUserDailyQuotas.Add(new AiUserDailyQuota{UserId=TestDataIds.UserId,DayUtc=DateTime.UtcNow.Date,SettledQuestions=13,UpdatedAtUtc=DateTime.UtcNow});
            await db.SaveChangesAsync();
        }
        var barrier=new QuotaReadBarrier();
        var options=new DbContextOptionsBuilder<DbflashcardContext>()
            .UseMySql(connection,ServerVersion.Parse("10.5.23-mariadb"))
            .AddInterceptors(barrier).Options;
        await using var dbA=new DbflashcardContext(options);
        await using var dbB=new DbflashcardContext(options);
        var a=AiDailyQuotaReservationService.ForDbContext(dbA);
        var b=AiDailyQuotaReservationService.ForDbContext(dbB);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var ta=Record.ExceptionAsync(()=>a.ReserveQuestionAsync(TestDataIds.UserId,100,15,60000,timeout.Token));
        var tb=Record.ExceptionAsync(()=>b.ReserveQuestionAsync(TestDataIds.UserId,100,15,60000,timeout.Token));
        var errors=await Task.WhenAll(ta,tb);
        using var verify=factory.Services.CreateScope();
        var row=await verify.ServiceProvider.GetRequiredService<DbflashcardContext>().AiUserDailyQuotas.AsNoTracking().SingleAsync();
        output.WriteLine($"simultaneous quota reads={barrier.Reads}; pending={row.PendingQuestions}; settled={row.SettledQuestions}; successes={errors.Count(x=>x is null)}");
        foreach(var error in errors.Where(x=>x is not null))
            output.WriteLine($"error={error!.GetType().Name}; root={error.GetBaseException().GetType().Name}: {error.GetBaseException().Message}");
        Assert.All(errors,x=>Assert.Null(x));
        Assert.Equal(2,row.PendingQuestions);
    }

    private sealed class QuotaReadBarrier:DbCommandInterceptor
    {
        private readonly TaskCompletionSource _both=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _reads;
        public int Reads=>_reads;
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData eventData,DbDataReader result,CancellationToken cancellationToken=default)
        {
            if(command.CommandText.Contains("FROM `AiUserDailyQuotas`",StringComparison.Ordinal))
            {
                if(Interlocked.Increment(ref _reads)>=2)_both.TrySetResult();
                await _both.Task.WaitAsync(TimeSpan.FromSeconds(5),cancellationToken);
            }
            return result;
        }
    }
}
