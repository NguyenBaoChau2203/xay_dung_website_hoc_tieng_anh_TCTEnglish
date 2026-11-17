namespace TCTEnglish.Services.Notifications;

/// <summary>
/// One admin announcement send. <see cref="BatchId"/> is made when the form is opened, so a double submit
/// or a refresh sends the same batch again and is recognised (plan 4.4, decision P7).
/// </summary>
public sealed record AdminAnnouncementRequest(
    string Title,
    string Message,
    string? Url,
    int AdminUserId,
    Guid BatchId);

/// <param name="Recipients">Rows written, or rows already written when <paramref name="AlreadySent"/>.</param>
/// <param name="AlreadySent">True when the same batch had already been sent and nothing new was written.</param>
public sealed record AdminAnnouncementOutcome(int Recipients, bool AlreadySent);

/// <summary>One announcement send as the admin history shows it.</summary>
/// <param name="IsLegacy">A row written before batch keys existed (grouped by text and exact send time).</param>
/// <param name="CopiesMayBeCleaned">
/// Older than the retention period: read copies may have been deleted, so <paramref name="Recipients"/>
/// can be lower than the number of people who received it.
/// </param>
public sealed record AnnouncementHistoryEntry(
    string Title,
    string Message,
    string? Url,
    DateTime CreatedAtUtc,
    int? SenderId,
    string SenderName,
    int Recipients,
    int ReadCount,
    bool IsLegacy,
    bool CopiesMayBeCleaned);

public sealed record AnnouncementHistoryPage(
    IReadOnlyList<AnnouncementHistoryEntry> Items,
    int Page,
    int TotalPages,
    int TotalCount);
