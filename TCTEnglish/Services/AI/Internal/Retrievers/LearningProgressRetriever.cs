using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Internal.Retrievers;

public sealed class LearningProgressRetriever : IKnowledgeRetriever
{
    private readonly DbflashcardContext _context;

    public LearningProgressRetriever(DbflashcardContext context)
    {
        _context = context;
    }

    public bool CanHandle(UserIntent intent)
        => intent == UserIntent.MyProgress || intent == UserIntent.StudyRecommendation;

    public async Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
    {
        var userSnapshot = await _context.Users
            .AsNoTracking()
            .Where(user => user.UserId == userId)
            .Select(user => new
            {
                user.Goal,
                user.Streak,
                user.LastStudyDate
            })
            .FirstOrDefaultAsync(ct);

        if (userSnapshot is null)
        {
            return [];
        }

        var progressItems = await _context.LearningProgresses
            .AsNoTracking()
            .Where(progress => progress.UserId == userId)
            .Join(
                _context.Cards.AsNoTracking(),
                progress => progress.CardId,
                card => card.CardId,
                (progress, card) => new
                {
                    progress.Status,
                    progress.LastReviewedDate,
                    card.SetId
                })
            .Join(
                _context.Sets.AsNoTracking().Where(set => set.OwnerId == userId),
                item => item.SetId,
                set => set.SetId,
                (item, set) => new
                {
                    item.Status,
                    item.LastReviewedDate,
                    set.SetId,
                    SetName = set.SetName
                })
            .ToListAsync(ct);

        if (progressItems.Count == 0)
        {
            return [];
        }

        var progressSetIds = progressItems
            .Select(item => item.SetId)
            .Distinct()
            .ToList();

        var ownedSetCardCounts = await _context.Sets
            .AsNoTracking()
            .Where(set => set.OwnerId == userId && progressSetIds.Contains(set.SetId))
            .Select(set => new { set.SetId, TotalCards = set.Cards.Count })
            .ToDictionaryAsync(set => set.SetId, set => set.TotalCards, ct);

        var today = DateTime.UtcNow.Date;
        var cardsReviewedToday = await _context.UserDailyActivities
            .AsNoTracking()
            .Where(activity => activity.UserId == userId && activity.ActivityDate == today)
            .Select(activity => (int?)activity.CardsReviewed)
            .FirstOrDefaultAsync(ct) ?? 0;

        var goalCount = Math.Max(0, userSnapshot.Goal ?? 0);
        var remainingGoal = goalCount > 0
            ? Math.Max(goalCount - cardsReviewedToday, 0)
            : 0;
        var streakDays = ComputeCurrentStreak(userSnapshot.LastStudyDate, userSnapshot.Streak, today);
        var masteredCount = progressItems.Count(item => IsMastered(item.Status));
        var learningCount = progressItems.Count(item => IsLearning(item.Status));
        var newCount = progressItems.Count(item => IsNew(item.Status));
        var goalMetToday = goalCount > 0 && cardsReviewedToday >= goalCount;

        var snippets = new List<KnowledgeSnippet>
        {
            new(
                "summary",
                string.Join(
                    '|',
                    $"streakDays={streakDays}",
                    $"masteredCount={masteredCount}",
                    $"learningCount={learningCount}",
                    $"newCount={newCount}",
                    $"goalCount={goalCount}",
                    $"goalMetToday={goalMetToday.ToString().ToLowerInvariant()}",
                    $"remainingCount={remainingGoal}"),
                KnowledgeSnippetSources.ProgressSummary)
        };

        var recommendation = progressItems
            .GroupBy(item => new { item.SetId, item.SetName })
            .Select(group => new
            {
                group.Key.SetName,
                TotalCards = ownedSetCardCounts.TryGetValue(group.Key.SetId, out var totalCards) ? totalCards : 0,
                MasteredCount = group.Count(item => IsMastered(item.Status)),
                LatestReviewAt = group.Max(item => item.LastReviewedDate)
            })
            .Select(group => new
            {
                group.SetName,
                group.LatestReviewAt,
                group.TotalCards,
                RemainingCount = Math.Max(0, group.TotalCards - group.MasteredCount)
            })
            .Where(group => group.TotalCards > 0 && group.RemainingCount > 0)
            .OrderByDescending(group => group.RemainingCount)
            .ThenByDescending(group => group.LatestReviewAt ?? DateTime.MinValue)
            .ThenBy(group => group.SetName)
            .FirstOrDefault();

        if (recommendation is not null)
        {
            snippets.Add(new KnowledgeSnippet(
                recommendation.SetName,
                $"remainingCount={recommendation.RemainingCount}|streakDays={streakDays}|goalRemaining={remainingGoal}",
                KnowledgeSnippetSources.StudyRecommendation,
                Priority: 4));
        }

        return snippets;
    }

    private static int ComputeCurrentStreak(DateTime? lastStudyDate, int? storedStreak, DateTime today)
    {
        var normalizedLastStudyDate = lastStudyDate?.Date;
        if (!normalizedLastStudyDate.HasValue)
        {
            return 0;
        }

        return normalizedLastStudyDate.Value >= today.AddDays(-1)
            ? Math.Max(storedStreak ?? 0, 0)
            : 0;
    }

    private static bool IsMastered(string? status)
        => string.Equals(status, "Mastered", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Learned", StringComparison.OrdinalIgnoreCase);

    private static bool IsLearning(string? status)
        => string.Equals(status, "Learning", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Reviewing", StringComparison.OrdinalIgnoreCase);

    private static bool IsNew(string? status) => !IsMastered(status) && !IsLearning(status);
}
