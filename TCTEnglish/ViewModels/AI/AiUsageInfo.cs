using System;

namespace TCTEnglish.ViewModels.AI;

public sealed class AiUsageInfo
{
    public int RequestedToday { get; init; }
    public int DailyLimit { get; init; }
    public bool IsUnlimited { get; init; }
}
