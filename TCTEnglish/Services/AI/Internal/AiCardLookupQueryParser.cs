using System.Text.RegularExpressions;

namespace TCTEnglish.Services.AI.Internal;

internal static partial class AiCardLookupQueryParser
{
    private static readonly HashSet<string> IgnoredTokens = new(StringComparer.Ordinal)
    {
        "ban",
        "cho",
        "co",
        "cua",
        "dinh",
        "du",
        "example",
        "giai",
        "gi",
        "giup",
        "khong",
        "la",
        "long",
        "nghia",
        "nay",
        "nao",
        "phien",
        "term",
        "thich",
        "tim",
        "toi",
        "tra",
        "tu",
        "vi",
        "word",
        "xem"
    };

    public static IReadOnlyList<string> ExtractSearchTerms(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return [];
        }

        var searchTerms = new List<string>();

        foreach (Match match in QuotedTextRegex().Matches(userMessage))
        {
            if (match.Groups.Count < 2)
            {
                continue;
            }

            var quotedValue = match.Groups[1].Success
                ? match.Groups[1].Value
                : match.Groups[2].Value;

            AddNormalizedTerm(searchTerms, quotedValue);
        }

        var filteredTokens = AiTextNormalizer.Tokenize(userMessage)
            .Where(token => token.Length > 1 && !IgnoredTokens.Contains(token))
            .ToList();

        if (filteredTokens.Count == 0)
        {
            return searchTerms;
        }

        AddNormalizedTerm(searchTerms, string.Join(' ', filteredTokens));

        for (var phraseLength = Math.Min(filteredTokens.Count, 3); phraseLength >= 1; phraseLength--)
        {
            AddNormalizedTerm(
                searchTerms,
                string.Join(' ', filteredTokens.Skip(filteredTokens.Count - phraseLength).Take(phraseLength)));
        }

        foreach (var token in filteredTokens)
        {
            AddNormalizedTerm(searchTerms, token);
        }

        return searchTerms;
    }

    public static string ExtractDisplayTerm(string? userMessage)
    {
        var searchTerm = ExtractSearchTerms(userMessage).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            return searchTerm;
        }

        return string.IsNullOrWhiteSpace(userMessage)
            ? "do"
            : userMessage.Trim();
    }

    private static void AddNormalizedTerm(ICollection<string> searchTerms, string rawValue)
    {
        var normalizedValue = AiTextNormalizer.Normalize(rawValue);
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return;
        }

        if (!searchTerms.Contains(normalizedValue))
        {
            searchTerms.Add(normalizedValue);
        }
    }

    [GeneratedRegex("\"([^\"]+)\"|'([^']+)'", RegexOptions.Compiled)]
    private static partial Regex QuotedTextRegex();
}
