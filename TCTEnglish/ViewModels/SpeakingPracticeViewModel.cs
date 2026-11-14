using System.Collections.Generic;

namespace TCTVocabulary.ViewModels
{
    public class SpeakingPracticeViewModel
    {
        public int VideoId { get; set; }
        public string Title { get; set; } = null!;
        public string YoutubeId { get; set; } = null!;
        public bool IsPrivate { get; set; }
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
        public bool FluencyAvailable { get; set; }
        public bool CompletenessAvailable { get; set; }
        public SpeakingComparisonViewModel? Comparison { get; set; }
        public bool IsPracticed => TotalScore.HasValue;
    }

    public sealed class SpeakingComparisonViewModel
    {
        public bool Available { get; init; }
        public string? UnavailableReason { get; init; }
        public List<SpeakingComparisonOperationViewModel> Operations { get; init; } = new();
    }

    public sealed class SpeakingComparisonOperationViewModel
    {
        public string Kind { get; init; } = string.Empty;
        public string ReferenceToken { get; init; } = string.Empty;
        public string HypothesisToken { get; init; } = string.Empty;
    }
}
