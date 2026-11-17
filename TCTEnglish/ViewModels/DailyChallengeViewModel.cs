namespace TCTVocabulary.ViewModels
{
    public class DailyChallengeViewModel
    {
        public int CardId { get; set; }
        public string Term { get; set; } = null!;
        public string ChallengeToken { get; set; } = string.Empty;

        public List<AnswerOption> Options { get; set; } = new();

        /// <summary>True when the learner already completed today's Daily Challenge.</summary>
        public bool IsCompletedToday { get; set; }
    }

    public class AnswerOption
    {
        public int CardId { get; set; }
        public string Definition { get; set; } = null!;
    }
}
