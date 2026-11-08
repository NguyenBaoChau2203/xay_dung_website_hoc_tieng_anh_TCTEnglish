using TCTEnglish.Services.AI.Internal;

namespace TCTEnglish.Services.AI.Rag;

internal enum StoredCardLookupKind
{
    Definition,
    Pronunciation,
    Example,
    Comparison
}

// Retrieval may match individual words. Full answers require this complete target.
internal sealed record StoredCardLookupRequest(StoredCardLookupKind Kind, string Target)
{
    private static readonly (string Prefix, string Suffix, StoredCardLookupKind Kind)[] Frames =
    [
        ("phien am cua ", "", StoredCardLookupKind.Pronunciation),
        ("phien am ", "", StoredCardLookupKind.Pronunciation),
        ("pronunciation of ", "", StoredCardLookupKind.Pronunciation),
        ("how to pronounce ", "", StoredCardLookupKind.Pronunciation),
        ("pronounce ", "", StoredCardLookupKind.Pronunciation),
        ("vi du cua ", "", StoredCardLookupKind.Example),
        ("vi du cho ", "", StoredCardLookupKind.Example),
        ("example of ", "", StoredCardLookupKind.Example),
        ("give me an example of ", "", StoredCardLookupKind.Example),
        ("what does ", " mean", StoredCardLookupKind.Definition),
        ("what do ", " mean", StoredCardLookupKind.Definition),
        ("what is the meaning of ", "", StoredCardLookupKind.Definition),
        ("meaning of ", "", StoredCardLookupKind.Definition),
        ("definition of ", "", StoredCardLookupKind.Definition),
        // Match complete Vietnamese question frames before their shorter prefixes.
        // Strip only framing at the boundaries; target words remain intact.
        ("dinh nghia cua tu ", " la gi", StoredCardLookupKind.Definition),
        ("dinh nghia cua ", " la gi", StoredCardLookupKind.Definition),
        ("dinh nghia cua tu ", "", StoredCardLookupKind.Definition),
        ("dinh nghia cua ", "", StoredCardLookupKind.Definition),
        ("nghia cua tu ", " la gi", StoredCardLookupKind.Definition),
        ("nghia cua ", " la gi", StoredCardLookupKind.Definition),
        ("nghia cua tu ", "", StoredCardLookupKind.Definition),
        ("nghia cua ", "", StoredCardLookupKind.Definition),
        ("tu ", " nghia la gi", StoredCardLookupKind.Definition),
        ("tu ", " la gi", StoredCardLookupKind.Definition),
        ("", " co nghia la gi", StoredCardLookupKind.Definition),
        ("", " nghia la gi", StoredCardLookupKind.Definition),
        ("", " la gi", StoredCardLookupKind.Definition),
        ("giai thich ", "", StoredCardLookupKind.Definition),
        ("explain ", "", StoredCardLookupKind.Definition),
        ("what is ", "", StoredCardLookupKind.Definition),
        ("so sanh ", "", StoredCardLookupKind.Comparison),
        ("compare ", "", StoredCardLookupKind.Comparison)
    ];

    internal static StoredCardLookupRequest? Parse(string? message)
    {
        var text = AiTextNormalizer.Normalize(message);
        if (text.Length == 0)
        {
            return null;
        }

        // Source labels remain the retriever's responsibility; never become target words.
        var sourceStart = new[] { " trong bo ", " trong set ", " giua bo ", " giua set " }
            .Select(marker => text.IndexOf(marker, StringComparison.Ordinal))
            .Where(index => index >= 0)
            .DefaultIfEmpty(text.Length)
            .Min();
        text = text[..sourceStart].Trim();

        foreach (var prefix in new[] { "please ", "can you ", "could you ", "ban hay ", "hay " })
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                text = text[prefix.Length..];
                break;
            }
        }

        const string notesSuffix = " against my notes";
        var notesText = text.EndsWith(" from class", StringComparison.Ordinal)
            ? text[..^" from class".Length]
            : text;
        if (notesText.StartsWith("check ", StringComparison.Ordinal)
            && notesText.EndsWith(notesSuffix, StringComparison.Ordinal))
        {
            return Create(StoredCardLookupKind.Definition, notesText["check ".Length..^notesSuffix.Length]);
        }

        foreach (var frame in Frames)
        {
            if (text.StartsWith(frame.Prefix, StringComparison.Ordinal)
                && text.EndsWith(frame.Suffix, StringComparison.Ordinal)
                && text.Length > frame.Prefix.Length + frame.Suffix.Length)
            {
                return Create(frame.Kind, text.Substring(frame.Prefix.Length,
                    text.Length - frame.Prefix.Length - frame.Suffix.Length));
            }
        }

        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length is < 1 or > 3 || tokens.Any(token => token is
            "what" or "who" or "which" or "how" or "when" or "where" or "why"
            or "is" or "are" or "does" or "do" or "la" or "gi" or "bao" or "nhieu"))
        {
            return null;
        }

        return Create(StoredCardLookupKind.Definition, text);
    }

    internal bool Supports(KnowledgeSnippet snippet)
    {
        if (!string.Equals(AiTextNormalizer.Normalize(snippet.Title), Target, StringComparison.Ordinal))
        {
            return false;
        }

        var field = Kind switch
        {
            StoredCardLookupKind.Pronunciation => "phonetic",
            StoredCardLookupKind.Example => "example",
            _ => "definition"
        };
        return snippet.Body.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => part.StartsWith(field + "=", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(part[(field.Length + 1)..]));
    }

    private static StoredCardLookupRequest? Create(StoredCardLookupKind kind, string target)
    {
        target = target.Trim();
        return target.Length == 0 ? null : new StoredCardLookupRequest(kind, target);
    }
}
