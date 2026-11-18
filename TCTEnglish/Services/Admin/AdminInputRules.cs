using System.Text.RegularExpressions;

namespace TCTEnglish.Services.Admin;

/// <summary>Result of <see cref="AdminInputRules.NormalizeText"/>: the cleaned text and whether it is over the limit.</summary>
public readonly record struct TextCheck(string Value, bool TooLong)
{
    public bool IsEmpty => Value.Length == 0;
}

/// <summary>
/// Pure input rules shared by the admin services (and mirrored by admin-content.js for instant feedback).
/// The server stays authoritative: bad data must never reach the learner pages.
/// </summary>
public static partial class AdminInputRules
{
    public static readonly IReadOnlyList<string> CefrLevels = new[] { "A1", "A2", "B1", "B2", "C1", "C2" };

    private static readonly HashSet<string> YoutubeHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com",
        "youtu.be", "www.youtu.be",
        "youtube-nocookie.com", "www.youtube-nocookie.com",
    };

    private static readonly string[] SchemelessYoutubePrefixes =
    {
        "youtu.be/", "youtube.com/", "www.youtube.com/", "m.youtube.com/", "music.youtube.com/",
        "youtube-nocookie.com/", "www.youtube-nocookie.com/",
    };

    private static readonly string[] YoutubePathPrefixes = { "embed", "shorts", "live", "v" };

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$", RegexOptions.CultureInvariant)]
    private static partial Regex YoutubeIdPattern();

    [GeneratedRegex(@"^(?:(\d{1,2}):([0-5]\d):([0-5]\d)|(\d{1,3}):([0-5]\d))$", RegexOptions.CultureInvariant)]
    private static partial Regex DurationPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRuns();

    [GeneratedRegex(@"\n{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex BlankLineRuns();

    public static bool IsCefrLevel(string? level) => level != null && CefrLevels.Contains(level);

    /// <summary>
    /// Reads a YouTube video id from a bare id or from watch, short, embed, shorts, live and nocookie links.
    /// Anything else (including look-alike hosts) is rejected.
    /// </summary>
    public static bool TryParseYoutubeId(string? input, out string? id)
    {
        id = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var value = input.Trim();
        if (YoutubeIdPattern().IsMatch(value))
        {
            id = value;
            return true;
        }

        if (!value.Contains("://", StringComparison.Ordinal)
            && SchemelessYoutubePrefixes.Any(p => value.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            value = "https://" + value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !YoutubeHosts.Contains(uri.Host))
        {
            return false;
        }

        string? candidate = null;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (uri.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            candidate = segments.Length > 0 ? segments[0] : null;
        }
        else if (segments.Length == 1 && segments[0].Equals("watch", StringComparison.OrdinalIgnoreCase))
        {
            candidate = System.Web.HttpUtility.ParseQueryString(uri.Query)["v"];
        }
        else if (segments.Length >= 2 && YoutubePathPrefixes.Contains(segments[0], StringComparer.OrdinalIgnoreCase))
        {
            candidate = segments[1];
        }

        if (candidate != null && YoutubeIdPattern().IsMatch(candidate))
        {
            id = candidate;
            return true;
        }

        return false;
    }

    public static string? YoutubeThumbnailUrl(string? youtubeId)
        => string.IsNullOrWhiteSpace(youtubeId) ? null : $"https://img.youtube.com/vi/{youtubeId}/hqdefault.jpg";

    /// <summary>
    /// Empty, an absolute https URL with a real host, or a local path that starts with a single slash.
    /// No whitespace or control characters, no user info, no http, data or script schemes.
    /// </summary>
    public static bool IsSafeImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        foreach (var ch in url)
        {
            if (char.IsWhiteSpace(ch) || char.IsControl(ch))
            {
                return false;
            }
        }

        if (url[0] == '/')
        {
            if (url.Length > 1 && (url[1] == '/' || url[1] == '\\'))
            {
                return false;
            }

            if (url.Contains('\\'))
            {
                return false;
            }

            return !url.Split('/').Any(segment => segment == ".." || segment == ".");
        }

        const string httpsPrefix = "https://";
        if (!url.StartsWith(httpsPrefix, StringComparison.OrdinalIgnoreCase)
            || url.Length == httpsPrefix.Length
            || url[httpsPrefix.Length] == '/')
        {
            return false;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && !string.IsNullOrEmpty(uri.Host)
            && string.IsNullOrEmpty(uri.UserInfo)
            && Uri.CheckHostName(uri.Host) != UriHostNameType.Unknown;
    }

    /// <summary>Audio and other media links follow the same rule as images.</summary>
    public static bool IsSafeMediaUrl(string? url) => IsSafeImageUrl(url);

    /// <summary>
    /// "m:ss", "mm:ss" or "h:mm:ss" becomes "mm:ss" or "h:mm:ss". Empty means "not set" (true, null).
    /// Zero-length and free text are rejected.
    /// </summary>
    public static bool TryNormalizeDuration(string? input, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        var match = DurationPattern().Match(input.Trim());
        if (!match.Success)
        {
            return false;
        }

        string result;
        int totalSeconds;
        if (match.Groups[1].Success)
        {
            var hours = int.Parse(match.Groups[1].Value);
            var minutes = int.Parse(match.Groups[2].Value);
            var seconds = int.Parse(match.Groups[3].Value);
            totalSeconds = hours * 3600 + minutes * 60 + seconds;
            result = $"{hours}:{minutes:00}:{seconds:00}";
        }
        else
        {
            var minutes = int.Parse(match.Groups[4].Value);
            var seconds = int.Parse(match.Groups[5].Value);
            totalSeconds = minutes * 60 + seconds;
            result = $"{minutes:00}:{seconds:00}";
        }

        if (totalSeconds == 0)
        {
            return false;
        }

        normalized = result;
        return true;
    }

    /// <summary>
    /// Trims; collapses every whitespace run in single-line fields; keeps line breaks (max one blank line) in multi-line fields.
    /// Reports text over <paramref name="max"/> instead of cutting it.
    /// </summary>
    public static TextCheck NormalizeText(string? text, int max, bool singleLine = true)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TextCheck(string.Empty, false);
        }

        string value;
        if (singleLine)
        {
            value = WhitespaceRuns().Replace(text, " ").Trim();
        }
        else
        {
            var unified = text.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = unified.Split('\n').Select(line => line.Trim());
            value = BlankLineRuns().Replace(string.Join('\n', lines), "\n\n").Trim();
        }

        return new TextCheck(value, value.Length > max);
    }

    [GeneratedRegex(@"^\d{1,5}(\.\d{1,3})?$", RegexOptions.CultureInvariant)]
    private static partial Regex PlainSecondsPattern();

    [GeneratedRegex(@"^[0-5]\d(\.\d{1,3})?$", RegexOptions.CultureInvariant)]
    private static partial Regex ClockSecondsPattern();

    [GeneratedRegex(@"^\d{1,5}$", RegexOptions.CultureInvariant)]
    private static partial Regex WholeNumberPattern();

    [GeneratedRegex(@"^[0-5]?\d$", RegexOptions.CultureInvariant)]
    private static partial Regex SexagesimalPattern();

    /// <summary>
    /// Reads a time typed by an admin: seconds ("5.5"), "m:ss(.s)" or "h:mm:ss(.s)". Empty means "not set" (true, null).
    /// Anything else, including negative numbers and minutes or seconds of 60 or more after a colon, is rejected.
    /// </summary>
    public static bool TryParseClock(string? input, out double? seconds)
    {
        seconds = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        var parts = input.Trim().Split(':');
        double total;
        if (parts.Length == 1)
        {
            if (!PlainSecondsPattern().IsMatch(parts[0]))
            {
                return false;
            }

            total = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
        }
        else if (parts.Length is 2 or 3)
        {
            var last = parts[^1];
            if (!ClockSecondsPattern().IsMatch(last))
            {
                return false;
            }

            var leading = parts[..^1];
            if (!WholeNumberPattern().IsMatch(leading[0]) || (leading.Length == 2 && !SexagesimalPattern().IsMatch(leading[1])))
            {
                return false;
            }

            var secondsPart = double.Parse(last, System.Globalization.CultureInfo.InvariantCulture);
            total = leading.Length == 1
                ? int.Parse(leading[0], System.Globalization.CultureInfo.InvariantCulture) * 60 + secondsPart
                : int.Parse(leading[0], System.Globalization.CultureInfo.InvariantCulture) * 3600
                    + int.Parse(leading[1], System.Globalization.CultureInfo.InvariantCulture) * 60 + secondsPart;
        }
        else
        {
            return false;
        }

        seconds = total;
        return true;
    }

    /// <summary>Seconds as "m:ss.s" (minutes are not capped), empty for null.</summary>
    public static string FormatClock(double? seconds)
    {
        if (!seconds.HasValue || !double.IsFinite(seconds.Value) || seconds.Value < 0)
        {
            return string.Empty;
        }

        var tenths = (long)Math.Round(seconds.Value * 10, MidpointRounding.AwayFromZero);
        var minutes = tenths / 600;
        var rest = tenths % 600;
        return $"{minutes}:{rest / 10:00}.{rest % 10}";
    }

    public static int TotalPages(int totalItems, int pageSize)
        => totalItems <= 0 || pageSize <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

    /// <summary>Page below 1 becomes 1; page beyond the last becomes the last (never negative for Skip).</summary>
    public static int ClampPage(int requested, int totalPages)
        => Math.Clamp(requested, 1, Math.Max(1, totalPages));
}
