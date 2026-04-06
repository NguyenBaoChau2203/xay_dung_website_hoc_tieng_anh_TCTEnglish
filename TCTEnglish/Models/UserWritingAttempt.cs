using System;

namespace TCTVocabulary.Models;

public class UserWritingAttempt
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int WritingExerciseId { get; set; }
    public int WritingExerciseSentenceId { get; set; }
    public string SubmittedAnswer { get; set; } = null!;
    public bool Passed { get; set; }
    public bool UsedAi { get; set; }
    public string EvaluationSource { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual WritingExercise WritingExercise { get; set; } = null!;
    public virtual WritingExerciseSentence WritingExerciseSentence { get; set; } = null!;
}
