namespace TCTVocabulary.ViewModels
{
    public class ClassPageViewModel
    {
        public List<ClassCardViewModel> Classes { get; set; } = new();
        public List<ClassCardViewModel> ManagedClasses { get; set; } = new();
        public List<ClassCardViewModel> JoinedClasses { get; set; } = new();
        public bool CanCreateClass { get; set; }
        public bool IsCreatorRole { get; set; }
        public string PageTitle { get; set; } = "Lớp học của tôi";
        public string PageDescription { get; set; } = "Quản lý lớp bạn phụ trách và lớp bạn đang tham gia";
    }

    public class BrowseClassesPageViewModel
    {
        public bool CanCreateClass { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsCreatorRole { get; set; }
        public string Role { get; set; } = string.Empty;
    }

    public class ClassCardViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public bool IsOwner { get; set; }
        public bool IsAssistant { get; set; }
        public bool HasPassword { get; set; }
        public bool RequiresApproval { get; set; }
        public bool IsPublic { get; set; } = true;
        public int MemberCount { get; set; }
        public int PendingRequestCount { get; set; }
    }

    public class ClassSearchResultViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public bool HasPassword { get; set; }
        public bool RequiresApproval { get; set; }
        public bool IsPublic { get; set; } = true;
        public string? ImageUrl { get; set; }
    }
}
