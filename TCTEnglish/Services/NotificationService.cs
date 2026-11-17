using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.Notifications;
using TCTVocabulary.Models;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services;

public class NotificationService : INotificationService
{
    // Column lengths of Notification (Title 200, Message 1000, EventKey 160).
    private const int TitleMaxLength = 200;
    private const int MessageMaxLength = 1000;
    private const int EventKeyMaxLength = 160;
    private const int AnnouncementChunkSize = 500;
    private const int AnnouncementRetentionDays = 30; // matches CleanupOldNotificationsAsync
    private const string AnnouncementKeyPrefix = "announce:";
    private const string DefaultSenderName = "Quản trị viên";

    private readonly DbflashcardContext _context;
    private readonly LearningBusinessClock _clock;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        DbflashcardContext context,
        LearningBusinessClock clock,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<List<NotificationViewModel>> GetNotificationsAsync(
        int userId, int page = 1, int pageSize = 20)
    {
        var pageModel = await GetNotificationsPageAsync(userId, page, pageSize, unreadOnly: false);
        return pageModel.Items;
    }

    public async Task<NotificationListViewModel> GetNotificationsPageAsync(
        int userId,
        int page = 1,
        int pageSize = 20,
        bool unreadOnly = false,
        NotificationCategory? category = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        if (category is { } selected)
        {
            var types = NotificationDisplay.TypesOf(selected);
            query = query.Where(n => types.Contains(n.Type));
        }

        var totalCount = await query.CountAsync();
        var unreadCount = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (page > totalPages)
        {
            page = totalPages;
        }

        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var nowUtc = _clock.UtcNow;
        var items = rows.Select(n => ToViewModel(n, nowUtc)).ToList();

        return new NotificationListViewModel
        {
            Items = items,
            UnreadCount = unreadCount,
            TotalCount = totalCount,
            CurrentPage = page,
            TotalPages = totalPages,
            HasMore = page < totalPages
        };
    }

    public async Task<NotificationSliceViewModel> GetNotificationsSliceAsync(
        int userId,
        int take,
        bool unreadOnly,
        DateTime? beforeCreatedUtc,
        int? beforeId,
        NotificationCategory? category = null)
    {
        take = Math.Clamp(take, 1, 50);

        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        if (category is { } selected)
        {
            var types = NotificationDisplay.TypesOf(selected);
            query = query.Where(n => types.Contains(n.Type));
        }

        var totalCount = await query.CountAsync();

        if (beforeCreatedUtc is { } before)
        {
            // Keyset on (CreatedAt, Id): equal timestamps are ordered by Id, so nothing repeats or is skipped.
            var cursorTime = NotificationDisplay.AsUtc(before);
            var cursorId = beforeId ?? int.MaxValue;
            query = query.Where(n => n.CreatedAt < cursorTime
                                     || (n.CreatedAt == cursorTime && n.Id < cursorId));
        }

        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(take + 1)
            .ToListAsync();

        var hasMore = rows.Count > take;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var nowUtc = _clock.UtcNow;
        var items = rows.Select(n => ToViewModel(n, nowUtc)).ToList();
        var unreadCount = await GetUnreadCountAsync(userId);

        return new NotificationSliceViewModel
        {
            Items = items,
            UnreadCount = unreadCount,
            TotalCount = totalCount,
            HasMore = hasMore,
            NextCursor = hasMore && rows.Count > 0
                ? new NotificationCursor(items[^1].CreatedAtUtc, items[^1].Id)
                : null
        };
    }

    public async Task<NotificationCategoryCounts> GetCategoryCountsAsync(int userId)
    {
        var grouped = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .GroupBy(n => n.Type)
            .Select(g => new
            {
                Type = g.Key,
                Total = g.Count(),
                Unread = g.Count(n => !n.IsRead)
            })
            .ToListAsync();

        var byCategory = Enum.GetValues<NotificationCategory>()
            .ToDictionary(
                category => category,
                category => new NotificationCategoryCount(
                    grouped.Where(g => NotificationDisplay.CategoryOf(g.Type) == category).Sum(g => g.Total),
                    grouped.Where(g => NotificationDisplay.CategoryOf(g.Type) == category).Sum(g => g.Unread)));

        return new NotificationCategoryCounts
        {
            Total = grouped.Sum(g => g.Total),
            Unread = grouped.Sum(g => g.Unread),
            ByCategory = byCategory
        };
    }

    private static NotificationViewModel ToViewModel(Notification notification, DateTime nowUtc)
    {
        var display = NotificationDisplay.Build(notification, nowUtc, NotificationDisplay.BusinessZone);
        return new NotificationViewModel
        {
            Id = display.Id,
            Type = notification.Type.ToString(),
            Title = display.Title,
            Message = display.Message,
            IsRead = display.IsRead,
            RelatedUrl = display.Url,
            IconClass = display.IconClass,
            CreatedAt = display.CreatedAtUtc,
            TimeAgo = display.TimeText,
            Category = display.Category,
            Tone = display.Tone,
            Url = display.Url,
            LinkLabel = display.LinkLabel,
            TimeText = display.TimeText,
            DayGroup = display.DayGroup,
            CreatedAtUtc = display.CreatedAtUtc
        };
    }

    public async Task<OperationResult> MarkAsReadAsync(int notificationId, int userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
            return OperationResult.NotFound("Không tìm thấy thông báo.");

        if (notification.IsRead)
            return OperationResult.Success();

        notification.IsRead = true;
        await _context.SaveChangesAsync();
        return OperationResult.Success();
    }

    public async Task<OperationResult> MarkAllAsReadAsync(int userId)
    {
        OperationResult result = await MarkAllAsReadAsync(userId, category: null);
        return result;
    }

    public async Task<OperationResult<int>> MarkAllAsReadAsync(int userId, NotificationCategory? category)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead);

        if (category is { } selected)
        {
            var types = NotificationDisplay.TypesOf(selected);
            query = query.Where(n => types.Contains(n.Type));
        }

        var updated = await query.ExecuteUpdateAsync(setters =>
            setters.SetProperty(n => n.IsRead, true));

        _logger.LogDebug(
            "MarkAllAsRead: user {UserId}, category {Category}, updated {Count} notifications",
            userId, category?.ToString() ?? "all", updated);

        return OperationResult<int>.Success(updated);
    }

    public async Task<OperationResult> CreateForUserAsync(
        int userId,
        NotificationType type,
        string title,
        string message,
        string? relatedUrl,
        string? iconClass,
        string eventKey)
    {
        title = title?.Trim() ?? string.Empty;
        message = message?.Trim() ?? string.Empty;
        eventKey = eventKey?.Trim() ?? string.Empty;

        if (userId <= 0)
            return OperationResult.Invalid("Người nhận không hợp lệ.");
        if (title.Length == 0)
            return OperationResult.Invalid("Tiêu đề không được để trống.");
        if (title.Length > TitleMaxLength)
            return OperationResult.Invalid($"Tiêu đề không được dài quá {TitleMaxLength} ký tự.");
        if (message.Length == 0)
            return OperationResult.Invalid("Nội dung không được để trống.");
        if (message.Length > MessageMaxLength)
            return OperationResult.Invalid($"Nội dung không được dài quá {MessageMaxLength} ký tự.");
        if (eventKey.Length == 0 || eventKey.Length > EventKeyMaxLength)
            return OperationResult.Invalid("Mã sự kiện thông báo không hợp lệ.");

        string? url = null;
        if (!string.IsNullOrWhiteSpace(relatedUrl))
        {
            if (!NotificationLinks.IsValidLocalPath(relatedUrl))
                return OperationResult.Invalid("Chỉ dùng đường dẫn bắt đầu bằng / trong TCT English.");

            url = NotificationLinks.ToSafeLocal(relatedUrl);
        }

        var outcome = await TryCreateWithEventKeyAsync(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            IconClass = NotificationDisplay.IsAllowedIcon(iconClass) ? iconClass!.Trim() : null,
            RelatedUrl = url,
            EventKey = eventKey,
            CreatedAt = _clock.UtcNow
        });

        return outcome == NotificationWriteOutcome.Failed
            ? OperationResult.Invalid("Không tạo được thông báo.")
            : OperationResult.Success();
    }

    public async Task GenerateStreakNotificationsAsync(int userId, StreakUpdateResult streakResult)
    {
        if (!streakResult.DidIncrease)
            return;

        var previousLongest = streakResult.PreviousLongestStreak;
        var currentStreak = streakResult.CurrentStreak;

        if (currentStreak <= previousLongest)
            return;

        var businessDate = _clock.BusinessDate;
        var eventKey = $"streak-record:{userId}:{businessDate:yyyy-MM-dd}:{currentStreak}";

        await TryCreateWithEventKeyAsync(new Notification
        {
            UserId = userId,
            Type = NotificationType.StreakRecord,
            Title = $"Kỷ lục mới: chuỗi {currentStreak} ngày học",
            Message = "Đây là chuỗi ngày học dài nhất của bạn.",
            IconClass = "fas fa-fire",
            RelatedUrl = "/Goals",
            EventKey = eventKey,
            CreatedAt = _clock.UtcNow
        });
    }

    public async Task GenerateGoalNotificationsAsync(int userId, GoalsActivityRecordResult activityResult)
    {
        if (activityResult.Status != OperationStatus.Success)
            return;

        var activeGoals = await _context.UserGoals
            .AsNoTracking()
            .Where(g => g.UserId == userId && g.IsActive && g.TargetValue > 0)
            .ToListAsync();

        if (activeGoals.Count == 0)
            return;

        var today = _clock.BusinessDate;
        var todayActivity = await _context.UserDailyActivities
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.ActivityDate == today)
            .Select(a => new
            {
                a.CardsReviewed,
                a.SpeakingCompletedCount,
                a.WritingCompletedCount,
                a.ReadingCompletedCount,
                a.ListeningCompletedCount
            })
            .FirstOrDefaultAsync();

        if (todayActivity == null)
            return;

        foreach (var goal in activeGoals)
        {
            var progressValue = goal.GoalArea switch
            {
                GoalArea.Vocabulary => todayActivity.CardsReviewed,
                GoalArea.Speaking => todayActivity.SpeakingCompletedCount,
                GoalArea.Writing => todayActivity.WritingCompletedCount,
                GoalArea.Reading => todayActivity.ReadingCompletedCount,
                GoalArea.Listening => todayActivity.ListeningCompletedCount,
                _ => 0
            };

            var percent = goal.TargetValue <= 0
                ? 0
                : (int)Math.Round(progressValue * 100d / goal.TargetValue);

            if (percent >= 100)
            {
                var eventKey = $"goal:{userId}:{goal.GoalArea}:{today:yyyy-MM-dd}:100";
                await TryCreateWithEventKeyAsync(new Notification
                {
                    UserId = userId,
                    Type = NotificationType.GoalCompleted,
                    Title = $"Hoàn thành mục tiêu {GetGoalAreaLabel(goal.GoalArea)}",
                    Message = $"Bạn đã hoàn thành mục tiêu {GetGoalAreaLabel(goal.GoalArea).ToLowerInvariant()} hôm nay. Giữ nhịp này nhé!",
                    IconClass = "fas fa-check-circle",
                    RelatedUrl = "/Goals",
                    EventKey = eventKey,
                    CreatedAt = _clock.UtcNow
                });
                continue;
            }

            if (percent >= 80)
            {
                var eventKey = $"goal:{userId}:{goal.GoalArea}:{today:yyyy-MM-dd}:80";
                await TryCreateWithEventKeyAsync(new Notification
                {
                    UserId = userId,
                    Type = NotificationType.GoalProgress,
                    Title = $"Sắp đạt mục tiêu {GetGoalAreaLabel(goal.GoalArea)}",
                    Message = $"Bạn đã đạt {percent}% mục tiêu {GetGoalAreaLabel(goal.GoalArea).ToLowerInvariant()} hôm nay, cố thêm chút nữa!",
                    IconClass = "fas fa-chart-line",
                    RelatedUrl = "/Goals",
                    EventKey = eventKey,
                    CreatedAt = _clock.UtcNow
                });
            }
        }
    }

    public async Task GenerateBadgeNotificationAsync(int userId, int badgeId)
    {
        var badge = await _context.Badges
            .AsNoTracking()
            .Where(b => b.Id == badgeId)
            .FirstOrDefaultAsync();

        if (badge == null)
        {
            _logger.LogWarning(
                "GenerateBadgeNotification: badge {BadgeId} not found for user {UserId}",
                badgeId, userId);
            return;
        }

        var eventKey = $"badge:{userId}:{badgeId}";
        await TryCreateWithEventKeyAsync(new Notification
        {
            UserId = userId,
            Type = NotificationType.BadgeEarned,
            Title = $"Huy hiệu mới: {badge.Name}",
            Message = $"Bạn vừa nhận huy hiệu \"{badge.Name}\".",
            IconClass = badge.IconClass,
            RelatedUrl = "/Goals#badges",
            EventKey = eventKey,
            CreatedAt = _clock.UtcNow
        });
    }

    public async Task<OperationResult> CreateAdminAnnouncementAsync(
        string title, string message, int adminUserId)
    {
        // Same rules as the batch send; this overload has no form to carry a batch id, so each call is its own batch.
        OperationResult<AdminAnnouncementOutcome> result = await SendAdminAnnouncementAsync(
            new AdminAnnouncementRequest(title, message, null, adminUserId, Guid.NewGuid()));
        return result;
    }

    public async Task<OperationResult<AdminAnnouncementOutcome>> SendAdminAnnouncementAsync(
        AdminAnnouncementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var title = request.Title?.Trim() ?? string.Empty;
        var message = request.Message?.Trim() ?? string.Empty;

        if (title.Length == 0)
            return OperationResult<AdminAnnouncementOutcome>.Invalid("Tiêu đề không được để trống.");
        if (title.Length > TitleMaxLength)
            return OperationResult<AdminAnnouncementOutcome>.Invalid($"Tiêu đề không được dài quá {TitleMaxLength} ký tự.");
        if (message.Length == 0)
            return OperationResult<AdminAnnouncementOutcome>.Invalid("Nội dung không được để trống.");
        if (message.Length > MessageMaxLength)
            return OperationResult<AdminAnnouncementOutcome>.Invalid($"Nội dung không được dài quá {MessageMaxLength} ký tự.");
        if (request.BatchId == Guid.Empty)
            return OperationResult<AdminAnnouncementOutcome>.Invalid(
                "Phiên gửi thông báo không hợp lệ. Vui lòng tải lại trang.");

        string? url = null;
        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            if (!NotificationLinks.IsValidLocalPath(request.Url))
            {
                const string linkError = "Chỉ dùng đường dẫn bắt đầu bằng / trong TCT English.";
                return OperationResult<AdminAnnouncementOutcome>.Invalid(
                    linkError, new Dictionary<string, string> { ["Url"] = linkError });
            }

            url = NotificationLinks.ToSafeLocal(request.Url);
        }

        var eventKey = AnnouncementKey(request.AdminUserId, request.BatchId);

        // The sender's own copy is part of every send, so it tells whether this batch was already written.
        var alreadyWritten = await _context.Notifications
            .AsNoTracking()
            .AnyAsync(n => n.UserId == request.AdminUserId && n.EventKey == eventKey);
        if (alreadyWritten)
            return await AlreadySentAsync(eventKey);

        var recipientIds = await _context.Users
            .AsNoTracking()
            .Where(u => u.Status != UserStatus.Blocked && u.AnonymizedAt == null)
            .OrderBy(u => u.UserId)
            .Select(u => u.UserId)
            .ToListAsync();

        if (recipientIds.Count == 0)
            return OperationResult<AdminAnnouncementOutcome>.Success(new AdminAnnouncementOutcome(0, false));

        var now = _clock.UtcNow;

        try
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                foreach (var chunk in recipientIds.Chunk(AnnouncementChunkSize))
                {
                    _context.Notifications.AddRange(chunk.Select(userId => new Notification
                    {
                        UserId = userId,
                        Type = NotificationType.AdminAnnouncement,
                        Title = title,
                        Message = message,
                        IconClass = "fa-solid fa-bullhorn",
                        RelatedUrl = url,
                        EventKey = eventKey,
                        CreatedAt = now
                    }));

                    await _context.SaveChangesAsync();
                    DetachAddedNotifications();
                }

                await transaction.CommitAsync();
            });
        }
        catch (DbUpdateException ex) when (TCTEnglish.Services.RelationalUniqueConflict.IsUnique(ex))
        {
            // Another request wrote this batch first; the whole transaction was rolled back.
            DetachAddedNotifications();
            _logger.LogInformation(
                "Admin {AdminUserId} announcement batch {BatchId} was already being sent; nothing new written",
                request.AdminUserId, request.BatchId);
            return await AlreadySentAsync(eventKey);
        }
        catch (DbUpdateException ex)
        {
            DetachAddedNotifications();
            _logger.LogError(ex,
                "Admin {AdminUserId} announcement batch {BatchId} failed and was rolled back",
                request.AdminUserId, request.BatchId);
            return OperationResult<AdminAnnouncementOutcome>.Invalid(
                "Gửi thông báo thất bại. Chưa có thông báo nào được gửi, vui lòng thử lại.");
        }

        // The notification text is not logged; the batch id identifies the send.
        _logger.LogInformation(
            "Admin {AdminUserId} sent announcement batch {BatchId} to {Count} users",
            request.AdminUserId, request.BatchId, recipientIds.Count);

        return OperationResult<AdminAnnouncementOutcome>.Success(
            new AdminAnnouncementOutcome(recipientIds.Count, false));
    }

    public async Task<AnnouncementHistoryPage> GetAnnouncementHistoryAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var announcements = _context.Notifications
            .AsNoTracking()
            .Where(n => n.Type == NotificationType.AdminAnnouncement);

        // A batch is every row that shares one announce key.
        var keyedBatches = announcements
            .Where(n => n.EventKey != null && n.EventKey.StartsWith(AnnouncementKeyPrefix))
            .GroupBy(n => n.EventKey!);

        // Older rows have no key: one send wrote every row with the same text and the same instant.
        var legacySends = announcements
            .Where(n => n.EventKey == null || !n.EventKey.StartsWith(AnnouncementKeyPrefix))
            .GroupBy(n => new { n.Title, n.Message, n.CreatedAt });

        var keyedTotal = await keyedBatches.CountAsync();
        var legacyTotal = await legacySends.CountAsync();
        var totalCount = keyedTotal + legacyTotal;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);

        // Keyed batches are newer than any legacy row, so they come first; the page window runs across both.
        // Ordering is applied to the group aggregates before the projection so the database does the paging.
        var skip = (page - 1) * pageSize;
        var rows = new List<GroupRow>();
        if (skip < keyedTotal)
        {
            rows.AddRange(await keyedBatches
                .OrderByDescending(g => g.Min(n => n.CreatedAt))
                .ThenBy(g => g.Key)
                .Skip(skip)
                .Take(pageSize)
                .Select(g => new GroupRow(
                    g.Key,
                    g.Min(n => n.Title)!,
                    g.Min(n => n.Message)!,
                    g.Min(n => n.RelatedUrl),
                    g.Min(n => n.CreatedAt),
                    g.Count(),
                    g.Count(n => n.IsRead)))
                .ToListAsync());
        }

        var remaining = pageSize - rows.Count;
        if (remaining > 0 && legacyTotal > 0)
        {
            rows.AddRange(await legacySends
                .OrderByDescending(g => g.Key.CreatedAt)
                .ThenBy(g => g.Key.Title)
                .ThenBy(g => g.Key.Message)
                .Skip(Math.Max(0, skip - keyedTotal))
                .Take(remaining)
                .Select(g => new GroupRow(
                    null,
                    g.Key.Title,
                    g.Key.Message,
                    g.Min(n => n.RelatedUrl),
                    g.Key.CreatedAt,
                    g.Count(),
                    g.Count(n => n.IsRead)))
                .ToListAsync());
        }

        var senderIds = rows
            .Select(r => ParseSenderId(r.Key))
            .Where(id => id != null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var senderNames = senderIds.Count == 0
            ? new Dictionary<int, string?>()
            : await _context.Users
                .AsNoTracking()
                .Where(u => senderIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

        var retentionCutoff = _clock.UtcNow.AddDays(-AnnouncementRetentionDays);
        var items = rows.Select(r =>
        {
            var senderId = ParseSenderId(r.Key);
            var name = senderId != null && senderNames.TryGetValue(senderId.Value, out var full)
                       && !string.IsNullOrWhiteSpace(full)
                ? full.Trim()
                : DefaultSenderName;
            var createdUtc = NotificationDisplay.AsUtc(r.CreatedAt);

            return new AnnouncementHistoryEntry(
                r.Title,
                r.Message,
                NotificationLinks.ToSafeLocal(r.Url),
                createdUtc,
                senderId,
                name,
                r.Recipients,
                r.ReadCount,
                IsLegacy: r.Key == null,
                CopiesMayBeCleaned: createdUtc < retentionCutoff);
        }).ToList();

        return new AnnouncementHistoryPage(items, page, totalPages, totalCount);
    }

    private sealed record GroupRow(
        string? Key,
        string Title,
        string Message,
        string? Url,
        DateTime CreatedAt,
        int Recipients,
        int ReadCount);

    private async Task<OperationResult<AdminAnnouncementOutcome>> AlreadySentAsync(string eventKey)
    {
        var existing = await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.EventKey == eventKey);
        return OperationResult<AdminAnnouncementOutcome>.Success(new AdminAnnouncementOutcome(existing, true));
    }

    private void DetachAddedNotifications()
    {
        foreach (var entry in _context.ChangeTracker.Entries<Notification>()
                     .Where(e => e.State == EntityState.Added)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private static string AnnouncementKey(int adminUserId, Guid batchId)
        => $"{AnnouncementKeyPrefix}{adminUserId}:{batchId:N}";

    private static int? ParseSenderId(string? eventKey)
    {
        if (eventKey == null || !eventKey.StartsWith(AnnouncementKeyPrefix, StringComparison.Ordinal))
            return null;

        var parts = eventKey.Split(':');
        return parts.Length >= 3 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
    }

    public async Task<int> CleanupOldNotificationsAsync(int daysToKeep = 30)
    {
        var cutoff = _clock.UtcNow.AddDays(-daysToKeep);

        var deleted = await _context.Notifications
            .Where(n => n.IsRead && n.CreatedAt < cutoff)
            .ExecuteDeleteAsync();

        if (deleted > 0)
        {
            _logger.LogInformation(
                "CleanupOldNotifications: deleted {Count} read notifications older than {DaysToKeep} days",
                deleted, daysToKeep);
        }

        return deleted;
    }

    private enum NotificationWriteOutcome
    {
        Created,
        Duplicate,
        Failed
    }

    private async Task<NotificationWriteOutcome> TryCreateWithEventKeyAsync(Notification notification)
    {
        if (string.IsNullOrWhiteSpace(notification.EventKey))
        {
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            return NotificationWriteOutcome.Created;
        }

        var exists = await _context.Notifications
            .AsNoTracking()
            .AnyAsync(n => n.UserId == notification.UserId && n.EventKey == notification.EventKey);
        if (exists)
            return NotificationWriteOutcome.Duplicate;

        _context.Notifications.Add(notification);
        try
        {
            await _context.SaveChangesAsync();
            return NotificationWriteOutcome.Created;
        }
        catch (DbUpdateException ex)
        {
            _context.Entry(notification).State = EntityState.Detached;

            if (TCTEnglish.Services.RelationalUniqueConflict.IsUnique(ex))
            {
                // Another request wrote the same event first: the (UserId, EventKey) index did its job.
                _logger.LogDebug(ex,
                    "Duplicate notification event ignored for user {UserId}, key {EventKey}",
                    notification.UserId,
                    notification.EventKey);
                return NotificationWriteOutcome.Duplicate;
            }

            // A real write failure must not look like a duplicate, but callers (Goals, billing)
            // must keep working: log it without the notification text and carry on.
            _logger.LogWarning(ex,
                "Notification write failed for user {UserId}, type {NotificationType}",
                notification.UserId,
                notification.Type);
            return NotificationWriteOutcome.Failed;
        }
    }

    private static string GetGoalAreaLabel(GoalArea area) => area switch
    {
        GoalArea.Vocabulary => "Từ vựng",
        GoalArea.Speaking => "Nói",
        GoalArea.Writing => "Viết",
        GoalArea.Reading => "Đọc",
        GoalArea.Listening => "Nghe",
        _ => area.ToString()
    };
}
