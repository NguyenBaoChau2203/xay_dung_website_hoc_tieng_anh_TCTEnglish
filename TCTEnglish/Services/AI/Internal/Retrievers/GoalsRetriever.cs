using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Internal.Retrievers;

public sealed class GoalsRetriever : IKnowledgeRetriever
{
    private readonly DbflashcardContext _context;

    public GoalsRetriever(DbflashcardContext context)
    {
        _context = context;
    }

    public bool CanHandle(UserIntent intent)
        => intent == UserIntent.MyProgress || intent == UserIntent.WebsiteGuide;

    public async Task<IReadOnlyList<KnowledgeSnippet>> RetrieveAsync(int userId, string userMessage, CancellationToken ct)
    {
        var normalizedMessage = AiTextNormalizer.Normalize(userMessage);
        if (!IsPersonalGoalsQuery(normalizedMessage))
        {
            return [];
        }

        var today = DateTime.UtcNow.Date;
        var todayActivity = await _context.UserDailyActivities
            .AsNoTracking()
            .Where(activity => activity.UserId == userId && activity.ActivityDate == today)
            .Select(activity => new ActivitySnapshot
            {
                XpEarned = activity.XpEarned,
                CardsReviewed = activity.CardsReviewed,
                SpeakingCompletedCount = activity.SpeakingCompletedCount,
                WritingCompletedCount = activity.WritingCompletedCount,
                ReadingCompletedCount = activity.ReadingCompletedCount,
                ListeningCompletedCount = activity.ListeningCompletedCount
            })
            .FirstOrDefaultAsync(ct) ?? new ActivitySnapshot();

        var totalXp = await _context.UserDailyActivities
            .AsNoTracking()
            .Where(activity => activity.UserId == userId)
            .SumAsync(activity => (int?)activity.XpEarned, ct) ?? 0;

        var goals = await _context.UserGoals
            .AsNoTracking()
            .Where(goal => goal.UserId == userId && goal.IsActive)
            .OrderBy(goal => goal.GoalArea)
            .Select(goal => new GoalSnapshot
            {
                Area = goal.GoalArea,
                TargetValue = goal.TargetValue
            })
            .ToListAsync(ct);

        if (goals.Count == 0)
        {
            var legacyVocabularyGoal = await _context.Users
                .AsNoTracking()
                .Where(user => user.UserId == userId)
                .Select(user => user.Goal)
                .FirstOrDefaultAsync(ct);

            if (legacyVocabularyGoal.GetValueOrDefault() > 0)
            {
                goals.Add(new GoalSnapshot
                {
                    Area = GoalArea.Vocabulary,
                    TargetValue = legacyVocabularyGoal.GetValueOrDefault()
                });
            }
        }

        var badgeCount = await _context.UserBadges
            .AsNoTracking()
            .Where(userBadge => userBadge.UserId == userId)
            .CountAsync(ct);

        var latestBadges = await _context.UserBadges
            .AsNoTracking()
            .Where(userBadge => userBadge.UserId == userId)
            .OrderByDescending(userBadge => userBadge.AwardedAt)
            .ThenByDescending(userBadge => userBadge.Id)
            .Select(userBadge => new
            {
                userBadge.Badge.Name,
                userBadge.AwardedAt
            })
            .Take(3)
            .ToListAsync(ct);

        var snippets = new List<KnowledgeSnippet>
        {
            new(
                "goal-summary",
                $"activeGoalCount={goals.Count}|todayXp={todayActivity.XpEarned}|totalXp={totalXp}|badgeCount={badgeCount}",
                KnowledgeSnippetSources.GoalSummary,
                "/Goals")
        };

        snippets.AddRange(goals.Select(goal =>
        {
            var completed = GetCompletedValue(goal.Area, todayActivity);
            var remaining = Math.Max(0, goal.TargetValue - completed);
            return new KnowledgeSnippet(
                goal.Area.ToString(),
                $"target={goal.TargetValue}|completed={completed}|remaining={remaining}",
                KnowledgeSnippetSources.GoalAreaItem,
                "/Goals",
                Priority: remaining > 0 ? 2 : 1);
        }));

        snippets.AddRange(latestBadges.Select(badge => new KnowledgeSnippet(
            badge.Name,
            $"awardedAt={badge.AwardedAt:yyyy-MM-dd}",
            KnowledgeSnippetSources.BadgeItem,
            "/Goals#badges")));

        return snippets;
    }

    private static bool IsPersonalGoalsQuery(string normalizedMessage)
    {
        var hasGoalKeyword = AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "goal",
            "goals",
            "muc tieu",
            "huy hieu",
            "badge",
            "xp",
            "daily challenge",
            "thu thach");

        if (!hasGoalKeyword)
        {
            return false;
        }

        if (AiTextNormalizer.ContainsAny(normalizedMessage, "cach", "huong dan", "o dau", "lam sao", "tao", "dat goal"))
        {
            return false;
        }

        return AiTextNormalizer.ContainsAny(
            normalizedMessage,
            "cua toi",
            "hom nay",
            "da dat",
            "da nhan",
            "bao nhieu",
            "tien do",
            "con thieu",
            "xem");
    }

    private static int GetCompletedValue(GoalArea area, ActivitySnapshot activity)
    {
        return area switch
        {
            GoalArea.Vocabulary => activity.CardsReviewed,
            GoalArea.Speaking => activity.SpeakingCompletedCount,
            GoalArea.Writing => activity.WritingCompletedCount,
            GoalArea.Reading => activity.ReadingCompletedCount,
            GoalArea.Listening => activity.ListeningCompletedCount,
            _ => 0
        };
    }

    private sealed class GoalSnapshot
    {
        public GoalArea Area { get; set; }

        public int TargetValue { get; set; }
    }

    private sealed class ActivitySnapshot
    {
        public int XpEarned { get; set; }

        public int CardsReviewed { get; set; }

        public int SpeakingCompletedCount { get; set; }

        public int WritingCompletedCount { get; set; }

        public int ReadingCompletedCount { get; set; }

        public int ListeningCompletedCount { get; set; }
    }
}
