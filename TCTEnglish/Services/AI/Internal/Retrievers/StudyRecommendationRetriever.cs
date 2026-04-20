using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services.AI.Internal;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Internal.Retrievers;

public sealed class StudyRecommendationRetriever : IKnowledgeRetriever
{
    private readonly DbflashcardContext _context;

    public StudyRecommendationRetriever(DbflashcardContext context)
    {
        _context = context;
    }

    public bool CanHandle(UserIntent intent) => intent == UserIntent.StudyRecommendation;

    public async Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        int goalCount = 0;
        int streakDays = 0;

        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new { u.Goal, u.Streak })
            .FirstOrDefaultAsync(ct);

        if (user is not null)
        {
            goalCount = user.Goal ?? 0;
            streakDays = user.Streak ?? 0;
        }

        // Just a simple approximation for recommendation since we don't track intraday card counts easily here
        int cardsMetToday = 0;
        int goalRemaining = Math.Max(0, goalCount - cardsMetToday);

        var recommendationSet = await _context.Sets
            .AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(s => new
            {
                s.SetId,
                Title = s.SetName,
                TotalCards = s.Cards.Count,
                MasteredCards = _context.LearningProgresses.Count(p => p.Card.SetId == s.SetId && p.UserId == userId && p.Status == "mastered")
            })
            .FirstOrDefaultAsync(ct);

        if (recommendationSet is null || recommendationSet.TotalCards == 0)
        {
            return [];
        }

        var remainingCount = Math.Max(0, recommendationSet.TotalCards - recommendationSet.MasteredCards);

        var body = $"remainingCount={remainingCount} | streakDays={streakDays} | goalRemaining={goalRemaining}";

        return
        [
            new KnowledgeSnippet(
                recommendationSet.Title ?? "Vocabulary",
                body,
                KnowledgeSnippetSources.StudyRecommendation,
                Priority: 3
            )
        ];
    }
}
