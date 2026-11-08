using System.Globalization;
using System.Text;

namespace TCTEnglish.Services.AI.Rag.Corpus;

/// <summary>
/// Matching form for Vietnamese and English. Display text is never rewritten here.
/// Letters fold to lowercase ASCII, including đ, and multiword keywords stay addressable
/// as a single phrase token in addition to their unigrams.
/// </summary>
public static class LexicalNormalizer
{
    public const string PhrasePrefix = "phrase:";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousWasSpace = false;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lowered = character switch
            {
                'đ' or 'Đ' => 'd',
                _ => char.ToLowerInvariant(character)
            };

            if (char.IsLetterOrDigit(lowered))
            {
                builder.Append(lowered);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    public static IReadOnlyList<string> Unigrams(string? value)
    {
        var normalized = Normalize(value);
        if (normalized.Length == 0)
        {
            return [];
        }

        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    public static string? PhraseToken(string? value)
    {
        var parts = Unigrams(value);
        if (parts.Count < 2)
        {
            return null;
        }

        return PhrasePrefix + string.Join('_', parts);
    }

    public static IReadOnlyList<string> KeywordTokens(IEnumerable<string> keywords)
    {
        var tokens = new List<string>();
        foreach (var keyword in keywords)
        {
            tokens.AddRange(Unigrams(keyword));
            var phrase = PhraseToken(keyword);
            if (phrase is not null)
            {
                tokens.Add(phrase);
            }
        }

        return tokens;
    }

    public static IReadOnlyList<string> QueryTokens(string? query, IEnumerable<string> indexedPhrases)
    {
        var tokens = new List<string>(Unigrams(query));
        var normalized = Normalize(query);
        if (normalized.Length == 0)
        {
            return tokens;
        }

        var padded = " " + normalized + " ";
        foreach (var phrase in indexedPhrases)
        {
            if (!phrase.StartsWith(PhrasePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var words = phrase[PhrasePrefix.Length..].Replace('_', ' ');
            if (words.Length == 0)
            {
                continue;
            }

            if (padded.Contains(" " + words + " ", StringComparison.Ordinal) && !tokens.Contains(phrase, StringComparer.Ordinal))
            {
                tokens.Add(phrase);
            }
        }

        return tokens;
    }
}
