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

    /// <summary>Open a leased stage. False means this turn no longer owns the row.</summary>
    Task<bool> BeginStageAsync(AiQuotaReservation reservation, AiQuotaTurnStage stage, TimeSpan lease, int unknownTokenBound, CancellationToken ct)
        => Task.FromResult(true);

    Task<bool> RecordStageSpendAsync(AiQuotaReservation reservation, AiQuotaTurnStage stage, int knownTokens, CancellationToken ct)
        => Task.FromResult(true);

    Task<bool> RecordStageUsageAsync(AiQuotaReservation reservation, AiQuotaTurnStage stage, int knownTokens, int unknownTokens, CancellationToken ct)
        => Task.FromResult(true);

    Task<bool> RecordStageUnknownAsync(AiQuotaReservation reservation, AiQuotaTurnStage stage, int unknownTokenBound, CancellationToken ct)
        => Task.FromResult(true);

    Task<bool> ClearStageUnknownAsync(AiQuotaReservation reservation, AiQuotaTurnStage stage, CancellationToken ct)
        => Task.FromResult(true);

    /// <summary>True only while the row is pending and its lease has not expired.</summary>
    Task<bool> OwnsTurnAsync(AiQuotaReservation reservation, CancellationToken ct)
        => Task.FromResult(true);
}

public sealed record AiQuotaReservation(Guid RequestId, int UserId, DateTime DayUtc, int ReservedTokens);

public sealed class AiDailyQuotaReservationService : IAiDailyQuotaReservationService
{
    private const int MaxTransientAttempts = 8;
    private readonly DbflashcardContext? _directContext;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly TimeProvider _clock;

    /// <summary>
    /// DI path: each quota transaction uses an isolated scoped DbContext so SaveChanges
    /// cannot flush the caller's pending AiMessage / AiRequestLog entries.
    /// </summary>
    public AiDailyQuotaReservationService(IServiceScopeFactory scopeFactory, TimeProvider? clock = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Direct context for focused MariaDB / unit fixtures that own a single DbContext.</summary>
    public static AiDailyQuotaReservationService ForDbContext(DbflashcardContext context, TimeProvider? clock = null)
        => new(context, clock);

    private AiDailyQuotaReservationService(DbflashcardContext context, TimeProvider? clock)
    {
        _directContext = context ?? throw new ArgumentNullException(nameof(context));
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<AiQuotaReservation> ReserveQuestionAsync(
        int userId,
        int plannedTokens,
        int questionLimit,
        int tokenBudget,
        CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var dayUtc = now.Date;
        var reservedTokens = Math.Max(plannedTokens, 0);
        var requestId = Guid.NewGuid();
        // Recover only rows whose stage lease has ended or that were explicitly yielded.
        // A live embedding/generation lease must survive another conversation's reserve.
        var pending = await UsingContextAsync(context => context.AiQuotaReservationLedgers.AsNoTracking()
            .Where(l => l.UserId == userId && l.Status == AiQuotaReservationStatus.Pending)
            .OrderBy(l => l.CreatedAtUtc).Take(32).ToListAsync(ct), ct);
        foreach (var row in pending)
        {
            if (AiTurnLifecycle.MayRecover(AiTurnLedgerSnapshot.FromLedger(row), now))
            {
                await RecoverKnownPendingAsync(row.RequestId, ct);
            }
        }

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
                        ActiveStage = (byte)AiQuotaTurnStage.Admitted,
                        LeaseExpiresAtUtc = now.Add(AiTurnLifecycle.StageLease),
                        CreatedAtUtc = now
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

                    // Yield the live lease. Another turn may recover this row; an open stage may not.
                    ledger.ActualTokens = tokens;
                    ledger.ActiveStage = (byte)AiQuotaTurnStage.AwaitingRecovery;
                    ledger.TerminalIntent = (byte)AiQuotaTerminalIntent.Settle;
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
                    if (ledger is null || ledger.Status != AiQuotaReservationStatus.Pending)
                    {
                        await tx.CommitAsync(ct);
                        return false;
                    }

                    var snapshot = AiTurnLedgerSnapshot.FromLedger(ledger);
                    if (!AiTurnLifecycle.MayRecover(snapshot, _clock.GetUtcNow().UtcDateTime))
                    {
                        await tx.CommitAsync(ct);
                        return false;
                    }

                    var reservation = new AiQuotaReservation(ledger.RequestId, ledger.UserId, ledger.DayUtc, ledger.ReservedTokens);
                    var terminal = AiTurnLifecycle.RecoveryTerminal(snapshot);
                    var charge = terminal == AiQuotaReservationStatus.Settled
                        ? AiTurnLifecycle.RecoveryCharge(snapshot)
                        : 0;
                    await ApplyTerminalStateAsync(context, reservation, terminal, charge, ct);
                    await tx.CommitAsync(ct);
                    return true;
                },
                context,
                ct),
            ct);
    }

    public Task<bool> OwnsTurnAsync(AiQuotaReservation reservation, CancellationToken ct)
        => UsingContextAsync(async context =>
        {
            var ledger = await context.AiQuotaReservationLedgers.AsNoTracking()
                .FirstOrDefaultAsync(row => row.RequestId == reservation.RequestId, ct);
            if (ledger is null || ledger.Status != AiQuotaReservationStatus.Pending || ledger.UserId != reservation.UserId)
            {
                return false;
            }

            return ledger.LeaseExpiresAtUtc is not DateTime lease || lease > _clock.GetUtcNow().UtcDateTime;
        }, ct);

    public Task<bool> BeginStageAsync(
        AiQuotaReservation reservation,
        AiQuotaTurnStage stage,
        TimeSpan lease,
        int unknownTokenBound,
        CancellationToken ct)
        => MutatePendingAsync(reservation, ct, ledger =>
        {
            var bound = Math.Max(0, unknownTokenBound);
            ledger.ActiveStage = (byte)stage;
            ledger.LeaseExpiresAtUtc = _clock.GetUtcNow().UtcDateTime.Add(lease <= TimeSpan.Zero ? AiTurnLifecycle.StageLease : lease);
            if (stage is AiQuotaTurnStage.Embedding or AiQuotaTurnStage.EmbeddingKnown)
            {
                ledger.UnknownEmbeddingTokens = bound;
            }
            else if (stage is AiQuotaTurnStage.Generating or AiQuotaTurnStage.Validating)
            {
                ledger.UnknownGenerationTokens = bound;
            }

            return Task.CompletedTask;
        });

    public Task<bool> RecordStageSpendAsync(
        AiQuotaReservation reservation,
        AiQuotaTurnStage stage,
        int knownTokens,
        CancellationToken ct)
        => RecordStageUsageAsync(reservation, stage, knownTokens, 0, ct);

    public Task<bool> RecordStageUsageAsync(
        AiQuotaReservation reservation,
        AiQuotaTurnStage stage,
        int knownTokens,
        int unknownTokens,
        CancellationToken ct)
        => MutatePendingAsync(reservation, ct, ledger =>
        {
            var known = Math.Max(0, knownTokens);
            var unknown = Math.Max(0, unknownTokens);
            ledger.LeaseExpiresAtUtc = _clock.GetUtcNow().UtcDateTime.Add(AiTurnLifecycle.StageLease);
            if (stage is AiQuotaTurnStage.Embedding or AiQuotaTurnStage.EmbeddingKnown)
            {
                ledger.KnownEmbeddingTokens = known;
                ledger.UnknownEmbeddingTokens = unknown;
                ledger.ActiveStage = (byte)AiQuotaTurnStage.EmbeddingKnown;
            }
            else
            {
                ledger.KnownGenerationTokens = known;
                ledger.UnknownGenerationTokens = unknown;
                ledger.ActiveStage = (byte)AiQuotaTurnStage.Validating;
            }

            // ActualTokens stays 0 while the stage lease is live so a legacy
            // ActualTokens>0 scan cannot settle this turn early.
            return Task.CompletedTask;
        });

    public Task<bool> RecordStageUnknownAsync(
        AiQuotaReservation reservation,
        AiQuotaTurnStage stage,
        int unknownTokenBound,
        CancellationToken ct)
        => MutatePendingAsync(reservation, ct, ledger =>
        {
            var bound = Math.Max(0, unknownTokenBound);
            ledger.LeaseExpiresAtUtc = _clock.GetUtcNow().UtcDateTime.Add(AiTurnLifecycle.StageLease);
            if (stage is AiQuotaTurnStage.Embedding or AiQuotaTurnStage.EmbeddingKnown)
            {
                ledger.UnknownEmbeddingTokens = bound;
                ledger.ActiveStage = (byte)AiQuotaTurnStage.Embedding;
            }
            else
            {
                ledger.UnknownGenerationTokens = bound;
                ledger.ActiveStage = (byte)AiQuotaTurnStage.Generating;
            }

            return Task.CompletedTask;
        });

    public Task<bool> ClearStageUnknownAsync(AiQuotaReservation reservation, AiQuotaTurnStage stage, CancellationToken ct)
        => MutatePendingAsync(reservation, ct, ledger =>
        {
            if (stage is AiQuotaTurnStage.Embedding or AiQuotaTurnStage.EmbeddingKnown)
            {
                ledger.UnknownEmbeddingTokens = 0;
                if ((ledger.KnownEmbeddingTokens ?? 0) == 0)
                {
                    ledger.ActiveStage = (byte)AiQuotaTurnStage.Admitted;
                }
            }
            else
            {
                ledger.UnknownGenerationTokens = 0;
            }

            ledger.LeaseExpiresAtUtc = _clock.GetUtcNow().UtcDateTime.Add(AiTurnLifecycle.StageLease);
            return Task.CompletedTask;
        });

    private Task<bool> MutatePendingAsync(
        AiQuotaReservation reservation,
        CancellationToken ct,
        Func<AiQuotaReservationLedger, Task> mutate)
        => UsingContextAsync(
            context => ExecuteWithTransientRetryAsync(
                async () =>
                {
                    await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                    DetachQuotaEntitiesOnly(context);
                    var ledger = await context.AiQuotaReservationLedgers
                        .FirstOrDefaultAsync(x => x.RequestId == reservation.RequestId, ct);
                    if (ledger is null || ledger.Status != AiQuotaReservationStatus.Pending)
                    {
                        await tx.CommitAsync(ct);
                        return false;
                    }

                    if (ledger.LeaseExpiresAtUtc is DateTime lease && lease <= _clock.GetUtcNow().UtcDateTime)
                    {
                        await tx.CommitAsync(ct);
                        return false;
                    }

                    if (ledger.UserId != reservation.UserId || ledger.DayUtc != reservation.DayUtc)
                    {
                        throw new InvalidOperationException("Quota reservation identity mismatch.");
                    }

                    await mutate(ledger);
                    await SaveQuotaChangesAsync(context, ct);
                    await tx.CommitAsync(ct);
                    return true;
                },
                context,
                ct),
            ct);

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

        var snapshot = AiTurnLedgerSnapshot.FromLedger(ledger);
        if (terminalStatus == AiQuotaReservationStatus.Released && AiTurnLifecycle.RecoveryCharge(snapshot) > 0)
        {
            terminalStatus = AiQuotaReservationStatus.Settled;
            actualTokens = AiTurnLifecycle.RecoveryCharge(snapshot);
        }

        if (terminalStatus == AiQuotaReservationStatus.Settled)
        {
            actualTokens = AiTurnLifecycle.SettlementTokens(snapshot, actualTokens);
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
