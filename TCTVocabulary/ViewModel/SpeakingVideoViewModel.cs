namespace TCTVocabulary.ViewModel
{
    public class SpeakingVideoViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string YoutubeId { get; set; } = null!;
        public string Level { get; set; } = null!;
        public string ThumbnailUrl { get; set; } = null!;
        public int SentenceCount { get; set; }
    }
}
