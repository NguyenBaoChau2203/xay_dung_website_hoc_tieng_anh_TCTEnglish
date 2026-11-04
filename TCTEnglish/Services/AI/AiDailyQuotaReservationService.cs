using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI;

public interface IAiDailyQuotaReservationService
{
    Task<AiQuotaReservation> ReserveQuestionAsync(int userId, int plannedTokens, int questionLimit, int tokenBudget, CancellationToken ct);
    Task SettleAsync(AiQuotaReservation reservation, int actualTokens, CancellationToken ct);
    Task ReleaseAsync(AiQuotaReservation reservation, CancellationToken ct);
    /// <summary>Record known provider usage on a still-Pending ledger without releasing counters.</summary>
    Task MarkKnownUsagePendingAsync(AiQuotaReservation reservation, int actualTokens, CancellationToken ct);
    /// <summary>Settle a Pending ledger that already has known ActualTokens (restart/reconcile).</summary>
    Task<bool> RecoverKnownPendingAsync(Guid requestId, CancellationToken ct);
}

public sealed record AiQuotaReservation(Guid RequestId, int UserId, DateTime DayUtc, int ReservedTokens);

public sealed class AiDailyQuotaReservationService : IAiDailyQuotaReservationService
{
    private const int MaxTransientAttempts = 8;
    private readonly DbflashcardContext? _directContext;
    private readonly IServiceScopeFactory? _scopeFactory;

    /// <summary>
    /// DI path: each quota transaction uses an isolated scoped DbContext so SaveChanges
    /// cannot flush the caller's pending AiMessage / AiRequestLog entries.
    /// </summary>
    public AiDailyQuotaReservationService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <summary>Direct context for focused MariaDB / unit fixtures that own a single DbContext.</summary>
    public static AiDailyQuotaReservationService ForDbContext(DbflashcardContext context)
        => new(context);

    private AiDailyQuotaReservationService(DbflashcardContext context)
    {
        _directContext = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AiQuotaReservation> ReserveQuestionAsync(
        int userId,
        int plannedTokens,
        int questionLimit,
        int tokenBudget,
        CancellationToken ct)
    {
        var dayUtc = DateTime.UtcNow.Date;
        var reservedTokens = Math.Max(plannedTokens, 0);
        var requestId = Guid.NewGuid();
        // A new scope/process recovers durable known usage before admitting another turn.
        var knownPending = await UsingContextAsync(context => context.AiQuotaReservationLedgers.AsNoTracking()
            .Where(l => l.UserId == userId && l.Status == AiQuotaReservationStatus.Pending && l.ActualTokens > 0)
            .OrderBy(l => l.CreatedAtUtc).Select(l => l.RequestId).Take(32).ToListAsync(ct), ct);
        foreach (var pendingId in knownPending)
            await RecoverKnownPendingAsync(pendingId, ct);

        return await UsingContextAsync(
            context => ExecuteWithTransientRetryAsync(
                async () =>
                {
                    await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                    DetachQuotaEntitiesOnly(context);

                    var existingLedger = await context.AiQuotaReservationLedgers
                        .FirstOrDefaultAsync(x => x.RequestId == requestId, ct);
                    if (existingLedger is not null)
                    {
                        await tx.CommitAsync(ct);
                        return new AiQuotaReservation(
                            existingLedger.RequestId,
                            existingLedger.UserId,
                            existingLedger.DayUtc,
                            existingLedger.ReservedTokens);
                    }

                    var row = await EnsureQuotaRowWithHistoricalBackfillAsync(context, userId, dayUtc, ct);

                    var usedQuestions = row.PendingQuestions + row.SettledQuestions;
                    if (questionLimit > 0 && usedQuestions >= questionLimit)
                    {
                        await SaveQuotaChangesAsync(context, ct);
                        await tx.CommitAsync(ct);
                        throw new AiRateLimitException(
                            "Bạn đã vượt quá giới hạn 15 câu hỏi AI mỗi ngày của gói Standard.",
                            "daily_question_limit_exceeded");
                    }

                    var usedTokens = row.PendingTokens + row.SettledTokens;
                    if (tokenBudget > 0 && usedTokens + reservedTokens > tokenBudget)
                    {
                        await SaveQuotaChangesAsync(context, ct);
                        await tx.CommitAsync(ct);
                        throw new AiRateLimitException(
                            "Bạn đã đạt giới hạn sử dụng AI trong ngày.",
                            "daily_token_budget_exceeded");
                    }

                    context.AiQuotaReservationLedgers.Add(new AiQuotaReservationLedger
                    {
                        RequestId = requestId,
                        UserId = userId,
                        DayUtc = dayUtc,
                        Status = AiQuotaReservationStatus.Pending,
                        ReservedTokens = reservedTokens,
                        ActualTokens = 0,
                        CreatedAtUtc = DateTime.UtcNow
                    });

                    row.PendingQuestions += 1;
                    row.PendingTokens += reservedTokens;
                    row.UpdatedAtUtc = DateTime.UtcNow;

                    await SaveQuotaChangesAsync(context, ct);
                    await tx.CommitAsync(ct);

                    return new AiQuotaReservation(requestId, userId, dayUtc, reservedTokens);
                },
                context,
                ct),
            ct);
    }

    public Task SettleAsync(AiQuotaReservation reservation, int actualTokens, CancellationToken ct)
        => FinalizeAsync(reservation, AiQuotaReservationStatus.Settled, Math.Max(actualTokens, 0), ct);

    public Task ReleaseAsync(AiQuotaReservation reservation, CancellationToken ct)
        => FinalizeAsync(reservation, AiQuotaReservationStatus.Released, actualTokens: 0, ct);

    public Task MarkKnownUsagePendingAsync(AiQuotaReservation reservation, int actualTokens, CancellationToken ct)
    {
        var tokens = Math.Max(actualTokens, 0);
        return UsingContextAsync(
            context => ExecuteWithTransientRetryAsync(
                async () =>
                {
                    await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                    DetachQuotaEntitiesOnly(context);

                    var ledger = await context.AiQuotaReservationLedgers
                        .FirstOrDefaultAsync(x => x.RequestId == reservation.RequestId, ct);
                    if (ledger is null)
                    {
                        await tx.CommitAsync(ct);
                        return true;
                    }

                    if (ledger.UserId != reservation.UserId || ledger.DayUtc != reservation.DayUtc)
                    {
                        throw new InvalidOperationException("Quota reservation identity mismatch.");
                    }

                    if (ledger.Status != AiQuotaReservationStatus.Pending)
                    {
                        await tx.CommitAsync(ct);
                        return true;
                    }

                    // Keep Pending (do not release counters); store known usage for reconcile.
                    ledger.ActualTokens = tokens;
                    await SaveQuotaChangesAsync(context, ct);
                    await tx.CommitAsync(ct);
                    return true;
                },
                context,
                ct),
            ct);
    }

    public Task<bool> RecoverKnownPendingAsync(Guid requestId, CancellationToken ct)
    {
        return UsingContextAsync(
            context => ExecuteWithTransientRetryAsync(
                async () =>
                {
                    await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                    DetachQuotaEntitiesOnly(context);

                    var ledger = await context.AiQuotaReservationLedgers
                        .FirstOrDefaultAsync(x => x.RequestId == requestId, ct);
                    if (ledger is null || ledger.Status != AiQuotaReservationStatus.Pending || ledger.ActualTokens <= 0)
                    {
                        await tx.CommitAsync(ct);
                        return false;
                    }

                    var reservation = new AiQuotaReservation(ledger.RequestId, ledger.UserId, ledger.DayUtc, ledger.ReservedTokens);
                    await ApplyTerminalStateAsync(context, reservation, AiQuotaReservationStatus.Settled, ledger.ActualTokens, ct);
                    await tx.CommitAsync(ct);
                    return true;
                },
                context,
                ct),
            ct);
    }

    private Task FinalizeAsync(
        AiQuotaReservation reservation,
        AiQuotaReservationStatus terminalStatus,
        int actualTokens,
        CancellationToken ct)
    {
        if (terminalStatus is not (AiQuotaReservationStatus.Settled or AiQuotaReservationStatus.Released))
        {
            throw new ArgumentOutOfRangeException(nameof(terminalStatus));
        }

        return UsingContextAsync(
            context => ExecuteWithTransientRetryAsync(
                async () =>
                {
                    await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                    await ApplyTerminalStateAsync(context, reservation, terminalStatus, actualTokens, ct);
                    await tx.CommitAsync(ct);
                    return true;
                },
                context,
                ct),
            ct);
    }

    private static async Task ApplyTerminalStateAsync(
        DbflashcardContext context,
        AiQuotaReservation reservation,
        AiQuotaReservationStatus terminalStatus,
        int actualTokens,
        CancellationToken ct)
    {
        DetachQuotaEntitiesOnly(context);

        var ledger = await context.AiQuotaReservationLedgers
            .FirstOrDefaultAsync(x => x.RequestId == reservation.RequestId, ct);

        if (ledger is null)
        {
            return;
        }

        if (ledger.UserId != reservation.UserId || ledger.DayUtc != reservation.DayUtc)
        {
            throw new InvalidOperationException("Quota reservation identity mismatch.");
        }

        if (ledger.Status != AiQuotaReservationStatus.Pending)
        {
            return;
        }

        var row = await context.AiUserDailyQuotas
            .FirstOrDefaultAsync(x => x.UserId == reservation.UserId && x.DayUtc == reservation.DayUtc, ct);
        if (row is null)
        {
            row = new AiUserDailyQuota
            {
                UserId = reservation.UserId,
                DayUtc = reservation.DayUtc,
                PendingQuestions = 0,
                SettledQuestions = 0,
                PendingTokens = 0,
                SettledTokens = 0,
                UpdatedAtUtc = DateTime.UtcNow
            };
            context.AiUserDailyQuotas.Add(row);
        }

        row.PendingQuestions = Math.Max(0, row.PendingQuestions - 1);
        row.PendingTokens = Math.Max(0, row.PendingTokens - ledger.ReservedTokens);

        if (terminalStatus == AiQuotaReservationStatus.Settled)
        {
            row.SettledQuestions += 1;
            row.SettledTokens += actualTokens;
            ledger.ActualTokens = actualTokens;
        }

        row.UpdatedAtUtc = DateTime.UtcNow;
        ledger.Status = terminalStatus;
        ledger.FinalizedAtUtc = DateTime.UtcNow;

        await SaveQuotaChangesAsync(context, ct);
    }

    private static async Task<AiUserDailyQuota> EnsureQuotaRowWithHistoricalBackfillAsync(
        DbflashcardContext context,
        int userId,
        DateTime dayUtc,
        CancellationToken ct)
    {
        var row = await context.AiUserDailyQuotas
            .FirstOrDefaultAsync(x => x.UserId == userId && x.DayUtc == dayUtc, ct);

        if (row is not null)
        {
            return row;
        }

        var historicalQuestions = await context.AiRequestLogs
            .AsNoTracking()
            .CountAsync(
                x => x.UserId == userId
                    && x.IsSuccess
                    && x.RequestedAtUtc >= dayUtc
                    && x.RequestedAtUtc < dayUtc.AddDays(1),
                ct);

        var historicalTokens = await context.AiRequestLogs
            .AsNoTracking()
            .Where(x => x.UserId == userId
                && x.IsSuccess
                && x.RequestedAtUtc >= dayUtc
                && x.RequestedAtUtc < dayUtc.AddDays(1))
            .SumAsync(x => x.TotalTokens ?? 0, ct);

        row = new AiUserDailyQuota
        {
            UserId = userId,
            DayUtc = dayUtc,
            PendingQuestions = 0,
            SettledQuestions = historicalQuestions,
            PendingTokens = 0,
            SettledTokens = historicalTokens,
            UpdatedAtUtc = DateTime.UtcNow
        };
        context.AiUserDailyQuotas.Add(row);
        return row;
    }

    /// <summary>
    /// Drop only quota aggregates/ledgers so Serializable reloads see DB truth without
    /// detaching caller entities (conversation, messages) on a shared direct context.
    /// </summary>
    private static void DetachQuotaEntitiesOnly(DbflashcardContext context)
    {
        var quotaEntries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is AiUserDailyQuota or AiQuotaReservationLedger)
            .ToList();

        foreach (var entry in quotaEntries)
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Persist quota rows only. When sharing a direct DbContext with the caller, temporarily
    /// detach non-quota dirty entries so SaveChanges cannot flush assistant/success-log side effects.
    /// </summary>
    private static async Task SaveQuotaChangesAsync(DbflashcardContext context, CancellationToken ct)
    {
        var suspended = context.ChangeTracker.Entries()
            .Where(e => e.Entity is not AiUserDailyQuota and not AiQuotaReservationLedger
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => (Entity: e.Entity, e.State))
            .ToList();

        foreach (var item in suspended)
        {
            context.Entry(item.Entity).State = EntityState.Detached;
        }

        try
        {
            await context.SaveChangesAsync(ct);
        }
        finally
        {
            foreach (var item in suspended)
            {
                context.Entry(item.Entity).State = item.State;
            }
        }
    }

    private async Task<T> UsingContextAsync<T>(Func<DbflashcardContext, Task<T>> action, CancellationToken ct)
    {
        if (_directContext is not null)
        {
            return await action(_directContext);
        }

        await using var scope = _scopeFactory!.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        return await action(context);
    }

    private static async Task<T> ExecuteWithTransientRetryAsync<T>(
        Func<Task<T>> action,
        DbflashcardContext context,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxTransientAttempts; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt < MaxTransientAttempts && IsTransientConcurrencyFailure(ex))
            {
                DetachQuotaEntitiesOnly(context);
                var delayMs = Math.Min(200, 25 * attempt * attempt);
                await Task.Delay(delayMs, ct);
            }
        }

        throw new InvalidOperationException("Quota transaction retry exhausted.");
    }

    private static bool IsTransientConcurrencyFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException mysql
                && mysql.ErrorCode is MySqlErrorCode.LockDeadlock
                    or MySqlErrorCode.LockWaitTimeout
                    or MySqlErrorCode.DuplicateKeyEntry)
            {
                return true;
            }

            if (current is MySqlException mysqlNum
                && mysqlNum.Number is 1213 or 1205 or 1062)
            {
                return true;
            }

            var message = current.Message ?? string.Empty;
            if (message.Contains("Deadlock found when trying to get lock", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Lock wait timeout exceeded", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
