using TCTVocabulary.Models;

namespace TCTVocabulary.Areas.Admin.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int SelectedRangeDays { get; set; } = 7;

        public DashboardKpiCardViewModel TotalUsersKpi { get; set; } = new();
        public DashboardKpiCardViewModel ActiveLearnersKpi { get; set; } = new();
        public DashboardKpiCardViewModel CompletionsKpi { get; set; } = new();
        public DashboardKpiCardViewModel CatalogKpi { get; set; } = new();

        public List<DashboardDailyPointViewModel> DailyActivity { get; set; } = new();
        public List<DashboardSkillBarViewModel> SkillBars { get; set; } = new();
        public List<DashboardToolCardViewModel> Tools { get; set; } = new();
        public List<DashboardAttentionItemViewModel> AttentionItems { get; set; } = new();
        public List<DashboardActivityItemViewModel> ActivityFeed { get; set; } = new();
        public List<RecentUserViewModel> RecentUsers { get; set; } = new();

        // Legacy totals kept for any partial reuse / tests
        public int TotalUsers { get; set; }
        public int TotalClasses { get; set; }
        public int TotalSets { get; set; }
        public int NewUsersLast7Days { get; set; }
        public int NewClassesLast7Days { get; set; }
        public int NewSetsLast7Days { get; set; }
    }

    public class DashboardKpiCardViewModel
    {
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
        public int? Delta { get; set; }
        public string? DeltaLabel { get; set; }
        public string IconClass { get; set; } = "bi-circle";
        public string Tone { get; set; } = "primary";
    }

    public class DashboardDailyPointViewModel
    {
        public DateTime Date { get; set; }
        public int Completions { get; set; }
        public int XpEarned { get; set; }
        public int ActiveLearners { get; set; }
    }

    public class DashboardSkillBarViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Percent { get; set; }
        public string Controller { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi-circle";
    }

    public class DashboardToolCardViewModel
    {
        public string Title { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Controller { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi-circle";
        public string Tone { get; set; } = "primary";
    }

    public class DashboardAttentionItemViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Severity { get; set; } = "warning";
        public string? Href { get; set; }
    }

    public class DashboardActivityItemViewModel
    {
        public string Message { get; set; } = string.Empty;
        public DateTime OccurredOn { get; set; }
        public string SkillLabel { get; set; } = string.Empty;
    }

    public class RecentUserViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public UserStatus Status { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
