namespace TCTVocabulary.ViewModels
{
    public class DashboardViewModel
    {
        public string? FullName { get; set; }
        public int Streak { get; set; }
        public int Goal { get; set; }

        public int FolderCount { get; set; }
        public int SetCount { get; set; }
        public int CardCount { get; set; }
        public int MasteredCount { get; set; }
        public int LearningCount { get; set; }
        public int NewCount { get; set; }

        public DailyChallengeViewModel? DailyChallenge { get; set; }
        public List<TodayFolderViewModel> TodayFolders { get; set; } = new();
        public List<DashboardHeatmapCellViewModel> HeatmapCells { get; set; } = new();

        public List<DailyFeatureTimeViewModel> FeatureTimeBreakdown { get; set; } = new();
        public RecentReadingViewModel? RecentInProcessReading { get; set; }
    }

    /// <summary>Thời gian học (phút) theo từng tính năng trong 1 ngày.</summary>
    public class DailyFeatureTimeViewModel
    {
        public string DayLabel { get; set; } = string.Empty;  // "T2" .. "CN"
        public string FullDate { get; set; } = string.Empty;  // "06/05" tooltip
        public bool IsToday { get; set; }
        public int FlashcardMinutes { get; set; }
        public int QuizMinutes      { get; set; }
        public int SpeakingMinutes  { get; set; }
        public int ReadingMinutes   { get; set; }
        public int ListeningMinutes { get; set; }
        public int WritingMinutes   { get; set; }
        public int GrammarMinutes   { get; set; }
        public int TotalMinutes => FlashcardMinutes + QuizMinutes + SpeakingMinutes
                                 + ReadingMinutes + ListeningMinutes + WritingMinutes + GrammarMinutes;
    }

    public class DashboardHeatmapCellViewModel
    {
        public string WeekLabel { get; set; } = string.Empty;
        public string DayLabel { get; set; } = string.Empty;
        public int ReviewedCards { get; set; }
    }

    public class TodayFolderViewModel
    {
        public int FolderId { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public int SetCount { get; set; }
        public string CreatorName { get; set; } = "Unknown";
    }
    public class RecentReadingViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? Level { get; set; }
        public string? Topic { get; set; }
        public DateTime LastViewed { get; set; }
    }
}
