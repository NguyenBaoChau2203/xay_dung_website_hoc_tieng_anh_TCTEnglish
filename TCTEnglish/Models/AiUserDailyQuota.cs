using System;
using TCTVocabulary.Models;

namespace TCTEnglish.Models;

/// <summary>
/// DB-authoritative daily AI quota counters (UTC day). Pending reservations occupy slots
/// before the provider call; settle or release without holding a transaction across HTTP.
/// </summary>
public class AiUserDailyQuota
{
    public int UserId { get; set; }
    public DateTime DayUtc { get; set; }
    public int PendingQuestions { get; set; }
    public int SettledQuestions { get; set; }
    public int PendingTokens { get; set; }
    public int SettledTokens { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
