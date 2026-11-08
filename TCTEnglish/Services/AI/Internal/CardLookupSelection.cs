namespace TCTEnglish.Services.AI.Internal;

public enum CardLookupAnswerMode
{
    None,
    Answer,
    Clarify
}

public static class CardLookupSelection
{
    public static (CardLookupAnswerMode Mode, IReadOnlyList<KnowledgeSnippet> Cards) Resolve(
        string? userMessage,
        IReadOnlyList<KnowledgeSnippet> snippets)
    {
        var cards = snippets
            .Where(snippet => snippet.Source == KnowledgeSnippetSources.CardLookupResult)
            .ToList();
        if (cards.Count == 0)
        {
            return (CardLookupAnswerMode.None, cards);
        }

        var topScore = cards.Max(card => card.Priority);
        var top = cards.Where(card => card.Priority == topScore).ToList();
        if (top.Count <= 1)
        {
            return (CardLookupAnswerMode.Answer, top);
        }

        var distinctIds = top
            .Where(card => card.EntityId is not null)
            .Select(card => card.EntityId!.Value)
            .Distinct()
            .ToList();
        var sameTerm = top
            .Select(card => AiTextNormalizer.Normalize(card.Title))
            .Distinct(StringComparer.Ordinal)
            .Count() == 1;
        if (distinctIds.Count <= 1 || !sameTerm)
        {
            return (CardLookupAnswerMode.Answer, top);
        }

        var normalizedMessage = AiTextNormalizer.Normalize(userMessage);
        var named = top.Where(card => SetNameIsSpecified(normalizedMessage, card)).ToList();
        if (named.Count == 1)
        {
            return (CardLookupAnswerMode.Answer, named);
        }

        if (IsComparison(normalizedMessage))
        {
            return (CardLookupAnswerMode.Answer, named.Count >= 2 ? named : top);
        }

        return (CardLookupAnswerMode.Clarify, top);
    }

    private static bool SetNameIsSpecified(string normalizedMessage, KnowledgeSnippet card)
    {
        var setName = AiTextNormalizer.Normalize(ReadField(card.Body, "setName"));
        return setName.Length >= 3
            && $" {normalizedMessage} ".Contains($" {setName} ", StringComparison.Ordinal);
    }

    private static bool IsComparison(string normalizedMessage)
    {
        return AiTextNormalizer.ContainsAny(normalizedMessage, "so sanh", "compare", "khac nhau");
    }

    private static string ReadField(string? body, string key)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        foreach (var part in body.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = part.Split('=', 2);
            if (split.Length == 2 && string.Equals(split[0], key, StringComparison.Ordinal))
            {
                return split[1];
            }
        }

        return string.Empty;
    }
}
