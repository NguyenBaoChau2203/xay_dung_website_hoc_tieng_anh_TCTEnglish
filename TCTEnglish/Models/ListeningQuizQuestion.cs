using System.ComponentModel.DataAnnotations;

namespace TCTVocabulary.Models;

public class ListeningQuizQuestion
{
    public int Id { get; set; }

    public int LessonId { get; set; }

    public int OrderIndex { get; set; }

    [Required]
    public string QuestionText { get; set; } = null!;

    [MaxLength(500)]
    public string? OptionA { get; set; }

    [MaxLength(500)]
    public string? OptionB { get; set; }

    [MaxLength(500)]
    public string? OptionC { get; set; }

    [MaxLength(500)]
    public string? OptionD { get; set; }

    /// <summary>Correct answer: "A", "B", "C", or "D"</summary>
    [Required]
    [MaxLength(1)]
    public string CorrectAnswer { get; set; } = null!;

    public string? Explanation { get; set; }

    // Navigation
    public ListeningLesson Lesson { get; set; } = null!;
}
