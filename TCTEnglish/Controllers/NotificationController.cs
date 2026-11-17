using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Services.Notifications;
using TCTVocabulary.Services;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Controllers;

[Authorize]
public class NotificationController : BaseController
{
    private const int PageSize = 20;

    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(
        INotificationService notificationService,
        ILogger<NotificationController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    // GET /Notification?filter=all|unread&category=learning|badge|payment|system&page=1
    // Also served at /Notification/Index (sidebar link and the website guide use that path).
    [HttpGet("Notification")]
    [HttpGet("Notification/Index")]
    public async Task<IActionResult> Index(int page = 1, string? filter = null, string? category = null)
    {
        var userId = GetCurrentUserId();
        var unreadOnly = string.Equals(filter, "unread", StringComparison.OrdinalIgnoreCase);
        // An unknown category is ignored (everything is shown), never echoed back.
        var selectedCategory = NotificationDisplay.ParseCategory(category);

        var viewModel = await _notificationService.GetNotificationsPageAsync(
            userId,
            page,
            PageSize,
            unreadOnly,
            selectedCategory);

        // The service clamps the page; a link beyond the last page goes to the last page with the same filters.
        if (page > viewModel.CurrentPage)
        {
            return RedirectToAction(nameof(Index), new
            {
                page = viewModel.CurrentPage,
                filter = unreadOnly ? "unread" : null,
                category = selectedCategory?.ToString().ToLowerInvariant()
            });
        }

        ViewData["Filter"] = unreadOnly ? "unread" : "all";
        ViewData["Category"] = selectedCategory?.ToString().ToLowerInvariant();
        ViewData["UnreadCount"] = viewModel.UnreadCount;

        return View(viewModel);
    }

    [HttpGet("api/notifications/unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetCurrentUserId();
        var count = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(new { count });
    }

    // GET api/notifications?page=1&pageSize=10&unreadOnly=false&category=badge
    // GET api/notifications?pageSize=10&before=<ISO-8601 UTC>&beforeId=<id>   (keyset: no duplicates or gaps)
    [HttpGet("api/notifications")]
    public async Task<IActionResult> GetNotifications(
        int page = 1,
        int pageSize = 10,
        bool unreadOnly = false,
        string? category = null,
        string? before = null,
        int? beforeId = null)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        page = Math.Max(1, page);

        NotificationCategory? selectedCategory = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            selectedCategory = NotificationDisplay.ParseCategory(category);
            if (selectedCategory == null)
                return BadRequest(new { error = "Loại thông báo không hợp lệ." });
        }

        DateTime? beforeUtc = null;
        if (!string.IsNullOrWhiteSpace(before))
        {
            if (!DateTime.TryParse(
                    before,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var parsed))
                return BadRequest(new { error = "Mốc thời gian không hợp lệ." });

            beforeUtc = parsed;
        }

        if (ModelState.TryGetValue(nameof(beforeId), out var beforeIdState) && beforeIdState.Errors.Count > 0)
            return BadRequest(new { error = "Mốc thông báo không hợp lệ." });

        var userId = GetCurrentUserId();

        if (beforeUtc != null)
        {
            var slice = await _notificationService.GetNotificationsSliceAsync(
                userId, pageSize, unreadOnly, beforeUtc, beforeId, selectedCategory);

            return Ok(new
            {
                success = true,
                data = new
                {
                    items = slice.Items,
                    unreadCount = slice.UnreadCount,
                    totalCount = slice.TotalCount,
                    currentPage = 0,
                    hasMore = slice.HasMore,
                    nextCursor = slice.NextCursor
                }
            });
        }

        var pageModel = await _notificationService.GetNotificationsPageAsync(
            userId, page, pageSize, unreadOnly, selectedCategory);

        var last = pageModel.Items.Count > 0 ? pageModel.Items[^1] : null;

        return Ok(new
        {
            success = true,
            data = new
            {
                items = pageModel.Items,
                unreadCount = pageModel.UnreadCount,
                totalCount = pageModel.TotalCount,
                currentPage = pageModel.CurrentPage,
                hasMore = pageModel.HasMore,
                nextCursor = pageModel.HasMore && last != null
                    ? new NotificationCursor(last.CreatedAtUtc, last.Id)
                    : null
            }
        });
    }

    [HttpPost("api/notifications/{id}/mark-read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationService.MarkAsReadAsync(id, userId);

        if (result.Status == OperationStatus.NotFound)
            return NotFound(new { error = "Không tìm thấy thông báo." });

        var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(new { success = true, unreadCount });
    }

    // POST api/notifications/mark-all-read[?category=badge]
    [HttpPost("api/notifications/mark-all-read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead([FromQuery] string? category = null)
    {
        NotificationCategory? selectedCategory = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            selectedCategory = NotificationDisplay.ParseCategory(category);
            // Never widen an unknown category into "mark everything".
            if (selectedCategory == null)
                return BadRequest(new { error = "Loại thông báo không hợp lệ." });
        }

        var userId = GetCurrentUserId();
        var result = await _notificationService.MarkAllAsReadAsync(userId, selectedCategory);
        var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

        return Ok(new { success = true, updated = result.Value, unreadCount });
    }
}
