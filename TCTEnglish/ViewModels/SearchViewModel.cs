namespace TCTVocabulary.ViewModels
{
    public class SearchViewModel
    {
        public string Query { get; set; } = string.Empty;

        public List<NavMenuSearchResultViewModel> NavMenus { get; set; } = new();
        public List<FolderSearchResultViewModel> Folders { get; set; } = new();
        public List<SearchClassResultViewModel> Classes { get; set; } = new();
        public List<UserSearchResultViewModel> Users { get; set; } = new();

        public int TotalResults => NavMenus.Count + Folders.Count + Users.Count;
    }

    public class NavMenuSearchResultViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string IconClass { get; set; } = "fas fa-link";
    }

    public class FolderSearchResultViewModel
    {
        public int FolderId { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public string CreatorName { get; set; } = string.Empty;
    }

    public class SearchClassResultViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string MembershipLabel { get; set; } = "Chưa tham gia";
    }

    public class UserSearchResultViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }

        public string AvatarInitial =>
            !string.IsNullOrWhiteSpace(FullName)
                ? FullName[0].ToString().ToUpperInvariant()
                : (!string.IsNullOrWhiteSpace(Email) ? Email[0].ToString().ToUpperInvariant() : "?");
    }
}
