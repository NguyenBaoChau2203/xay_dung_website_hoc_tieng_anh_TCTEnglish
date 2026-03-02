using System.Collections.Generic;

namespace TCTVocabulary.ViewModel
{
    public class SpeakingPracticeViewModel
    {
        public int VideoId { get; set; }
        public string Title { get; set; } = null!;
        public string YoutubeId { get; set; } = null!;
        public List<SpeakingSentenceViewModel> Sentences { get; set; } = new();
    }

    public class SpeakingSentenceViewModel
    {
        public int Id { get; set; }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public string Text { get; set; } = null!;
        public string VietnameseMeaning { get; set; } = null!;
    }
}
