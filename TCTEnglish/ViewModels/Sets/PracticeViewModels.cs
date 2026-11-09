using System.Text.Json.Serialization;

namespace TCTEnglish.ViewModels.Sets;

/// <summary>Query string of /Practice/{mode}. Every value is validated; unknown values fall back to defaults.</summary>
public sealed class PracticeQuery
{
    public int? Set { get; set; }
    public string? Scope { get; set; }
    public int? N { get; set; }
    public string? Source { get; set; }
    public string? Srs { get; set; }
    public string? Types { get; set; }
    public string? Ask { get; set; }
    public string? Words { get; set; }
    public string? From { get; set; }

    /// <summary>Card ids parsed from <see cref="Words"/> (distinct, positive, max 50).</summary>
    [JsonIgnore]
    public List<int> WordIds => TCTEnglish.Services.Sets.PracticeModes.ParseWords(Words);
}

public sealed class PracticeExampleDto
{
    public string Text { get; set; } = string.Empty;
    public string? Vi { get; set; }
}

public sealed class PracticeVerbFormsDto
{
    public string? Past { get; set; }
    public string? PastParticiple { get; set; }
    public string? Ing { get; set; }
    public string? ThirdPerson { get; set; }
}

/// <summary>Read-only projection of one card for drills. Never carries personal notes, raw DetailsJson or share data.</summary>
public sealed class PracticeWordDto
{
    public int Id { get; set; }
    public int SetId { get; set; }
    public string Term { get; set; } = string.Empty;
    public string? Phonetic { get; set; }
    public string? PhoneticUk { get; set; }
    public string Definition { get; set; } = string.Empty;
    public string? MeaningEn { get; set; }
    public List<string> PartsOfSpeech { get; set; } = new();
    public string? Topic { get; set; }
    public string? Cefr { get; set; }
    public string? Emoji { get; set; }
    public string? ImageUrl { get; set; }
    public List<PracticeExampleDto> Examples { get; set; } = new();
    public PracticeVerbFormsDto? VerbForms { get; set; }
    public string? Plural { get; set; }
    public string Status { get; set; } = "new";
    public int WrongCount { get; set; }
}

public sealed class PracticeSessionViewModel
{
    public string Mode { get; set; } = string.Empty;
    public string ModeLabel { get; set; } = string.Empty;
    public string Hue { get; set; } = "primary";
    public PracticeQuery Query { get; set; } = new();
    public string ScopeKey { get; set; } = "mine";
    public string ScopeName { get; set; } = string.Empty;
    public int? SetId { get; set; }
    public List<PracticeWordDto> Targets { get; set; } = new();
    public List<PracticeWordDto> Pool { get; set; } = new();
    public bool CountsToSrs { get; set; }
    public string TimeFeature { get; set; } = "Quiz";
    public int UserId { get; set; }
    public string ExitUrl { get; set; } = "/Practice";
    public string AgainUrl { get; set; } = "/Practice";
    public string HubUrl { get; set; } = "/Practice";
    public string? SetUrl { get; set; }
    public string TestUrl { get; set; } = "/Practice/test";
    public string LearnUrl { get; set; } = "/Practice/learn";
    public bool Empty { get; set; }
    public string? EmptyReason { get; set; }
}

public sealed class PracticeHubSetDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int WordCount { get; set; }
    public int DueCount { get; set; }
}

public sealed class PracticeHubViewModel
{
    public List<PracticeHubSetDto> Sets { get; set; } = new();
    public int? PreselectedSetId { get; set; }
    public int UserId { get; set; }
    public int HardWordCount { get; set; }
    public int TotalWords => Sets.Sum(s => s.WordCount);
}

public sealed class HardWordRowDto
{
    public int CardId { get; set; }
    public int SetId { get; set; }
    public string SetName { get; set; } = string.Empty;
    public string Term { get; set; } = string.Empty;
    public string? Phonetic { get; set; }
    public string Definition { get; set; } = string.Empty;
    public int WrongCount { get; set; }
    public DateTime? LastReviewed { get; set; }
}

public sealed class HardWordsViewModel
{
    public int? SetId { get; set; }
    public List<PracticeHubSetDto> Sets { get; set; } = new();
    public List<HardWordRowDto> Rows { get; set; } = new();

    [JsonIgnore]
    public string WordIdsCsv => string.Join(',', Rows.Take(PracticeLimits.MaxWords).Select(r => r.CardId));
}

public static class PracticeLimits
{
    public const int MaxWords = 50;
}
