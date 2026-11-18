namespace TCTEnglish.ViewModels;

public sealed class ReadingAssistRequest
{
    public int PassageId { get; set; }

    public string SelectedText { get; set; } = "";

    /// <summary>Translate or Dictionary.</summary>
    public string Mode { get; set; } = ReadingAssistModes.Translate;

    public int? ParagraphNo { get; set; }

    public string? SurroundingSentence { get; set; }
}

public static class ReadingAssistModes
{
    public const string Translate = "Translate";
    public const string Dictionary = "Dictionary";
}

public static class ReadingAssistSources
{
    public const string PassageVocab = "PassageVocab";
    public const string Ai = "Ai";
    public const string Notebook = "Notebook";
}

public sealed class ReadingAssistResultVm
{
    public bool Success { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string Mode { get; set; } = ReadingAssistModes.Translate;

    public string Source { get; set; } = ReadingAssistSources.Ai;

    public bool Cached { get; set; }

    public int? VocabItemId { get; set; }

    public string Word { get; set; } = "";

    public string? Lemma { get; set; }

    public string? Translation { get; set; }

    public string? PartOfSpeech { get; set; }

    public string? Ipa { get; set; }

    public string? EnglishDefinition { get; set; }

    public string? VietnameseMeaning { get; set; }

    public string? Example { get; set; }
}

public sealed class SaveNotebookApiRequest
{
    public int? PassageId { get; set; }

    public int? VocabItemId { get; set; }

    public string Word { get; set; } = "";

    public string? Ipa { get; set; }

    public string? PartOfSpeech { get; set; }

    public string? EnglishDefinition { get; set; }

    public string? VietnameseMeaning { get; set; }

    public string? Example { get; set; }
}

public sealed class ReadingNotebookFields
{
    public string? Ipa { get; set; }

    public string? PartOfSpeech { get; set; }

    public string? EnglishDefinition { get; set; }

    public string? VietnameseMeaning { get; set; }

    public string? Example { get; set; }
}
