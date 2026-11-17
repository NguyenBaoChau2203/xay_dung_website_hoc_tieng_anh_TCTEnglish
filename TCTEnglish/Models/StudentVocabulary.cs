using System;

namespace TCTVocabulary.Models;

public class StudentVocabulary
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int? SourceReadingPassageId { get; set; }

    public int? SourceVocabularyItemId { get; set; }

    public string Word { get; set; } = null!;

    public string? Ipa { get; set; }

    public string? PartOfSpeech { get; set; }

    public string? EnglishDefinition { get; set; }

    public string? VietnameseMeaning { get; set; }

    public string? Example { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ReadingPassage? SourceReadingPassage { get; set; }

    public virtual ReadingVocabularyItem? SourceVocabularyItem { get; set; }
}
