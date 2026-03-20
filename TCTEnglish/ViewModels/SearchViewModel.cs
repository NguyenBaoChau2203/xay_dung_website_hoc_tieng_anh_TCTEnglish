namespace TCTVocabulary.ViewModels
{
    public class SearchViewModel
    {
        public string Query { get; set; } = string.Empty;

        public List<FolderSearchResultViewModel> Folders { get; set; } = new();
        public List<SearchClassResultViewModel> Classes { get; set; } = new();
        public List<UserSearchResultViewModel> Users { get; set; } = new();

        public int TotalResults => Folders.Count + Classes.Count + Users.Count;
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
