using System;

namespace TCTVocabulary.Models;

public class UserWritingSentenceProgress
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int WritingExerciseId { get; set; }
    public int SentenceId { get; set; }
    public int AttemptCount { get; set; }
    public bool IsPassed { get; set; }
    public string? AcceptedAnswer { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? PassedAt { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual WritingExercise WritingExercise { get; set; } = null!;
    public virtual WritingExerciseSentence Sentence { get; set; } = null!;
}
