using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTEnglish.Tests.Infrastructure;
using TCTVocabulary.Models;
using Xunit;

namespace TCTEnglish.Tests;

/// <summary>
/// R1/R2 MariaDB proof: overlapping DbContexts + historical backfill on disposable acceptance DB.
/// </summary>
[Collection("MariaDbAcceptance")]
[Trait("Category", "MariaDbAcceptance")]
public sealed class MariaDbAiQuotaConcurrencyTests
{
    [MariaDbAcceptanceFact]
    public async Task OverlappingScopes_SettleBoth_OnMariaDb()
    {
        var connectionString = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")
            ?? throw new InvalidOperationException("TCT_ACCEPTANCE_MARIADB missing.");
        var runId = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");

        await using var factory = new MariaDbAcceptanceFactory(connectionString, runId);
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
        var db = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var row = await db.AiUserDailyQuotas.AsNoTracking().SingleAsync(x => x.UserId == TestDataIds.UserId);
        Assert.Equal(2, row.SettledQuestions);
        Assert.Equal(0, row.PendingQuestions);
        Assert.Equal(40, row.SettledTokens);
        Assert.Equal(0, row.PendingTokens);

        var ledgers = await db.AiQuotaReservationLedgers.AsNoTracking()
            .Where(x => x.UserId == TestDataIds.UserId)
            .ToListAsync();
        Assert.Equal(2, ledgers.Count);
        Assert.All(ledgers, x => Assert.Equal(AiQuotaReservationStatus.Settled, x.Status));
    }

    [MariaDbAcceptanceFact]
    public async Task HistoricalSuccessLogs_BlockSixteenth_OnMariaDb()
    {
        var connectionString = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_MARIADB")
            ?? throw new InvalidOperationException("TCT_ACCEPTANCE_MARIADB missing.");
        var runId = Environment.GetEnvironmentVariable("TCT_ACCEPTANCE_RUN_ID");

        await using var factory = new MariaDbAcceptanceFactory(connectionString, runId);
        await factory.InitializeAsync();

        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var conversation = new AiConversation
            {
                Id = Guid.NewGuid(),
                UserId = TestDataIds.UserId,
                Title = "historical-mariadb",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.AiConversations.Add(conversation);
            for (var i = 0; i < 15; i++)
            {
                db.AiRequestLogs.Add(new AiRequestLog
                {
                    Id = Guid.NewGuid(),
                    UserId = TestDataIds.UserId,
                    ConversationId = conversation.Id,
                    IsSuccess = true,
                    RequestedAtUtc = DateTime.UtcNow,
                    TotalTokens = 20
                });
            }

            await db.SaveChangesAsync();
        }

        using var scope = factory.Services.CreateScope();
        var quota = scope.ServiceProvider.GetRequiredService<IAiDailyQuotaReservationService>();
        var error = await Record.ExceptionAsync(() =>
            quota.ReserveQuestionAsync(TestDataIds.UserId, 10, 15, 60000, default));

        Assert.IsType<AiRateLimitException>(error);

        using var verify = factory.Services.CreateScope();
        var assertDb = verify.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var row = await assertDb.AiUserDailyQuotas.AsNoTracking()
            .SingleAsync(x => x.UserId == TestDataIds.UserId);
        Assert.Equal(15, row.SettledQuestions);
        Assert.Equal(0, row.PendingQuestions);
    }
}
