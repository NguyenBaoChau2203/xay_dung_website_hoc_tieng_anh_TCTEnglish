namespace TCTVocabulary.Models;

public class ReadingVocabularyItem
{
    public int Id { get; set; }

    public int ReadingPassageId { get; set; }

    public string? ExternalVocabularyKey { get; set; }

    public string Word { get; set; } = null!;

    public string? Ipa { get; set; }

    public string? PartOfSpeech { get; set; }

    public string? EnglishDefinition { get; set; }

    public string? VietnameseMeaning { get; set; }

    public string? Example { get; set; }

    public int? ParagraphId { get; set; }

    public virtual ReadingPassage ReadingPassage { get; set; } = null!;

    public virtual ReadingParagraph? Paragraph { get; set; }
}
