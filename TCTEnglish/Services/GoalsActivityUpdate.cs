namespace TCTVocabulary.Services
{
    public sealed class GoalsActivityUpdate
    {
        public int XpEarned { get; init; }
        public int CardsReviewed { get; init; }
        public int NewCardsLearned { get; init; }
        public int VocabularyCompletedCount { get; init; }
        public int QuizzesCompleted { get; init; }
        public int SpeakingCompletedCount { get; init; }
        public int WritingCompletedCount { get; init; }
        public int ReadingCompletedCount { get; init; }
        public int ListeningCompletedCount { get; init; }

        public bool HasChanges =>
            XpEarned > 0
            || CardsReviewed > 0
            || NewCardsLearned > 0
            || VocabularyCompletedCount > 0
            || QuizzesCompleted > 0
            || SpeakingCompletedCount > 0
            || WritingCompletedCount > 0
            || ReadingCompletedCount > 0
            || ListeningCompletedCount > 0;

        public bool HasNegativeValues =>
            XpEarned < 0
            || CardsReviewed < 0
            || NewCardsLearned < 0
            || VocabularyCompletedCount < 0
            || QuizzesCompleted < 0
            || SpeakingCompletedCount < 0
            || WritingCompletedCount < 0
            || ReadingCompletedCount < 0
            || ListeningCompletedCount < 0;
    }
}
