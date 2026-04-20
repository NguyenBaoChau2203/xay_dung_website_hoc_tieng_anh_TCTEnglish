namespace TCTVocabulary.Services
{
    public sealed class StreakUpdateResult
    {
        public int CurrentStreak { get; init; }
        public bool DidIncrease { get; init; }
        public int StreakXpAwarded { get; init; }
    }
}
