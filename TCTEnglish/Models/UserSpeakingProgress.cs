using System;

namespace TCTVocabulary.Models;

public class UserSpeakingProgress
{
    public int Id { get; set; }
    
    // Foreign Keys
    public int UserId { get; set; }
    public int SentenceId { get; set; }
    
    // Scoring metrics based on speech analysis API
    public double TotalScore { get; set; }
    public double AccuracyScore { get; set; }
    public double FluencyScore { get; set; }
    public double CompletenessScore { get; set; }
    
    // Tracking
    public DateTime PracticedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public virtual User User { get; set; } = null!;
    public virtual SpeakingSentence SpeakingSentence { get; set; } = null!;
}
