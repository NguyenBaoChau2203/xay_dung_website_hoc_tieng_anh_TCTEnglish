using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Services.Notifications;
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
    private readonly ILogger<NotificationManagementController> _logger;

    public NotificationManagementController(
        INotificationService notificationService,
        ILogger<NotificationManagementController> logger)
    {
        _notificationService = notificationService;
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
        return View(new CreateAnnouncementViewModel { BatchId = Guid.NewGuid() });
    }

    // POST /Admin/NotificationManagement/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAnnouncementViewModel model)
    {
        ViewData["Title"] = "Gửi thông báo hệ thống";

        // The batch id is what makes a double submit harmless; a missing one gets a fresh form.
        if (model.BatchId == Guid.Empty)
        {
            ModelState.Remove(nameof(CreateAnnouncementViewModel.BatchId));
            model.BatchId = Guid.NewGuid();
            ModelState.AddModelError(string.Empty, "Phiên gửi thông báo không hợp lệ. Vui lòng kiểm tra nội dung và gửi lại.");
            return View(model);
        }

        if (!ModelState.IsValid)
            return View(model);

        var adminUserId = GetCurrentUserId();
        var result = await _notificationService.SendAdminAnnouncementAsync(
            new AdminAnnouncementRequest(model.Title, model.Message, model.Url, adminUserId, model.BatchId));

        if (!result.IsSuccess)
        {
            var message = result.ErrorMessage ?? "Gửi thông báo thất bại.";
            if (result.FieldErrors is { Count: > 0 } fields)
            {
                foreach (var (field, error) in fields)
                    ModelState.AddModelError(field, error);
            }
            else
            {
                ModelState.AddModelError(string.Empty, message);
            }

            return View(model);
        }

        var outcome = result.Value!;
        _logger.LogInformation(
            "Admin {AdminId} announcement batch {BatchId}: {Recipients} recipients, already sent: {AlreadySent}",
            adminUserId, model.BatchId, outcome.Recipients, outcome.AlreadySent);

        TempData["Success"] = outcome.AlreadySent
            ? "Thông báo này đã được gửi trước đó."
            : $"Đã gửi thông báo tới {outcome.Recipients} người dùng.";
        return RedirectToAction(nameof(History));
    }

    // =========================================================================
    // History
    // =========================================================================

    // GET /Admin/NotificationManagement/History?page=1
    public async Task<IActionResult> History(int page = 1)
    {
        ViewData["Title"] = "Lịch sử thông báo";

        var history = await _notificationService.GetAnnouncementHistoryAsync(page, PageSize);

        return View(new AnnouncementHistoryViewModel
        {
            Items = history.Items.Select(item => new AnnouncementItemViewModel
            {
                Title = item.Title,
                Message = item.Message,
                CreatedAt = item.CreatedAtUtc,
                CreatedAtText = TimeZoneInfo.ConvertTimeFromUtc(item.CreatedAtUtc, NotificationDisplay.BusinessZone)
                    .ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                CreatedByName = item.SenderName,
                RecipientCount = item.Recipients,
                ReadCount = item.ReadCount,
                Url = item.Url,
                IsLegacy = item.IsLegacy,
                CopiesMayBeCleaned = item.CopiesMayBeCleaned
            }).ToList(),
            CurrentPage = history.Page,
            TotalPages = history.TotalPages
        });
    }
}
