using System;

namespace TCTVocabulary.Models;

public class UserDailyActivity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime ActivityDate { get; set; }
    public int XpEarned { get; set; }
    public int StreakXpAwarded { get; set; }
    public int CardsReviewed { get; set; }
    public int NewCardsLearned { get; set; }
    public int VocabularyCompletedCount { get; set; }
    public int QuizzesCompleted { get; set; }
    public int SpeakingCompletedCount { get; set; }
    public int WritingCompletedCount { get; set; }
    public int ReadingCompletedCount { get; set; }
    public int ListeningCompletedCount { get; set; }

    public virtual User User { get; set; } = null!;
}
