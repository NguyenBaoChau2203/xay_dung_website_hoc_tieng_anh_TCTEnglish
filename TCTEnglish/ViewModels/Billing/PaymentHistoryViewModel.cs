using System;
using System.Collections.Generic;
using TCTEnglish.Services.Billing;

namespace TCTEnglish.ViewModels.Billing
{
    /// <summary>
    /// A single payment order shown in a user's purchase history.
    /// </summary>
    public class PaymentHistoryItemViewModel
    {
        public string OrderCode { get; init; } = null!;
        public string PlanName { get; init; } = null!;
        public string Provider { get; init; } = null!;
        public decimal AmountVnd { get; init; }
        public string Status { get; init; } = null!;
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? PaidAtUtc { get; init; }

        public string NormalizedStatus => (Status ?? string.Empty).Trim().ToLowerInvariant();

        public string StatusBadgeClass => NormalizedStatus switch
        {
            PaymentOrderStatuses.Pending => "badge bg-warning text-dark rounded-pill px-3",
            PaymentOrderStatuses.Paid => "badge bg-success rounded-pill px-3",
            PaymentOrderStatuses.Failed => "badge bg-danger rounded-pill px-3",
            PaymentOrderStatuses.Cancelled => "badge bg-secondary rounded-pill px-3",
            PaymentOrderStatuses.Expired => "badge bg-dark rounded-pill px-3",
            PaymentOrderStatuses.Refunded => "badge bg-info text-dark rounded-pill px-3",
            PaymentOrderStatuses.PartiallyRefunded => "badge bg-info text-dark rounded-pill px-3",
            PaymentOrderStatuses.ManualReview => "badge bg-warning text-dark rounded-pill px-3",
            _ => "badge bg-light text-dark border rounded-pill px-3"
        };

        public string StatusDisplayName => NormalizedStatus switch
        {
            PaymentOrderStatuses.Pending => "Chờ xác nhận",
            PaymentOrderStatuses.Paid => "Thành công",
            PaymentOrderStatuses.Failed => "Thất bại",
            PaymentOrderStatuses.Cancelled => "Đã hủy",
            PaymentOrderStatuses.Expired => "Hết hạn",
            PaymentOrderStatuses.Refunded => "Đã hoàn tiền",
            PaymentOrderStatuses.PartiallyRefunded => "Hoàn tiền một phần",
            PaymentOrderStatuses.ManualReview => "Chờ kiểm tra thủ công",
            _ => Status
        };

        public string StatusIconClass => NormalizedStatus switch
        {
            PaymentOrderStatuses.Pending => "bi bi-clock-history me-1",
            PaymentOrderStatuses.Paid => "bi bi-check-circle me-1",
            PaymentOrderStatuses.Failed => "bi bi-x-circle me-1",
            PaymentOrderStatuses.Cancelled => "bi bi-slash-circle me-1",
            PaymentOrderStatuses.Expired => "bi bi-hourglass-split me-1",
            PaymentOrderStatuses.Refunded => "bi bi-arrow-counterclockwise me-1",
            PaymentOrderStatuses.PartiallyRefunded => "bi bi-arrow-left-right me-1",
            PaymentOrderStatuses.ManualReview => "bi bi-search me-1",
            _ => "bi bi-info-circle me-1"
        };

        public string ProviderDisplayName => Provider?.ToLowerInvariant() switch
        {
            PaymentProviders.VNPay => "VNPay",
            PaymentProviders.MoMo => "MoMo",
            _ => Provider ?? string.Empty
        };

        public string ProviderBadgeClass => Provider?.ToLowerInvariant() switch
        {
            PaymentProviders.VNPay => "badge bg-primary bg-opacity-10 text-primary px-3",
            PaymentProviders.MoMo => "badge bg-danger bg-opacity-10 text-danger px-3",
            _ => "badge bg-secondary bg-opacity-10 text-secondary px-3"
        };
    }

    /// <summary>
    /// Complete payment history for the current user.
    /// </summary>
    public class PaymentHistoryViewModel
    {
        public List<PaymentHistoryItemViewModel> Orders { get; init; } = new();
    }
}
