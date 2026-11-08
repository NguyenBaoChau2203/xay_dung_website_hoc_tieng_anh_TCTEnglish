using System.Text.RegularExpressions;

namespace TCTEnglish.Services.AI.Rag.Corpus;

/// <summary>
/// Phase 2 supports a small extraction grammar, not general question answering.
/// Full answers bind the entire subject, relation and qualifiers in one sentence.
/// Search fragments and passage requests produce labelled excerpts; other
/// requests fail closed until a later grounded-answer phase supports them.
/// </summary>
public static class LexicalAnswerAdmission
{
    private const string Time = @"(?:at (?:dawn|midnight|noon|night)|in the (?:morning|evening))";
    private const string Colors = "red|blue|green|white|black|yellow|orange|purple|brown|gray|grey|pink";

    public static bool IsExplicitSourceRequest(string message)
    {
        var question = LexicalNormalizer.Normalize(message);
        var request = Parse(question);
        return (request is not null && (request.Kind != Kind.Location || request.Verb.Length > 0))
            // Routing an unsupported request here does not admit an answer.
            || Match(question, @"^(?:what does the (?:stored )?(?:passage|lesson|text) say |ai da ).+$").Success;
    }

    public static bool ReferencesSourceTopic(string message, string text)
    {
        // This only selects the retrieval path. It cannot admit a full answer.
        var words = LexicalNormalizer.Unigrams(message);
        var source = LexicalNormalizer.Normalize(text);
        for (var index = 0; index < words.Count - 1; index++)
        {
            for (var length = 2; length <= 4 && index + length <= words.Count; length++)
            {
                var phraseWords = words.Skip(index).Take(length).ToArray();
                var phrase = string.Join(' ', phraseWords);
                if (!phraseWords.Any(SearchFramingWords.Contains) && IsNounPhrase(phrase) && HasPhrase(source, phrase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static bool TryAdmit(string message, IReadOnlyList<LexicalHit> hits,
        out IReadOnlyList<LexicalHit> admitted, out string? scope)
    {
        admitted = [];
        scope = null;
        var question = LexicalNormalizer.Normalize(message);
        if (question.Length is 0 or > 1000)
        {
            return false;
        }

        var requests = question.Split(" and ", StringSplitOptions.None).Select(Parse).ToArray();
        if (requests.Length <= 2 && requests.All(request => request is not null))
        {
            var first = requests[0]!;
            if (requests.Length > 1 && (requests.Any(request => request!.Kind != Kind.VisibilityTime && request.Kind != Kind.Location)
                || requests.Any(request => request!.Subject != first.Subject)))
            {
                return false;
            }

            var supported = hits.FirstOrDefault(hit => requests.All(request => Covers(request!, hit.Text)));
            if (supported is null)
            {
                return false;
            }

            admitted = [supported];
            scope = first.Kind == Kind.Reference ? "excerpt" : "full";
            return true;
        }

        // A keyword fragment is a search, never proof of a factual answer.
        var words = question.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (message.Contains('?') || words.Length > 16 || words.Any(RequestWords.Contains))
        {
            return false;
        }

        var excerpt = hits.FirstOrDefault(hit => words.Any(word =>
            word.Length > 2 && HasPhrase(LexicalNormalizer.Normalize(hit.Text), word)));
        if (excerpt is null)
        {
            return false;
        }

        admitted = [excerpt];
        scope = "excerpt";
        return true;
    }

    private static Request? Parse(string question)
    {
        var match = Match(question, @"^(?:where can i find|can i find) (?<subject>.+) in this lesson$");
        if (!match.Success)
        {
            match = Match(question, @"^in this lesson can i find (?<subject>.+)$");
        }
        if (match.Success)
        {
            return Create(Kind.SourceLookup, match);
        }

        match = Match(question, @"^what does the (?:stored )?(?:passage|lesson|text) say about (?<subject>.+)$");
        if (match.Success)
        {
            return Create(Kind.Reference, match);
        }

        match = Match(question, @"^when is (?<subject>.+) visible$");
        if (match.Success)
        {
            return Create(Kind.VisibilityTime, match);
        }

        match = Match(question, @"^where is (?<subject>.+?)(?: (?<verb>visible))?$");
        if (match.Success)
        {
            return Create(Kind.Location, match);
        }

        match = Match(question, @"^where does (?<subject>.+) (?<verb>stand|hang)$");
        if (match.Success)
        {
            return Create(Kind.Location, match);
        }

        match = Match(question, @"^how many people live near (?<subject>.+)$");
        if (match.Success)
        {
            return Create(Kind.PeopleCount, match);
        }

        match = Match(question, @"^when was (?<subject>.+) (?<verb>constructed|installed|built)$");
        if (match.Success)
        {
            return Create(Kind.ConstructionTime, match);
        }

        match = Match(question, @"^who (?<verb>built|installed|constructed) (?<subject>.+)$");
        if (match.Success)
        {
            return Create(Kind.Agent, match);
        }

        match = Match(question, @"^(?:what|which) color is (?<subject>.+)$");
        if (match.Success)
        {
            return Create(Kind.Color, match);
        }

        match = Match(question, @"^why does (?<subject>.+?) (?<verb>glow|shine|ring|burn)(?<qualifier> " + Time + @")?$");
        return match.Success ? Create(Kind.Cause, match) : null;
    }

    private static Request? Create(Kind kind, Match match)
    {
        var subject = RemoveArticle(match.Groups["subject"].Value);
        return IsNounPhrase(subject)
            ? new Request(kind, subject, match.Groups["verb"].Value, match.Groups["qualifier"].Value)
            : null;
    }

    private static bool Covers(Request request, string text)
    {
        var source = LexicalNormalizer.Normalize(text);
        var subject = @"(?:the |a |an )?" + Regex.Escape(request.Subject);
        switch (request.Kind)
        {
            case Kind.Reference:
            case Kind.SourceLookup:
                return HasPhrase(source, request.Subject);
            case Kind.PeopleCount:
                return Match(source, @"^(?:[0-9]+|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve) people live near " + subject + "$").Success;
            case Kind.Cause:
                var predicate = request.Verb == "shine" ? "shines" : request.Verb + "s";
                return Match(source, "^" + subject + " " + predicate + Regex.Escape(request.Qualifier) + @" because .+$").Success;
            case Kind.ConstructionTime:
                return Match(source, "^" + subject + " was " + request.Verb + @" (?:in [0-9]{4}|" + Time + ")$").Success;
            case Kind.Agent:
                return Match(source, "^" + subject + " was " + request.Verb + @" by [\p{L}]+(?: [\p{L}]+){0,4}$").Success
                    && Match(text.Trim(), @"\bby \p{Lu}[\p{L}]*(?: \p{Lu}[\p{L}]*){0,4}[.!]?$").Success;
            case Kind.Color:
                if (Match(source, "^" + subject + " is (?:" + Colors + ")$").Success)
                {
                    return true;
                }
                // A color adjective on this exact subject also states its color.
                return Match(request.Subject, "^(?:" + Colors + @") [\p{L} ]+$").Success
                    && TryLocation(source, subject, string.Empty);
            case Kind.VisibilityTime:
                return Visibility(source, subject) is { Success: true } visibility
                    && visibility.Groups["time"].Success
                    && (!visibility.Groups["place"].Success || IsPlace(visibility.Groups["place"].Value));
            case Kind.Location:
                return TryLocation(source, subject, request.Verb);
            default:
                return false;
        }
    }

    private static bool TryLocation(string source, string subject, string verb)
    {
        var visibility = Visibility(source, subject);
        if ((verb.Length == 0 || verb == "visible") && visibility.Success
            && visibility.Groups["place"].Success && IsPlace(visibility.Groups["place"].Value))
        {
            return true;
        }

        var predicate = verb switch
        {
            "stand" => "stands",
            "hang" => "hangs",
            "" => "(?:stands|hangs|is located)",
            _ => null
        };
        if (predicate is null)
        {
            return false;
        }

        var location = Match(source, "^" + subject + " " + predicate + @" (?<place>.+)$");
        return location.Success && IsPlace(location.Groups["place"].Value);
    }

    private static Match Visibility(string source, string subject) => Match(
        source, "^" + subject + @" is visible (?:(?<time>" + Time + @")(?: (?<place>.+))?|(?<place>.+))$");

    private static bool IsPlace(string value)
    {
        // Ambiguous temporal prepositions (at/in/on) are deliberately unsupported.
        var match = Match(value, @"^(?:beside|near|inside|outside|above|below|under|behind|opposite|in front of) (?<object>.+)$");
        return match.Success && IsNounPhrase(RemoveArticle(match.Groups["object"].Value));
    }

    private static bool IsNounPhrase(string value)
    {
        var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length is > 0 and <= 12
            && tokens.All(token => token.All(char.IsLetter) && !RequestWords.Contains(token) && !RelationWords.Contains(token));
    }

    private static string RemoveArticle(string value)
    {
        foreach (var article in new[] { "the ", "a ", "an " })
        {
            if (value.StartsWith(article, StringComparison.Ordinal))
            {
                return value[article.Length..];
            }
        }
        return value;
    }

    private static bool HasPhrase(string text, string phrase) => (" " + text + " ").Contains(" " + phrase + " ", StringComparison.Ordinal);

    private static Match Match(string input, string pattern) => Regex.Match(
        input, pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));

    private enum Kind { Reference, SourceLookup, VisibilityTime, Location, PeopleCount, ConstructionTime, Agent, Color, Cause }

    private sealed record Request(Kind Kind, string Subject, string Verb, string Qualifier);

    private static readonly HashSet<string> RequestWords = new(StringComparer.Ordinal)
    {
        "what", "which", "when", "where", "who", "why", "how", "please", "tell", "explain", "give", "show",
        "is", "are", "was", "were", "be", "do", "does", "did", "can", "could", "would", "should", "has", "have",
        "ai", "gi", "nao", "sao", "bao", "nhieu", "hay", "cho", "biet", "la", "co", "khong"
    };

    private static readonly HashSet<string> RelationWords = new(StringComparer.Ordinal)
    {
        "and", "or", "but", "while", "although", "if", "not", "no", "never", "because", "by", "about",
        "beside", "near", "inside", "outside", "above", "below", "under", "behind", "opposite", "at", "in", "on",
        "dawn", "morning", "evening", "night", "noon", "midnight", "today", "tonight", "sang", "trua", "toi", "dem"
    };

    private static readonly HashSet<string> SearchFramingWords = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "this", "that", "lesson", "passage", "stored", "saved", "text", "example", "examples",
        "i", "you", "me", "my", "your", "of", "to", "for", "with"
    };
}
