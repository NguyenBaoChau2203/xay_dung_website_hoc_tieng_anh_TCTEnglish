namespace TCTEnglish.Services.Notifications;

/// <summary>
/// The only rule for links stored in or shown from a notification: a path inside this site.
/// Applied when a link is created and again when it is displayed (defence in depth).
/// </summary>
public static class NotificationLinks
{
    /// <summary>Length of <c>Notification.RelatedUrl</c>.</summary>
    public const int MaxLength = 500;

    /// <summary>
    /// Returns the trimmed local path, cut to <see cref="MaxLength"/>, or null when the value
    /// is not a single-slash path (no scheme, no <c>//host</c>, no backslash, no control character).
    /// </summary>
    public static string? ToSafeLocal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (value[0] != '/')
        {
            return null;
        }

        if (value.Length > 1 && (value[1] == '/' || value[1] == '\\'))
        {
            return null;
        }

        foreach (var ch in value)
        {
            // Browsers treat a backslash like a slash, so none is allowed anywhere.
            if (ch == '\\' || char.IsControl(ch))
            {
                return null;
            }
        }

        return value.Length > MaxLength ? value[..MaxLength] : value;
    }

    /// <summary>True when the value is a safe local path that fits the column without cutting.</summary>
    public static bool IsValidLocalPath(string? raw)
    {
        var safe = ToSafeLocal(raw);
        return safe != null && raw!.Trim().Length <= MaxLength;
    }
}
