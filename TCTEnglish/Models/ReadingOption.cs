namespace TCTVocabulary.Models
{
    public class ReadingOption
    {
        public int Id { get; set; }

        public int QuestionId { get; set; }

        public string OptionText { get; set; } = null!;

        public bool IsCorrect { get; set; }

        public virtual ReadingQuestion Question { get; set; } = null!;
    }
}