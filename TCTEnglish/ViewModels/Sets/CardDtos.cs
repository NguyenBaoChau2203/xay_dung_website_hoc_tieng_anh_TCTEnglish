using System.Text.Json.Serialization;

namespace TCTEnglish.ViewModels.Sets;

public sealed class CardExample
{
    public string? Text { get; set; }
    public string? Vi { get; set; }
}

public sealed class CardCollocation
{
    public string? Text { get; set; }
    public string? Vi { get; set; }
}

public sealed class CardWordFamilyItem
{
    public string? Text { get; set; }
    public string? Pos { get; set; }
    public string? Vi { get; set; }
}

public sealed class CardVerbForms
{
    public string? Past { get; set; }
    public string? PastParticiple { get; set; }
    public string? Ing { get; set; }
    public string? ThirdPerson { get; set; }

    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(Past) && string.IsNullOrWhiteSpace(PastParticiple)
        && string.IsNullOrWhiteSpace(Ing) && string.IsNullOrWhiteSpace(ThirdPerson);
}

public sealed class CardImageSource
{
    public string? Provider { get; set; }
    public string? PageUrl { get; set; }
    public string? Author { get; set; }
}

/// <summary>Stored in Card.DetailsJson (camelCase, nulls omitted).</summary>
public sealed class CardDetailsJson
{
    public List<CardExample> Examples { get; set; } = new();
    public List<string> Synonyms { get; set; } = new();
    public List<string> Antonyms { get; set; } = new();
    public List<CardCollocation> Collocations { get; set; } = new();
    public List<CardWordFamilyItem> WordFamily { get; set; } = new();
    public CardVerbForms? VerbForms { get; set; }
    public string? Plural { get; set; }
}

/// <summary>Raw card input from the composer, importer, edit form and API.</summary>
public class CardInputDto
{
    public string? Term { get; set; }
    public string? Definition { get; set; }
    public string? Phonetic { get; set; }
    public string? PhoneticUk { get; set; }
    public string? MeaningEn { get; set; }
    public List<string>? PartsOfSpeech { get; set; }
    public string? Topic { get; set; }
    public string? Emoji { get; set; }
    public string? Cefr { get; set; }
    public string? PersonalNote { get; set; }
    public string? ImageUrl { get; set; }
    public CardImageSource? ImageSource { get; set; }
    public List<CardExample>? Examples { get; set; }
    public List<string>? Synonyms { get; set; }
    public List<string>? Antonyms { get; set; }
    public List<CardCollocation>? Collocations { get; set; }
    public List<CardWordFamilyItem>? WordFamily { get; set; }
    public CardVerbForms? VerbForms { get; set; }
    public string? Plural { get; set; }
    public bool AiGenerated { get; set; }
}

/// <summary>Validated, trimmed card content ready to be written to a Card row.</summary>
public sealed class CardData
{
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string? Phonetic { get; set; }
    public string? PhoneticUk { get; set; }
    public string? MeaningEn { get; set; }
    public string? PartsOfSpeech { get; set; }
    public string? Topic { get; set; }
    public string? Emoji { get; set; }
    public string? Cefr { get; set; }
    public string? PersonalNote { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageSourceJson { get; set; }
    public string? Example { get; set; }
    public string? ExampleTranslation { get; set; }
    public string? DetailsJson { get; set; }
    public bool AiGenerated { get; set; }
}

public sealed class CardBatchRequest
{
    public List<CardInputDto>? Cards { get; set; }
}

public sealed class CardInsertedDto
{
    public int Id { get; set; }
    public string Term { get; set; } = string.Empty;
}

public sealed class CardBatchResultDto
{
    public List<CardInsertedDto> Inserted { get; set; } = new();
    public List<string> Skipped { get; set; } = new();
    public List<string> Invalid { get; set; } = new();
}

public sealed class CardBulkRequest
{
    public string? Action { get; set; }
    public List<int>? CardIds { get; set; }
    public int? TargetSetId { get; set; }
    public string? Topic { get; set; }
    public bool? Suspended { get; set; }
}

public sealed class CardBulkResultDto
{
    public int Count { get; set; }
    public int? Skipped { get; set; }
}

public sealed class CardStatusDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class CardRowDto
{
    public int CardId { get; set; }
    public string Term { get; set; } = string.Empty;
    public string? Phonetic { get; set; }
    public string Definition { get; set; } = string.Empty;
    public string? Cefr { get; set; }
    public string? Topic { get; set; }
    public string? ImageUrl { get; set; }
    public string? Emoji { get; set; }
    public CardStatusDto Status { get; set; } = new();
    public bool IsSuspended { get; set; }
}

public sealed class CardSearchQuery
{
    public string? Q { get; set; }
    public string? Topic { get; set; }
    public string? Pos { get; set; }
    public string? Status { get; set; }
    public string? Level { get; set; }
    public string? Sort { get; set; }
    public int Offset { get; set; }
}

public sealed class CardPageDto
{
    public List<CardRowDto> Rows { get; set; } = new();
    public int? NextOffset { get; set; }
    public int Total { get; set; }
}

public sealed class DuplicateHitDto
{
    public int CardId { get; set; }
    public int SetId { get; set; }
    public string SetName { get; set; } = string.Empty;
    public string Term { get; set; } = string.Empty;
}

public sealed class ShareLinkRequest
{
    public bool Enabled { get; set; }
    public bool Regenerate { get; set; }
}

public sealed class SuspendRequest
{
    public bool Suspended { get; set; }
}
