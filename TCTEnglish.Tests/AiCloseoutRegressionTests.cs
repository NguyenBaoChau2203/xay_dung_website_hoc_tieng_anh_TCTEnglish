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

namespace TCTEnglish.Tests;

public sealed class AiCloseoutRegressionTests
{
    [Fact]
    public async Task KnownReply_SaveFailure_KeepsRecoverableUsageWithoutOrphanReply()
    {
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiProviderClient>();
            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, _, _) =>
                Task.FromResult(new AiProviderReply("known", 10, 10, 20, "fake", "save-failure"))));
        });
        factory.SaveInterceptors.Add(new FailReplySaveOnce());
        await factory.InitializeAsync();
        using var run = factory.Services.CreateScope();
        var error = await Assert.ThrowsAsync<AiProviderException>(() => run.ServiceProvider
            .GetRequiredService<IAiChatService>().SendAsync(TestDataIds.UserId, null, "Help", default));
        Assert.Equal("reply_persistence_failed", error.ErrorCode);
        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.False(await db.AiMessages.AnyAsync());
        Assert.False(await db.AiConversations.AnyAsync());
        Assert.False(await db.AiRequestLogs.AnyAsync(l => l.IsSuccess));
        var ledger = await db.AiQuotaReservationLedgers.AsNoTracking().SingleAsync();
        Assert.Equal(AiQuotaReservationStatus.Pending, ledger.Status);
        Assert.Equal(20, ledger.ActualTokens);
        Assert.True(await verify.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>().RecoverKnownPendingAsync(ledger.RequestId, default));
    }

    private sealed class FailReplySaveOnce : SaveChangesInterceptor
    {
        private bool failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!failed && data.Context!.ChangeTracker.Entries<AiMessage>().Any(e => e.State == EntityState.Added && e.Entity.Role == AiMessageRole.Assistant))
            {
                failed = true;
                throw new DbUpdateException("Synthetic reply persistence failure.");
            }
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task NextReservation_RecoversKnownUsageFromPreviousScope()
    {
        await using var factory = new TestWebApplicationFactory(); await factory.InitializeAsync();
        Guid previous;
        using (var first = factory.Services.CreateScope())
        {
            var quota = first.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>();
            var reservation = await quota.ReserveQuestionAsync(TestDataIds.UserId, 100, 15, 60000, default);
            previous = reservation.RequestId;
            await quota.MarkKnownUsagePendingAsync(reservation, 20, default);
        }
        using var next = factory.Services.CreateScope();
        await next.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>().ReserveQuestionAsync(TestDataIds.UserId, 100, 15, 60000, default);
        using var verify = factory.Services.CreateScope(); var db = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        Assert.Equal(AiQuotaReservationStatus.Settled, (await db.AiQuotaReservationLedgers.AsNoTracking().SingleAsync(l => l.RequestId == previous)).Status);
        var row = await db.AiUserDailyQuotas.AsNoTracking().SingleAsync();
        Assert.Equal(1, row.SettledQuestions); Assert.Equal(20, row.SettledTokens); Assert.Equal(1, row.PendingQuestions);
    }

    [Fact]
    public async Task Context_UsesTokenBudgetInsteadOfSilentlyCappingAtHundredMessages()
    {
        IReadOnlyList<AiContextMessage>? sent = null;
        await using var factory = new TestWebApplicationFactory(services =>
        {
            services.RemoveAll<IAiProviderClient>();
            services.AddScoped<IAiProviderClient>(_ => new StubAiProviderClient((_, messages, _) =>
            { sent = messages.ToArray(); return Task.FromResult(new AiProviderReply("ok", 20, 10, 30, "fake", "context")); }));
            services.PostConfigure<AiOptions>(o => { o.MaxOutputTokens = 600; o.RequestTokenBudget = 4600; o.DailyTokenBudgetPerUser = 60000; });
        });
        await factory.InitializeAsync();
        var id = Guid.NewGuid();
        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            db.AiConversations.Add(new() { Id = id, UserId = TestDataIds.UserId, Title = "context", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
            for (int i = 0; i < 200; i++) db.AiMessages.Add(new() { Id = Guid.NewGuid(), ConversationId = id, Role = i % 2 == 0 ? AiMessageRole.User : AiMessageRole.Assistant, Content = "Hi", CreatedAtUtc = DateTime.UtcNow.AddSeconds(i) });
            await db.SaveChangesAsync();
        }
        using var run = factory.Services.CreateScope();
        await run.ServiceProvider.GetRequiredService<IAiChatService>().SendAsync(TestDataIds.UserId, id, "Help", default);
        Assert.NotNull(sent); Assert.Equal(202, sent.Count);
    }
}
