using System.ComponentModel.DataAnnotations;

namespace TCTVocabulary.Models;

public class ListeningTranscriptLine
{
    public int Id { get; set; }

    public int LessonId { get; set; }

    public int OrderIndex { get; set; }

    [Required]
    [MaxLength(100)]
    public string Speaker { get; set; } = null!;

    [Required]
    public string Text { get; set; } = null!;

    public string? VietnameseMeaning { get; set; }

    /// <summary>Start time in seconds</summary>
    public double? StartTime { get; set; }

    /// <summary>End time in seconds</summary>
    public double? EndTime { get; set; }

    // Navigation
    public ListeningLesson Lesson { get; set; } = null!;
}
