namespace TCTVocabulary.ViewModels
{
    public class DailyChallengeViewModel
    {
        public int CardId { get; set; }
        public string Term { get; set; } = null!;

        public List<AnswerOption> Options { get; set; } = new();
    }

    public class AnswerOption
    {
        public int CardId { get; set; }
        public string Definition { get; set; } = null!;
    }
}
