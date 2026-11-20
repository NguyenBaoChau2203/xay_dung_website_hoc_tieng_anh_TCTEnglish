using System;
using System.Text.Json;

namespace TCTEnglish.Services.Billing
{
    /// <summary>The four things a learner can be looking at on the order page.</summary>
    public static class PaymentOrderStages
    {
        public const string Waiting = "waiting";
        public const string Confirming = "confirming";
        public const string Paid = "paid";
        public const string NotCompleted = "not_completed";
    }

    /// <summary>
    /// Pure helpers shared by the status endpoint, the order page and the history list, so the learner
    /// never sees two different readings of the same order.
    /// </summary>
    public static class PaymentOrderPresentation
    {
        private static readonly TimeSpan QueryWindow = TimeSpan.FromHours(24);

        /// <summary>
        /// Plan 4.9: pending (not expired) = waiting; pending or recently expired with a successful return, and
        /// manual review = confirming; paid = paid; everything else = not completed.
        /// </summary>
        public static string ResolveStage(
            string status,
            DateTime expiresAtUtc,
            bool returnReportedSuccess,
            DateTime nowUtc)
        {
            var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
            var expired = expiresAtUtc <= nowUtc;

            switch (normalized)
            {
                case PaymentOrderStatuses.Paid:
                    return PaymentOrderStages.Paid;

                case PaymentOrderStatuses.ManualReview:
                    return PaymentOrderStages.Confirming;

                case PaymentOrderStatuses.Pending:
                    if (returnReportedSuccess)
                    {
                        return PaymentOrderStages.Confirming;
                    }

                    return expired ? PaymentOrderStages.NotCompleted : PaymentOrderStages.Waiting;

                case PaymentOrderStatuses.Expired:
                    // A paid-looking return for an order that just expired can still be confirmed by a query.
                    return returnReportedSuccess && nowUtc - expiresAtUtc <= QueryWindow
                        ? PaymentOrderStages.Confirming
                        : PaymentOrderStages.NotCompleted;

                default:
                    return PaymentOrderStages.NotCompleted;
            }
        }

        /// <summary>
        /// Whether the provider's return page said the buyer paid (VNPay <c>vnp_ResponseCode=00</c> with
        /// <c>vnp_TransactionStatus</c> absent or <c>00</c>; MoMo <c>resultCode=0</c>). This is only a hint for
        /// the wording of the page; it never confirms an order.
        /// </summary>
        public static bool ReturnReportedSuccess(string provider, string? returnPayloadJson)
        {
            if (string.IsNullOrWhiteSpace(returnPayloadJson))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(returnPayloadJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                string? Read(string name)
                {
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            return property.Value.ValueKind == JsonValueKind.String
                                ? property.Value.GetString()
                                : property.Value.GetRawText();
                        }
                    }

                    return null;
                }

                if (provider.Equals(PaymentProviders.MoMo, StringComparison.OrdinalIgnoreCase))
                {
                    return Read("resultCode") == "0";
                }

                if (provider.Equals(PaymentProviders.PayOS, StringComparison.OrdinalIgnoreCase))
                {
                    // payOS appends code=00&status=PAID&cancel=false to the return address (unsigned: a hint only).
                    return Read("status") == "PAID" && Read("code") == "00" && Read("cancel") != "true";
                }

                var status = Read("vnp_TransactionStatus");
                return Read("vnp_ResponseCode") == "00" && (string.IsNullOrEmpty(status) || status == "00");
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
