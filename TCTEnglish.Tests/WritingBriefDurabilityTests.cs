using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Models;
using TCTEnglish.Services.Accounts;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTEnglish.Tests.TestHelpers;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using Xunit;

namespace TCTEnglish.Tests;

public sealed class WritingBriefDurabilityTests
{
    [Fact]
    public async Task AdminKeepsUnlimitedPolicyAndBlockedOrAnonymizedUsersNeverDispatch()
    {
        var calls = 0;
        await using var f = WritingBriefGenerationTests.Factory((_, _) => { calls++; return Task.FromResult(WritingBriefLifecycleTests.Reply()); });
        await WritingBriefGenerationTests.Premium(f);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId);
        user.Role = Roles.Admin;
        db.WritingGenerationLogs.AddRange(Enumerable.Range(0, 5).Select(_ => new WritingGenerationLog
        { Id = Guid.NewGuid(), UserId = user.UserId, RequestType = "create-from-ai", IsSuccess = true, RequestedAtUtc = DateTime.UtcNow }));
        await db.SaveChangesAsync();
        Assert.Equal(WritingCreateFromAiOutcome.Success, (await WritingBriefLifecycleTests.Create(f, WritingBriefLifecycleTests.Request())).Outcome);
        user.Status = UserStatus.Blocked; await db.SaveChangesAsync();
        Assert.Equal(WritingCreateFromAiOutcome.Forbidden, (await WritingBriefLifecycleTests.Create(f, WritingBriefLifecycleTests.Request("blocked"))).Outcome);
        user.Status = UserStatus.Offline; user.AnonymizedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        Assert.Equal(WritingCreateFromAiOutcome.Forbidden, (await WritingBriefLifecycleTests.Create(f, WritingBriefLifecycleTests.Request("anonymized"))).Outcome);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task DowngradeDuringDispatchPreventsPublicationAndTemplateBypass()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var f = WritingBriefGenerationTests.Factory(async (_, _) =>
        { entered.TrySetResult(); await release.Task; return WritingBriefLifecycleTests.Reply(); });
        await WritingBriefGenerationTests.Premium(f);
        var running = WritingBriefLifecycleTests.Create(f, WritingBriefLifecycleTests.Request());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        (await db.Users.SingleAsync(u => u.UserId == TestDataIds.UserId)).Role = Roles.Standard; await db.SaveChangesAsync();
        release.SetResult();
        var result = await running;
        Assert.Equal(WritingCreateFromAiOutcome.Forbidden, result.Outcome); Assert.Empty(result.Templates);
        Assert.False(await db.WritingExercises.AnyAsync(e => e.UserId == TestDataIds.UserId));
        Assert.False(await db.WritingGenerationLogs.AnyAsync(l => l.IsSuccess));
    }

    [Fact]
    public async Task IndependentConnectionsRespectCapAndReplay()
    {
        var calls = 0;
        await using var f = WritingBriefGenerationTests.Factory(async (_, _) => { Interlocked.Increment(ref calls); await Task.Delay(50); return WritingBriefLifecycleTests.Reply(); });
        await WritingBriefGenerationTests.Premium(f);
        await AssertQuotaRace(f);
        Assert.Equal(5, calls);
    }

    internal static async Task AssertQuotaRace(TestWebApplicationFactory f)
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 9).Select(i => WritingBriefLifecycleTests.Create(f, WritingBriefLifecycleTests.Request("race-" + i))))
            .WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal(5, results.Count(r => r.Outcome == WritingCreateFromAiOutcome.Success));
        Assert.Equal(4, results.Count(r => r.Outcome == WritingCreateFromAiOutcome.QuotaExceeded));
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(5, await db.WritingGenerationLogs.CountAsync(l => l.IsSuccess));
        Assert.Equal(5, await db.WritingExercises.CountAsync(e => e.UserId == TestDataIds.UserId));
        Assert.False(await db.WritingGenerationOperations.AnyAsync(o => o.Reserved));
    }

    [Fact]
    public async Task BothModesAndTemplateEngineShareExactlyFiveSuccesses()
    {
        var calls = 0;
        await using var f = WritingBriefGenerationTests.Factory((_, _) =>
        { if (++calls <= 2) return Task.FromResult(WritingBriefLifecycleTests.Reply()); throw new AiProviderException("synthetic", "circuit_open", false); });
        await WritingBriefGenerationTests.Premium(f);
        await AssertSharedQuota(f);
        Assert.Equal(5, calls);
    }

    internal static async Task AssertSharedQuota(TestWebApplicationFactory f)
    {
        for (var i = 0; i < 2; i++)
            Assert.Equal(WritingCreateFromAiOutcome.Success, (await WritingBriefLifecycleTests.Create(f,
                new() { SourceText = "A complete fictional source for bilingual conversion.", IdempotencyKey = "text-" + i })).Outcome);
        using var scope = f.Services.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<IWritingService>();
        for (var i = 0; i < 3; i++)
        {
            var op = await WritingBriefLifecycleTests.Create(f, WritingBriefLifecycleTests.Request("template-" + i));
            Assert.Equal(WritingCreateFromAiOutcome.TemplateOffer, op.Outcome);
            Assert.Equal(WritingCreateFromAiOutcome.Success, (await service.ConfirmWritingTemplateAsync(TestDataIds.UserId, op.OperationId!.Value, WritingBriefLifecycleTests.Choice())).Outcome);
        }
        Assert.Equal(WritingCreateFromAiOutcome.QuotaExceeded, (await WritingBriefLifecycleTests.Create(f, WritingBriefLifecycleTests.Request("sixth"))).Outcome);
    }

    [Fact]
    public async Task UtcMidnightRechecksFinalQuotaAndExpiredOffersNeverRevive()
    {
        var clock = new GoalsStage03RegressionTests.TestClock(new DateTime(2026, 11, 15, 23, 59, 59));
        await using var f = new TestWebApplicationFactory(s =>
        {
            GoalsStage03RegressionTests.Configure(s, clock); s.RemoveAll<IAiProviderClient>();
            s.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, _, _) =>
            { clock.Now = clock.Now.AddSeconds(2); return Task.FromResult(WritingBriefLifecycleTests.Reply()); }));
        });
        await WritingBriefGenerationTests.Premium(f);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        db.WritingGenerationLogs.AddRange(Enumerable.Range(0, 5).Select(_ => new WritingGenerationLog
        { Id = Guid.NewGuid(), UserId = TestDataIds.UserId, RequestType = "create-from-ai", IsSuccess = true, RequestedAtUtc = clock.Now.Date.AddDays(1) }));
        await db.SaveChangesAsync();
        var result = await WritingBriefLifecycleTests.Create(f, WritingBriefLifecycleTests.Request());
        Assert.Equal(WritingCreateFromAiOutcome.QuotaExceeded, result.Outcome);
        Assert.False(await db.WritingExercises.AnyAsync(e => e.UserId == TestDataIds.UserId));
        Assert.False((await db.WritingGenerationOperations.AsNoTracking().SingleAsync()).Reserved);
        db.WritingGenerationOperations.Add(new WritingGenerationOperation { Id = Guid.NewGuid(), UserId = TestDataIds.UserId,
            IdempotencyKey = "expired-offer", PayloadHash = "synthetic", State = "awaiting_template", InputMode = "from_brief",
            CandidateKeys = "leave-request", Level = "beginner", ContentType = "emails", Length = "short", Tone = "formal",
            CreatedAtUtc = clock.Now.AddDays(-2), OfferExpiresAtUtc = clock.Now.AddSeconds(-1), QuotaDayUtc = clock.Now.Date });
        await db.SaveChangesAsync();
        var expired = await db.WritingGenerationOperations.SingleAsync(o => o.IdempotencyKey == "expired-offer");
        var confirmation = await scope.ServiceProvider.GetRequiredService<IWritingService>()
            .ConfirmWritingTemplateAsync(TestDataIds.UserId, expired.Id, WritingBriefLifecycleTests.Choice());
        Assert.Equal("offer_expired", confirmation.ErrorCode); Assert.Empty(confirmation.Templates);
    }

    [Fact]
    public async Task ExpiredLeaseIsRecoveredWithoutRedispatchAndOldFenceCannotPublish()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = new GoalsStage03RegressionTests.TestClock(new DateTime(2026, 11, 15, 0, 0, 0));
        var calls = 0;
        await using var f = new TestWebApplicationFactory(s =>
        {
            GoalsStage03RegressionTests.Configure(s, clock); s.RemoveAll<IAiProviderClient>();
            s.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient(async (_, _, _) =>
            { Interlocked.Increment(ref calls); entered.SetResult(); await release.Task; return WritingBriefLifecycleTests.Reply(); }));
        });
        await WritingBriefGenerationTests.Premium(f);
        var running = WritingBriefLifecycleTests.Create(f, WritingBriefLifecycleTests.Request());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        clock.Now = clock.Now.AddSeconds(101);
        var recovered = await WritingBriefLifecycleTests.Create(f, WritingBriefLifecycleTests.Request());
        Assert.Equal(WritingCreateFromAiOutcome.TemplateOffer, recovered.Outcome);
        Assert.True(recovered.ProviderOutcomeUncertain);
        using var scope = f.Services.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<IWritingService>();
        var local = await service.ConfirmWritingTemplateAsync(TestDataIds.UserId, recovered.OperationId!.Value, WritingBriefLifecycleTests.Choice());
        Assert.Equal(WritingCreateFromAiOutcome.Success, local.Outcome);
        release.SetResult(); var late = await running;
        Assert.Equal(local.ExerciseId, late.ExerciseId); Assert.Equal("internal_template", late.GenerationMethod);
        Assert.Equal(1, calls);
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(1, await db.WritingGenerationLogs.CountAsync(l => l.IsSuccess));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualAccountRemovalOrAnonymizationDeletesReceiptsAndPrivateContent(bool keepLedger)
    {
        await using var f = WritingBriefGenerationTests.Factory((_, _) => Task.FromResult(WritingBriefLifecycleTests.Reply()));
        await WritingBriefGenerationTests.Premium(f);
        await AssertCleanup(f, keepLedger);
    }

    internal static async Task AssertCleanup(TestWebApplicationFactory f, bool keepLedger)
    {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var user = new User { Email = Guid.NewGuid() + "@example.invalid", Role = Roles.Premium, PasswordHash = BCrypt.Net.BCrypt.HashPassword("BriefFixture1!") };
        db.Users.Add(user); await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<IWritingService>();
        var op = await service.CreateFromAiAsync(WritingBriefLifecycleTests.Request(), user.UserId);
        Assert.Equal(WritingCreateFromAiOutcome.Success, op.Outcome);
        if (keepLedger) db.PaymentOrders.Add(new PaymentOrder { UserId = user.UserId,
            Plan = new PremiumPlan { Code = Guid.NewGuid().ToString("N"), Name = "Fixture", Description = "Fixture", DurationDays = 1 },
            OrderCode = Guid.NewGuid().ToString("N"), Provider = "fixture", Status = "Paid",
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var deletion = await scope.ServiceProvider.GetRequiredService<IAccountDeletionService>().DeleteAsync(user.UserId, "CONFIRM", "BriefFixture1!", null);
        Assert.True(deletion.Succeeded, deletion.Error); Assert.Equal(!keepLedger, deletion.Removed);
        Assert.False(await db.WritingGenerationOperations.AnyAsync(o => o.UserId == user.UserId));
        Assert.False(await db.WritingExercises.AnyAsync(e => e.UserId == user.UserId));
        Assert.Null(await service.GetWritingGenerationAsync(user.UserId, op.OperationId!.Value));
    }
}
