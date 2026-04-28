using System;
using TCTEnglish.Services.Billing;

namespace TCTEnglish.ViewModels.Billing
{
    public class PaymentResultViewModel
    {
        public int? OwnerUserId { get; init; }
        public string OrderCode { get; init; } = string.Empty;
        public string PlanName { get; init; } = "Premium";
        public decimal AmountVnd { get; init; }
        public string OrderStatus { get; init; } = PaymentOrderStatuses.Pending;
        public bool HasValidReturnSignature { get; init; }
        public bool CanShowOrderSummary { get; init; }
        public DateTime? PaidAtUtc { get; init; }

        public string NormalizedStatus => (OrderStatus ?? string.Empty).Trim().ToLowerInvariant();

        public bool IsPaid => HasValidReturnSignature
            && NormalizedStatus == PaymentOrderStatuses.Paid;

        public bool IsPending => HasValidReturnSignature
            && NormalizedStatus == PaymentOrderStatuses.Pending;

        public bool CanRetry => HasValidReturnSignature
            && (NormalizedStatus == PaymentOrderStatuses.Failed
                || NormalizedStatus == PaymentOrderStatuses.Cancelled
                || NormalizedStatus == PaymentOrderStatuses.Expired
                || NormalizedStatus == PaymentOrderStatuses.Refunded);

        public string Title => HasValidReturnSignature
            ? NormalizedStatus switch
            {
                PaymentOrderStatuses.Paid => "Thanh toán thành công",
                PaymentOrderStatuses.Pending => "Đang chờ xác nhận thanh toán",
                PaymentOrderStatuses.Cancelled => "Giao dịch đã hủy",
                PaymentOrderStatuses.Expired => "Đơn thanh toán đã hết hạn",
                PaymentOrderStatuses.Refunded => "Khoản thanh toán đã được hoàn lại",
                PaymentOrderStatuses.PartiallyRefunded => "Khoản thanh toán đã được hoàn tiền một phần",
                PaymentOrderStatuses.ManualReview => "Đơn thanh toán đang chờ kiểm tra thủ công",
                PaymentOrderStatuses.Failed => "Thanh toán thất bại",
                _ => "Trạng thái thanh toán chưa xác định"
            }
            : "Dữ liệu trả về không hợp lệ";

        public string Message => HasValidReturnSignature
            ? NormalizedStatus switch
            {
                PaymentOrderStatuses.Paid => "Premium chỉ được kích hoạt sau khi IPN xác nhận. Đơn hàng này đã được xác nhận thành công trong hệ thống.",
                PaymentOrderStatuses.Pending => "Hệ thống đã nhận được lượt quay lại từ cổng thanh toán nhưng vẫn đang chờ IPN xác nhận trước khi kích hoạt Premium.",
                PaymentOrderStatuses.Cancelled => "Giao dịch đã bị hủy trên cổng thanh toán. Bạn có thể tạo đơn mới khi cần.",
                PaymentOrderStatuses.Expired => "Đơn thanh toán đã hết hạn trước khi được xác nhận. Vui lòng tạo đơn mới để tiếp tục.",
                PaymentOrderStatuses.Refunded => "Khoản thanh toán đã được hoàn lại. Premium sẽ không được kích hoạt từ đơn hàng này.",
                PaymentOrderStatuses.PartiallyRefunded => "Đơn hàng đã được hoàn tiền một phần. Vui lòng liên hệ hỗ trợ nếu cần đối soát thêm.",
                PaymentOrderStatuses.ManualReview => "Đơn hàng đang chờ kiểm tra thủ công từ quản trị viên trước khi có kết luận cuối cùng.",
                PaymentOrderStatuses.Failed => "Thanh toán không thành công. Bạn có thể thử lại bằng một đơn thanh toán mới.",
                _ => "Hệ thống chưa xác định được trạng thái cuối cùng của đơn hàng này."
            }
            : "Dữ liệu trả về từ cổng thanh toán không hợp lệ.";

        public string StatusBadgeClass => HasValidReturnSignature
            ? NormalizedStatus switch
            {
                PaymentOrderStatuses.Paid => "badge bg-success-subtle text-success-emphasis border border-success-subtle",
                PaymentOrderStatuses.Pending => "badge bg-warning-subtle text-warning-emphasis border border-warning-subtle",
                PaymentOrderStatuses.Cancelled => "badge bg-secondary-subtle text-secondary-emphasis border border-secondary-subtle",
                PaymentOrderStatuses.Expired => "badge bg-dark text-white",
                PaymentOrderStatuses.Refunded => "badge bg-info-subtle text-info-emphasis border border-info-subtle",
                PaymentOrderStatuses.PartiallyRefunded => "badge bg-info-subtle text-info-emphasis border border-info-subtle",
                PaymentOrderStatuses.ManualReview => "badge bg-warning-subtle text-warning-emphasis border border-warning-subtle",
                PaymentOrderStatuses.Failed => "badge bg-danger-subtle text-danger-emphasis border border-danger-subtle",
                _ => "badge bg-light text-dark border"
            }
            : "badge bg-danger-subtle text-danger-emphasis border border-danger-subtle";

        public string StatusDisplayName => HasValidReturnSignature
            ? NormalizedStatus switch
            {
                PaymentOrderStatuses.Paid => "Đã thanh toán",
                PaymentOrderStatuses.Pending => "Chờ xác nhận",
                PaymentOrderStatuses.Cancelled => "Đã hủy",
                PaymentOrderStatuses.Expired => "Hết hạn",
                PaymentOrderStatuses.Refunded => "Đã hoàn tiền",
                PaymentOrderStatuses.PartiallyRefunded => "Hoàn tiền một phần",
                PaymentOrderStatuses.ManualReview => "Manual review",
                PaymentOrderStatuses.Failed => "Thất bại",
                _ => OrderStatus
            }
            : "Dữ liệu không hợp lệ";

        public string IconContainerClass => HasValidReturnSignature
            ? NormalizedStatus switch
            {
                PaymentOrderStatuses.Paid => "bg-success text-white",
                PaymentOrderStatuses.Pending => "bg-warning text-dark",
                PaymentOrderStatuses.Cancelled => "bg-secondary text-white",
                PaymentOrderStatuses.Expired => "bg-dark text-white",
                PaymentOrderStatuses.Refunded => "bg-info text-dark",
                PaymentOrderStatuses.PartiallyRefunded => "bg-info text-dark",
                PaymentOrderStatuses.ManualReview => "bg-warning text-dark",
                PaymentOrderStatuses.Failed => "bg-danger text-white",
                _ => "bg-secondary text-white"
            }
            : "bg-danger text-white";

        public string IconClass => HasValidReturnSignature
            ? NormalizedStatus switch
            {
                PaymentOrderStatuses.Paid => "bi bi-check-lg",
                PaymentOrderStatuses.Pending => "bi bi-hourglass-split",
                PaymentOrderStatuses.Cancelled => "bi bi-x-lg",
                PaymentOrderStatuses.Expired => "bi bi-clock-history",
                PaymentOrderStatuses.Refunded => "bi bi-arrow-counterclockwise",
                PaymentOrderStatuses.PartiallyRefunded => "bi bi-arrow-left-right",
                PaymentOrderStatuses.ManualReview => "bi bi-search",
                PaymentOrderStatuses.Failed => "bi bi-exclamation-lg",
                _ => "bi bi-info-lg"
            }
            : "bi bi-shield-exclamation";
    }
}
