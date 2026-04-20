using System.Globalization;
using System.Text;

namespace TCTEnglish.Services.AI.Internal;

internal static class AiTextNormalizer
{
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

    public static bool ContainsAny(string normalizedText, params string[] phrases)
    {
        foreach (var phrase in phrases)
        {
            if (ContainsPhrase(normalizedText, phrase))
            {
                return true;
            }
        }

        return false;
    }

    public static int CountMatches(string normalizedText, IEnumerable<string> phrases)
    {
        var count = 0;

        foreach (var phrase in phrases)
        {
            if (ContainsPhrase(normalizedText, phrase))
            {
                count++;
            }
        }

        return count;
    }

    public static HashSet<string> Tokenize(string? value)
    {
        return Normalize(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool ContainsPhrase(string normalizedText, string phrase)
    {
        var normalizedPhrase = Normalize(phrase);
        if (string.IsNullOrWhiteSpace(normalizedPhrase))
        {
            return false;
        }

        return $" {normalizedText} ".Contains($" {normalizedPhrase} ", StringComparison.Ordinal);
    }
}
