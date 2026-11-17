using System;
using System.Globalization;

namespace TCTEnglish.Services.Billing
{
    /// <summary>
    /// Billing times are stored in UTC and shown in Vietnam time (F17), whatever the server's own time zone is.
    /// Uses the same business time zone ids as <c>BusinessDateHelper</c> and falls back to UTC.
    /// </summary>
    public static class BillingTime
    {
        private static readonly TimeZoneInfo Zone = Resolve();

        public static DateTime ToBusiness(DateTime utc)
        {
            var value = utc.Kind == DateTimeKind.Local
                ? utc.ToUniversalTime()
                : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(value, Zone);
        }

        /// <summary><c>dd/MM/yyyy HH:mm</c> in Vietnam time.</summary>
        public static string ToDisplay(DateTime utc)
            => ToBusiness(utc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        /// <summary><c>dd/MM/yyyy</c> in Vietnam time.</summary>
        public static string ToDisplayDate(DateTime utc)
            => ToBusiness(utc).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        /// <summary>Money as <c>399.000 ₫</c>: dot grouping and a non-breaking space before the sign.</summary>
        public static string FormatMoney(decimal amountVnd)
            => Convert.ToInt64(decimal.Truncate(amountVnd), CultureInfo.InvariantCulture)
                   .ToString("N0", CultureInfo.InvariantCulture)
                   .Replace(',', '.') + " ₫";

        private static TimeZoneInfo Resolve()
        {
            foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return TimeZoneInfo.Utc;
        }
    }
}
