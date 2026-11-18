using System;

namespace TCTVocabulary.Models;

public class ReadingAnnotation
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int ReadingPassageId { get; set; }

    public string AnnotationType { get; set; } = "Highlight"; // Highlight | Underline | Note

    public int StartOffset { get; set; }

    public int EndOffset { get; set; }

    public string SelectedText { get; set; } = null!;

    public string? NoteText { get; set; }

    public string Color { get; set; } = "yellow";

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ReadingPassage ReadingPassage { get; set; } = null!;
}
