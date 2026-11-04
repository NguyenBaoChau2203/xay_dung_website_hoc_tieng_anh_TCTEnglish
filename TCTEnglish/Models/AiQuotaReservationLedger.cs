using System;
using TCTVocabulary.Models;

namespace TCTEnglish.Models;

public enum AiQuotaReservationStatus : byte
{
    Pending = 0,
    Settled = 1,
    Released = 2
}

/// <summary>
/// Per-request quota reservation identity. Terminal settle/release is idempotent by RequestId.
/// Aggregate counters live on <see cref="AiUserDailyQuota"/> and are updated with fresh DB state.
/// </summary>
public class AiQuotaReservationLedger
{
    public Guid RequestId { get; set; }
    public int UserId { get; set; }
    public DateTime DayUtc { get; set; }
    public AiQuotaReservationStatus Status { get; set; }
    public int ReservedTokens { get; set; }
    public int ActualTokens { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? FinalizedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
