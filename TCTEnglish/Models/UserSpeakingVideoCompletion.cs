using System;

namespace TCTVocabulary.Models;

public class UserSpeakingVideoCompletion
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int VideoId { get; set; }
    public int CompletedSentenceCount { get; set; }
    public int RequiredSentenceCount { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime LastEvaluatedAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; } = null!;
    public virtual SpeakingVideo SpeakingVideo { get; set; } = null!;
}
