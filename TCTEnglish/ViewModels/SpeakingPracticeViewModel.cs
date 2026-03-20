using System.Collections.Generic;

namespace TCTVocabulary.ViewModels
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
        public double? TotalScore { get; set; }
        public double? AccuracyScore { get; set; }
        public double? FluencyScore { get; set; }
        public double? CompletenessScore { get; set; }
        public bool IsPracticed => TotalScore.HasValue;
    }
}
