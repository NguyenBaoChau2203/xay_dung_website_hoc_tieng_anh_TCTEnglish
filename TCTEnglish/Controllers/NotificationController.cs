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
        var userId = GetCurrentUserId();
        var unreadOnly = string.Equals(filter, "unread", StringComparison.OrdinalIgnoreCase);

        var viewModel = await _notificationService.GetNotificationsPageAsync(
            userId,
            page,
            PageSize,
            unreadOnly);

        ViewData["Filter"] = filter ?? "all";
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

    [HttpGet("api/notifications")]
    public async Task<IActionResult> GetNotifications(int page = 1, int pageSize = 10, bool unreadOnly = false)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        page = Math.Max(1, page);

        var userId = GetCurrentUserId();
        var pageModel = await _notificationService.GetNotificationsPageAsync(
            userId, page, pageSize, unreadOnly);

        return Ok(new
        {
            success = true,
            data = new
            {
                items = pageModel.Items,
                unreadCount = pageModel.UnreadCount,
                totalCount = pageModel.TotalCount,
                currentPage = pageModel.CurrentPage,
                hasMore = pageModel.HasMore
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

        return Ok(new { success = true });
    }

    [HttpPost("api/notifications/mark-all-read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetCurrentUserId();
        await _notificationService.MarkAllAsReadAsync(userId);
        return Ok(new { success = true });
    }
}
