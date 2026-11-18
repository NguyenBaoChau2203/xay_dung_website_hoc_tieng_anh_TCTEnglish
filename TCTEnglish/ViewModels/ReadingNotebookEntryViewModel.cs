namespace TCTEnglish.ViewModels;

public sealed class ReadingNotebookEntryViewModel
{
    public int Id { get; init; }
    public string Word { get; init; } = string.Empty;
    public string? Ipa { get; init; }
    public string? PartOfSpeech { get; init; }
    public string? EnglishDefinition { get; init; }
    public string? VietnameseMeaning { get; init; }
    public string? Example { get; init; }
}
