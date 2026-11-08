namespace TCTEnglish.Services.AI;

public interface IAiTurnStageJournal
{
    bool HasCharge { get; }

    int Charge { get; }

    Task<bool> BeginAsync(AiQuotaTurnStage stage, int unknownBound, CancellationToken ct);

    Task<bool> RecordKnownAsync(AiQuotaTurnStage stage, int knownTokens, CancellationToken ct);

    Task<bool> RecordUsageAsync(AiQuotaTurnStage stage, int knownTokens, int unknownTokens, CancellationToken ct);

    Task<bool> RecordUnknownAsync(AiQuotaTurnStage stage, int unknownBound, CancellationToken ct);

    Task<bool> ClearUnknownAsync(AiQuotaTurnStage stage, CancellationToken ct);

    Task<bool> OwnsAsync(CancellationToken ct);
}

public sealed class AiTurnStageJournal : IAiTurnStageJournal
{
    private readonly IAiDailyQuotaReservationService _quota;
    private readonly AiQuotaReservation _reservation;
    private int _embeddingKnown;
    private int _embeddingUnknown;
    private int _generationKnown;
    private int _generationUnknown;

    public AiTurnStageJournal(IAiDailyQuotaReservationService quota, AiQuotaReservation reservation)
    {
        _quota = quota ?? throw new ArgumentNullException(nameof(quota));
        _reservation = reservation;
    }

    public bool HasCharge => Charge > 0;

    public int Charge
    {
        get
        {
            long total = (long)_embeddingKnown + _embeddingUnknown + _generationKnown + _generationUnknown;
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }
    }

    public Task<bool> OwnsAsync(CancellationToken ct)
        => _quota.OwnsTurnAsync(_reservation, ct);

    public async Task<bool> BeginAsync(AiQuotaTurnStage stage, int unknownBound, CancellationToken ct)
    {
        var bound = Math.Max(0, unknownBound);
        if (!await _quota.BeginStageAsync(_reservation, stage, AiTurnLifecycle.StageLease, bound, ct))
        {
            return false;
        }

        AssignUnknown(stage, bound);
        return true;
    }

    public Task<bool> RecordKnownAsync(AiQuotaTurnStage stage, int knownTokens, CancellationToken ct)
        => RecordUsageAsync(stage, knownTokens, 0, ct);

    public async Task<bool> RecordUsageAsync(AiQuotaTurnStage stage, int knownTokens, int unknownTokens, CancellationToken ct)
    {
        var known = Math.Max(0, knownTokens);
        var unknown = Math.Max(0, unknownTokens);
        if (!await _quota.RecordStageUsageAsync(_reservation, stage, known, unknown, ct))
        {
            return false;
        }

        AssignKnown(stage, known);
        AssignUnknown(stage, unknown);
        return true;
    }

    public async Task<bool> RecordUnknownAsync(AiQuotaTurnStage stage, int unknownBound, CancellationToken ct)
    {
        var bound = Math.Max(0, unknownBound);
        if (!await _quota.RecordStageUnknownAsync(_reservation, stage, bound, ct))
        {
            return false;
        }

        AssignUnknown(stage, bound);
        return true;
    }

    public async Task<bool> ClearUnknownAsync(AiQuotaTurnStage stage, CancellationToken ct)
    {
        if (!await _quota.ClearStageUnknownAsync(_reservation, stage, ct))
        {
            return false;
        }

        AssignUnknown(stage, 0);
        return true;
    }

    private void AssignKnown(AiQuotaTurnStage stage, int value)
    {
        if (stage is AiQuotaTurnStage.Embedding or AiQuotaTurnStage.EmbeddingKnown)
        {
            _embeddingKnown = value;
            return;
        }

        _generationKnown = value;
    }

    private void AssignUnknown(AiQuotaTurnStage stage, int value)
    {
        if (stage is AiQuotaTurnStage.Embedding or AiQuotaTurnStage.EmbeddingKnown)
        {
            _embeddingUnknown = value;
            return;
        }

        _generationUnknown = value;
    }
}
