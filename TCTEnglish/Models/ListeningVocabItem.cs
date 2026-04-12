using System.ComponentModel.DataAnnotations;

namespace TCTVocabulary.Models;

public class ListeningVocabItem
{
    public int Id { get; set; }

    public int LessonId { get; set; }

    public int OrderIndex { get; set; }

    [Required]
    [MaxLength(200)]
    public string Word { get; set; } = null!;

    [Required]
    public string Definition { get; set; } = null!;

    public string? ExampleSentence { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    // Navigation
    public ListeningLesson Lesson { get; set; } = null!;
}
