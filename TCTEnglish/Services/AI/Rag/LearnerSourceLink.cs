using System.Globalization;

namespace TCTEnglish.Services.AI.Rag;

/// <summary>
/// Server-owned local citation links. Model text never supplies the href.
/// </summary>
public static class LearnerSourceLink
{
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
