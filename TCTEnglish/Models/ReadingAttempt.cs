using System;
using System.Collections.Generic;

namespace TCTVocabulary.Models;

public class ReadingAttempt
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int ReadingPassageId { get; set; }

    public string Mode { get; set; } = "Practice"; // Practice | Timed

    public DateTime StartedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public bool IsSubmitted { get; set; }

    public int? TimeLimitSeconds { get; set; }

    public int? ElapsedSeconds { get; set; }

    public int? Score { get; set; }

    public int? MaxScore { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ReadingPassage ReadingPassage { get; set; } = null!;

    public virtual ICollection<ReadingStudentAnswer> Answers { get; set; }
        = new List<ReadingStudentAnswer>();
}
