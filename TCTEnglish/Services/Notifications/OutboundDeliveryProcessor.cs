using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTEnglish.Services.Emails;
using TCTEnglish.Services.Learning;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Notifications;

public interface IOutboundDeliveryProcessor
{
    Task ScheduleAsync(CancellationToken cancellationToken = default);
    Task<int> DispatchDueAsync(string workerId, CancellationToken cancellationToken = default);
}

public sealed class OutboundDeliveryProcessor : IOutboundDeliveryProcessor
{
    private readonly DbflashcardContext _context;
    private readonly LearningBusinessClock _clock;
    private readonly IWebPushTransport _pushTransport;
    private readonly IAppEmailSender _emailSender;
    private readonly IOptions<OutboundDeliveryOptions> _options;
    private readonly TimeProvider _timeProvider;

    public OutboundDeliveryProcessor(
        DbflashcardContext context,
        LearningBusinessClock clock,
        IWebPushTransport pushTransport,
        IAppEmailSender emailSender,
        IOptions<OutboundDeliveryOptions> options,
        TimeProvider timeProvider)
    {
        _context = context;
        _clock = clock;
        _pushTransport = pushTransport;
        _emailSender = emailSender;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task ScheduleAsync(CancellationToken cancellationToken = default)
    {
        var cursor = 0;
        while (true)
        {
            var preferences = await _context.UserNotificationPreferences.AsNoTracking()
                .Where(row => row.UserId > cursor && (row.PushOptIn || row.WeeklyEmailOptIn))
                .OrderBy(row => row.UserId).Take(100).ToListAsync(cancellationToken);
            if (preferences.Count == 0) break;
            foreach (var preference in preferences)
            {
                if (!ReminderTimeZone.TryResolve(preference.TimeZoneId, out var zone)) continue;
                var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
                var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
                await ScheduleDailyAsync(preference, localNow, utcNow, cancellationToken);
                await ScheduleWeeklyAsync(preference, localNow, utcNow, cancellationToken);
            }
            cursor = preferences[^1].UserId;
        }
    }

    public async Task<int> DispatchDueAsync(string workerId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        // A stopped worker may have dispatched before losing its receipt. Never resend blindly.
        await _context.OutboundDeliveries
            .Where(row => row.Status == OutboundDeliveryStatuses.Leased && row.LeaseUntilUtc <= now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, OutboundDeliveryStatuses.Unknown)
                .SetProperty(row => row.LastOutcome, "lease-expired:outcome-unknown")
                .SetProperty(row => row.LeaseOwner, (string?)null)
                .SetProperty(row => row.LeaseUntilUtc, (DateTime?)null), cancellationToken);
        var ids = await _context.OutboundDeliveries.AsNoTracking()
            .Where(row => (row.Status == OutboundDeliveryStatuses.Pending || row.Status == OutboundDeliveryStatuses.Failed)
                          && row.NextAttemptAtUtc <= now
                          && row.AttemptCount < 4
                          && (row.LeaseUntilUtc == null || row.LeaseUntilUtc < now))
            .OrderBy(row => row.Id)
            .Select(row => row.Id)
            .Take(25)
            .ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var id in ids)
        {
            if (await DispatchOneAsync(id, workerId, now, cancellationToken))
            {
                sent++;
            }
        }

        return sent;
    }

    private async Task ScheduleDailyAsync(UserNotificationPreference preference, DateTime localNow, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (!preference.DailyReminderEnabled || !preference.PushOptIn)
        {
            return;
        }

        var reminder = localNow.Date.AddMinutes(preference.ReminderMinutes);
        var periodKey = "daily:" + localNow.ToString("yyyy-MM-dd");
        if (localNow < reminder)
        {
            return;
        }

        var expired = localNow > reminder.AddMinutes(60);
        var subscriptions = await _context.WebPushSubscriptions.AsNoTracking()
            .Where(row => row.UserId == preference.UserId && row.RevokedAtUtc == null)
            .Select(row => row.EndpointHash)
            .ToListAsync(cancellationToken);
        if (subscriptions.Count == 0)
        {
            return;
        }

        var goalsMet = await GoalsMetAsync(preference.UserId, cancellationToken);
        foreach (var hash in subscriptions)
        {
            await EnsureDeliveryAsync(
                preference.UserId,
                OutboundChannels.Push,
                periodKey,
                hash,
                expired ? OutboundDeliveryStatuses.Expired : goalsMet ? OutboundDeliveryStatuses.Skipped : OutboundDeliveryStatuses.Pending,
                utcNow,
                cancellationToken);
        }
    }

    private async Task ScheduleWeeklyAsync(UserNotificationPreference preference, DateTime localNow, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (!preference.WeeklyEmailOptIn)
        {
            return;
        }

        var user = await _context.Users.AsNoTracking()
            .Where(row => row.UserId == preference.UserId)
            .Select(row => new { row.EmailVerifiedAt, row.AnonymizedAt, row.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (user == null || user.AnonymizedAt != null || user.Status == UserStatus.Blocked || user.EmailVerifiedAt == null)
        {
            return;
        }

        var slot = StartOfWeek(localNow.Date).AddHours(8);
        if (localNow < slot || localNow > slot.AddHours(24))
        {
            if (localNow > slot.AddHours(24) && localNow < slot.AddDays(2))
            {
                await EnsureDeliveryAsync(preference.UserId, OutboundChannels.Email, WeeklyPeriodKey(), "email", OutboundDeliveryStatuses.Expired, utcNow, cancellationToken);
            }

            return;
        }

        await EnsureDeliveryAsync(preference.UserId, OutboundChannels.Email, WeeklyPeriodKey(), "email", OutboundDeliveryStatuses.Pending, utcNow, cancellationToken);
    }

    private async Task<bool> DispatchOneAsync(long id, string workerId, DateTime now, CancellationToken cancellationToken)
    {
        now = _timeProvider.GetUtcNow().UtcDateTime;
        var leaseUntil = now.AddMinutes(2);
        var leaseToken = Guid.NewGuid().ToString("N");
        var claimed = await _context.OutboundDeliveries
            .Where(row => row.Id == id
                          && (row.Status == OutboundDeliveryStatuses.Pending || row.Status == OutboundDeliveryStatuses.Failed)
                          && row.NextAttemptAtUtc <= now && row.AttemptCount < 4
                          && (row.LeaseUntilUtc == null || row.LeaseUntilUtc < now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.Status, OutboundDeliveryStatuses.Leased)
                .SetProperty(row => row.LeaseOwner, leaseToken)
                .SetProperty(row => row.LeaseUntilUtc, leaseUntil)
                .SetProperty(row => row.AttemptCount, row => row.AttemptCount + 1), cancellationToken);
        if (claimed == 0)
        {
            return false;
        }

        var delivery = await _context.OutboundDeliveries.AsNoTracking().FirstAsync(row => row.Id == id, cancellationToken);
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(row => row.UserId == delivery.UserId, cancellationToken);
        var preference = await _context.UserNotificationPreferences.AsNoTracking().FirstOrDefaultAsync(row => row.UserId == delivery.UserId, cancellationToken);
        if (user == null || user.AnonymizedAt != null || user.Status == UserStatus.Blocked || preference == null
            || (delivery.Channel == OutboundChannels.Push && (!preference.PushOptIn || !preference.DailyReminderEnabled))
            || (delivery.Channel == OutboundChannels.Email && !preference.WeeklyEmailOptIn))
        {
            delivery.Status = OutboundDeliveryStatuses.Skipped;
            delivery.LastOutcome = "opt-out";
            await SettleAsync(delivery, cancellationToken);
            return false;
        }

        var eligibility = await CheckEligibilityAsync(delivery, preference, cancellationToken);
        if (eligibility != null)
        {
            delivery.Status = eligibility;
            delivery.LastOutcome = "eligibility-changed";
            await SettleAsync(delivery, cancellationToken);
            return false;
        }

        try
        {
            return delivery.Channel == OutboundChannels.Push
                ? await DispatchPushAsync(delivery, cancellationToken)
                : await DispatchEmailAsync(delivery, user, cancellationToken);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            delivery.Status = OutboundDeliveryStatuses.Unknown;
            delivery.LastOutcome = "dispatch-interrupted:outcome-unknown";
            await SettleAsync(delivery, CancellationToken.None);
            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested) throw;
            return false;
        }
    }

    private async Task<string?> CheckEligibilityAsync(OutboundDelivery delivery, UserNotificationPreference preference, CancellationToken cancellationToken)
    {
        if (!ReminderTimeZone.TryResolve(preference.TimeZoneId, out var zone)) return OutboundDeliveryStatuses.Skipped;
        var local = TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, zone);
        if (delivery.Channel == OutboundChannels.Push)
        {
            if (!DateTime.TryParseExact(delivery.PeriodKey, "'daily:'yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var day) || day.Date != local.Date
                || local > day.Date.AddMinutes(preference.ReminderMinutes + 60)) return OutboundDeliveryStatuses.Expired;
            if (local < day.Date.AddMinutes(preference.ReminderMinutes)) return OutboundDeliveryStatuses.Skipped;
            if (await GoalsMetAsync(delivery.UserId, cancellationToken)) return OutboundDeliveryStatuses.Skipped;
        }
        else if (delivery.Channel == OutboundChannels.Email)
        {
            var slot = StartOfWeek(local.Date).AddHours(8);
            if (delivery.PeriodKey != WeeklyPeriodKey() || local < slot || local >= slot.AddHours(24))
                return OutboundDeliveryStatuses.Expired;
        }
        else return OutboundDeliveryStatuses.Skipped;
        return null;
    }

    private async Task<bool> SettleAsync(OutboundDelivery delivery, CancellationToken cancellationToken)
        => await _context.OutboundDeliveries
            .Where(row => row.Id == delivery.Id && row.Status == OutboundDeliveryStatuses.Leased && row.LeaseOwner == delivery.LeaseOwner)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, delivery.Status)
                .SetProperty(row => row.LastOutcome, delivery.LastOutcome)
                .SetProperty(row => row.NextAttemptAtUtc, delivery.NextAttemptAtUtc)
                .SetProperty(row => row.LeaseOwner, (string?)null)
                .SetProperty(row => row.LeaseUntilUtc, (DateTime?)null), cancellationToken) == 1;

    private async Task<bool> DispatchPushAsync(OutboundDelivery delivery, CancellationToken cancellationToken)
    {
        var subscription = await _context.WebPushSubscriptions.AsNoTracking().FirstOrDefaultAsync(
            row => row.UserId == delivery.UserId && row.EndpointHash == delivery.SubscriptionKey && row.RevokedAtUtc == null,
            cancellationToken);
        if (subscription == null)
        {
            delivery.Status = OutboundDeliveryStatuses.Revoked;
            delivery.LastOutcome = "missing";
            await SettleAsync(delivery, cancellationToken);
            return false;
        }

        var payload = JsonSerializer.Serialize(new
        {
            title = "Nhắc học TCT English",
            body = "Đến giờ ôn tập bạn đã chọn.",
            accountCode = TCTEnglish.Services.Accounts.AccountDisplayCode.Format(delivery.UserId),
            url = "/Account/Profile?tab=profile"
        });
        var result = await _pushTransport.SendAsync(subscription.Endpoint, subscription.P256dh, subscription.Auth, payload, cancellationToken);
        ApplyPushResult(delivery, subscription, result);
        var settled = await SettleAsync(delivery, cancellationToken);
        if (settled && result == PushSendResult.Gone)
        {
            await _context.WebPushSubscriptions.Where(row => row.Id == subscription.Id && row.UserId == subscription.UserId
                    && row.LastSeenAtUtc == subscription.LastSeenAtUtc && row.RevokedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.RevokedAtUtc, subscription.RevokedAtUtc)
                    .SetProperty(row => row.Endpoint, "revoked").SetProperty(row => row.P256dh, "revoked")
                    .SetProperty(row => row.Auth, "revoked"), cancellationToken);
        }
        return settled && result == PushSendResult.Sent;
    }

    private async Task<bool> DispatchEmailAsync(OutboundDelivery delivery, TCTVocabulary.Models.User user, CancellationToken cancellationToken)
    {
        if (!_options.Value.PublicSiteConfigured)
        {
            delivery.Status = OutboundDeliveryStatuses.Failed;
            delivery.LastOutcome = "public-site-not-configured";
            delivery.NextAttemptAtUtc = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(30);
            await SettleAsync(delivery, cancellationToken);
            return false;
        }
        if (user.EmailVerifiedAt == null)
        {
            delivery.Status = OutboundDeliveryStatuses.Skipped;
            delivery.LastOutcome = "unverified";
            await SettleAsync(delivery, cancellationToken);
            return false;
        }

        var summary = await BuildWeeklySummaryAsync(user.UserId, delivery.PeriodKey, cancellationToken);
        var result = await _emailSender.TrySendTrackedHtmlAsync(
            user.Email,
            "Tổng kết tuần học TCT English",
            summary,
            cancellationToken);
        delivery.LastOutcome = result.ToString();
        delivery.Status = result switch
        {
            EmailDispatchResult.Sent => OutboundDeliveryStatuses.Sent,
            EmailDispatchResult.Unknown => OutboundDeliveryStatuses.Unknown,
            _ => OutboundDeliveryStatuses.Failed
        };
        if (result is EmailDispatchResult.Failed or EmailDispatchResult.NotConfigured)
        {
            delivery.NextAttemptAtUtc = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(30 * delivery.AttemptCount);
        }

        var settled = await SettleAsync(delivery, cancellationToken);
        return settled && result == EmailDispatchResult.Sent;
    }

    private void ApplyPushResult(OutboundDelivery delivery, WebPushSubscription subscription, PushSendResult result)
    {
        delivery.LastOutcome = result.ToString();
        switch (result)
        {
            case PushSendResult.Sent:
                delivery.Status = OutboundDeliveryStatuses.Sent;
                break;
            case PushSendResult.Gone:
                delivery.Status = OutboundDeliveryStatuses.Revoked;
                subscription.RevokedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                subscription.Endpoint = "revoked";
                subscription.P256dh = "revoked";
                subscription.Auth = "revoked";
                break;
            case PushSendResult.RetryLater:
                delivery.Status = OutboundDeliveryStatuses.Failed;
                delivery.NextAttemptAtUtc = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(15 * Math.Max(1, delivery.AttemptCount));
                break;
            case PushSendResult.Unknown:
                delivery.Status = OutboundDeliveryStatuses.Unknown;
                break;
            default:
                delivery.Status = OutboundDeliveryStatuses.Failed;
                delivery.NextAttemptAtUtc = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(30);
                break;
        }
    }

    private async Task EnsureDeliveryAsync(int userId, string channel, string periodKey, string subscriptionKey, string status, DateTime utcNow, CancellationToken cancellationToken)
    {
        var exists = await _context.OutboundDeliveries.AnyAsync(
            row => row.UserId == userId && row.Channel == channel && row.PeriodKey == periodKey && row.SubscriptionKey == subscriptionKey,
            cancellationToken);
        if (exists)
        {
            return;
        }

        var delivery = new OutboundDelivery
        {
            UserId = userId,
            Channel = channel,
            PeriodKey = periodKey,
            SubscriptionKey = subscriptionKey,
            Status = status,
            NextAttemptAtUtc = utcNow,
            CreatedAtUtc = utcNow
        };
        _context.OutboundDeliveries.Add(delivery);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _context.Entry(delivery).State = EntityState.Detached;
            if (!await _context.OutboundDeliveries.AsNoTracking().AnyAsync(
                row => row.UserId == userId && row.Channel == channel && row.PeriodKey == periodKey && row.SubscriptionKey == subscriptionKey,
                cancellationToken)) throw;
        }
    }

    private async Task<bool> GoalsMetAsync(int userId, CancellationToken cancellationToken)
    {
        var goals = await _context.UserGoals.AsNoTracking()
            .Where(row => row.UserId == userId && row.IsActive && row.TargetValue > 0)
            .ToListAsync(cancellationToken);
        if (goals.Count == 0)
        {
            return false;
        }

        var today = _clock.BusinessDate;
        var activity = await _context.UserDailyActivities.AsNoTracking()
            .FirstOrDefaultAsync(row => row.UserId == userId && row.ActivityDate == today, cancellationToken);
        return goals.All(goal => Count(activity, goal.GoalArea) >= goal.TargetValue);
    }

    private async Task<string> BuildWeeklySummaryAsync(int userId, string periodKey, CancellationToken cancellationToken)
    {
        if (!DateTime.TryParseExact(periodKey, "'weekly:'yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var start)) throw new InvalidOperationException("Invalid weekly delivery period.");
        var end = start.AddDays(6);
        var activities = await _context.UserDailyActivities.AsNoTracking()
            .Where(row => row.UserId == userId && row.ActivityDate >= start && row.ActivityDate <= end)
            .ToListAsync(cancellationToken);
        var startUtc = _clock.GetUtcStartOfBusinessDate(start);
        var endUtc = _clock.GetUtcEndExclusiveOfBusinessDate(end);
        var segments = await _context.LearningActivitySegments.AsNoTracking()
            .Where(row => row.UserId == userId
                          && row.AcceptedSeconds > 0
                          && row.EndedAtUtc > startUtc && row.StartedAtUtc < endUtc)
            .Select(row => new { row.StartedAtUtc, row.EndedAtUtc, row.AcceptedSeconds })
            .ToListAsync(cancellationToken);
        var coveredUntil = startUtc;
        var seconds = 0;
        foreach (var segment in segments.OrderBy(row => row.StartedAtUtc).ThenBy(row => row.EndedAtUtc))
        {
            var from = segment.EndedAtUtc.AddSeconds(-segment.AcceptedSeconds);
            if (from < segment.StartedAtUtc) from = segment.StartedAtUtc;
            if (from < coveredUntil) from = coveredUntil;
            var until = segment.EndedAtUtc < endUtc ? segment.EndedAtUtc : endUtc;
            if (until > from) seconds += (int)Math.Floor((until - from).TotalSeconds);
            if (until > coveredUntil) coveredUntil = until;
        }
        var studied = activities.Count(row => row.CardsReviewed + row.SpeakingCompletedCount + row.WritingCompletedCount + row.ReadingCompletedCount + row.ListeningCompletedCount > 0);
        return TctEmailRenderer.RenderWeeklyLearningSummary(start, end, studied, seconds / 60, _options.Value.AccountSettingsUrl);
    }

    private string WeeklyPeriodKey()
    {
        var today = _clock.BusinessDate;
        var previousMonday = StartOfWeek(today).AddDays(-7);
        return "weekly:" + previousMonday.ToString("yyyy-MM-dd");
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.Date.AddDays(-offset);
    }

    private static int Count(UserDailyActivity? activity, GoalArea area)
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
}
