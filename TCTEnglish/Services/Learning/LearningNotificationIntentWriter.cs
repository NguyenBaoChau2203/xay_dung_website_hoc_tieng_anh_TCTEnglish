using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.Notifications;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Learning;

public interface ILearningNotificationIntentWriter
{
    Task EnqueueAsync(
        int userId,
        string feature,
        string contentKey,
        DateTime occurredAtUtc,
        LearningActivityTransition? transition,
        CancellationToken cancellationToken);
}

/// <summary>
/// Writes payload v2 outbox intents for goal/streak/badge using the same rules as direct notifications.
/// Does not invent GoalCompleted when the user has no matching goal.
/// </summary>
public sealed class LearningNotificationIntentWriter : ILearningNotificationIntentWriter
{
    private readonly DbflashcardContext _context;
    private readonly LearningBusinessClock _clock;

    public LearningNotificationIntentWriter(DbflashcardContext context, LearningBusinessClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task EnqueueAsync(
        int userId,
        string feature,
        string contentKey,
        DateTime occurredAtUtc,
        LearningActivityTransition? transition,
        CancellationToken cancellationToken)
    {
        var businessDate = _clock.ToBusinessDateFromUtcStorage(occurredAtUtc);
        var intents = new List<NotificationIntent>();

        var activeGoals = await _context.UserGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId && g.IsActive && g.TargetValue > 0)
            .ToListAsync(cancellationToken);

        var activity = await _context.UserDailyActivities
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.ActivityDate == businessDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (activity != null)
        {
            foreach (var goal in activeGoals)
            {
                var progressValue = goal.GoalArea switch
                {
                    GoalArea.Vocabulary => activity.CardsReviewed,
                    GoalArea.Speaking => activity.SpeakingCompletedCount,
                    GoalArea.Writing => activity.WritingCompletedCount,
                    GoalArea.Reading => activity.ReadingCompletedCount,
                    GoalArea.Listening => activity.ListeningCompletedCount,
                    _ => 0
                };
                var percent = goal.TargetValue <= 0 ? 0 : (int)Math.Round(progressValue * 100d / goal.TargetValue);
                if (percent >= 100)
                {
                    intents.Add(new NotificationIntent(
                        NotificationType.GoalCompleted,
                        $"goal:{userId}:{goal.GoalArea}:{businessDate:yyyy-MM-dd}:100",
                        "Hoàn thành mục tiêu",
                        $"Bạn đã hoàn thành mục tiêu {goal.GoalArea} hôm nay.",
                        "fas fa-check-circle",
                        "/Goals",
                        null));
                }
                else if (percent >= 80)
                {
                    intents.Add(new NotificationIntent(
                        NotificationType.GoalProgress,
                        $"goal:{userId}:{goal.GoalArea}:{businessDate:yyyy-MM-dd}:80",
                        "Gần đến đích",
                        $"Bạn đã đạt {percent}% mục tiêu {goal.GoalArea}.",
                        "fas fa-chart-line",
                        "/Goals",
                        null));
                }
            }
        }

        if (transition is { DidIncrease: true, CurrentStreak: > 0 } record
            && record.CurrentStreak > record.PreviousLongestStreak)
        {
            intents.Add(new NotificationIntent(
                NotificationType.StreakRecord,
                $"streak-record:{userId}:{businessDate:yyyy-MM-dd}:{record.CurrentStreak}",
                "Kỷ lục streak",
                $"Streak {record.CurrentStreak} ngày — kỷ lục mới của bạn.",
                "fas fa-fire",
                "/Goals",
                null));
        }

        if (transition != null)
        {
            foreach (var badgeId in transition.NewlyAwardedBadgeIds.Distinct())
            {
                intents.Add(new NotificationIntent(
                    NotificationType.BadgeEarned,
                    $"badge:{userId}:{badgeId}",
                    "Huy hiệu mới",
                    "Bạn vừa nhận một huy hiệu.",
                    "fas fa-award",
                    "/Goals",
                    badgeId.ToString()));
            }
        }

        var keys = intents.Select(i => i.EventKey).ToList();
        var existingNotifications = await _context.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && n.EventKey != null && keys.Contains(n.EventKey))
            .Select(n => n.EventKey!)
            .ToListAsync(cancellationToken);
        var existingOutbox = await _context.LearningNotificationOutboxEvents.AsNoTracking()
            .Where(o => o.UserId == userId && o.NotificationEventKey != null && keys.Contains(o.NotificationEventKey))
            .Select(o => o.NotificationEventKey!)
            .ToListAsync(cancellationToken);
        var seen = existingNotifications.Concat(existingOutbox).ToHashSet(StringComparer.Ordinal);

        foreach (var intent in intents)
        {
            if (!seen.Add(intent.EventKey))
            {
                continue;
            }

            var payload = JsonSerializer.Serialize(new
            {
                version = 2,
                notificationType = intent.Type.ToString(),
                eventKey = intent.EventKey,
                title = intent.Title,
                message = intent.Message,
                iconClass = intent.IconClass,
                relatedUrl = intent.RelatedUrl,
                occurredAtUtc,
                businessDate = businessDate.ToString("yyyy-MM-dd"),
                feature,
                contentKey
            });

            _context.LearningNotificationOutboxEvents.Add(new LearningNotificationOutbox
            {
                EventId = Guid.NewGuid(),
                EventType = intent.Type.ToString(),
                UserId = userId,
                ContentKey = contentKey,
                BadgeId = intent.BadgeId,
                BusinessDate = businessDate,
                OccurredAtUtc = occurredAtUtc,
                NotificationEventKey = intent.EventKey,
                PayloadJson = payload,
                PayloadVersion = 2,
                CreatedAtUtc = occurredAtUtc,
                AttemptCount = 0,
                NextAttemptAtUtc = occurredAtUtc
            });
        }
    }

    private sealed record NotificationIntent(
        NotificationType Type,
        string EventKey,
        string Title,
        string Message,
        string IconClass,
        string RelatedUrl,
        string? BadgeId);
}
