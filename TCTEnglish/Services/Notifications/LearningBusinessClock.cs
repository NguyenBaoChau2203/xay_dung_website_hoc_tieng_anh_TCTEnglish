namespace TCTEnglish.Services.Notifications;

/// <summary>
/// Vietnam business-day clock for Goals/Notifications/Streak/worker (UTC+7).
/// Injectable via <see cref="TimeProvider"/> for tests.
/// </summary>
public sealed class LearningBusinessClock
{
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();
    private readonly TimeProvider _timeProvider;

    public LearningBusinessClock(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public DateTime BusinessDate =>
        TimeZoneInfo.ConvertTimeFromUtc(UtcNow, BusinessTimeZone).Date;

    public DateTime GetUtcStartOfBusinessDate(DateTime businessDate)
    {
        var localStart = DateTime.SpecifyKind(businessDate.Date, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localStart, BusinessTimeZone);
    }

    public DateTime GetUtcEndExclusiveOfBusinessDate(DateTime businessDate)
        => GetUtcStartOfBusinessDate(businessDate.Date.AddDays(1));

    public DateTime ToBusinessDateFromUtcStorage(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Unspecified)
        {
            dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }

        var utc = dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();
        return TimeZoneInfo.ConvertTimeFromUtc(utc, BusinessTimeZone).Date;
    }

    private static TimeZoneInfo ResolveBusinessTimeZone()
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
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
