using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Learning;

public static class GoalTargetHistoryWriter
{
    public static async Task AppendAsync(DbflashcardContext context, int userId, GoalArea goalArea, int targetValue, bool isActive, DateTime effectiveFromUtc)
    {
        var area = (int)goalArea;
        var next = context.UserGoalTargetRevisions.Local
            .Where(row => row.UserId == userId && row.GoalArea == area)
            .Select(row => row.RevisionNumber)
            .DefaultIfEmpty(0)
            .Max();
        var storedMax = await context.UserGoalTargetRevisions
            .Where(row => row.UserId == userId && row.GoalArea == area)
            .Select(row => (int?)row.RevisionNumber)
            .MaxAsync() ?? 0;
        context.UserGoalTargetRevisions.Add(new UserGoalTargetRevision
        {
            UserId = userId,
            GoalArea = area,
            TargetValue = targetValue,
            IsActive = isActive,
            RevisionNumber = Math.Max(next, storedMax) + 1,
            EffectiveFromUtc = effectiveFromUtc,
            RecordedAtUtc = effectiveFromUtc
        });
    }
}
