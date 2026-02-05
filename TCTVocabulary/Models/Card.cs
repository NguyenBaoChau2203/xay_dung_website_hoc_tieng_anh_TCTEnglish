using System;
using System.Collections.Generic;

namespace TCTVocabulary.Models;

public partial class Card
{
    public int CardId { get; set; }

    public int SetId { get; set; }

    public string Term { get; set; } = null!;

    public string Definition { get; set; } = null!;

    public virtual ICollection<LearningProgress> LearningProgresses { get; set; } = new List<LearningProgress>();
    public string? ImageUrl { get; set; } // Thêm
    public virtual Set Set { get; set; } = null!;
}
