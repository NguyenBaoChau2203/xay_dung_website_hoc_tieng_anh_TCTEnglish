using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TCTVocabulary.Models;

public class ListeningLesson
{
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = null!;

    /// <summary>CEFR level: A1, A2, B1, B2, C1</summary>
    [Required]
    [MaxLength(10)]
    public string Level { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Topic { get; set; } = null!;

    [MaxLength(50)]
    public string? YoutubeId { get; set; }

    [MaxLength(500)]
    public string? AudioUrl { get; set; }

    [MaxLength(500)]
    public string? ThumbnailUrl { get; set; }

    /// <summary>Formatted duration string, e.g. "5:30"</summary>
    [MaxLength(20)]
    public string? Duration { get; set; }

    [MaxLength(150)]
    public string? Speaker1Name { get; set; }

    [MaxLength(150)]
    public string? Speaker2Name { get; set; }

    [MaxLength(100)]
    public string? Speaker1Country { get; set; }

    [MaxLength(100)]
    public string? Speaker2Country { get; set; }

    public bool IsPublished { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<ListeningTranscriptLine> TranscriptLines { get; set; } = new List<ListeningTranscriptLine>();
    public ICollection<ListeningQuizQuestion> QuizQuestions { get; set; } = new List<ListeningQuizQuestion>();
    public ICollection<ListeningVocabItem> VocabItems { get; set; } = new List<ListeningVocabItem>();
    public ICollection<UserListeningProgress> UserProgresses { get; set; } = new List<UserListeningProgress>();
}
