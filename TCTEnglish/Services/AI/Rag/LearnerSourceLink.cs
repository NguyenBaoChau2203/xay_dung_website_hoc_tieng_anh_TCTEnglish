using System.Globalization;
using System.Text.RegularExpressions;

namespace TCTEnglish.Services.AI.Rag;

/// <summary>
/// Server-owned local citation links. Model text never supplies the href.
/// </summary>
public static class LearnerSourceLink
{
    // Keep this read-only navigation allowlist aligned with ai-chat.js. Citation
    // links remain separately validated; a citation is not automatically a CTA.
    private const string NavigationPattern = @"^/(?:Speaking(?:/(?:Index|Practice/[1-9]\d*))?|Practice(?:/(?:HardWords|learn|test|match|flashcard|quiz|typing|listening|cloze))?|Sets(?:/[1-9]\d*)?|Goals(?:/Index)?|Account/(?:Profile|Settings|Login)|Premium|Billing/History|Notification/Index|AI/Chat|Reading/(?:Study|Quiz)/[1-9]\d*|Home/(?:Index|CreateSet|Folder|CreateClass|Class|Contact|Grammar|Privacypolicy|Termsofuse|Search|Reading(?:/(?:Study|Quiz|Practice)/[1-9]\d*)?|Listening(?:/Practice/[1-9]\d*)?|Writing(?:/Exercises|/Practice)?|(?:Study|QuizMode|WriteMode|MatchingMode|ClassDetail)/[1-9]\d*))$";

    public static bool IsNavigationAllowed(string? route)
    {
        if (!IsAllowed(route)) return false;
        var path = route!.Split('?')[0];
        if (route.Contains('?') && !path.Equals("/Home/Writing/Practice", StringComparison.OrdinalIgnoreCase)) return false;
        return Regex.IsMatch(path, NavigationPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(50));
    }

    public static string NavigationLabel(string route, string? title)
    {
        var path = route.ToLowerInvariant();
        if (path.StartsWith("/speaking")) return "Mở luyện nói";
        if (path.StartsWith("/home/writing")) return "Mở luyện viết";
        if (path.StartsWith("/home/listening")) return "Mở luyện nghe";
        if (path.StartsWith("/home/reading") || path.StartsWith("/reading/")) return "Mở luyện đọc";
        if (path.StartsWith("/practice/hardwords")) return "Ôn từ khó";
        if (path.StartsWith("/practice")) return "Luyện từ vựng";
        if (path.StartsWith("/goals")) return "Xem Goals và danh hiệu";
        if (path.StartsWith("/account/profile")) return "Mở hồ sơ cá nhân";
        if (path.StartsWith("/account/settings")) return "Mở cài đặt tài khoản";
        return string.IsNullOrWhiteSpace(title) ? "Mở trang liên quan" : "Mở " + title.Trim();
    }

    // Fixed classes from the Font Awesome family already loaded by the layouts.
    public static string ActionIcon(string? route)
    {
        var path = route?.ToLowerInvariant() ?? string.Empty;
        if (path.StartsWith("/speaking")) return "fa-solid fa-microphone";
        if (path.StartsWith("/home/writing")) return "fa-solid fa-pen";
        if (path.StartsWith("/home/listening")) return "fa-solid fa-headphones";
        if (path.StartsWith("/home/reading") || path.StartsWith("/reading/")) return "fa-solid fa-book-open";
        if (path.StartsWith("/practice")) return "fa-solid fa-layer-group";
        if (path.StartsWith("/goals")) return "fa-solid fa-bullseye";
        if (path.StartsWith("/account/profile")) return "fa-solid fa-user";
        if (path.StartsWith("/account/settings")) return "fa-solid fa-gear";
        return "fa-solid fa-arrow-right";
    }

    public static bool IsAllowed(string? route)
    {
        if (string.IsNullOrWhiteSpace(route)
            || !route.StartsWith('/')
            || route.StartsWith("//", StringComparison.Ordinal)
            || route.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (route.Contains("://", StringComparison.Ordinal)
            || route.Contains('\\')
            || route.Contains('<')
            || route.Contains('>')
            || route.Contains('"')
            || route.Contains('\n')
            || route.Contains('\r')
            || route.Contains(' ')
            || route.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        var path = route;
        var queryIndex = route.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            path = route[..queryIndex];
            var query = route[(queryIndex + 1)..];
            const string exercise = "exerciseId=";
            if (!query.StartsWith(exercise, StringComparison.Ordinal)
                || !int.TryParse(query[exercise.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var exerciseId)
                || exerciseId <= 0
                || exerciseId.ToString(CultureInfo.InvariantCulture) != query[exercise.Length..])
            {
                return false;
            }
        }

        return path.Length is > 1 and <= 200
            && path.All(item => char.IsAsciiLetterOrDigit(item) || item is '/' or '-' or '_');
    }
}
