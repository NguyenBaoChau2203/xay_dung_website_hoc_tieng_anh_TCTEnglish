using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCTEnglish.Models;
using TCTVocabulary.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Services.AI.Rag;
using Microsoft.Extensions.Options;
using System.Net;
using TCTEnglish.Tests.Infrastructure;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// Provider proof for the stage lease. SQLite does not approve this concurrency behavior.
/// </summary>
[Collection("MariaDbAcceptance")]
[Trait("Category", "MariaDbAcceptance")]
public sealed class MariaDbAiTurnLifecycleTests
{
    [MariaDbAcceptanceFact]
    public async Task LiveStage_SurvivesOtherScopeReserve_ThenExpiredRecoverySettlesKnownSpend()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")
            ?? throw new InvalidOperationException("TCT_ACCEPTANCE_MARIADB missing.");
        var runId = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");
        var clock = new MariaClock();
        await using var factory = new MariaDbAcceptanceFactory(connection, runId, services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        });
        await factory.InitializeAsync();

        Guid first;
        using (var scopeA = factory.Services.CreateScope())
        {
            var quota = scopeA.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>();
            var reservation = await quota.ReserveQuestionAsync(TestDataIds.UserId, 100, 15, 60000, default);
            first = reservation.RequestId;
            await quota.BeginStageAsync(reservation, AiQuotaTurnStage.Embedding, AiTurnLifecycle.StageLease, 40, default);
            await quota.RecordStageSpendAsync(reservation, AiQuotaTurnStage.Embedding, 17, default);
        }

        using (var scopeB = factory.Services.CreateScope())
        {
            await scopeB.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>()
                .ReserveQuestionAsync(TestDataIds.UserId, 80, 15, 60000, default);
        }

        using (var verify = factory.Services.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var live = await db.AiQuotaReservationLedgers.AsNoTracking().SingleAsync(row => row.RequestId == first);
            Assert.Equal(AiQuotaReservationStatus.Pending, live.Status);
            Assert.Equal(17, live.KnownEmbeddingTokens);
            Assert.Equal(0, live.ActualTokens);
        }

        clock.Utc = clock.Utc.AddMinutes(4);
        using var recovery = factory.Services.CreateScope();
        await recovery.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>()
            .ReserveQuestionAsync(TestDataIds.UserId, 10, 15, 60000, default);
        using var after = factory.Services.CreateScope();
        var afterDb = after.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var settled = await afterDb.AiQuotaReservationLedgers.AsNoTracking().SingleAsync(row => row.RequestId == first);
        Assert.Equal(AiQuotaReservationStatus.Settled, settled.Status);
        Assert.Equal(17, settled.ActualTokens);
        var counters = await afterDb.AiUserDailyQuotas.AsNoTracking().SingleAsync(row => row.UserId == TestDataIds.UserId);
        Assert.Equal(1, counters.SettledQuestions);
        Assert.Equal(17, counters.SettledTokens);
    }

    [MariaDbAcceptanceFact]
    public async Task ExpiredOwner_DoesNotAcceptALateGenerationReceipt()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")
            ?? throw new InvalidOperationException("TCT_ACCEPTANCE_MARIADB missing.");
        var runId = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");
        var clock = new MariaClock();
        await using var factory = new MariaDbAcceptanceFactory(connection, runId, services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        });
        await factory.InitializeAsync();

        AiQuotaReservation reservation;
        using (var scopeA = factory.Services.CreateScope())
        {
            var quota = scopeA.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>();
            reservation = await quota.ReserveQuestionAsync(TestDataIds.UserId, 40, 15, 60000, default);
            Assert.True(await quota.BeginStageAsync(reservation, AiQuotaTurnStage.Embedding, AiTurnLifecycle.StageLease, 22, default));
        }

        clock.Utc = clock.Utc.AddMinutes(4);
        using (var scopeB = factory.Services.CreateScope())
        {
            await scopeB.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>()
                .ReserveQuestionAsync(TestDataIds.UserId, 10, 15, 60000, default);
        }

        using var late = factory.Services.CreateScope();
        var lateQuota = late.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>();
        Assert.False(await lateQuota.OwnsTurnAsync(reservation, default));
        Assert.False(await lateQuota.RecordStageUsageAsync(reservation, AiQuotaTurnStage.Generating, 105, 0, default));
        var db = late.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var ledger = await db.AiQuotaReservationLedgers.AsNoTracking().SingleAsync(row => row.RequestId == reservation.RequestId);
        Assert.Equal(AiQuotaReservationStatus.Settled, ledger.Status);
        Assert.Null(ledger.KnownGenerationTokens);
        Assert.Equal(22, ledger.ActualTokens);
        var counters = await db.AiUserDailyQuotas.AsNoTracking().SingleAsync(row => row.UserId == TestDataIds.UserId);
        Assert.Equal(22, counters.SettledTokens);
        Assert.Equal(1, counters.SettledQuestions);
    }

    [MariaDbAcceptanceFact]
    public async Task CancelledGenerationReceipt_IsDurableAndSettledOnce()
    {
        var connection = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")!;
        var runId = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");
        await using var factory = new MariaDbAcceptanceFactory(connection, runId);
        await factory.InitializeAsync();
        using var scope = factory.Services.CreateScope();
        var quota = scope.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>();
        var reservation = await quota.ReserveQuestionAsync(TestDataIds.UserId, 2000, 15, 60000, default);
        var journal = new AiTurnStageJournal(quota, reservation);
        Assert.True(await journal.BeginAsync(AiQuotaTurnStage.Generating, 1500, default));
        using var caller = new CancellationTokenSource();
        var handler = new AiRagPhase4CloseoutTests.Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new AiRagPhase4CloseoutTests.EofContent(AiRagPhase4CloseoutTests.TextBody, caller) }));
        var selector = new OpenAiChatSentenceSelector(AiRagPhase4CloseoutTests.TextClient(handler), Options.Create(new AiOptions()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => selector.SelectAsync(new ChatSentenceSelectionRequest(
            TestDataIds.UserId, "what color is the bell", [], [], 64)
        {
            MaxInputUpper = 2000,
            ConfirmCurrent = journal.OwnsAsync,
            RecordUsage = (known, unknown, token) => journal.RecordUsageAsync(AiQuotaTurnStage.Generating, known, unknown, token)
        }, caller.Token));
        Assert.Equal(105, journal.Charge);
        await quota.SettleAsync(reservation, journal.Charge, default);
        await quota.SettleAsync(reservation, journal.Charge, default);
        using var verify = factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var ledger = await db.AiQuotaReservationLedgers.AsNoTracking().SingleAsync(x => x.RequestId == reservation.RequestId);
        Assert.Equal(105, ledger.KnownGenerationTokens);
        Assert.Equal(0, ledger.UnknownGenerationTokens);
        Assert.Equal(105, ledger.ActualTokens);
        var counters = await db.AiUserDailyQuotas.AsNoTracking().SingleAsync(x => x.UserId == TestDataIds.UserId);
        Assert.Equal(1, counters.SettledQuestions);
        Assert.Equal(105, counters.SettledTokens);
    }

    private sealed class MariaClock : TimeProvider
    {
        public DateTimeOffset Utc { get; set; } = new(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Utc;
    }
}
