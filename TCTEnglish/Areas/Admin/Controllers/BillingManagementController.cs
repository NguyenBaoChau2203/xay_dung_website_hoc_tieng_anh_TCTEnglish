using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.Billing;
using TCTEnglish.ViewModels.Billing;
using TCTVocabulary.Areas.Admin.ViewModels.Billing;
using TCTVocabulary.Controllers;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTVocabulary.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class BillingManagementController : BaseController
{
    private const int PageSize = 20;
    private const int ExpiringSoonDays = 7;

    private static readonly string[] OrderStatuses =
    {
        PaymentOrderStatuses.Pending,
        PaymentOrderStatuses.Paid,
        PaymentOrderStatuses.Failed,
        PaymentOrderStatuses.Cancelled,
        PaymentOrderStatuses.Expired,
        PaymentOrderStatuses.Refunded,
        PaymentOrderStatuses.PartiallyRefunded,
        PaymentOrderStatuses.ManualReview
    };

    private static readonly string[] SubscriptionStatusFilters =
    {
        SubscriptionStatuses.Active,
        SubscriptionStatuses.Expired,
        SubscriptionStatuses.Cancelled,
        SubscriptionStatuses.Revoked
    };

    private static readonly string[] Providers =
    {
        PaymentProviders.VNPay,
        PaymentProviders.MoMo
    };

    private readonly DbflashcardContext _context;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IPaymentProviderHealthService _healthService;
    private readonly IPaymentAuditService _auditService;
    private readonly ILogger<BillingManagementController> _logger;

    public BillingManagementController(
        DbflashcardContext context,
        ISubscriptionService subscriptionService,
        IPaymentProviderHealthService healthService,
        IPaymentAuditService auditService,
        ILogger<BillingManagementController> logger)
    {
        _context = context;
        _subscriptionService = subscriptionService;
        _healthService = healthService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Admin: Payment provider health dashboard — /Admin/BillingManagement/Health
    /// Shows config readiness for VNPay, MoMo, Bank Transfer and billing workers.
    /// NEVER exposes secret values — only Present / Missing indicators.
    /// </summary>
    [HttpGet]
    public IActionResult Health()
    {
        var model = new PaymentHealthViewModel
        {
            Providers = _healthService.GetProviderHealthStatus(),
            Workers   = _healthService.GetWorkerHealthStatus()
        };

        return View(model);
    }

    public async Task<IActionResult> Index(
        string? provider,
        string? status,
        string? q,
        int page = 1)
    {
        provider = NormalizeFilter(provider);
        status = NormalizeFilter(status);
        q = NormalizeFilter(q);

        var query = _context.PaymentOrders
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(o => o.Provider == provider);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = ApplyOrderSearch(query, q);
        }

        var totalFilteredCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalFilteredCount / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var rawOrders = await query
            .Include(o => o.User)
            .Include(o => o.Plan)
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var orders = rawOrders.Select(o => new AdminPaymentOrderRowViewModel
        {
            Id = o.Id,
            OrderCode = o.OrderCode,
            UserId = o.UserId,
            UserEmail = o.User?.Email ?? "N/A",
            UserDisplayName = o.User?.FullName ?? "N/A",
            PlanName = o.Plan?.Name ?? "N/A",
            Provider = o.Provider,
            AmountVnd = o.AmountVnd,
            Currency = o.Currency,
            Status = o.Status,
            CreatedAtUtc = o.CreatedAtUtc,
            ExpiresAtUtc = o.ExpiresAtUtc,
            PaidAtUtc = o.PaidAtUtc,
            ProviderTransactionId = o.ProviderTransactionId
        }).ToList();

        return View(new AdminBillingIndexViewModel
        {
            Orders = orders,
            ProviderFilter = provider,
            StatusFilter = status,
            SearchQuery = q,
            CurrentPage = page,
            TotalPages = totalPages,
            PageSize = PageSize,
            TotalFilteredCount = totalFilteredCount,
            Providers = Providers,
            Statuses = OrderStatuses
        });
    }

    public async Task<IActionResult> Details(long id)
    {
        var order = await _context.PaymentOrders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new AdminPaymentOrderDetailsViewModel
            {
                Id = o.Id,
                OrderCode = o.OrderCode,
                UserId = o.UserId,
                UserEmail = o.User.Email,
                UserDisplayName = o.User.FullName ?? "N/A",
                PlanCode = o.Plan.Code,
                PlanName = o.Plan.Name,
                PlanDurationDays = o.Plan.DurationDays,
                Provider = o.Provider,
                AmountVnd = o.AmountVnd,
                Currency = o.Currency,
                Status = o.Status,
                CreatedAtUtc = o.CreatedAtUtc,
                ExpiresAtUtc = o.ExpiresAtUtc,
                PaidAtUtc = o.PaidAtUtc,
                ProviderTransactionId = o.ProviderTransactionId,
                ProviderResponseCode = o.ProviderResponseCode,
                ProviderTransactionStatus = o.ProviderTransactionStatus,
                FailureMessage = o.FailureMessage,
                ReturnPayloadJson = o.ReturnPayloadJson,
                IpnPayloadJson = o.IpnPayloadJson
            })
            .FirstOrDefaultAsync();

        if (order == null)
        {
            return NotFound();
        }

        order.Events = await _context.PaymentEvents
            .AsNoTracking()
            .Where(e => e.PaymentOrderId == id)
            .OrderByDescending(e => e.ReceivedAtUtc)
            .Select(e => new AdminPaymentEventViewModel
            {
                Id = e.Id,
                Provider = e.Provider,
                EventKey = e.EventKey,
                SignatureValid = e.SignatureValid,
                PayloadJson = e.PayloadJson,
                ProcessingStatus = e.ProcessingStatus,
                ProcessingMessage = e.ProcessingMessage,
                ReceivedAtUtc = e.ReceivedAtUtc,
                ProcessedAtUtc = e.ProcessedAtUtc
            })
            .ToListAsync();

        order.AuditHistory = await _context.PaymentAdminActions
            .AsNoTracking()
            .Where(a => a.PaymentOrderId == id)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new AdminAuditActionViewModel
            {
                Id              = a.Id,
                ActionType      = a.ActionType,
                Reason          = a.Reason,
                OldStatus       = a.OldStatus,
                NewStatus       = a.NewStatus,
                AdminEmail      = a.AdminUser != null ? a.AdminUser.Email : "N/A",
                AdminDisplayName = a.AdminUser != null ? (a.AdminUser.FullName ?? a.AdminUser.Email) : "N/A",
                CreatedAtUtc    = a.CreatedAtUtc,
                IpAddress       = a.IpAddress
            })
            .ToListAsync();

        order.ManualReviewRequest = new AdminMarkManualReviewRequest
        {
            PaymentOrderId = id
        };

        return View(order);
    }

    public async Task<IActionResult> Subscriptions(
        string? status,
        string? q,
        int page = 1)
    {
        status = NormalizeFilter(status);
        q = NormalizeFilter(q);

        var query = _context.UserSubscriptions
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(s => s.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = ApplySubscriptionSearch(query, q);
        }

        var totalFilteredCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalFilteredCount / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var subscriptions = await query
            .OrderByDescending(s => s.EndsAtUtc)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(s => new AdminSubscriptionRowViewModel
            {
                Id = s.Id,
                UserId = s.UserId,
                UserEmail = s.User.Email,
                UserDisplayName = s.User.FullName ?? "N/A",
                PlanName = s.Plan.Name,
                PlanCode = s.Plan.Code,
                Status = s.Status,
                StartsAtUtc = s.StartsAtUtc,
                EndsAtUtc = s.EndsAtUtc,
                CreatedAtUtc = s.CreatedAtUtc,
                CancelledAtUtc = s.CancelledAtUtc,
                CancelReason = s.CancelReason,
                ActivatedByOrderCode = s.ActivatedByPaymentOrder == null
                    ? null
                    : s.ActivatedByPaymentOrder.OrderCode
            })
            .ToListAsync();

        var plans = await _context.PremiumPlans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new AdminPremiumPlanOptionViewModel
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                DurationDays = p.DurationDays,
                PriceVnd = p.PriceVnd
            })
            .ToListAsync();

        return View(new AdminSubscriptionsViewModel
        {
            Subscriptions = subscriptions,
            Plans = plans,
            StatusFilter = status,
            SearchQuery = q,
            CurrentPage = page,
            TotalPages = totalPages,
            PageSize = PageSize,
            TotalFilteredCount = totalFilteredCount,
            Statuses = SubscriptionStatusFilters
        });
    }

    [HttpGet]
    public async Task<IActionResult> PremiumUsers(
        string? filter,
        string? q,
        int page = 1)
    {
        filter = NormalizePremiumFilter(filter);
        q = NormalizeFilter(q);

        var allRows = await BuildPremiumUserRowsAsync();
        var filteredRows = ApplyPremiumUserFilter(allRows, filter);
        filteredRows = ApplyPremiumUserSearch(filteredRows, q);

        var totalFilteredCount = filteredRows.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalFilteredCount / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var pageRows = filteredRows
            .OrderByDescending(r => r.IsActiveSubscription)
            .ThenBy(r => r.DaysRemaining ?? int.MaxValue)
            .ThenBy(r => r.UserId)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        var plans = await _context.PremiumPlans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new AdminPremiumPlanOptionViewModel
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                DurationDays = p.DurationDays,
                PriceVnd = p.PriceVnd
            })
            .ToListAsync();

        var nowUtc = DateTime.UtcNow;
        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = monthStart.AddMonths(1);
        var revenueThisMonth = await _context.PaymentOrders
            .AsNoTracking()
            .Where(o => o.Status == PaymentOrderStatuses.Paid
                     && o.PaidAtUtc.HasValue
                     && o.PaidAtUtc.Value >= monthStart
                     && o.PaidAtUtc.Value < nextMonthStart)
            .SumAsync(o => (decimal?)o.AmountVnd) ?? 0m;

        var model = new AdminPremiumUsersViewModel
        {
            Dashboard = new AdminPremiumDashboardCardsViewModel
            {
                ActivePremiumCount = allRows.Count(r => r.IsActiveSubscription),
                StandardCount = allRows.Count(r => !r.IsActiveSubscription),
                ExpiringSoonCount = allRows.Count(r => r.IsActiveSubscription
                    && r.DaysRemaining.HasValue
                    && r.DaysRemaining.Value <= ExpiringSoonDays),
                ExpiredCount = allRows.Count(r => !r.IsActiveSubscription
                    && string.Equals(r.SubscriptionStatus, SubscriptionStatuses.Expired, StringComparison.OrdinalIgnoreCase)),
                RevenueThisMonthVnd = revenueThisMonth
            },
            Users = pageRows,
            Filter = filter ?? PremiumUserFilters.All,
            SearchQuery = q,
            CurrentPage = page,
            TotalPages = totalPages,
            PageSize = PageSize,
            TotalFilteredCount = totalFilteredCount,
            Filters = PremiumUserFilters.Values,
            Plans = plans
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> PremiumUserDetails(int userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new
            {
                u.UserId,
                u.Email,
                u.FullName,
                u.Role
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound();
        }

        var allRows = await BuildPremiumUserRowsAsync();
        var row = allRows.FirstOrDefault(r => r.UserId == userId);

        var subscriptionHistory = await _context.UserSubscriptions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new AdminSubscriptionHistoryItemViewModel
            {
                Id = s.Id,
                PlanName = s.Plan.Name,
                Status = s.Status,
                StartsAtUtc = s.StartsAtUtc,
                EndsAtUtc = s.EndsAtUtc,
                CreatedAtUtc = s.CreatedAtUtc,
                CancelledAtUtc = s.CancelledAtUtc,
                CancelReason = s.CancelReason,
                ActivatedByOrderCode = s.ActivatedByPaymentOrder == null
                    ? null
                    : s.ActivatedByPaymentOrder.OrderCode,
                Provider = s.ActivatedByPaymentOrder == null
                    ? null
                    : s.ActivatedByPaymentOrder.Provider
            })
            .ToListAsync();

        var activatedOrderIds = await _context.UserSubscriptions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.ActivatedByPaymentOrderId.HasValue)
            .Select(s => s.ActivatedByPaymentOrderId!.Value)
            .Distinct()
            .ToListAsync();
        var activatedOrderIdSet = activatedOrderIds.ToHashSet();

        var paymentOrders = await _context.PaymentOrders
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(o => new AdminPaymentOrderHistoryItemViewModel
            {
                Id = o.Id,
                OrderCode = o.OrderCode,
                Provider = o.Provider,
                AmountVnd = o.AmountVnd,
                Status = o.Status,
                CreatedAtUtc = o.CreatedAtUtc,
                PaidAtUtc = o.PaidAtUtc,
                IsPaidButNotActivated = o.Status == PaymentOrderStatuses.Paid && !activatedOrderIdSet.Contains(o.Id)
            })
            .ToListAsync();

        var model = new AdminPremiumUserDetailsViewModel
        {
            UserId = user.UserId,
            Email = user.Email,
            FullName = user.FullName ?? "N/A",
            AccountType = Roles.Normalize(user.Role),
            CurrentSubscription = row,
            SubscriptionHistory = subscriptionHistory,
            PaymentOrders = paymentOrders,
            RolePremiumWithoutActiveSubscription = row?.RolePremiumWithoutActiveSubscription ?? false,
            ActiveSubscriptionButRoleNotPremium = row?.ActiveSubscriptionButRoleNotPremium ?? false,
            PaidOrderNotActivated = row?.PaidOrderNotActivated ?? false
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Grant(AdminGrantPremiumRequest model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = GetModelErrorMessage();
            return RedirectToAction(nameof(Subscriptions));
        }

        var targetUserId = await ResolveUserIdAsync(model.UserLookup);
        if (!targetUserId.HasValue)
        {
            TempData["Error"] = "Không tìm thấy người dùng theo email hoặc ID đã nhập.";
            return RedirectToAction(nameof(Subscriptions));
        }

        var result = await _subscriptionService.GrantManualAsync(
            targetUserId.Value,
            model.PlanId,
            model.DurationDays,
            model.Reason);

        if (result.Status != OperationStatus.Success)
        {
            TempData["Error"] = result.ErrorMessage ?? "Không thể cấp Premium.";
            return RedirectToAction(nameof(Subscriptions));
        }

        var adminId = GetCurrentUserId();
        _logger.LogInformation(
            "Admin {AdminId} granted Premium to User {UserId} with Plan {PlanId}, DurationDays {DurationDays}. Reason: {Reason}",
            adminId, targetUserId.Value, model.PlanId, model.DurationDays, model.Reason);

        await _auditService.RecordAsync(
            adminUserId:    adminId,
            actionType:     AdminActionTypes.GrantPremium,
            reason:         model.Reason,
            payloadJson:    System.Text.Json.JsonSerializer.Serialize(new
            {
                targetUserId = targetUserId.Value,
                planId       = model.PlanId,
                durationDays = model.DurationDays
            }),
            newStatus:      "active",
            ipAddress:      HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent:      HttpContext.Request.Headers["User-Agent"].ToString());

        TempData["Success"] = "Đã cấp Premium thủ công thành công.";
        return RedirectToAction(nameof(Subscriptions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(AdminRevokePremiumRequest model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = GetModelErrorMessage();
            return RedirectToAction(nameof(Subscriptions));
        }

        var result = await _subscriptionService.RevokeAsync(model.UserId, model.Reason);
        if (result.Status != OperationStatus.Success)
        {
            TempData["Error"] = result.ErrorMessage ?? "Không thể thu hồi Premium.";
            return RedirectToAction(nameof(Subscriptions));
        }

        var adminId = GetCurrentUserId();
        _logger.LogInformation(
            "Admin {AdminId} revoked Premium for User {UserId}. Reason: {Reason}",
            adminId, model.UserId, model.Reason);

        await _auditService.RecordAsync(
            adminUserId:    adminId,
            actionType:     AdminActionTypes.RevokePremium,
            reason:         model.Reason,
            payloadJson:    System.Text.Json.JsonSerializer.Serialize(new { targetUserId = model.UserId }),
            newStatus:      SubscriptionStatuses.Revoked,
            ipAddress:      HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent:      HttpContext.Request.Headers["User-Agent"].ToString());

        TempData["Success"] = "Đã thu hồi Premium thành công.";
        return RedirectToAction(nameof(Subscriptions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkManualReview(AdminMarkManualReviewRequest model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = GetModelErrorMessage();
            return RedirectToAction(nameof(Details), new { id = model.PaymentOrderId });
        }

        var order = await _context.PaymentOrders
            .FirstOrDefaultAsync(o => o.Id == model.PaymentOrderId);

        if (order == null)
            return NotFound();

        var oldStatus = order.Status;
        order.Status = PaymentOrderStatuses.ManualReview;
        await _context.SaveChangesAsync();

        var adminId = GetCurrentUserId();
        _logger.LogWarning(
            "Admin {AdminId} marked PaymentOrder {OrderId} as ManualReview. Reason: {Reason}",
            adminId, order.Id, model.Reason);

        await _auditService.RecordAsync(
            adminUserId:    adminId,
            actionType:     AdminActionTypes.MarkManualReview,
            reason:         model.Reason,
            paymentOrderId: order.Id,
            oldStatus:      oldStatus,
            newStatus:      PaymentOrderStatuses.ManualReview,
            ipAddress:      HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent:      HttpContext.Request.Headers["User-Agent"].ToString());

        TempData["Success"] = $"Order {order.OrderCode} đã được đánh dấu Manual Review.";
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveManualReviewConfirmPaid(AdminResolveManualReviewRequest model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = GetModelErrorMessage();
            return RedirectToAction(nameof(Details), new { id = model.PaymentOrderId });
        }

        var order = await _context.PaymentOrders
            .Include(o => o.Plan)
            .FirstOrDefaultAsync(o => o.Id == model.PaymentOrderId);
        if (order == null)
            return NotFound();

        if (!string.Equals(order.Status, PaymentOrderStatuses.ManualReview, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Chi co the confirm paid cho order dang manual_review.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        if (order.AmountVnd <= 0 || order.PlanId <= 0 || order.Plan == null)
        {
            TempData["Error"] = "Order thieu du lieu amount/plan, khong the confirm paid.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        var oldStatus = order.Status;
        var nowUtc = DateTime.UtcNow;
        order.Status = PaymentOrderStatuses.Paid;
        order.PaidAtUtc ??= nowUtc;
        order.UpdatedAtUtc = nowUtc;
        order.FailureMessage = null;

        await _context.PaymentEvents.AddAsync(new PaymentEvent
        {
            Provider = order.Provider,
            EventType = PaymentEventTypes.ManualConfirm,
            EventKey = $"manual-confirm:{order.OrderCode}:{nowUtc.Ticks}",
            PaymentOrderId = order.Id,
            SignatureValid = true,
            ResultCode = "admin_confirm_paid",
            PayloadJson = "{}",
            ProcessingStatus = PaymentEventProcessingStatuses.Processed,
            ProcessingMessage = model.Reason,
            ReceivedAtUtc = nowUtc,
            ProcessedAtUtc = nowUtc
        });

        await _context.SaveChangesAsync();
        await _subscriptionService.ActivateFromPaidOrderAsync(order.Id);

        var adminId = GetCurrentUserId();
        await _auditService.RecordAsync(
            adminUserId: adminId,
            actionType: AdminActionTypes.ResolveManualReview,
            reason: model.Reason,
            paymentOrderId: order.Id,
            oldStatus: oldStatus,
            newStatus: PaymentOrderStatuses.Paid,
            payloadJson: System.Text.Json.JsonSerializer.Serialize(new { resolution = "confirm_paid" }),
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: HttpContext.Request.Headers["User-Agent"].ToString());

        TempData["Success"] = $"Order {order.OrderCode} da duoc confirm paid va kich hoat subscription.";
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveManualReviewReject(AdminResolveManualReviewRequest model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = GetModelErrorMessage();
            return RedirectToAction(nameof(Details), new { id = model.PaymentOrderId });
        }

        var order = await _context.PaymentOrders.FirstOrDefaultAsync(o => o.Id == model.PaymentOrderId);
        if (order == null)
            return NotFound();

        if (!string.Equals(order.Status, PaymentOrderStatuses.ManualReview, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Chi co the reject cho order dang manual_review.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        var oldStatus = order.Status;
        var nowUtc = DateTime.UtcNow;
        order.Status = PaymentOrderStatuses.Failed;
        order.FailureMessage = model.Reason;
        order.UpdatedAtUtc = nowUtc;

        await _context.PaymentEvents.AddAsync(new PaymentEvent
        {
            Provider = order.Provider,
            EventType = PaymentEventTypes.ManualReject,
            EventKey = $"manual-reject:{order.OrderCode}:{nowUtc.Ticks}",
            PaymentOrderId = order.Id,
            SignatureValid = true,
            ResultCode = "admin_reject",
            PayloadJson = "{}",
            ProcessingStatus = PaymentEventProcessingStatuses.Processed,
            ProcessingMessage = model.Reason,
            ReceivedAtUtc = nowUtc,
            ProcessedAtUtc = nowUtc
        });

        await _context.SaveChangesAsync();

        var adminId = GetCurrentUserId();
        await _auditService.RecordAsync(
            adminUserId: adminId,
            actionType: AdminActionTypes.ResolveManualReview,
            reason: model.Reason,
            paymentOrderId: order.Id,
            oldStatus: oldStatus,
            newStatus: PaymentOrderStatuses.Failed,
            payloadJson: System.Text.Json.JsonSerializer.Serialize(new { resolution = "reject" }),
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: HttpContext.Request.Headers["User-Agent"].ToString());

        TempData["Success"] = $"Order {order.OrderCode} da duoc reject.";
        return RedirectToAction(nameof(Details), new { id = order.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CleanupExpiredPending()
    {
        var nowUtc = DateTime.UtcNow;
        var expiredPendingOrders = await _context.PaymentOrders
            .Where(o => o.Status == PaymentOrderStatuses.Pending && o.ExpiresAtUtc <= nowUtc)
            .ToListAsync();

        if (expiredPendingOrders.Count == 0)
        {
            TempData["Success"] = "Khong co don pending qua han de don.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var order in expiredPendingOrders)
        {
            order.Status = PaymentOrderStatuses.Expired;
            order.FailureMessage ??= "Don pending qua han duoc don boi admin.";
            order.UpdatedAtUtc = nowUtc;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminId} cleaned up {Count} expired pending payment orders.",
            GetCurrentUserId(),
            expiredPendingOrders.Count);

        TempData["Success"] = $"Da don {expiredPendingOrders.Count} don pending qua han (chuyen sang expired).";
        return RedirectToAction(nameof(Index));
    }

    private IQueryable<PaymentOrder> ApplyOrderSearch(IQueryable<PaymentOrder> query, string q)
    {
        if (long.TryParse(q, out var orderId))
        {
            return query.Where(o => o.Id == orderId
                || o.UserId == orderId
                || o.OrderCode.Contains(q));
        }

        return query.Where(o => o.OrderCode.Contains(q)
            || o.User.Email.Contains(q)
            || (o.User.FullName != null && o.User.FullName.Contains(q))
            || (o.ProviderTransactionId != null && o.ProviderTransactionId.Contains(q)));
    }

    private IQueryable<UserSubscription> ApplySubscriptionSearch(
        IQueryable<UserSubscription> query,
        string q)
    {
        if (int.TryParse(q, out var userId))
        {
            return query.Where(s => s.UserId == userId
                || (s.ActivatedByPaymentOrder != null
                    && s.ActivatedByPaymentOrder.OrderCode.Contains(q)));
        }

        return query.Where(s => s.User.Email.Contains(q)
            || (s.User.FullName != null && s.User.FullName.Contains(q))
            || (s.ActivatedByPaymentOrder != null
                && s.ActivatedByPaymentOrder.OrderCode.Contains(q)));
    }

    private async Task<List<AdminPremiumUserRowViewModel>> BuildPremiumUserRowsAsync()
    {
        var nowUtc = DateTime.UtcNow;

        var users = await _context.Users
            .AsNoTracking()
            .Select(u => new
            {
                u.UserId,
                u.Email,
                u.FullName,
                u.Role
            })
            .ToListAsync();

        var subscriptions = await _context.UserSubscriptions
            .AsNoTracking()
            .Select(s => new
            {
                s.Id,
                s.UserId,
                s.Status,
                s.StartsAtUtc,
                s.EndsAtUtc,
                PlanName = s.Plan.Name,
                ActivatedByOrderCode = s.ActivatedByPaymentOrder == null ? null : s.ActivatedByPaymentOrder.OrderCode,
                Provider = s.ActivatedByPaymentOrder == null ? null : s.ActivatedByPaymentOrder.Provider,
                ActivatedByPaymentOrderId = s.ActivatedByPaymentOrderId
            })
            .ToListAsync();

        var paidOrders = await _context.PaymentOrders
            .AsNoTracking()
            .Where(o => o.Status == PaymentOrderStatuses.Paid)
            .Select(o => new
            {
                o.Id,
                o.UserId,
                o.OrderCode
            })
            .ToListAsync();

        var activatedOrderIdSet = subscriptions
            .Where(s => s.ActivatedByPaymentOrderId.HasValue)
            .Select(s => s.ActivatedByPaymentOrderId!.Value)
            .ToHashSet();

        var latestByUser = subscriptions
            .GroupBy(s => s.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.EndsAtUtc)
                      .ThenByDescending(s => s.StartsAtUtc)
                      .First());

        var activeByUser = subscriptions
            .Where(s => string.Equals(s.Status, SubscriptionStatuses.Active, StringComparison.OrdinalIgnoreCase)
                     && s.EndsAtUtc > nowUtc)
            .GroupBy(s => s.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.EndsAtUtc)
                      .ThenByDescending(s => s.StartsAtUtc)
                      .First());

        var paidOrderCodesByUser = paidOrders
            .GroupBy(o => o.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.OrderCode).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var hasPaidNotActivatedByUser = paidOrders
            .GroupBy(o => o.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Any(o => !activatedOrderIdSet.Contains(o.Id)));

        var rows = new List<AdminPremiumUserRowViewModel>(users.Count);
        foreach (var user in users)
        {
            var normalizedRole = Roles.Normalize(user.Role);
            var hasActive = activeByUser.TryGetValue(user.UserId, out var active);
            latestByUser.TryGetValue(user.UserId, out var latest);
            var selected = active ?? latest;

            int? daysRemaining = null;
            if (selected != null)
            {
                var remaining = (int)Math.Ceiling((selected.EndsAtUtc - nowUtc).TotalDays);
                daysRemaining = remaining;
            }

            var subscriptionStatus = selected?.Status ?? "none";
            if (hasActive)
            {
                subscriptionStatus = SubscriptionStatuses.Active;
            }

            var row = new AdminPremiumUserRowViewModel
            {
                UserId = user.UserId,
                Email = user.Email,
                FullName = user.FullName ?? "N/A",
                AccountType = normalizedRole,
                PlanName = selected?.PlanName,
                SubscriptionStatus = subscriptionStatus,
                StartsAtUtc = selected?.StartsAtUtc,
                EndsAtUtc = selected?.EndsAtUtc,
                DaysRemaining = hasActive ? daysRemaining : null,
                ActivatedByOrderCode = selected?.ActivatedByOrderCode,
                Provider = selected?.Provider,
                IsActiveSubscription = hasActive,
                IsManualSubscription = selected != null && string.IsNullOrWhiteSpace(selected.ActivatedByOrderCode),
                RolePremiumWithoutActiveSubscription = normalizedRole == Roles.Premium && !hasActive,
                ActiveSubscriptionButRoleNotPremium = hasActive && !string.Equals(normalizedRole, Roles.Premium, StringComparison.OrdinalIgnoreCase),
                PaidOrderNotActivated = hasPaidNotActivatedByUser.TryGetValue(user.UserId, out var flag) && flag,
                SearchOrderCodes = paidOrderCodesByUser.TryGetValue(user.UserId, out var codes)
                    ? codes.ToList()
                    : new List<string>()
            };

            rows.Add(row);
        }

        return rows;
    }

    private static List<AdminPremiumUserRowViewModel> ApplyPremiumUserFilter(
        IEnumerable<AdminPremiumUserRowViewModel> rows,
        string? filter)
    {
        var normalized = NormalizePremiumFilter(filter);
        return normalized switch
        {
            PremiumUserFilters.Active => rows.Where(r => r.IsActiveSubscription).ToList(),
            PremiumUserFilters.Standard => rows.Where(r => !r.IsActiveSubscription).ToList(),
            PremiumUserFilters.ExpiringSoon => rows.Where(r => r.IsActiveSubscription
                && r.DaysRemaining.HasValue
                && r.DaysRemaining.Value <= ExpiringSoonDays).ToList(),
            PremiumUserFilters.Expired => rows.Where(r => !r.IsActiveSubscription
                && string.Equals(r.SubscriptionStatus, SubscriptionStatuses.Expired, StringComparison.OrdinalIgnoreCase)).ToList(),
            PremiumUserFilters.Revoked => rows.Where(r => string.Equals(r.SubscriptionStatus, SubscriptionStatuses.Revoked, StringComparison.OrdinalIgnoreCase)).ToList(),
            PremiumUserFilters.Manual => rows.Where(r => r.IsManualSubscription).ToList(),
            PremiumUserFilters.VnPay => rows.Where(r => string.Equals(r.Provider, PaymentProviders.VNPay, StringComparison.OrdinalIgnoreCase)).ToList(),
            PremiumUserFilters.MoMo => rows.Where(r => string.Equals(r.Provider, PaymentProviders.MoMo, StringComparison.OrdinalIgnoreCase)).ToList(),
            _ => rows.ToList()
        };
    }

    private static List<AdminPremiumUserRowViewModel> ApplyPremiumUserSearch(
        IEnumerable<AdminPremiumUserRowViewModel> rows,
        string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return rows.ToList();
        }

        var keyword = q.Trim();
        var hasUserId = int.TryParse(keyword, out var userId);

        return rows
            .Where(r =>
                (hasUserId && r.UserId == userId)
                || r.Email.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || r.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(r.ActivatedByOrderCode)
                    && r.ActivatedByOrderCode.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || r.SearchOrderCodes.Any(code => code.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private async Task<int?> ResolveUserIdAsync(string lookup)
    {
        var normalizedLookup = lookup.Trim();
        var query = _context.Users.AsNoTracking();

        if (int.TryParse(normalizedLookup, out var userId))
        {
            return await query
                .Where(u => u.UserId == userId)
                .Select(u => (int?)u.UserId)
                .FirstOrDefaultAsync();
        }

        return await query
            .Where(u => u.Email == normalizedLookup)
            .Select(u => (int?)u.UserId)
            .FirstOrDefaultAsync();
    }

    private string GetModelErrorMessage()
    {
        return ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e))
            ?? "Dữ liệu không hợp lệ.";
    }

    private static string? NormalizeFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizePremiumFilter(string? value)
    {
        var normalized = NormalizeFilter(value)?.ToLowerInvariant();
        return PremiumUserFilters.Values.Contains(normalized)
            ? normalized!
            : PremiumUserFilters.All;
    }
}
