using System;

namespace TCTVocabulary.Models;

public class ReadingReviewLog
{
    public int Id { get; set; }

    public int ReadingPassageId { get; set; }

    public int ActorUserId { get; set; }

    public string FromStatus { get; set; } = null!;

    public string ToStatus { get; set; } = null!;

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ReadingPassage ReadingPassage { get; set; } = null!;

    public virtual User ActorUser { get; set; } = null!;
}
