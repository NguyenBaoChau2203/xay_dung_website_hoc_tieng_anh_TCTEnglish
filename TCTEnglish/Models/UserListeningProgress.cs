using System;

namespace TCTVocabulary.Models;

public class UserListeningProgress
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int LessonId { get; set; }

    public bool TranscriptCompleted { get; set; } = false;

    public bool QuizCompleted { get; set; } = false;

    /// <summary>Quiz score as percentage 0–100, null if quiz not completed.</summary>
    public int? QuizScore { get; set; }

    public bool VocabReviewed { get; set; } = false;

    public DateTime? CompletedAt { get; set; }

    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public ListeningLesson Lesson { get; set; } = null!;
}
