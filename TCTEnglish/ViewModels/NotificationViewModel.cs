using TCTEnglish.Services.Notifications;

namespace TCTVocabulary.ViewModels;

public class NotificationViewModel
{
    // ── Original fields (kept: other scripts and tests read them) ──
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public string? RelatedUrl { get; set; }              // safe local path or null
    public string? IconClass { get; set; }               // always an allowed Font Awesome class
    public DateTime CreatedAt { get; set; }              // UTC (kind set, serialised with "Z")
    public string TimeAgo { get; set; } = string.Empty;  // same text as TimeText

    // ── Added by the shared display model ──
    public string Category { get; set; } = string.Empty; // learning | badge | payment | system
    public string Tone { get; set; } = string.Empty;     // streak | success | warning | accent | primary
    public string? Url { get; set; }                     // same value as RelatedUrl
    public string? LinkLabel { get; set; }
    public string TimeText { get; set; } = string.Empty; // "5 phút trước", "Hôm qua, 21:15", "10/11/2026"
    public string DayGroup { get; set; } = string.Empty; // today | yesterday | earlier
    public DateTime CreatedAtUtc { get; set; }           // ISO-8601 with "Z"
}

public class NotificationListViewModel
{
    public List<NotificationViewModel> Items { get; set; } = new();
    public int UnreadCount { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public bool HasMore { get; set; }
}

/// <summary>Keyset position: the oldest item already shown, by (CreatedAt, Id).</summary>
public sealed record NotificationCursor(DateTime Before, int BeforeId);

/// <summary>One keyset slice for the bell (no duplicates or gaps when notifications arrive).</summary>
public sealed class NotificationSliceViewModel
{
    public List<NotificationViewModel> Items { get; set; } = new();
    public int UnreadCount { get; set; }
    public int TotalCount { get; set; }   // matching rows before the cursor is applied
    public bool HasMore { get; set; }
    public NotificationCursor? NextCursor { get; set; }
}

public sealed record NotificationCategoryCount(int Total, int Unread);

/// <summary>Counts for the filter chips, always with an entry for every category.</summary>
public sealed class NotificationCategoryCounts
{
    public int Total { get; init; }
    public int Unread { get; init; }
    public IReadOnlyDictionary<NotificationCategory, NotificationCategoryCount> ByCategory { get; init; }
        = new Dictionary<NotificationCategory, NotificationCategoryCount>();
}

/// <summary>The read-only reminder settings and "when you get notified" card beside the list.</summary>
public sealed class NotificationSidePanelViewModel
{
    /// <summary>False when the preference read failed: only the "change settings" button is shown.</summary>
    public bool HasSettings { get; init; }
    public string DailyReminder { get; init; } = string.Empty;   // "Bật · 20:00" / "Tắt"
    public string BrowserPush { get; init; } = string.Empty;     // "Bật" / "Tắt"
    public string WeeklyEmail { get; init; } = string.Empty;     // "Bật" / "Tắt"
    public string SettingsUrl { get; init; } = "/Account/Profile#section-notifications";
    public int RetentionDays { get; init; } = 30;
}

/// <summary>Everything the notification page renders.</summary>
public sealed class NotificationPageViewModel
{
    public NotificationListViewModel List { get; init; } = new();
    public string Filter { get; init; } = "all";                 // all | unread
    public string? Category { get; init; }                       // learning | badge | payment | system | null
    public NotificationCategoryCounts Counts { get; init; } = new();
    public NotificationSidePanelViewModel Side { get; init; } = new();
}
