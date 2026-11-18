namespace TCTVocabulary.Models
{
    public class ReadingOption
    {
        public int Id { get; set; }

        public int QuestionId { get; set; }

        public string? ExternalOptionKey { get; set; }

        public string OptionText { get; set; } = null!;

        public bool IsCorrect { get; set; }

        /// <summary>For Matching: left/right pair key shared by a matched pair.</summary>
        public string? MatchKey { get; set; }

        /// <summary>For Matching: Left or Right side.</summary>
        public string? MatchSide { get; set; }

        public int OptionOrder { get; set; }

        public virtual ReadingQuestion Question { get; set; } = null!;
    }
}
