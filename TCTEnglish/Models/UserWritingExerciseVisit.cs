namespace TCTEnglish.Models;

/// <summary>Personal navigation history; opening a page is not learning credit.</summary>
public sealed class UserWritingExerciseVisit
{
    public int UserId { get; set; }
    public int WritingExerciseId { get; set; }
    public DateTime LastOpenedAtUtc { get; set; }
    public TCTVocabulary.Models.User User { get; set; } = null!;
    public TCTVocabulary.Models.WritingExercise WritingExercise { get; set; } = null!;
}
