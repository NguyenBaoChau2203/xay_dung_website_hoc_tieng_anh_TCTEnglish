using System;
using System.Collections.Generic;

namespace TCTVocabulary.Models;

public partial class LearningProgress
{
    public int ProgressId { get; set; }

    public int UserId { get; set; }

    public int CardId { get; set; }

    public string? Status { get; set; }

    public int? WrongCount { get; set; }

    public int RepetitionCount { get; set; }

    public DateTime? LastReviewedDate { get; set; }
    public DateTime? NextReviewDate { get; set; }

    public virtual Card Card { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
