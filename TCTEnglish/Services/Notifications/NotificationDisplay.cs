using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TCTEnglish.Models;

namespace TCTEnglish.Services.Notifications;

/// <summary>Learner-facing grouping used for the page filters and the bell.</summary>
public enum NotificationCategory
{
    Learning,
    Badge,
    Payment,
    System
}

/// <summary>One notification as every surface (page, bell, JSON API) shows it.</summary>
public sealed record NotificationDisplayItem(
    int Id,
    string Category,
    string Tone,
    string IconClass,
    string Title,
    string Message,
    string? Url,
    string? LinkLabel,
    bool IsRead,
    DateTime CreatedAtUtc,
    string TimeText,
    string DayGroup);

/// <summary>
/// Pure display rules: category, tone, icon, clean title, safe link, Vietnam-time text and day group.
/// The page, the bell panel and the API all build their items here so they cannot disagree.
/// </summary>
public static class NotificationDisplay
{
    public const string DayGroupToday = "today";
    public const string DayGroupYesterday = "yesterday";
    public const string DayGroupEarlier = "earlier";

    private const string FallbackIcon = "fa-solid fa-bell";

    private static readonly Regex AllowedIcon = new(
        @"^(fas|far|fa-solid|fa-regular) fa-[a-z0-9-]+\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] WeekdayNames =
    {
        "Chủ nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy"
    };

    /// <summary>
    /// The business zone (Vietnam). <c>LearningBusinessClock</c> keeps its zone private, so the same
    /// identifiers are resolved here in the same order.
    /// </summary>
    public static TimeZoneInfo BusinessZone { get; } = ResolveBusinessZone();

    public static NotificationCategory CategoryOf(NotificationType type) => type switch
    {
        NotificationType.StreakWarning
            or NotificationType.StreakRecord
            or NotificationType.GoalProgress
            or NotificationType.GoalCompleted => NotificationCategory.Learning,
        NotificationType.BadgeEarned or NotificationType.BadgeNear => NotificationCategory.Badge,
        NotificationType.PremiumActivated => NotificationCategory.Payment,
        _ => NotificationCategory.System
    };

    public static NotificationDisplayItem Build(Notification notification, DateTime nowUtc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var createdUtc = AsUtc(notification.CreatedAt);
        var url = NotificationLinks.ToSafeLocal(notification.RelatedUrl);

        return new NotificationDisplayItem(
            notification.Id,
            CategoryOf(notification.Type).ToString().ToLowerInvariant(),
            ToneOf(notification.Type),
            IconOf(notification),
            CleanTitle(notification.Title),
            notification.Message ?? string.Empty,
            url,
            url == null ? null : LinkLabelOf(notification.Type, url),
            notification.IsRead,
            createdUtc,
            TimeText(createdUtc, nowUtc, zone),
            DayGroup(createdUtc, nowUtc, zone));
    }

    /// <summary>Removes leading emoji, symbols and spaces; keeps letters, digits and punctuation.</summary>
    public static string CleanTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var trimmed = title.Trim();
        var index = 0;
        foreach (var rune in trimmed.EnumerateRunes())
        {
            if (!IsDecoration(rune))
            {
                break;
            }

            index += rune.Utf16SequenceLength;
        }

        var cleaned = trimmed[index..].TrimStart();
        return cleaned.Length == 0 ? trimmed : cleaned;
    }

    public static string TimeText(DateTime createdUtc, DateTime nowUtc, TimeZoneInfo zone)
    {
        var created = AsUtc(createdUtc);
        var now = AsUtc(nowUtc);
        var diff = now - created;

        // Also covers a creation time a little ahead of this server's clock.
        if (diff < TimeSpan.FromMinutes(1))
        {
            return "Vừa xong";
        }

        if (diff < TimeSpan.FromMinutes(60))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{(int)diff.TotalMinutes} phút trước");
        }

        var localCreated = TimeZoneInfo.ConvertTimeFromUtc(created, zone);
        var days = CalendarDaysAgo(created, now, zone);

        if (days <= 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{(int)diff.TotalHours} giờ trước");
        }

        if (days == 1)
        {
            return $"Hôm qua, {localCreated.ToString("HH:mm", CultureInfo.InvariantCulture)}";
        }

        if (days < 7)
        {
            return $"{WeekdayNames[(int)localCreated.DayOfWeek]}, {localCreated.ToString("HH:mm", CultureInfo.InvariantCulture)}";
        }

        return localCreated.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    }

    public static string DayGroup(DateTime createdUtc, DateTime nowUtc, TimeZoneInfo zone)
    {
        var days = CalendarDaysAgo(AsUtc(createdUtc), AsUtc(nowUtc), zone);
        return days <= 0 ? DayGroupToday : days == 1 ? DayGroupYesterday : DayGroupEarlier;
    }

    /// <summary>Parses a category name from a query string (case-insensitive); null when unknown.</summary>
    public static NotificationCategory? ParseCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<NotificationCategory>(value.Trim(), ignoreCase: true, out var parsed)
               && Enum.IsDefined(parsed)
            ? parsed
            : null;
    }

    private static int CalendarDaysAgo(DateTime createdUtc, DateTime nowUtc, TimeZoneInfo zone)
    {
        var createdDay = TimeZoneInfo.ConvertTimeFromUtc(createdUtc, zone).Date;
        var today = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone).Date;
        return (today - createdDay).Days;
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        // Notification times are stored as UTC and come back from the database without a kind.
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string ToneOf(NotificationType type) => type switch
    {
        NotificationType.StreakWarning or NotificationType.StreakRecord => "streak",
        NotificationType.GoalProgress or NotificationType.GoalCompleted => "success",
        NotificationType.BadgeEarned or NotificationType.BadgeNear => "warning",
        NotificationType.PremiumActivated => "accent",
        _ => "primary"
    };

    private static string IconOf(Notification notification)
    {
        var stored = notification.IconClass?.Trim();
        if (!string.IsNullOrEmpty(stored) && AllowedIcon.IsMatch(stored))
        {
            return stored;
        }

        return notification.Type switch
        {
            NotificationType.StreakWarning => "fa-solid fa-fire-flame-curved",
            NotificationType.StreakRecord => "fa-solid fa-fire",
            NotificationType.GoalProgress or NotificationType.GoalCompleted => "fa-solid fa-bullseye",
            NotificationType.BadgeEarned or NotificationType.BadgeNear => "fa-solid fa-medal",
            NotificationType.PremiumActivated => "fa-solid fa-crown",
            NotificationType.AdminAnnouncement => "fa-solid fa-bullhorn",
            _ => FallbackIcon
        };
    }

    private static string LinkLabelOf(NotificationType type, string url)
    {
        if (type == NotificationType.StreakWarning)
        {
            return "Học ngay";
        }

        if (url.StartsWith("/Goals#badges", StringComparison.OrdinalIgnoreCase))
        {
            return "Xem huy hiệu";
        }

        var pathEnd = url.IndexOfAny(new[] { '?', '#' });
        var path = pathEnd < 0 ? url : url[..pathEnd];
        if (path.Equals("/Goals", StringComparison.OrdinalIgnoreCase))
        {
            return "Xem mục tiêu";
        }

        if (url.StartsWith("/Billing/Order/", StringComparison.OrdinalIgnoreCase))
        {
            return "Xem biên nhận";
        }

        return "Xem chi tiết";
    }

    private static bool IsDecoration(Rune rune)
    {
        if (Rune.IsWhiteSpace(rune))
        {
            return true;
        }

        return Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.OtherSymbol       // emoji and pictographs
            or UnicodeCategory.ModifierSymbol // skin-tone modifiers
            or UnicodeCategory.Format         // zero-width joiner
            or UnicodeCategory.NonSpacingMark; // variation selector after an emoji
    }

    private static TimeZoneInfo ResolveBusinessZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
