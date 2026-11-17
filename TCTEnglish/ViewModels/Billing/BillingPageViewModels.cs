using System;
using System.Collections.Generic;
using TCTEnglish.Services.Billing;

namespace TCTEnglish.ViewModels.Billing
{
    /// <summary>One "label: value" line of the order summary, receipt or status card.</summary>
    public sealed record BillingRow(string Label, string Value, bool Emphasis = false, bool Monospace = false, string? Key = null);

    /// <summary>Order summary card: the pricing page's "Gói / Hiệu lực / Tổng thanh toán" and the order page's version with the code.</summary>
    public sealed class OrderSummaryViewModel
    {
        public string Heading { get; init; } = "Đơn hàng";
        public IReadOnlyList<BillingRow> Rows { get; init; } = Array.Empty<BillingRow>();
        public string TotalLabel { get; init; } = "Tổng thanh toán";
        public string TotalText { get; init; } = string.Empty;

        /// <summary>Ids the pricing script updates in place (empty on the server-rendered order page).</summary>
        public bool Live { get; init; }
    }

    /// <summary>Receipt shown on the paid order page.</summary>
    public sealed class ReceiptViewModel
    {
        public IReadOnlyList<BillingRow> Rows { get; init; } = Array.Empty<BillingRow>();
    }

    public enum PremiumStatusMode
    {
        Free,
        Premium,
        Permanent
    }

    /// <summary>The account's current state, in plain words (plan 5.1 and 5.3).</summary>
    public sealed class PremiumStatusViewModel
    {
        public PremiumStatusMode Mode { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public bool Compact { get; init; }
        public bool ShowHistoryLink { get; init; }

        /// <summary>
        /// Free: "Bạn đang dùng gói Miễn phí". Premium with an end date: "Bạn đang dùng Premium" with the date and days left
        /// (the compact history version reads "Premium đến …"). Permanent (admin or legacy): no end date.
        /// </summary>
        public static PremiumStatusViewModel From(PremiumAccessSnapshot access, bool compact, string? activatedFromOrderCode = null)
        {
            if (access.IsPermanent)
            {
                return new PremiumStatusViewModel
                {
                    Mode = PremiumStatusMode.Permanent,
                    Title = compact ? "Premium không thời hạn" : "Bạn đang dùng Premium",
                    Text = "Tài khoản của bạn có quyền Premium không thời hạn.",
                    Compact = compact,
                    ShowHistoryLink = !compact
                };
            }

            if (access.IsPremium && access.PremiumEndsAtUtc.HasValue)
            {
                var date = BillingTime.ToDisplayDate(access.PremiumEndsAtUtc.Value);
                var days = access.DaysLeft ?? 0;
                var left = days == 1 ? "còn 1 ngày" : $"còn {days} ngày";
                string text;
                if (compact)
                {
                    text = string.IsNullOrWhiteSpace(activatedFromOrderCode)
                        ? $"Còn {days} ngày."
                        : $"Còn {days} ngày. Kích hoạt từ đơn {activatedFromOrderCode}.";
                }
                else
                {
                    text = $"Có hiệu lực đến {date} ({left}). Mua thêm sẽ được cộng nối vào hạn này.";
                }

                return new PremiumStatusViewModel
                {
                    Mode = PremiumStatusMode.Premium,
                    Title = compact ? $"Premium đến {date}" : "Bạn đang dùng Premium",
                    Text = text,
                    Compact = compact,
                    ShowHistoryLink = !compact
                };
            }

            if (access.IsPremium)
            {
                // Premium role without an active subscription row to read (lapsed but not yet downgraded).
                return new PremiumStatusViewModel
                {
                    Mode = PremiumStatusMode.Premium,
                    Title = "Bạn đang dùng Premium",
                    Text = "Tài khoản của bạn đang có quyền Premium.",
                    Compact = compact,
                    ShowHistoryLink = !compact
                };
            }

            return new PremiumStatusViewModel
            {
                Mode = PremiumStatusMode.Free,
                Title = "Bạn đang dùng gói Miễn phí",
                Text = "Nâng cấp để mở các tính năng dùng AI và nội dung riêng của bạn.",
                Compact = compact,
                ShowHistoryLink = !compact
            };
        }
    }
}
