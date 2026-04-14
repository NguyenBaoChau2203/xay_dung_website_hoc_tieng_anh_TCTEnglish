using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Internal.Retrievers;

public sealed class CardLookupRetriever : IKnowledgeRetriever
{
    private readonly DbflashcardContext _context;

    public CardLookupRetriever(DbflashcardContext context)
    {
        _context = context;
    }

    public bool CanHandle(UserIntent intent) => intent == UserIntent.CardLookup;

    public async Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
    {
        var searchTerms = AiCardLookupQueryParser.ExtractSearchTerms(userMessage);
        if (searchTerms.Count == 0)
        {
            return [];
        }

        var cards = await _context.Cards
            .AsNoTracking()
            .Join(
                _context.Sets.AsNoTracking().Where(set => set.OwnerId == userId),
                card => card.SetId,
                set => set.SetId,
                (card, set) => new
                {
                    card.Term,
                    card.Definition,
                    card.Phonetic,
                    card.Example,
                    SetName = set.SetName
                })
            .Take(500)
            .ToListAsync(ct);

        var matches = cards
            .Select(card => new
            {
                Card = card,
                Score = Score(card.Term, card.Definition, searchTerms)
            })
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Card.Term)
            .Take(3)
            .ToList();

        return matches.Select(result => new KnowledgeSnippet(
            result.Card.Term,
            string.Join(
                '|',
                $"setName={SanitizeMetadataValue(result.Card.SetName)}",
                $"definition={SanitizeMetadataValue(result.Card.Definition)}",
                $"phonetic={SanitizeMetadataValue(result.Card.Phonetic)}",
                $"example={SanitizeMetadataValue(result.Card.Example)}"),
            KnowledgeSnippetSources.CardLookupResult,
            Priority: result.Score))
            .ToList();
    }

    private static int Score(string term, string definition, IReadOnlyList<string> searchTerms)
    {
        var normalizedTerm = AiTextNormalizer.Normalize(term);
        var normalizedDefinition = AiTextNormalizer.Normalize(definition);
        var bestScore = 0;

        foreach (var searchTerm in searchTerms)
        {
            if (string.Equals(normalizedTerm, searchTerm, StringComparison.Ordinal))
            {
                bestScore = Math.Max(bestScore, 100);
                continue;
            }

            if (normalizedTerm.StartsWith($"{searchTerm} ", StringComparison.Ordinal)
                || normalizedTerm.EndsWith($" {searchTerm}", StringComparison.Ordinal)
                || normalizedTerm.Contains($" {searchTerm} ", StringComparison.Ordinal))
            {
                bestScore = Math.Max(bestScore, 85);
                continue;
            }

            if (normalizedTerm.Contains(searchTerm, StringComparison.Ordinal))
            {
                bestScore = Math.Max(bestScore, 70);
            }

            if (normalizedDefinition.Contains(searchTerm, StringComparison.Ordinal))
            {
                bestScore = Math.Max(bestScore, 40);
            }
        }

        return bestScore;
    }

    private static string SanitizeMetadataValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace('|', '/')
            .Replace('=', ':')
            .Trim();
    }
}
