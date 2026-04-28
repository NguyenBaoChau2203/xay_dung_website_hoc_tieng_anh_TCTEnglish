using System.ComponentModel.DataAnnotations;
using TCTEnglish.Services.Billing;

namespace TCTVocabulary.Areas.Admin.ViewModels.Billing;

public class AdminBillingIndexViewModel
{
    public List<AdminPaymentOrderRowViewModel> Orders { get; set; } = new();
    public string? ProviderFilter { get; set; }
    public string? StatusFilter { get; set; }
    public string? SearchQuery { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalFilteredCount { get; set; }
    public IReadOnlyList<string> Providers { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Statuses { get; set; } = Array.Empty<string>();
}

public class AdminPaymentOrderRowViewModel
{
    public long Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public decimal AmountVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public string? ProviderTransactionId { get; set; }

    public string StatusBadgeClass => Status.ToLowerInvariant() switch
    {
        PaymentOrderStatuses.Paid => "badge bg-success",
        PaymentOrderStatuses.Pending => "badge bg-warning text-dark",
        PaymentOrderStatuses.Failed => "badge bg-danger",
        PaymentOrderStatuses.Cancelled => "badge bg-secondary",
        PaymentOrderStatuses.Expired => "badge bg-dark",
        PaymentOrderStatuses.Refunded => "badge bg-info text-dark",
        PaymentOrderStatuses.PartiallyRefunded => "badge bg-info text-dark",
        PaymentOrderStatuses.ManualReview => "badge bg-warning text-dark",
        _ => "badge bg-light text-dark border"
    };
}

public class AdminPaymentOrderDetailsViewModel
{
    public long Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public int PlanDurationDays { get; set; }
    public string Provider { get; set; } = string.Empty;
    public decimal AmountVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderResponseCode { get; set; }
    public string? ProviderTransactionStatus { get; set; }
    public string? FailureMessage { get; set; }
    public string? ReturnPayloadJson { get; set; }
    public string? IpnPayloadJson { get; set; }
    public List<AdminPaymentEventViewModel> Events { get; set; } = new();
    public List<AdminAuditActionViewModel> AuditHistory { get; set; } = new();
    public AdminMarkManualReviewRequest ManualReviewRequest { get; set; } = new();

    public string StatusBadgeClass => Status.ToLowerInvariant() switch
    {
        PaymentOrderStatuses.Paid => "badge bg-success",
        PaymentOrderStatuses.Pending => "badge bg-warning text-dark",
        PaymentOrderStatuses.Failed => "badge bg-danger",
        PaymentOrderStatuses.Cancelled => "badge bg-secondary",
        PaymentOrderStatuses.Expired => "badge bg-dark",
        PaymentOrderStatuses.Refunded => "badge bg-info text-dark",
        PaymentOrderStatuses.PartiallyRefunded => "badge bg-info text-dark",
        PaymentOrderStatuses.ManualReview => "badge bg-warning text-dark",
        _ => "badge bg-light text-dark border"
    };
}

public class AdminPaymentEventViewModel
{
    public long Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string EventKey { get; set; } = string.Empty;
    public bool SignatureValid { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public string ProcessingStatus { get; set; } = string.Empty;
    public string? ProcessingMessage { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}

public class AdminSubscriptionsViewModel
{
    public List<AdminSubscriptionRowViewModel> Subscriptions { get; set; } = new();
    public List<AdminPremiumPlanOptionViewModel> Plans { get; set; } = new();
    public AdminGrantPremiumRequest GrantRequest { get; set; } = new();
    public AdminRevokePremiumRequest RevokeRequest { get; set; } = new();
    public string? StatusFilter { get; set; }
    public string? SearchQuery { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalFilteredCount { get; set; }
    public IReadOnlyList<string> Statuses { get; set; } = Array.Empty<string>();
}

public class AdminSubscriptionRowViewModel
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelReason { get; set; }
    public string? ActivatedByOrderCode { get; set; }
    public bool IsActive => Status.Equals(SubscriptionStatuses.Active, StringComparison.OrdinalIgnoreCase)
        && EndsAtUtc > DateTime.UtcNow;
    public bool IsManualGrant => string.IsNullOrEmpty(ActivatedByOrderCode);

    public string StatusBadgeClass => Status.ToLowerInvariant() switch
    {
        SubscriptionStatuses.Active => "badge bg-success",
        SubscriptionStatuses.Expired => "badge bg-dark",
        SubscriptionStatuses.Cancelled => "badge bg-secondary",
        SubscriptionStatuses.Revoked => "badge bg-danger",
        _ => "badge bg-light text-dark border"
    };
}

public class AdminPremiumPlanOptionViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public decimal PriceVnd { get; set; }
}

public class AdminGrantPremiumRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email hoặc ID người dùng.")]
    [StringLength(256)]
    public string UserLookup { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Gói Premium không hợp lệ.")]
    public int PlanId { get; set; }

    [Range(1, 3650, ErrorMessage = "Thời hạn phải từ 1 đến 3650 ngày.")]
    public int? DurationDays { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do cấp Premium.")]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class AdminRevokePremiumRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Người dùng không hợp lệ.")]
    public int UserId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do thu hồi Premium.")]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class AdminMarkManualReviewRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "PaymentOrder không hợp lệ.")]
    public long PaymentOrderId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do đánh dấu Manual Review.")]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public class AdminResolveManualReviewRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "PaymentOrder không hợp lệ.")]
    public long PaymentOrderId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do xử lý manual review.")]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public class AdminAuditActionViewModel
{
    public long Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? IpAddress { get; set; }

    public string ActionBadgeClass => ActionType switch
    {
        "grant_premium"       => "badge bg-success",
        "revoke_premium"      => "badge bg-danger",
        "mark_manual_review"  => "badge bg-warning text-dark",
        "resolve_manual_review" => "badge bg-info text-dark",
        "replay_reconcile"    => "badge bg-primary",
        _                     => "badge bg-secondary"
    };
}

public static class PremiumUserFilters
{
    public const string All = "all";
    public const string Active = "active";
    public const string Standard = "standard";
    public const string ExpiringSoon = "expiring_soon";
    public const string Expired = "expired";
    public const string Revoked = "revoked";
    public const string Manual = "manual";
    public const string VnPay = "vnpay";
    public const string MoMo = "momo";

    public static readonly IReadOnlyList<string> Values = new[]
    {
        All,
        Active,
        Standard,
        ExpiringSoon,
        Expired,
        Revoked,
        Manual,
        VnPay,
        MoMo
    };
}

public class AdminPremiumUsersViewModel
{
    public AdminPremiumDashboardCardsViewModel Dashboard { get; set; } = new();
    public List<AdminPremiumUserRowViewModel> Users { get; set; } = new();
    public List<AdminPremiumPlanOptionViewModel> Plans { get; set; } = new();
    public string Filter { get; set; } = PremiumUserFilters.All;
    public string? SearchQuery { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalFilteredCount { get; set; }
    public IReadOnlyList<string> Filters { get; set; } = PremiumUserFilters.Values;
    public AdminGrantPremiumRequest GrantRequest { get; set; } = new();
    public AdminRevokePremiumRequest RevokeRequest { get; set; } = new();
}

public class AdminPremiumDashboardCardsViewModel
{
    public int ActivePremiumCount { get; set; }
    public int StandardCount { get; set; }
    public int ExpiringSoonCount { get; set; }
    public int ExpiredCount { get; set; }
    public decimal RevenueThisMonthVnd { get; set; }
}

public class AdminPremiumUserRowViewModel
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? PlanName { get; set; }
    public string SubscriptionStatus { get; set; } = "none";
    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public int? DaysRemaining { get; set; }
    public string? ActivatedByOrderCode { get; set; }
    public string? Provider { get; set; }
    public bool IsActiveSubscription { get; set; }
    public bool IsManualSubscription { get; set; }
    public bool RolePremiumWithoutActiveSubscription { get; set; }
    public bool ActiveSubscriptionButRoleNotPremium { get; set; }
    public bool PaidOrderNotActivated { get; set; }
    public List<string> SearchOrderCodes { get; set; } = new();

    public string SubscriptionBadgeClass => SubscriptionStatus.ToLowerInvariant() switch
    {
        SubscriptionStatuses.Active => "badge bg-success",
        SubscriptionStatuses.Expired => "badge bg-dark",
        SubscriptionStatuses.Revoked => "badge bg-danger",
        SubscriptionStatuses.Cancelled => "badge bg-secondary",
        "none" => "badge bg-light text-dark border",
        _ => "badge bg-light text-dark border"
    };
}

public class AdminPremiumUserDetailsViewModel
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public AdminPremiumUserRowViewModel? CurrentSubscription { get; set; }
    public List<AdminSubscriptionHistoryItemViewModel> SubscriptionHistory { get; set; } = new();
    public List<AdminPaymentOrderHistoryItemViewModel> PaymentOrders { get; set; } = new();
    public bool RolePremiumWithoutActiveSubscription { get; set; }
    public bool ActiveSubscriptionButRoleNotPremium { get; set; }
    public bool PaidOrderNotActivated { get; set; }
}

public class AdminSubscriptionHistoryItemViewModel
{
    public long Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelReason { get; set; }
    public string? ActivatedByOrderCode { get; set; }
    public string? Provider { get; set; }
    public bool IsManual => string.IsNullOrWhiteSpace(ActivatedByOrderCode);
}

public class AdminPaymentOrderHistoryItemViewModel
{
    public long Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public decimal AmountVnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public bool IsPaidButNotActivated { get; set; }
}
