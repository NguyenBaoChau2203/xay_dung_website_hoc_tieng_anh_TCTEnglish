using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.Notifications;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Learning;

public sealed class LearningHabitSnapshot
{
    public int WindowDays { get; init; }
    public string WindowLabel { get; init; } = string.Empty;
    public string UpdatedAtText { get; init; } = string.Empty;
    public string EstimatedTimeText { get; init; } = "Chưa đủ dữ liệu";
    public string PracticeDaysText { get; init; } = "Chưa đủ dữ liệu";
    public string UsualHourText { get; init; } = "Chưa đủ dữ liệu";
    public string TopSkillText { get; init; } = "Chưa đủ dữ liệu";
    public string GoalRateText { get; init; } = "Chưa đủ dữ liệu";
    public string Suggestion { get; init; } = "Hãy bắt đầu một bài học để có số liệu.";
    public bool GoalRateKnown { get; init; }
    public int GoalDaysMet { get; init; }
    public int GoalDaysEligible { get; init; }
    public int? GoalRatePercent { get; init; }
}

public interface ILearningHabitService
{
    Task<LearningHabitSnapshot> BuildAsync(int userId, int windowDays, CancellationToken cancellationToken = default);
}

public sealed class LearningHabitService : ILearningHabitService
{
    private readonly DbflashcardContext _context;
    private readonly LearningBusinessClock _clock;

    public LearningHabitService(DbflashcardContext context, LearningBusinessClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<LearningHabitSnapshot> BuildAsync(int userId, int windowDays, CancellationToken cancellationToken = default)
    {
        windowDays = windowDays == 28 ? 28 : 7;
        var today = _clock.BusinessDate;
        var windowEnd = today;
        var windowStart = today.AddDays(-(windowDays - 1));
        var rangeStartUtc = _clock.GetUtcStartOfBusinessDate(windowStart);
        var rangeEndUtc = _clock.GetUtcEndExclusiveOfBusinessDate(today);

        var segments = await _context.LearningActivitySegments.AsNoTracking()
            .Where(row => row.UserId == userId
                          && row.AcceptedSeconds > 0
                          && row.StartedAtUtc < rangeEndUtc
                          && row.EndedAtUtc > rangeStartUtc)
            .Select(row => new SegmentRow(row.Skill, row.StartedAtUtc, row.EndedAtUtc, row.AcceptedSeconds))
            .ToListAsync(cancellationToken);
        segments = NormalizeSegments(segments, rangeStartUtc, _clock.UtcNow < rangeEndUtc ? _clock.UtcNow : rangeEndUtc);
        var activities = await _context.UserDailyActivities.AsNoTracking()
            .Where(row => row.UserId == userId && row.ActivityDate >= windowStart && row.ActivityDate <= today)
            .ToListAsync(cancellationToken);
        var revisions = await _context.UserGoalTargetRevisions.AsNoTracking()
            .Where(row => row.UserId == userId && row.EffectiveFromUtc < rangeEndUtc)
            .ToListAsync(cancellationToken);

        var mergedSeconds = MergeSeconds(segments);
        var practiceDays = CountPracticeDays(windowStart, today, segments, activities);
        var hour = DescribeHour(segments);
        var skill = DescribeSkill(segments);
        var goal = DescribeGoalRate(today, windowStart, revisions, activities);
        var suggestion = BuildSuggestion(practiceDays.ObservedDays, practiceDays.ActiveDays, goal);

        return new LearningHabitSnapshot
        {
            WindowDays = windowDays,
            WindowLabel = $"{windowStart:dd/MM/yyyy} – {windowEnd:dd/MM/yyyy} (giờ Việt Nam)",
            UpdatedAtText = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, BusinessZone()).ToString("HH:mm dd/MM/yyyy") + " (giờ Việt Nam)",
            EstimatedTimeText = mergedSeconds <= 0 ? "Chưa đủ dữ liệu thời gian tương tác" : FormatDuration(mergedSeconds),
            PracticeDaysText = practiceDays.ObservedDays == 0
                ? "Chưa có ngày quan sát"
                : $"{practiceDays.ActiveDays}/{practiceDays.ObservedDays} ngày có luyện tập",
            UsualHourText = hour,
            TopSkillText = skill,
            GoalRateText = goal.Text,
            GoalRateKnown = goal.Known,
            GoalDaysMet = goal.Met,
            GoalDaysEligible = goal.Eligible,
            GoalRatePercent = goal.Percent,
            Suggestion = suggestion
        };
    }

    private static List<SegmentRow> NormalizeSegments(IReadOnlyList<SegmentRow> segments, DateTime rangeStart, DateTime rangeEnd)
    {
        var result = new List<SegmentRow>();
        var coveredUntil = rangeStart;
        foreach (var segment in segments.OrderBy(row => row.StartedAtUtc).ThenBy(row => row.EndedAtUtc).ThenBy(row => row.Skill, StringComparer.Ordinal))
        {
            var start = segment.StartedAtUtc;
            var acceptedStart = segment.EndedAtUtc.AddSeconds(-segment.AcceptedSeconds);
            if (start < acceptedStart) start = acceptedStart;
            if (start < coveredUntil) start = coveredUntil;
            var end = segment.EndedAtUtc < rangeEnd ? segment.EndedAtUtc : rangeEnd;
            while (start < end)
            {
                // The learning business timezone is UTC+7; hour boundaries are UTC hour boundaries too.
                var nextHour = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
                var until = nextHour < end ? nextHour : end;
                var seconds = (int)Math.Floor((until - start).TotalSeconds);
                if (seconds > 0) result.Add(new SegmentRow(segment.Skill, start, until, seconds));
                start = until;
            }
            if (end > coveredUntil) coveredUntil = end;
        }
        return result;
    }

    private static int MergeSeconds(IReadOnlyList<SegmentRow> segments)
    {
        var intervals = segments
            .Select(row => (Start: row.StartedAtUtc, End: row.EndedAtUtc))
            .Where(row => row.End > row.Start)
            .OrderBy(row => row.Start)
            .ToList();
        if (intervals.Count == 0)
        {
            return 0;
        }

        var total = 0d;
        var currentStart = intervals[0].Start;
        var currentEnd = intervals[0].End;
        for (var i = 1; i < intervals.Count; i++)
        {
            if (intervals[i].Start <= currentEnd)
            {
                if (intervals[i].End > currentEnd)
                {
                    currentEnd = intervals[i].End;
                }
            }
            else
            {
                total += (currentEnd - currentStart).TotalSeconds;
                currentStart = intervals[i].Start;
                currentEnd = intervals[i].End;
            }
        }

        total += (currentEnd - currentStart).TotalSeconds;
        return (int)Math.Round(total);
    }

    private (int ActiveDays, int ObservedDays) CountPracticeDays(
        DateTime windowStart,
        DateTime today,
        IReadOnlyList<SegmentRow> segments,
        IReadOnlyList<UserDailyActivity> activities)
    {
        var observed = new HashSet<DateTime>();
        var active = new HashSet<DateTime>();
        foreach (var segment in segments)
        {
            var day = _clock.ToBusinessDateFromUtcStorage(segment.StartedAtUtc);
            if (day >= windowStart && day <= today)
            {
                observed.Add(day);
            }
        }

        foreach (var activity in activities)
        {
            var day = activity.ActivityDate.Date;
            if (HasConfirmedActivity(activity))
            {
                observed.Add(day);
                active.Add(day);
            }
        }

        var secondsByDay = segments
            .GroupBy(row => _clock.ToBusinessDateFromUtcStorage(row.StartedAtUtc))
            .ToDictionary(group => group.Key, group => group.Sum(row => row.AcceptedSeconds));
        foreach (var pair in secondsByDay)
        {
            if (pair.Key >= windowStart && pair.Key <= today && pair.Value >= 60)
            {
                active.Add(pair.Key);
            }
        }

        if (observed.Count == 0)
        {
            return (0, 0);
        }

        var first = observed.Min();
        var span = 0;
        var activeCount = 0;
        for (var day = first; day <= today; day = day.AddDays(1))
        {
            span++;
            if (active.Contains(day))
            {
                activeCount++;
            }
        }

        return (activeCount, span);
    }

    private string DescribeHour(IReadOnlyList<SegmentRow> segments)
    {
        var byDay = segments.GroupBy(row => _clock.ToBusinessDateFromUtcStorage(row.StartedAtUtc)).ToList();
        var total = segments.Sum(row => row.AcceptedSeconds);
        if (byDay.Count < 3 || total < 600)
        {
            return "Chưa đủ dữ liệu (cần ít nhất 3 ngày và 10 phút)";
        }

        var buckets = new int[24];
        foreach (var segment in segments)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(segment.StartedAtUtc, DateTimeKind.Utc), BusinessZone());
            buckets[local.Hour] += segment.AcceptedSeconds;
        }

        var best = 0;
        for (var hour = 1; hour < 24; hour++)
        {
            if (buckets[hour] > buckets[best])
            {
                best = hour;
            }
        }

        var nextHour = best + 1;
        return $"{best:00}:00–{nextHour:00}:00";
    }

    private static string DescribeSkill(IReadOnlyList<SegmentRow> segments)
    {
        if (segments.Count == 0)
        {
            return "Chưa đủ dữ liệu";
        }

        var totals = segments
            .GroupBy(row => row.Skill)
            .Select(group => new { Skill = group.Key, Seconds = group.Sum(row => row.AcceptedSeconds) })
            .OrderByDescending(row => row.Seconds)
            .ThenBy(row => row.Skill, StringComparer.Ordinal)
            .ToList();
        var top = totals[0].Seconds;
        if (top <= 0)
        {
            return "Chưa đủ dữ liệu";
        }

        var tied = totals.Where(row => row.Seconds == top).Select(row => SkillLabel(row.Skill)).ToList();
        var share = (int)Math.Round(100d * top / totals.Sum(row => row.Seconds));
        return tied.Count == 1
            ? $"{tied[0]} ({share}%)"
            : $"Đồng hạng: {string.Join(", ", tied)} ({share}%)";
    }

    private GoalRate DescribeGoalRate(
        DateTime today,
        DateTime windowStart,
        IReadOnlyList<UserGoalTargetRevision> revisions,
        IReadOnlyList<UserDailyActivity> activities)
    {
        if (revisions.Count == 0)
        {
            return new GoalRate(false, 0, 0, null, "Chưa đủ lịch sử mục tiêu");
        }

        var eligible = 0;
        var met = 0;
        var activityByDate = activities.ToDictionary(row => row.ActivityDate.Date, row => row);
        foreach (var area in Enum.GetValues<GoalArea>())
        {
            var areaRows = revisions.Where(row => row.GoalArea == (int)area)
                .OrderBy(row => row.EffectiveFromUtc).ThenBy(row => row.RevisionNumber).ToList();
            if (areaRows.Count == 0)
            {
                continue;
            }

            var baselineDay = _clock.ToBusinessDateFromUtcStorage(areaRows[0].EffectiveFromUtc);
            for (var day = windowStart; day < today; day = day.AddDays(1))
            {
                if (day <= baselineDay)
                {
                    continue;
                }

                var endExclusive = _clock.GetUtcEndExclusiveOfBusinessDate(day);
                var revision = areaRows.LastOrDefault(row => row.EffectiveFromUtc < endExclusive);
                if (revision == null || !revision.IsActive || revision.TargetValue <= 0)
                {
                    continue;
                }

                eligible++;
                activityByDate.TryGetValue(day, out var activity);
                if (CompletedCount(activity, area) >= revision.TargetValue)
                {
                    met++;
                }
            }
        }

        if (eligible == 0)
        {
            return new GoalRate(false, 0, 0, null, "Chưa đủ ngày đã kết thúc có lịch sử mục tiêu. Hôm nay chưa tính.");
        }

        var percent = (int)Math.Round(100d * met / eligible);
        return new GoalRate(true, met, eligible, percent, $"{met}/{eligible} ({percent}%). Hôm nay chưa tính.");
    }

    private static string BuildSuggestion(int observedDays, int activeDays, GoalRate goal)
    {
        if (observedDays == 0)
        {
            return "Hãy bắt đầu một bài học để có số liệu.";
        }

        if (activeDays < 3)
        {
            return "Bạn mới có ít ngày luyện tập. Có thể chọn giờ nhắc ở phần cài đặt.";
        }

        if (goal.Known && goal.Met < goal.Eligible)
        {
            return "Có mục tiêu chưa đạt trong các ngày đã kết thúc. Hãy mở kỹ năng tương ứng và luyện tiếp.";
        }

        return "Tiếp tục luyện đều trong khung giờ bạn đang dùng.";
    }

    private static int CompletedCount(UserDailyActivity? activity, GoalArea area)
    {
        if (activity == null)
        {
            return 0;
        }

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

    private static bool HasConfirmedActivity(UserDailyActivity activity)
        => activity.CardsReviewed > 0
           || activity.VocabularyCompletedCount > 0
           || activity.QuizzesCompleted > 0
           || activity.SpeakingCompletedCount > 0
           || activity.WritingCompletedCount > 0
           || activity.ReadingCompletedCount > 0
           || activity.ListeningCompletedCount > 0
           || activity.XpEarned > 0;

    private static string SkillLabel(string skill) => skill switch
    {
        nameof(GoalArea.Vocabulary) => "Từ vựng",
        nameof(GoalArea.Speaking) => "Phát âm",
        nameof(GoalArea.Writing) => "Viết",
        nameof(GoalArea.Reading) => "Đọc",
        nameof(GoalArea.Listening) => "Nghe",
        _ => skill
    };

    private static string FormatDuration(int seconds)
    {
        var minutes = seconds / 60;
        if (minutes < 60)
        {
            return $"{minutes} phút (ước tính)";
        }

        return $"{minutes / 60} giờ {minutes % 60} phút (ước tính)";
    }

    private static TimeZoneInfo BusinessZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
        }
    }

    private sealed record SegmentRow(string Skill, DateTime StartedAtUtc, DateTime EndedAtUtc, int AcceptedSeconds);
    private readonly record struct GoalRate(bool Known, int Met, int Eligible, int? Percent, string Text);
}
