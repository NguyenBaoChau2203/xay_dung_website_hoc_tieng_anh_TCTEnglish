using TCTEnglish.Models;

namespace TCTEnglish.Services.AI;

/// <summary>
/// Durable stage of one reserved chat turn. Null on the ledger means a legacy row.
/// </summary>
public enum AiQuotaTurnStage : byte
{
    Admitted = 1,
    Embedding = 2,
    EmbeddingKnown = 3,
    Generating = 4,
    Validating = 5,
    AwaitingRecovery = 6
}

public enum AiQuotaTerminalIntent : byte
{
    None = 0,
    Settle = 1,
    Release = 2
}

/// <summary>
/// Pure snapshot used by recovery. A live stage with an unexpired lease is owned by
/// the turn that opened it and must not be terminal-settled by another conversation.
/// </summary>
public sealed record AiTurnLedgerSnapshot(
    AiQuotaReservationStatus Status,
    AiQuotaTurnStage? Stage,
    DateTime? LeaseExpiresAtUtc,
    int KnownEmbeddingTokens,
    int KnownGenerationTokens,
    int UnknownEmbeddingTokens,
    int UnknownGenerationTokens,
    int ActualTokens,
    AiQuotaTerminalIntent TerminalIntent)
{
    public static AiTurnLedgerSnapshot FromLedger(AiQuotaReservationLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        return new AiTurnLedgerSnapshot(
            ledger.Status,
            ledger.ActiveStage is null ? null : (AiQuotaTurnStage)ledger.ActiveStage.Value,
            ledger.LeaseExpiresAtUtc,
            ledger.KnownEmbeddingTokens ?? 0,
            ledger.KnownGenerationTokens ?? 0,
            ledger.UnknownEmbeddingTokens ?? 0,
            ledger.UnknownGenerationTokens ?? 0,
            ledger.ActualTokens,
            ledger.TerminalIntent is null ? AiQuotaTerminalIntent.None : (AiQuotaTerminalIntent)ledger.TerminalIntent.Value);
    }
}

public static class AiTurnLifecycle
{
    public static readonly TimeSpan StageLease = TimeSpan.FromMinutes(3);

    public static bool IsLive(AiQuotaTurnStage? stage)
        => stage is AiQuotaTurnStage.Admitted
            or AiQuotaTurnStage.Embedding
            or AiQuotaTurnStage.EmbeddingKnown
            or AiQuotaTurnStage.Generating
            or AiQuotaTurnStage.Validating;

    /// <summary>
    /// True only when this pending row is no longer an active leased stage.
    /// Legacy rows with known ActualTokens and no stage remain recoverable.
    /// </summary>
    public static bool MayRecover(AiTurnLedgerSnapshot state, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Status != AiQuotaReservationStatus.Pending)
        {
            return false;
        }

        if (IsLive(state.Stage) && state.LeaseExpiresAtUtc is { } lease && lease > utcNow)
        {
            return false;
        }

        if (state.Stage == AiQuotaTurnStage.AwaitingRecovery)
        {
            return true;
        }

        if (state.LeaseExpiresAtUtc is { } expired && expired <= utcNow && state.Stage is not null)
        {
            return true;
        }

        return state.Stage is null && state.LeaseExpiresAtUtc is null && state.ActualTokens > 0;
    }

    public static int ConservativeCharge(AiTurnLedgerSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        long total = (long)NonNegative(state.KnownEmbeddingTokens)
            + NonNegative(state.KnownGenerationTokens)
            + NonNegative(state.UnknownEmbeddingTokens)
            + NonNegative(state.UnknownGenerationTokens);
        return total >= int.MaxValue ? int.MaxValue : (int)total;
    }

    /// <summary>
    /// Paid stages already on the ledger are the bill. A zero stage bill uses the
    /// caller amount so deterministic replies and legacy ActualTokens still settle.
    /// </summary>
    public static int SettlementTokens(AiTurnLedgerSnapshot state, int requestedActual)
    {
        var conservative = ConservativeCharge(state);
        if (conservative > 0)
        {
            return conservative;
        }

        return NonNegative(requestedActual);
    }

    public static int RecoveryCharge(AiTurnLedgerSnapshot state)
    {
        var conservative = ConservativeCharge(state);
        return conservative > 0 ? conservative : NonNegative(state.ActualTokens);
    }

    public static AiQuotaReservationStatus RecoveryTerminal(AiTurnLedgerSnapshot state)
        => RecoveryCharge(state) > 0 || state.TerminalIntent == AiQuotaTerminalIntent.Settle
            ? AiQuotaReservationStatus.Settled
            : AiQuotaReservationStatus.Released;

    private static int NonNegative(int value) => value < 0 ? 0 : value;
}
