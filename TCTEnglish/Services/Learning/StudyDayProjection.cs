using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services.Notifications;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Learning;

/// <summary>Read-only confirmed presence. Missing evidence never manufactures history.</summary>
public static class StudyDayProjection
{
    public static int CurrentStreak(DateTime? lastStudyDate, int? storedStreak, DateTime today)
        => lastStudyDate?.Date is DateTime last && last >= today.Date.AddDays(-1) && last <= today.Date
            ? Math.Max(storedStreak ?? 0, 0) : 0;

    public static bool HasConfirmedActivity(UserDailyActivity activity)
        => activity.CardsReviewed > 0 || activity.NewCardsLearned > 0
           || activity.VocabularyCompletedCount > 0 || activity.QuizzesCompleted > 0
           || activity.SpeakingCompletedCount > 0 || activity.WritingCompletedCount > 0
           || activity.ReadingCompletedCount > 0 || activity.ListeningCompletedCount > 0
           || activity.StreakXpAwarded > 0;

    public static async Task<HashSet<DateTime>> ReadAsync(DbflashcardContext context, int userId,
        DateTime? from, DateTime today, LearningBusinessClock clock, CancellationToken cancellationToken = default,
        bool includeAreaCounters = true)
    {
        var daily = context.UserDailyActivities.AsNoTracking().Where(a => a.UserId == userId && a.ActivityDate <= today);
        if (from.HasValue) daily = daily.Where(a => a.ActivityDate >= from.Value);
        var confirmedDaily = includeAreaCounters
            ? daily.Where(a => a.CardsReviewed > 0 || a.NewCardsLearned > 0 || a.QuizzesCompleted > 0
                || a.SpeakingCompletedCount > 0 || a.StreakXpAwarded > 0 || a.VocabularyCompletedCount > 0
                || a.WritingCompletedCount > 0 || a.ReadingCompletedCount > 0 || a.ListeningCompletedCount > 0)
            : daily.Where(a => a.CardsReviewed > 0 || a.NewCardsLearned > 0 || a.QuizzesCompleted > 0
                || a.SpeakingCompletedCount > 0 || a.StreakXpAwarded > 0);
        var dates = (await confirmedDaily.Select(a => a.ActivityDate).Distinct().ToListAsync(cancellationToken))
            .Select(day => day.Date).ToHashSet();
        var attempts = context.UserWritingAttempts.AsNoTracking()
            .Where(a => a.UserId == userId && a.Passed && a.CreatedAtUtc <= clock.UtcNow);
        if (from.HasValue)
        {
            var start = clock.GetUtcStartOfBusinessDate(from.Value);
            attempts = attempts.Where(a => a.CreatedAtUtc >= start);
        }
        // UTC+7 has no DST. Both SQLite and MariaDB translate this before DISTINCT.
        var writingDates = await attempts.Select(a => a.CreatedAtUtc.AddHours(7).Date).Distinct().ToListAsync(cancellationToken);
        dates.UnionWith(writingDates.Where(day => day <= today));
        return dates;
    }
}
