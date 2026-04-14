using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Internal.Retrievers;

public sealed class UserVocabularyRetriever : IKnowledgeRetriever
{
    private readonly DbflashcardContext _context;

    public UserVocabularyRetriever(DbflashcardContext context)
    {
        _context = context;
    }

    public bool CanHandle(UserIntent intent) => intent == UserIntent.MyVocabulary;

    public async Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
    {
        var totalCount = await _context.Sets
            .AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .CountAsync(ct);

        if (totalCount == 0)
        {
            return [];
        }

        var sets = await _context.Sets
            .AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue)
            .ThenBy(x => x.SetId)
            .Select(x => new
            {
                x.SetName,
                CardCount = x.Cards.Count()
            })
            .Take(5)
            .ToListAsync(ct);

        var snippets = new List<KnowledgeSnippet>
        {
            new(
                "summary",
                $"totalCount={totalCount}",
                KnowledgeSnippetSources.UserVocabularySummary)
        };

        snippets.AddRange(sets.Select(x => new KnowledgeSnippet(
            x.SetName,
            $"cardCount={x.CardCount}",
            KnowledgeSnippetSources.UserVocabularySet)));

        return snippets;
    }
}
