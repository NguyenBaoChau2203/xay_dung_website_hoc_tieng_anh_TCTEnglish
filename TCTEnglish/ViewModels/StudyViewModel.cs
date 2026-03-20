namespace TCTVocabulary.ViewModels
{
    public class StudyViewModel
    {
        public VocabularySetSummaryViewModel Set { get; set; } = new();
        public List<VocabularyCardItemViewModel> Cards { get; set; } = new();
        public List<int> MasteredCardIds { get; set; } = new();
        public List<int> LearningCardIds { get; set; } = new();
        public int CurrentIndex { get; set; } = 1;
        public string? TopicName { get; set; }
        public bool IsReview { get; set; }
        public string StudyMode { get; set; } = "all";
        public int StudyTotal { get; set; }
    }

    public class VocabularySetSummaryViewModel
    {
        public int SetId { get; set; }
        public string SetName { get; set; } = string.Empty;
        public int? FolderId { get; set; }
        public string? FolderName { get; set; }
        public string? Description { get; set; }
    }

    public class VocabularyCardItemViewModel
    {
        public int CardId { get; set; }
        public string Term { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string? Phonetic { get; set; }
        public string? Example { get; set; }
        public string? ExampleTranslation { get; set; }
        public string Topic { get; set; } = "Chưa phân loại";
        public string LearningStatus { get; set; } = "New";
        public bool IsDueForReview { get; set; }
        public bool IsLearned { get; set; }
    }
}
