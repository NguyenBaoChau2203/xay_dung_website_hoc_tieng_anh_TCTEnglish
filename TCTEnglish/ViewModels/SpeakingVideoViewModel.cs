using System;

namespace TCTVocabulary.ViewModels
{
    public class SpeakingVideoViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string YoutubeId { get; set; } = null!;
        public string Level { get; set; } = null!;
        public string Topic { get; set; } = null!;
        public string? ThumbnailUrl { get; set; }
        public string? Duration { get; set; }
        public int SentenceCount { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsLocked { get; set; }
        public string? ImportStatus { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
