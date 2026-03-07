namespace TCTVocabulary.Areas.Admin.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalClasses { get; set; }
        public int TotalSets { get; set; }

        // Trend data (new registrations in the last 7 days)
        public int NewUsersLast7Days { get; set; }
        public int NewClassesLast7Days { get; set; }
        public int NewSetsLast7Days { get; set; }

        // Recent users for the quick-glance table
        public List<RecentUserViewModel> RecentUsers { get; set; } = new();
    }

    public class RecentUserViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
