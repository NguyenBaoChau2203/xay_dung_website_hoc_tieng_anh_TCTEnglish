using System;

namespace TCTVocabulary.Models;

public class UserWritingExerciseProgress
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int WritingExerciseId { get; set; }
    public int TotalSentenceCount { get; set; }
    public int PassedSentenceCount { get; set; }
    public int AttemptCount { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual WritingExercise WritingExercise { get; set; } = null!;
}
