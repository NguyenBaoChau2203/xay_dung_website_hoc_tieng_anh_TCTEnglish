using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingBriefCommitTests
{
    internal sealed class BeforePublication : SaveChangesInterceptor
    {
        public bool Fired;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Fired && eventData.Context!.ChangeTracker.Entries<WritingGenerationOperation>().Any(e => e.Entity.State == "succeeded"))
            { Fired = true; throw new InvalidOperationException("synthetic pre-save interruption"); }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
    internal sealed class AfterPublication : DbTransactionInterceptor
    {
        public bool Fired;
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (!Fired && eventData.Context!.ChangeTracker.Entries<WritingGenerationOperation>().Any(e => e.Entity.State == "succeeded"))
            { Fired = true; throw new InvalidOperationException("synthetic lost commit acknowledgement"); }
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoveryBeforeOrAfterCommitNeverRedispatchesOrDuplicates(bool afterCommit)
    {
        var clock = new GoalsStage03RegressionTests.TestClock(new DateTime(2026, 11, 15, 1, 0, 0));
        var calls = 0;
        await using var f = new TestWebApplicationFactory(s =>
        {
            GoalsStage03RegressionTests.Configure(s, clock); s.RemoveAll<IAiProviderClient>();
            s.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, _, _) => { calls++; return Task.FromResult(WritingBriefLifecycleTests.Reply()); }));
        });
        var before = new BeforePublication(); var after = new AfterPublication();
        f.SaveInterceptors.Add(afterCommit ? after : before);
        await AssertRecovery(f, clock, afterCommit, before, after);
        Assert.Equal(1, calls);
    }

    internal static async Task AssertRecovery(TestWebApplicationFactory f, GoalsStage03RegressionTests.TestClock clock,
        bool afterCommit, BeforePublication before, AfterPublication after)
    {
        await WritingBriefGenerationTests.Premium(f);
        var first = await WritingBriefLifecycleTests.Create(f, WritingBriefLifecycleTests.Request());
        Assert.Equal(WritingCreateFromAiOutcome.Pending, first.Outcome);
        Assert.Equal(afterCommit, after.Fired); Assert.Equal(!afterCommit, before.Fired);
        clock.Now = clock.Now.AddSeconds(101);
        var recovered = await WritingBriefLifecycleTests.Create(f, WritingBriefLifecycleTests.Request());
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        if (!afterCommit)
        {
            Assert.Equal(WritingCreateFromAiOutcome.TemplateOffer, recovered.Outcome);
            Assert.False(await db.WritingExercises.AnyAsync(e => e.UserId == TestDataIds.UserId));
            Assert.Equal(300, recovered.KnownProviderTokens);
            recovered = await scope.ServiceProvider.GetRequiredService<IWritingService>()
                .ConfirmWritingTemplateAsync(TestDataIds.UserId, recovered.OperationId!.Value, WritingBriefLifecycleTests.Choice());
        }
        Assert.Equal(WritingCreateFromAiOutcome.Success, recovered.Outcome);
        Assert.Equal(1, await db.WritingGenerationLogs.CountAsync(l => l.IsSuccess));
        Assert.Equal(1, await db.WritingExercises.CountAsync(e => e.UserId == TestDataIds.UserId));
    }
}
