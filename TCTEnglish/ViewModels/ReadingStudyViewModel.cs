namespace TCTEnglish.ViewModels
{
    public class ReadingStudyViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = "";

        public string Content { get; set; } = "";

        public string? ImageUrl { get; set; }

        public string Level { get; set; } = "A1";

        public string? Topic { get; set; }

        public int? EstimatedTimeMinutes { get; set; }

        public bool IsCompleted { get; set; }

        public int? AttemptId { get; set; }

        public string Mode { get; set; } = "Practice";

        public int? TimeLimitSeconds { get; set; }

        public List<ReadingParagraphViewModel> Paragraphs { get; set; } = new();

        public List<QuestionViewModel> Questions { get; set; } = new();

        public List<ReadingVocabViewModel> Vocabulary { get; set; } = new();
    }

    public class ReadingParagraphViewModel
    {
        public int Id { get; set; }
        public int Order { get; set; }
        public string Content { get; set; } = "";
    }

    public class ReadingVocabViewModel
    {
        public int Id { get; set; }
        public string Word { get; set; } = "";
        public string? Ipa { get; set; }
        public string? PartOfSpeech { get; set; }
        public string? EnglishDefinition { get; set; }
        public string? VietnameseMeaning { get; set; }
        public string? Example { get; set; }
        public int? ParagraphNo { get; set; }
    }
}
