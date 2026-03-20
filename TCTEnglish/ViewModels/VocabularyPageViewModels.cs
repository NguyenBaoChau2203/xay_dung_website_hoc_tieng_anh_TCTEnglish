namespace TCTVocabulary.ViewModels
{
    public class VocabularyIndexViewModel
    {
        public VocabularyDashboardStatsViewModel Stats { get; set; } = new();
        public VocabularyContinueLearningViewModel? ContinueLearning { get; set; }
        public List<string> AllTags { get; set; } = new();
        public List<VocabularyFolderSectionViewModel> Folders { get; set; } = new();
    }

    public class VocabularyDashboardStatsViewModel
    {
        public int TotalCards { get; set; }
        public int TotalSets { get; set; }
        public int MasteredCards { get; set; }
        public int DueToday { get; set; }
        public int Streak { get; set; }
        public int LongestStreak { get; set; }
    }

    public class VocabularyContinueLearningViewModel
    {
        public int SetId { get; set; }
        public string SetName { get; set; } = string.Empty;
        public int CardCount { get; set; }
        public int RemainingCardCount { get; set; }
        public int ProgressPercentage { get; set; }
        public bool HasProgress { get; set; }
    }

    public class VocabularyFolderSectionViewModel
    {
        public int FolderId { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public int SetCount { get; set; }
        public List<VocabularySetListItemViewModel> Sets { get; set; } = new();
    }

    public class VocabularyFolderDetailViewModel
    {
        public int FolderId { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public int TotalSets { get; set; }
        public int TotalCards { get; set; }
        public int TotalViews { get; set; }
        public List<VocabularySetListItemViewModel> Sets { get; set; } = new();
    }

    public class VocabularySetListItemViewModel
    {
        public int SetId { get; set; }
        public string SetName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string DescriptionText { get; set; } = "Không có mô tả";
        public int CardCount { get; set; }
        public int MasteredCardCount { get; set; }
        public int DueTodayCount { get; set; }
        public int RemainingCardCount { get; set; }
        public int ProgressPercentage { get; set; }
        public int EstimatedMinutes { get; set; }
        public int ViewCount { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class VocabularySetDetailViewModel
    {
        public VocabularySetSummaryViewModel Set { get; set; } = new();
        public VocabularySetStatsViewModel Stats { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public List<VocabularyTopicSummaryViewModel> Topics { get; set; } = new();
        public List<VocabularyRelatedSetViewModel> RelatedSets { get; set; } = new();
    }

    public class VocabularyTopicsViewModel
    {
        public VocabularySetSummaryViewModel Set { get; set; } = new();
        public VocabularySetStatsViewModel Stats { get; set; } = new();
        public List<VocabularyTopicSummaryViewModel> Topics { get; set; } = new();
    }

    public class VocabularyTopicDetailViewModel
    {
        public VocabularySetSummaryViewModel Set { get; set; } = new();
        public VocabularyTopicSummaryViewModel Topic { get; set; } = new();
        public VocabularySetStatsViewModel Stats { get; set; } = new();
        public List<VocabularyTopicCardViewModel> Cards { get; set; } = new();
    }

    public class VocabularySetStatsViewModel
    {
        public int TotalCards { get; set; }
        public int LearnedCards { get; set; }
        public int RemainingCards { get; set; }
        public int Accuracy { get; set; }
        public int NewCards { get; set; }
        public int ReviewingCards { get; set; }
        public int MasteredCards { get; set; }
        public int DueToday { get; set; }
    }

    public class VocabularyTopicSummaryViewModel
    {
        public string TopicName { get; set; } = string.Empty;
        public int TotalCards { get; set; }
        public int LearnedCards { get; set; }
        public int Progress { get; set; }
    }

    public class VocabularyRelatedSetViewModel
    {
        public int SetId { get; set; }
        public string SetName { get; set; } = string.Empty;
        public int CardCount { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class VocabularyTopicCardViewModel
    {
        public int CardId { get; set; }
        public string Term { get; set; } = string.Empty;
        public string Definition { get; set; } = string.Empty;
        public string? Phonetic { get; set; }
        public string? Example { get; set; }
        public string? ExampleTranslation { get; set; }
        public string? ImageUrl { get; set; }
        public string StatusClass { get; set; } = "new";
        public string StatusText { get; set; } = "Mới";
    }
}
