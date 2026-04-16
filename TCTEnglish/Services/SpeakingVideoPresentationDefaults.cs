namespace TCTVocabulary.Services;

internal static class SpeakingVideoPresentationDefaults
{
    public const string DefaultTopic = "General";
    public const string UnassignedLevel = "Unassigned";
    public const string UntitledVideo = "Untitled video";
    public const string UntitledPlaylist = "Untitled playlist";

    public static string NormalizeTitle(string? title)
    {
        return string.IsNullOrWhiteSpace(title) ? UntitledVideo : title.Trim();
    }

    public static string NormalizeYoutubeId(string? youtubeId)
    {
        return youtubeId?.Trim() ?? string.Empty;
    }

    public static string NormalizeLevel(string? level)
    {
        return string.IsNullOrWhiteSpace(level)
            ? UnassignedLevel
            : level.Trim().ToUpperInvariant();
    }

    public static string NormalizeTopic(string? topic)
    {
        return string.IsNullOrWhiteSpace(topic) ? DefaultTopic : topic.Trim();
    }

    public static string NormalizePlaylistName(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? UntitledPlaylist : name.Trim();
    }

    public static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static string? ResolveThumbnailUrl(string? thumbnailUrl, string? youtubeId)
    {
        if (!string.IsNullOrWhiteSpace(thumbnailUrl))
        {
            return thumbnailUrl.Trim();
        }

        var normalizedYoutubeId = NormalizeYoutubeId(youtubeId);
        return string.IsNullOrWhiteSpace(normalizedYoutubeId)
            ? null
            : $"https://img.youtube.com/vi/{normalizedYoutubeId}/hqdefault.jpg";
    }
}
