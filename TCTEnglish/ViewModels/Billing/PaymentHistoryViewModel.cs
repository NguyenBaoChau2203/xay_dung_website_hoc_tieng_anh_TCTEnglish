using System;
using System.Collections.Generic;
using TCTEnglish.Services.Billing;

namespace TCTEnglish.ViewModels.Billing
{
    /// <summary>One row of the payment history. Words, tone and icon come from <see cref="PaymentOrderLabels"/>.</summary>
    public class PaymentHistoryItemViewModel
    {
        public string OrderCode { get; init; } = null!;
        public string PlanName { get; init; } = null!;
        public string Provider { get; init; } = null!;
        public decimal AmountVnd { get; init; }
        public string Status { get; init; } = null!;
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? PaidAtUtc { get; init; }
        public DateTime ExpiresAtUtc { get; init; }
        public string? BankCode { get; init; }

        /// <summary>The stored return parameters, only read for open orders (to tell "đang xác nhận" from "chờ thanh toán").</summary>
        public string? ReturnPayloadJson { get; init; }

        public OrderStatusView StatusView => PaymentOrderLabels.Describe(
            Status,
            ExpiresAtUtc,
            PaymentOrderPresentation.ReturnReportedSuccess(Provider, ReturnPayloadJson),
            DateTime.UtcNow);

        public string Method => PaymentOrderLabels.Method(Provider, BankCode);
        public string AmountText => BillingTime.FormatMoney(AmountVnd);
        public string TimeText => BillingTime.ToDisplay(PaidAtUtc ?? CreatedAtUtc);
        public string OrderUrl => "/Billing/Order/" + Uri.EscapeDataString(OrderCode);

        /// <summary>
        /// Plan 5.3: waiting and not expired → "Tiếp tục thanh toán" (primary); paid → "Xem biên nhận"; everything else
        /// → "Xem chi tiết". All of them open the order page.
        /// </summary>
        public string ActionLabel => StatusView.Stage switch
        {
            PaymentOrderStages.Waiting => "Tiếp tục thanh toán",
            PaymentOrderStages.Paid => "Xem biên nhận",
            _ => "Xem chi tiết"
        };

        public bool ActionIsPrimary => StatusView.Stage == PaymentOrderStages.Waiting;
    }

    public class PaymentHistoryViewModel
    {
        public const int PageSize = 20;

        public List<PaymentHistoryItemViewModel> Orders { get; init; } = new();

        /// <summary>The compact "Premium đến …" card; null when the page was built without an access snapshot.</summary>
        public PremiumStatusViewModel? Status { get; set; }

        /// <summary>Code of the order that activated the running subscription, for the status card.</summary>
        public string? ActivatedFromOrderCode { get; init; }

        public int Page { get; init; } = 1;
        public int TotalCount { get; init; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        public bool HasPrevious => Page > 1;
        public bool HasNext => Page < TotalPages;

        /// <summary>The header action reads "Mua thêm Premium" for a Premium account and "Nâng cấp Premium" otherwise.</summary>
        public bool IsPremium => Status is { Mode: PremiumStatusMode.Premium or PremiumStatusMode.Permanent };
    }
}
