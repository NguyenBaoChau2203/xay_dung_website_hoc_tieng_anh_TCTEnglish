using System;

namespace TCTEnglish.Services.Billing
{
    /// <summary>How the learner sees one status: words, tone (maps to a chip colour) and a Font Awesome icon.</summary>
    public sealed record OrderStatusView(string Label, string Tone, string IconClass, string Stage);

    /// <summary>
    /// The single learner-facing mapping of an order's status (plan 4.9). The history list, the order page and the
    /// status chip all use it, so the same order never reads two ways. Admin pages show their own labels.
    /// </summary>
    public static class PaymentOrderLabels
    {
        public const string ToneWarning = "warning";
        public const string ToneSuccess = "success";
        public const string ToneDanger = "danger";
        public const string ToneNeutral = "neutral";
        public const string ToneInfo = "info";

        public static OrderStatusView Describe(
            string status, DateTime expiresAtUtc, bool returnReportedSuccess, DateTime nowUtc)
        {
            var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
            var stage = PaymentOrderPresentation.ResolveStage(normalized, expiresAtUtc, returnReportedSuccess, nowUtc);

            return stage switch
            {
                PaymentOrderStages.Waiting => new("Chờ thanh toán", ToneWarning, "fa-solid fa-clock", stage),
                PaymentOrderStages.Confirming => new("Đang xác nhận", ToneWarning, "fa-solid fa-hourglass-half", stage),
                PaymentOrderStages.Paid => new("Đã thanh toán", ToneSuccess, "fa-solid fa-circle-check", stage),
                _ => normalized switch
                {
                    PaymentOrderStatuses.Cancelled => new("Đã hủy", ToneNeutral, "fa-solid fa-ban", stage),
                    PaymentOrderStatuses.Failed => new("Thất bại", ToneDanger, "fa-solid fa-circle-xmark", stage),
                    PaymentOrderStatuses.Refunded => new("Đã hoàn tiền", ToneInfo, "fa-solid fa-rotate-left", stage),
                    PaymentOrderStatuses.PartiallyRefunded => new("Hoàn tiền một phần", ToneInfo, "fa-solid fa-rotate-left", stage),
                    // expired, and a pending order whose time is up
                    _ => new("Hết hạn", ToneNeutral, "fa-solid fa-hourglass-end", stage)
                }
            };
        }

        public static string ProviderName(string? provider)
        {
            return (provider ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                PaymentProviders.VNPay => "VNPay",
                PaymentProviders.MoMo => "MoMo",
                var other => other
            };
        }

        /// <summary>"VNPay · NCB" when the bank is known, otherwise just the provider.</summary>
        public static string Method(string? provider, string? bankCode)
        {
            var name = ProviderName(provider);
            return string.IsNullOrWhiteSpace(bankCode) ? name : $"{name} · {bankCode.Trim()}";
        }
    }
}
