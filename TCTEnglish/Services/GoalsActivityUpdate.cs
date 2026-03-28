namespace TCTVocabulary.Services
{
    public sealed class GoalsActivityUpdate
    {
        public int XpEarned { get; init; }
        public int CardsReviewed { get; init; }
        public int NewCardsLearned { get; init; }
        public int QuizzesCompleted { get; init; }
        public int SpeakingCompletedCount { get; init; }

        public bool HasChanges =>
            XpEarned > 0
            || CardsReviewed > 0
            || NewCardsLearned > 0
            || QuizzesCompleted > 0
            || SpeakingCompletedCount > 0;

        public bool HasNegativeValues =>
            XpEarned < 0
            || CardsReviewed < 0
            || NewCardsLearned < 0
            || QuizzesCompleted < 0
            || SpeakingCompletedCount < 0;
    }
}
