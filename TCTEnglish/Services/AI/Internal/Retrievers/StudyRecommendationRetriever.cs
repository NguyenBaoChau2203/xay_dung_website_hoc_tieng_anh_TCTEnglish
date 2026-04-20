using Microsoft.EntityFrameworkCore;
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

        var today = DateTime.UtcNow.Date;
        var cardsMetToday = await _context.UserDailyActivities
            .AsNoTracking()
            .Where(activity => activity.UserId == userId && activity.ActivityDate == today)
            .Select(activity => (int?)activity.CardsReviewed)
            .FirstOrDefaultAsync(ct) ?? 0;

        int goalRemaining = Math.Max(0, goalCount - cardsMetToday);

        var ownedSets = await _context.Sets
            .AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(s => new
            {
                s.SetId,
                Title = s.SetName,
                s.CreatedAt,
                TotalCards = s.Cards.Count
            })
            .ToListAsync(ct);

        if (ownedSets.Count == 0)
        {
            return [];
        }

        var masteredProgressItems = await _context.LearningProgresses
            .AsNoTracking()
            .Where(progress => progress.UserId == userId)
            .Join(
                _context.Cards.AsNoTracking(),
                progress => progress.CardId,
                card => card.CardId,
                (progress, card) => new
                {
                    progress.Status,
                    card.SetId
                })
            .Join(
                _context.Sets.AsNoTracking().Where(set => set.OwnerId == userId),
                item => item.SetId,
                set => set.SetId,
                (item, set) => item)
            .ToListAsync(ct);

        var masteredCountsBySet = masteredProgressItems
            .Where(item => IsMastered(item.Status))
            .GroupBy(item => item.SetId)
            .ToDictionary(group => group.Key, group => group.Count());

        var recommendationSet = ownedSets
            .Select(set =>
            {
                masteredCountsBySet.TryGetValue(set.SetId, out var masteredCards);
                var remainingCount = Math.Max(0, set.TotalCards - masteredCards);

                return new
                {
                    set.Title,
                    set.CreatedAt,
                    set.TotalCards,
                    RemainingCount = remainingCount
                };
            })
            .Where(set => set.TotalCards > 0 && set.RemainingCount > 0)
            .OrderByDescending(set => set.CreatedAt ?? DateTime.MinValue)
            .ThenBy(set => set.Title)
            .FirstOrDefault();

        if (recommendationSet is null)
        {
            return [];
        }

        var remainingCount = recommendationSet.RemainingCount;

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

    private static bool IsMastered(string? status)
        => string.Equals(status, "Mastered", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Learned", StringComparison.OrdinalIgnoreCase);
}
