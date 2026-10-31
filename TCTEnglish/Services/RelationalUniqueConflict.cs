using Microsoft.EntityFrameworkCore;

namespace TCTEnglish.Services;

/// <summary>
/// Identifies unique-index conflicts without treating every database failure as a duplicate.
/// </summary>
internal static class RelationalUniqueConflict
{
    public static bool IsUnique(DbUpdateException exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current.GetType().Name == "MySqlException")
            {
                var number = current.GetType().GetProperty("Number")?.GetValue(current);
                if (number is int mysql && mysql is 1062 or 1022)
                {
                    return true;
                }
            }

            if (current.GetType().Name == "SqliteException")
            {
                var code = current.GetType().GetProperty("SqliteErrorCode")?.GetValue(current);
                if (code is int sqlite && sqlite == 19
                    && current.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        var text = exception.ToString();
        return text.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase);
    }
}
