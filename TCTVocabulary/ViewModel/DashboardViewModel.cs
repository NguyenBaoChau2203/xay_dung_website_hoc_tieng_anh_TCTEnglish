using TCTVocabulary.Models;

namespace TCTVocabulary.ViewModel
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
        public List<Folder> TodayFolders { get; set; } = new();
    }
}
