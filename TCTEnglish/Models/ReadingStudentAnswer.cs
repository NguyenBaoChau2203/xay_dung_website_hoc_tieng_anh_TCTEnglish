namespace TCTVocabulary.Models;

public class ReadingStudentAnswer
{
    public int Id { get; set; }

    public int AttemptId { get; set; }

    public int QuestionId { get; set; }

    public int? SelectedOptionId { get; set; }

    public string? AnswerText { get; set; }

    public string? MatchingJson { get; set; }

    public bool? IsCorrect { get; set; }

    public bool IsFlagged { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ReadingAttempt Attempt { get; set; } = null!;

    public virtual ReadingQuestion Question { get; set; } = null!;

    public virtual ReadingOption? SelectedOption { get; set; }
}
