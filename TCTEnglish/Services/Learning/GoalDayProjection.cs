using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services.Notifications;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Learning;

internal static class GoalDayProjection
{
    public static int Progress(UserDailyActivity? activity, GoalArea area) => activity == null ? 0 : area switch
    {
        GoalArea.Vocabulary => activity.CardsReviewed,
        GoalArea.Speaking => activity.SpeakingCompletedCount,
        GoalArea.Writing => activity.WritingCompletedCount,
        GoalArea.Reading => activity.ReadingCompletedCount,
        GoalArea.Listening => activity.ListeningCompletedCount,
        _ => 0
    };

    public static async Task<List<GoalsStudyDayViewModel>> BuildAsync(DbflashcardContext context, int userId,
        DateTime today, IReadOnlyCollection<UserGoal> currentGoals, LearningBusinessClock clock,
        bool hasAreaCounters)
    {
        var start = today.AddDays(-6);
        var confirmed = await StudyDayProjection.ReadAsync(context, userId, start, today, clock,
            includeAreaCounters: hasAreaCounters);
        var revisions = await context.UserGoalTargetRevisions.AsNoTracking()
            .Where(r => r.UserId == userId && r.EffectiveFromUtc <= clock.UtcNow).ToListAsync();
        var creationDates = await context.UserGoals.AsNoTracking().Where(g => g.UserId == userId)
            .Select(g => new { g.GoalArea, g.CreatedAt }).ToDictionaryAsync(g => g.GoalArea, g => g.CreatedAt);
        // Old databases missing area columns retain vocabulary chart compatibility;
        // their aggregate goal attainment stays unknown until migration is applied.
        var activities = hasAreaCounters ? await context.UserDailyActivities.AsNoTracking()
            .Where(a => a.UserId == userId && a.ActivityDate >= start && a.ActivityDate <= today).ToListAsync() : [];
        var result = new List<GoalsStudyDayViewModel>();
        for (var day = start; day <= today; day = day.AddDays(1))
        {
            var known = hasAreaCounters;
            var targets = new Dictionary<GoalArea, int>();
            if (day == today)
            {
                foreach (var goal in currentGoals.Where(g => g.IsActive && g.TargetValue > 0))
                    targets[goal.GoalArea] = goal.TargetValue;
            }
            else
            {
                known &= revisions.Count > 0;
                foreach (var area in Enum.GetValues<GoalArea>())
                {
                    var history = revisions.Where(r => r.GoalArea == (int)area)
                        .OrderBy(r => r.EffectiveFromUtc).ThenBy(r => r.RevisionNumber).ToList();
                    if (history.Count == 0)
                    {
                        if (currentGoals.Any(g => g.GoalArea == area)) known = false;
                        continue;
                    }
                    var baseline = clock.ToBusinessDateFromUtcStorage(history[0].EffectiveFromUtc);
                    // A newly created goal cannot change a finished day before it existed.
                    // A legacy goal whose revisions started later still has unknown history.
                    if (day < baseline && creationDates.TryGetValue(area, out var created)
                        && clock.ToBusinessDateFromUtcStorage(created) == baseline)
                        continue;
                    if (day <= baseline)
                    {
                        known = false;
                        continue;
                    }
                    var end = clock.GetUtcEndExclusiveOfBusinessDate(day);
                    var target = history.LastOrDefault(r => r.EffectiveFromUtc < end);
                    if (target is { IsActive: true, TargetValue: > 0 }) targets[area] = target.TargetValue;
                }
            }
            var activity = activities.SingleOrDefault(a => a.ActivityDate.Date == day);
            result.Add(new GoalsStudyDayViewModel
            {
                BusinessDate = day, DayLabel = day.DayOfWeek == DayOfWeek.Sunday ? "CN" : $"T{(int)day.DayOfWeek + 1}",
                IsToday = day == today, HasConfirmedStudy = confirmed.Contains(day),
                ActiveGoalCount = known ? targets.Count : null,
                MetGoalCount = known ? targets.Count(t => Progress(activity, t.Key) >= t.Value) : null
            });
        }
        return result;
    }
}
