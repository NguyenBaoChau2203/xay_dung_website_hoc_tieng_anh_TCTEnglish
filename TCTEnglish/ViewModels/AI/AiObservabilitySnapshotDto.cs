using System;
using System.Collections.Generic;

namespace TCTEnglish.ViewModels.AI;

public sealed record AiObservabilitySnapshotDto(
    int RequestCount,
    int ErrorCount,
    double ErrorRatePercent,
    int? LatencyP50Ms,
    int? LatencyP95Ms,
    int TokensUsedToday,
    int DailyTokenBudget,
    double BudgetUsagePercent,
    bool IsBudgetWarning,
    bool IsBudgetExceeded,
    bool IsErrorSpikeDetected,
    IReadOnlyList<AiTokenUsageByDayDto> TokenUsageByDay);

public sealed record AiTokenUsageByDayDto(
    DateOnly Date,
    int TotalTokens);

