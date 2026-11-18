using System;

namespace TCTEnglish.ViewModels;

public sealed class RecentSpeakingVideoViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string YoutubeId { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? Duration { get; set; }
    public int SentenceCount { get; set; }
    public bool IsPrivate { get; set; }
    public DateTime LastOpenedAtUtc { get; set; }
}
