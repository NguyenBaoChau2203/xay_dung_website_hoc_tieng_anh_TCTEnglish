using System;
using TCTEnglish.Services.Billing;

namespace TCTEnglish.ViewModels.Billing
{
    /// <summary>
    /// Everything the order page needs (plan 5.2): one page per order for waiting, confirming, paid and
    /// not-completed. Built by <see cref="OrderPageBuilder"/>; the view only formats it.
    /// </summary>
    public sealed class OrderPageViewModel
    {
        public string OrderCode { get; init; } = string.Empty;
        public string PlanName { get; init; } = "Premium";
        public int DurationDays { get; init; }
        public decimal AmountVnd { get; init; }
        public string AmountText => BillingTime.FormatMoney(AmountVnd);

        public string Provider { get; init; } = string.Empty;
        public string ProviderName => PaymentOrderLabels.ProviderName(Provider);
        public bool IsMoMo => Provider.Equals(PaymentProviders.MoMo, StringComparison.OrdinalIgnoreCase);
        public bool IsPayOS => Provider.Equals(PaymentProviders.PayOS, StringComparison.OrdinalIgnoreCase);

        /// <summary>Raw order status (pending, paid, cancelled…), used to pick the wording of the not-completed page.</summary>
        public string Status { get; init; } = string.Empty;

        public OrderStatusView StatusView { get; init; } = null!;
        public string Stage => StatusView.Stage;
        public bool IsManualReview { get; init; }

        public DateTime ExpiresAtUtc { get; init; }
        public long ExpiresAtMs { get; init; }
        public long NowMs { get; init; }
        public bool IsExpired { get; init; }

        public DateTime CreatedAtUtc { get; init; }
        public DateTime? PaidAtUtc { get; init; }
        public string? ProviderTransactionId { get; init; }
        public string? BankCode { get; init; }

        public string? QrPayload { get; init; }

        /// <summary>A safe https (or MoMo app) address; null when the stored value is not one.</summary>
        public string? PayUrl { get; init; }
        public string? DeepLink { get; init; }

        /// <summary>VNPay: the stored payment address of a waiting, unexpired order.</summary>
        public string? ResumeUrl { get; init; }

        /// <summary>Local mock mode of MoMo: the dashed box with the simulate button. Development only, checked on the server.</summary>
        public bool IsMockMode { get; init; }

        public DateTime? PremiumEndsAtUtc { get; init; }
        public bool ReturnReportedSuccess { get; init; }

        /// <summary>Server time (UTC) the page was built at, for "lần gần nhất".</summary>
        public DateTime BuiltAtUtc { get; init; }

        public OrderSummaryViewModel Summary { get; init; } = new();
        public ReceiptViewModel Receipt { get; init; } = new();

        public bool CanPayAgain => Status != PaymentOrderStatuses.Refunded && Status != PaymentOrderStatuses.PartiallyRefunded;

        /// <summary>The one sentence that says why the order is not completed (plan 5.2). Never claims that no money was taken.</summary>
        public string NotCompletedReason => Status switch
        {
            PaymentOrderStatuses.Cancelled when IsPayOS => "Bạn đã hủy thanh toán. Bạn có thể chọn gói và thanh toán lại.",
            PaymentOrderStatuses.Cancelled => $"Bạn đã hủy giao dịch trên {ProviderName}.",
            PaymentOrderStatuses.Failed => "Giao dịch không thành công.",
            PaymentOrderStatuses.Refunded => "Khoản thanh toán này đã được hoàn lại.",
            PaymentOrderStatuses.PartiallyRefunded => "Khoản thanh toán này đã được hoàn lại một phần.",
            _ => "Đơn đã hết hạn trước khi được thanh toán."
        };
    }
}
