using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    // GET /Notification?page=1&filter=unread
    public async Task<IActionResult> Index(int page = 1, string? filter = null)
    {
        page = Math.Max(1, page);
        var userId = GetCurrentUserId();

        var allItems    = await _notificationService.GetNotificationsAsync(userId, page: 1, pageSize: 500);
        var unreadCount = allItems.Count(n => !n.IsRead);

        IEnumerable<NotificationViewModel> filtered =
            string.Equals(filter, "unread", StringComparison.OrdinalIgnoreCase)
                ? allItems.Where(n => !n.IsRead)
                : allItems;

        var filteredList = filtered.ToList();
        var totalPages   = (int)Math.Ceiling(filteredList.Count / (double)PageSize);
        var pageItems    = filteredList
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        ViewData["Filter"]      = filter ?? "all";
        ViewData["UnreadCount"] = unreadCount;

        var viewModel = new NotificationListViewModel
        {
            Items       = pageItems,
            UnreadCount = unreadCount,
            CurrentPage = page,
            TotalPages  = Math.Max(1, totalPages),
            HasMore     = page < totalPages
        };

        return View(viewModel);
    }

    // GET /api/notifications/unread-count
    // → { count: 5 }
    [HttpGet("api/notifications/unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetCurrentUserId();
        var count  = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(new { count });
    }

    // GET /api/notifications?page=1&pageSize=10
    // → NotificationListViewModel as JSON
    [HttpGet("api/notifications")]
    public async Task<IActionResult> GetNotifications(int page = 1, int pageSize = 10)
    {
        // Clamp pageSize — never let client request 10 000 rows
        pageSize = Math.Clamp(pageSize, 1, 50);
        page     = Math.Max(1, page);

        var userId        = GetCurrentUserId();
        var items         = await _notificationService.GetNotificationsAsync(userId, page, pageSize);
        var unreadCount   = await _notificationService.GetUnreadCountAsync(userId);

        // We need a rough total to compute HasMore — use a sentinel approach:
        // If we received exactly <pageSize> items there MAY be more.
        var hasMore = items.Count == pageSize;

        return Ok(new
        {
            success     = true,
            data = new
            {
                items,
                unreadCount,
                currentPage  = page,
                hasMore
            }
        });
    }

    // POST /api/notifications/{id}/mark-read
    // → { success: true }
    [HttpPost("api/notifications/{id}/mark-read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationService.MarkAsReadAsync(id, userId);

        if (result.Status == OperationStatus.NotFound)
            return NotFound(new { error = "Không tìm thấy thông báo." });

        return Ok(new { success = true });
    }

    // POST /api/notifications/mark-all-read
    // → { success: true }
    [HttpPost("api/notifications/mark-all-read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetCurrentUserId();
        await _notificationService.MarkAllAsReadAsync(userId);
        return Ok(new { success = true });
    }
}
