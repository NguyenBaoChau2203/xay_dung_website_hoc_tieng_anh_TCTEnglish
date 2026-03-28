using System;
using System.Collections.Generic;

namespace TCTVocabulary.Models;

public class WritingExercise
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string Level { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string Topic { get; set; } = null!;
    public string PreviewText { get; set; } = null!;
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<WritingExerciseSentence> WritingExerciseSentences { get; set; } = new List<WritingExerciseSentence>();
}
