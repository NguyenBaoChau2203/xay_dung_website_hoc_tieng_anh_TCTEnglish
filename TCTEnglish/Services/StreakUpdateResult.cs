namespace TCTVocabulary.Services
{
    public sealed class StreakUpdateResult
    {
        public int CurrentStreak { get; init; }
        public bool DidIncrease { get; init; }
        public int StreakXpAwarded { get; init; }

        /// <summary>Longest streak before this update was saved (for record notifications).</summary>
        public int PreviousLongestStreak { get; init; }
    }
}
