using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace TCTVocabulary.Services;

public static class YoutubeUrlHelper
{
    private static readonly Regex YoutubeIdRegex = new("^[a-zA-Z0-9_-]{11}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string? NormalizeYoutubeId(string input)
    {
        var value = input?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (YoutubeIdRegex.IsMatch(value))
        {
            return value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var host = uri.Host.ToLowerInvariant();

        if (host.Contains("youtu.be", StringComparison.Ordinal))
        {
            var shortId = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return shortId is not null && YoutubeIdRegex.IsMatch(shortId) ? shortId : null;
        }

        if (!host.Contains("youtube.com", StringComparison.Ordinal))
        {
            return null;
        }

        var query = uri.Query.TrimStart('?');
        if (!string.IsNullOrWhiteSpace(query))
        {
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2, StringSplitOptions.None);
                if (parts.Length != 2 || !parts[0].Equals("v", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var candidate = Uri.UnescapeDataString(parts[1]);
                if (YoutubeIdRegex.IsMatch(candidate))
                {
                    return candidate;
                }
            }
        }

        var pathSegments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (pathSegments.Length >= 2 &&
            (pathSegments[0].Equals("shorts", StringComparison.OrdinalIgnoreCase) ||
             pathSegments[0].Equals("embed", StringComparison.OrdinalIgnoreCase)) &&
            YoutubeIdRegex.IsMatch(pathSegments[1]))
        {
            return pathSegments[1];
        }

        return null;
    }

    public static string BuildDefaultThumbnailUrl(string youtubeId)
    {
        return $"https://img.youtube.com/vi/{youtubeId}/hqdefault.jpg";
    }
}
