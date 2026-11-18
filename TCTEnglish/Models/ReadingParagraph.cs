namespace TCTVocabulary.Models;

public class ReadingParagraph
{
    public int Id { get; set; }

    public int ReadingPassageId { get; set; }

    public string? ExternalParagraphKey { get; set; }

    public int ParagraphOrder { get; set; }

    public string ParagraphContent { get; set; } = null!;

    public virtual ReadingPassage ReadingPassage { get; set; } = null!;
}
