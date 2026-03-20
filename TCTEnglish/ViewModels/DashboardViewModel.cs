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

        public DailyChallengeViewModel? DailyChallenge { get; set; }
        public List<TodayFolderViewModel> TodayFolders { get; set; } = new();
    }

    public class TodayFolderViewModel
    {
        public int FolderId { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public int SetCount { get; set; }
        public string CreatorName { get; set; } = "Unknown";
    }
}
