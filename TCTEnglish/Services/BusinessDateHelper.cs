namespace TCTVocabulary.Services
{
    public static class BusinessDateHelper
    {
        private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();

        public static DateTime Today => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BusinessTimeZone).Date;

        public static DateTime ToBusinessDate(DateTime dateTime)
        {
            return ToBusinessDate(dateTime, assumeUtcWhenUnspecified: false);
        }

        public static DateTime ToBusinessDateFromUtcStorage(DateTime dateTime)
        {
            return ToBusinessDate(dateTime, assumeUtcWhenUnspecified: true);
        }

        private static DateTime ToBusinessDate(DateTime dateTime, bool assumeUtcWhenUnspecified)
        {
            if (dateTime.Kind == DateTimeKind.Unspecified)
            {
                if (!assumeUtcWhenUnspecified)
                {
                    return dateTime.Date;
                }

                dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            }

            var utcDateTime = dateTime.Kind == DateTimeKind.Utc
                ? dateTime
                : dateTime.ToUniversalTime();

            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, BusinessTimeZone).Date;
        }

        private static TimeZoneInfo ResolveBusinessTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                }
                catch (TimeZoneNotFoundException)
                {
                    return TimeZoneInfo.Utc;
                }
                catch (InvalidTimeZoneException)
                {
                    return TimeZoneInfo.Utc;
                }
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
