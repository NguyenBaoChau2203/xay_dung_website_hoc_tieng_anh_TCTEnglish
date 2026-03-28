namespace TCTVocabulary.Models;

public class WritingExerciseSentence
{
    public int Id { get; set; }
    public int WritingExerciseId { get; set; }
    public int SortOrder { get; set; }
    public string VietnameseText { get; set; } = null!;
    public string EnglishMeaning { get; set; } = null!;
    public bool BreakAfter { get; set; }

    public WritingExercise WritingExercise { get; set; } = null!;
}
