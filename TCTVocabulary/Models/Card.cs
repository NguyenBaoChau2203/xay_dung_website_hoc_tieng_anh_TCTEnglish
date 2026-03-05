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
    public string? ImageUrl { get; set; }
    public string? Phonetic { get; set; }
    public string? Example { get; set; }
    public string? ExampleTranslation { get; set; }
    public string? Topic { get; set; }
    public virtual Set Set { get; set; } = null!;
}
