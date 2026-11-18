using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TCTEnglish.ViewModels.Sets;

namespace TCTEnglish.Services.Sets;

public static class CardDetailsCodec
{
    public const int MaxExamples = 6;
    public const int MaxSynonyms = 10;
    public const int MaxCollocations = 8;
    public const int MaxWordFamily = 8;

    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string? Serialize(CardDetailsJson details)
    {
        var empty = details.Examples.Count == 0 && details.Synonyms.Count == 0 && details.Antonyms.Count == 0
            && details.Collocations.Count == 0 && details.WordFamily.Count == 0
            && (details.VerbForms == null || details.VerbForms.IsEmpty) && string.IsNullOrWhiteSpace(details.Plural);
        return empty ? null : JsonSerializer.Serialize(details, Options);
    }

    /// <summary>Bad or empty JSON is treated as empty details; callers may log the returned flag.</summary>
    public static CardDetailsJson Parse(string? json, out bool wasInvalid)
    {
        wasInvalid = false;
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CardDetailsJson();
        }

        try
        {
            return JsonSerializer.Deserialize<CardDetailsJson>(json, Options) ?? new CardDetailsJson();
        }
        catch (JsonException)
        {
            wasInvalid = true;
            return new CardDetailsJson();
        }
    }

    public static CardImageSource? ParseImageSource(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CardImageSource>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public static class CardInputNormalizer
{
    public const int MaxTerm = 255;
    public const int MaxDefinition = 1000;

    public sealed record Result(CardData Data, IReadOnlyDictionary<string, string> FieldErrors)
    {
        public bool IsValid => FieldErrors.Count == 0;
    }

    public static Result Normalize(CardInputDto? input)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        var data = new CardData();
        input ??= new CardInputDto();

        data.Term = Clean(input.Term) ?? string.Empty;
        if (data.Term.Length == 0)
        {
            errors["Term"] = "Nhập từ";
        }
        else if (data.Term.Length > MaxTerm)
        {
            errors["Term"] = $"Tối đa {MaxTerm} ký tự";
        }

        data.Definition = Clean(input.Definition) ?? string.Empty;
        if (data.Definition.Length == 0)
        {
            errors["Definition"] = "Nhập nghĩa tiếng Việt";
        }
        else if (data.Definition.Length > MaxDefinition)
        {
            errors["Definition"] = $"Tối đa {MaxDefinition} ký tự";
        }

        data.Phonetic = Limited(input.Phonetic, 100, "Phonetic", errors);
        data.PhoneticUk = Limited(input.PhoneticUk, 100, "PhoneticUk", errors);
        data.MeaningEn = Limited(input.MeaningEn, 400, "MeaningEn", errors);
        data.Topic = Limited(input.Topic, 60, "Topic", errors);
        data.PersonalNote = Limited(input.PersonalNote, 1000, "PersonalNote", errors);
        data.PartsOfSpeech = CardPartsOfSpeech.Join(input.PartsOfSpeech);
        data.Emoji = SingleGrapheme(input.Emoji);
        data.AiGenerated = input.AiGenerated;

        var cefr = Clean(input.Cefr)?.ToUpperInvariant();
        if (cefr != null && !CefrLevels.IsValid(cefr))
        {
            errors["Cefr"] = "Mức CEFR không hợp lệ";
        }
        else
        {
            data.Cefr = cefr;
        }

        var image = Clean(input.ImageUrl);
        if (image != null && !IsAllowedImageUrl(image))
        {
            errors["ImageUrl"] = "Ảnh không hợp lệ";
        }
        else
        {
            data.ImageUrl = image;
        }

        data.ImageSourceJson = BuildImageSource(input.ImageSource, data.ImageUrl);

        var details = new CardDetailsJson
        {
            Examples = (input.Examples ?? new())
                .Select(e => new CardExample { Text = Truncate(Clean(e?.Text), 300), Vi = Truncate(Clean(e?.Vi), 300) })
                .Where(e => e.Text != null)
                .Take(CardDetailsCodec.MaxExamples)
                .ToList(),
            Synonyms = CleanList(input.Synonyms, 80, CardDetailsCodec.MaxSynonyms),
            Antonyms = CleanList(input.Antonyms, 80, CardDetailsCodec.MaxSynonyms),
            Collocations = (input.Collocations ?? new())
                .Select(c => new CardCollocation { Text = Truncate(Clean(c?.Text), 120), Vi = Truncate(Clean(c?.Vi), 200) })
                .Where(c => c.Text != null)
                .Take(CardDetailsCodec.MaxCollocations)
                .ToList(),
            WordFamily = (input.WordFamily ?? new())
                .Select(w => new CardWordFamilyItem
                {
                    Text = Truncate(Clean(w?.Text), 80),
                    Pos = CardPartsOfSpeech.IsValid(Clean(w?.Pos)?.ToLowerInvariant()) ? Clean(w?.Pos)!.ToLowerInvariant() : null,
                    Vi = Truncate(Clean(w?.Vi), 200),
                })
                .Where(w => w.Text != null)
                .Take(CardDetailsCodec.MaxWordFamily)
                .ToList(),
            VerbForms = CleanVerbForms(input.VerbForms),
            Plural = Truncate(Clean(input.Plural), 80),
        };

        data.DetailsJson = CardDetailsCodec.Serialize(details);
        data.Example = details.Examples.FirstOrDefault()?.Text;
        data.ExampleTranslation = details.Examples.FirstOrDefault()?.Vi;

        return new Result(data, errors);
    }

    public static bool IsAllowedImageUrl(string url)
    {
        if (url.Length > 1000 || url.Contains("..", StringComparison.Ordinal) || url.Contains('\\') || url.Any(char.IsControl))
        {
            return false;
        }

        if (url.StartsWith("/images/cards/", StringComparison.Ordinal) || url.StartsWith("/uploads/shared/", StringComparison.Ordinal))
        {
            return !url.Contains('?') && !url.Contains('#');
        }

        return IsPixabayUrl(url);
    }

    public static bool IsPixabayUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        return host == "pixabay.com" || host.EndsWith(".pixabay.com", StringComparison.Ordinal);
    }

    private static string? BuildImageSource(CardImageSource? source, string? imageUrl)
    {
        if (source == null || imageUrl == null)
        {
            return null;
        }

        var provider = Clean(source.Provider)?.ToLowerInvariant();
        if (provider != "pixabay" && provider != "upload")
        {
            return null;
        }

        var pageUrl = Clean(source.PageUrl);
        if (pageUrl != null && !(Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)))
        {
            pageUrl = null;
        }

        var normalized = new CardImageSource { Provider = provider, PageUrl = Truncate(pageUrl, 400), Author = Truncate(Clean(source.Author), 100) };
        return JsonSerializer.Serialize(normalized, CardDetailsCodec.Options);
    }

    private static CardVerbForms? CleanVerbForms(CardVerbForms? forms)
    {
        if (forms == null)
        {
            return null;
        }

        var cleaned = new CardVerbForms
        {
            Past = Truncate(Clean(forms.Past), 80),
            PastParticiple = Truncate(Clean(forms.PastParticiple), 80),
            Ing = Truncate(Clean(forms.Ing), 80),
            ThirdPerson = Truncate(Clean(forms.ThirdPerson), 80),
        };
        return cleaned.IsEmpty ? null : cleaned;
    }

    private static string? SingleGrapheme(string? value)
    {
        var text = Clean(value);
        if (text == null || text.Length > 16)
        {
            return null;
        }

        return new StringInfo(text).LengthInTextElements == 1 ? text : null;
    }

    private static List<string> CleanList(IEnumerable<string>? items, int maxLength, int maxCount)
        => (items ?? Array.Empty<string>())
            .Select(i => Truncate(Clean(i), maxLength))
            .Where(i => i != null)
            .Select(i => i!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .ToList();

    private static string? Limited(string? value, int max, string field, Dictionary<string, string> errors)
    {
        var text = Clean(value);
        if (text != null && text.Length > max)
        {
            errors[field] = $"Tối đa {max} ký tự";
            return null;
        }

        return text;
    }

    private static string? Truncate(string? value, int max)
        => value != null && value.Length > max ? value[..max] : value;

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
