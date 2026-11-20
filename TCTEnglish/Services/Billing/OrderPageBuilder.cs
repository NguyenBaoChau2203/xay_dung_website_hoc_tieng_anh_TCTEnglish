using System;
using System.Collections.Generic;
using TCTEnglish.Models;
using TCTEnglish.ViewModels.Billing;

namespace TCTEnglish.Services.Billing
{
    /// <summary>Pure construction of <see cref="OrderPageViewModel"/> from an order (plan 5.2).</summary>
    public static class OrderPageBuilder
    {
        public static OrderPageViewModel Build(
            PaymentOrder order,
            DateTime? premiumEndsAtUtc,
            bool isMockMode,
            DateTime nowUtc)
        {
            var returned = PaymentOrderPresentation.ReturnReportedSuccess(order.Provider, order.ReturnPayloadJson);
            var view = PaymentOrderLabels.Describe(order.Status, order.ExpiresAtUtc, returned, nowUtc);
            var days = order.Plan?.DurationDays ?? 0;
            var planName = order.Plan?.Name ?? "Premium";
            var isMoMo = order.Provider.Equals(PaymentProviders.MoMo, StringComparison.OrdinalIgnoreCase);
            var expired = order.ExpiresAtUtc <= nowUtc;
            var waiting = view.Stage == PaymentOrderStages.Waiting;

            var summary = new OrderSummaryViewModel
            {
                Heading = "Đơn hàng",
                Rows = new[]
                {
                    new BillingRow("Mã đơn", order.OrderCode, Emphasis: true, Monospace: true),
                    new BillingRow("Gói", planName, Emphasis: true),
                    new BillingRow("Hiệu lực", days > 0 ? $"{days} ngày" : "—", Emphasis: true)
                },
                TotalText = BillingTime.FormatMoney(order.AmountVnd)
            };

            var receipt = new ReceiptViewModel
            {
                Rows = new[]
                {
                    new BillingRow("Mã đơn", order.OrderCode, Monospace: true),
                    new BillingRow("Gói", planName),
                    new BillingRow("Số tiền", BillingTime.FormatMoney(order.AmountVnd)),
                    new BillingRow("Phương thức", PaymentOrderLabels.Method(order.Provider, order.BankCode)),
                    new BillingRow("Thời gian", BillingTime.ToDisplay(order.PaidAtUtc ?? order.UpdatedAtUtc)),
                    new BillingRow(
                        "Mã giao dịch",
                        string.IsNullOrWhiteSpace(order.ProviderTransactionId) ? "—" : order.ProviderTransactionId!,
                        Monospace: true)
                }
            };

            return new OrderPageViewModel
            {
                OrderCode = order.OrderCode,
                PlanName = planName,
                DurationDays = days,
                AmountVnd = order.AmountVnd,
                Provider = order.Provider,
                Status = (order.Status ?? string.Empty).Trim().ToLowerInvariant(),
                StatusView = view,
                IsManualReview = string.Equals(order.Status, PaymentOrderStatuses.ManualReview, StringComparison.OrdinalIgnoreCase),
                ExpiresAtUtc = order.ExpiresAtUtc,
                ExpiresAtMs = ToMs(order.ExpiresAtUtc),
                NowMs = ToMs(nowUtc),
                IsExpired = expired,
                CreatedAtUtc = order.CreatedAtUtc,
                PaidAtUtc = order.PaidAtUtc,
                ProviderTransactionId = order.ProviderTransactionId,
                BankCode = order.BankCode,
                QrPayload = isMoMo ? order.ProviderQrCodePayload : null,
                PayUrl = isMoMo ? SafeAddress(order.ProviderPaymentUrl, allowAppScheme: false) : null,
                DeepLink = isMoMo ? SafeAddress(order.ProviderDeepLink, allowAppScheme: true) : null,
                ResumeUrl = !isMoMo && waiting && !expired
                    ? SafeAddress(order.ProviderPaymentUrl, allowAppScheme: false) ?? LocalPayOSMockPath(order)
                    : null,
                // Mock mode is decided by the caller (Development + MoMo mock flag) and never leaks to a paid or closed order.
                IsMockMode = isMockMode && isMoMo && waiting,
                PremiumEndsAtUtc = premiumEndsAtUtc,
                ReturnReportedSuccess = returned,
                BuiltAtUtc = nowUtc,
                Summary = summary,
                Receipt = receipt
            };
        }

        /// <summary>
        /// The local fake payOS page is a relative address (<c>/Billing/PayOSMock?orderCode=…</c>) that only the mock
        /// gateway stores. It is accepted only in exactly that shape, for a payOS order of this very order code; the
        /// page itself answers 404 unless the Development mock is on.
        /// </summary>
        private static string? LocalPayOSMockPath(PaymentOrder order)
        {
            if (!order.Provider.Equals(PaymentProviders.PayOS, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var expected = $"/Billing/PayOSMock?orderCode={Uri.EscapeDataString(order.OrderCode)}";
            return string.Equals(order.ProviderPaymentUrl, expected, StringComparison.Ordinal) ? expected : null;
        }

        /// <summary>
        /// Only https (and, for the app deep link, the MoMo schemes) may become a link; anything else stored in the
        /// database is dropped instead of being rendered into an href.
        /// </summary>
        internal static string? SafeAddress(string? value, bool allowAppScheme)
        {
            if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
            {
                return null;
            }

            var scheme = uri.Scheme.ToLowerInvariant();
            if (scheme == Uri.UriSchemeHttps)
            {
                return uri.AbsoluteUri;
            }

            return allowAppScheme && (scheme == "momo" || scheme == "momoapp")
                ? uri.AbsoluteUri
                : null;
        }

        private static long ToMs(DateTime utc)
            => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
    }
}
