using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Internal.Retrievers;

public sealed class ClassRetriever : IKnowledgeRetriever
{
    private readonly DbflashcardContext _context;

    public ClassRetriever(DbflashcardContext context)
    {
        _context = context;
    }

    public bool CanHandle(UserIntent intent) => intent == UserIntent.ClassInfo;

    public async Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
    {
        var classes = await _context.Classes
            .AsNoTracking()
            .Where(currentClass =>
                currentClass.OwnerId == userId
                || currentClass.ClassMembers.Any(member => member.UserId == userId))
            .OrderByDescending(currentClass => currentClass.OwnerId == userId)
            .ThenBy(currentClass => currentClass.ClassName)
            .Select(currentClass => new
            {
                currentClass.ClassName,
                IsOwner = currentClass.OwnerId == userId,
                MemberCount = currentClass.ClassMembers.Count() + 1
            })
            .ToListAsync(ct);

        if (classes.Count == 0)
        {
            return [];
        }

        var ownerClassName = classes
            .Where(currentClass => currentClass.IsOwner)
            .Select(currentClass => currentClass.ClassName)
            .FirstOrDefault() ?? string.Empty;

        var snippets = new List<KnowledgeSnippet>
        {
            new(
                "summary",
                $"totalCount={classes.Count}|ownerClassName={ownerClassName}",
                KnowledgeSnippetSources.ClassInfoSummary)
        };

        snippets.AddRange(classes.Take(5).Select(currentClass => new KnowledgeSnippet(
            currentClass.ClassName,
            $"role={(currentClass.IsOwner ? "owner" : "member")}|memberCount={currentClass.MemberCount}",
            KnowledgeSnippetSources.ClassInfoItem)));

        return snippets;
    }
}
