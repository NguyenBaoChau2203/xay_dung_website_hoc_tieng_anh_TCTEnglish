using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Areas.Admin.ViewModels;
using TCTVocabulary.Controllers;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTVocabulary.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class NotificationManagementController : BaseController
{
    private const int PageSize = 20;

    private readonly INotificationService _notificationService;
    private readonly DbflashcardContext _context;
    private readonly ILogger<NotificationManagementController> _logger;

    public NotificationManagementController(
        INotificationService notificationService,
        DbflashcardContext context,
        ILogger<NotificationManagementController> logger)
    {
        _notificationService = notificationService;
        _context = context;
        _logger = logger;
    }

    // GET /Admin/NotificationManagement
    public IActionResult Index() => RedirectToAction(nameof(Create));

    // =========================================================================
    // Create announcement
    // =========================================================================

    // GET /Admin/NotificationManagement/Create
    public IActionResult Create()
    {
        ViewData["Title"] = "Gửi thông báo hệ thống";
        return View(new CreateAnnouncementViewModel());
    }

    // POST /Admin/NotificationManagement/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAnnouncementViewModel model)
    {
        ViewData["Title"] = "Gửi thông báo hệ thống";

        if (!ModelState.IsValid)
            return View(model);

        var adminUserId = GetCurrentUserId();
        var result = await _notificationService.CreateAdminAnnouncementAsync(
            model.Title, model.Message, adminUserId);

        if (result.Status != OperationStatus.Success)
        {
            ModelState.AddModelError(string.Empty,
                result.ErrorMessage ?? "Gửi thông báo thất bại.");
            return View(model);
        }

        // Count recipients for TempData message
        var recipientCount = await _context.Users
            .AsNoTracking()
            .CountAsync(u => u.Status != UserStatus.Blocked);

        _logger.LogInformation(
            "Admin {AdminId} sent announcement '{Title}' to {Count} users",
            adminUserId, model.Title, recipientCount);

        TempData["Success"] = $"Đã gửi thông báo tới {recipientCount} người dùng.";
        return RedirectToAction(nameof(History));
    }

    // =========================================================================
    // History
    // =========================================================================

    // GET /Admin/NotificationManagement/History?page=1
    public async Task<IActionResult> History(int page = 1)
    {
        ViewData["Title"] = "Lịch sử thông báo";

        page = Math.Max(1, page);

        // Group AdminAnnouncement notifications by (Title, Message + minute-truncated CreatedAt)
        // to reconstruct unique broadcast events.
        // Strategy: get one row per "broadcast minute" per unique (Title, Message) pair.
        var allAdminNotifs = await _context.Set<Notification>()
            .AsNoTracking()
            .Where(n => n.Type == NotificationType.AdminAnnouncement)
            .Select(n => new
            {
                n.Title,
                n.Message,
                n.UserId,
                n.CreatedAt
            })
            .ToListAsync();

        // Group into broadcast events (same title+message within same UTC minute)
        var grouped = allAdminNotifs
            .GroupBy(n => new
            {
                n.Title,
                n.Message,
                MinuteBucket = new DateTime(
                    n.CreatedAt.Year, n.CreatedAt.Month, n.CreatedAt.Day,
                    n.CreatedAt.Hour, n.CreatedAt.Minute, 0, DateTimeKind.Utc)
            })
            .Select(g => new
            {
                g.Key.Title,
                g.Key.Message,
                CreatedAt = g.Key.MinuteBucket,
                RecipientCount = g.Count()
            })
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        var totalPages = (int)Math.Ceiling(grouped.Count / (double)PageSize);

        var pageItems = grouped
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        var items = pageItems.Select(g => new AnnouncementItemViewModel
        {
            Title          = g.Title,
            Message        = g.Message,
            CreatedAt      = g.CreatedAt,
            CreatedByName  = "Admin",   // simplified — sender not stored per notification
            RecipientCount = g.RecipientCount
        }).ToList();

        var viewModel = new AnnouncementHistoryViewModel
        {
            Items       = items,
            CurrentPage = page,
            TotalPages  = Math.Max(1, totalPages)
        };

        return View(viewModel);
    }
}
