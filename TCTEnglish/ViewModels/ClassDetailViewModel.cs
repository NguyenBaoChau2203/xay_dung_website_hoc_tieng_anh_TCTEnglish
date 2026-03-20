namespace TCTVocabulary.ViewModels
{
    public class ClassDetailViewModel
    {
        public ClassSummaryViewModel Class { get; set; } = new();
        public List<ClassMemberItemViewModel> Members { get; set; } = new();
        public List<ClassMessageViewModel> Messages { get; set; } = new();
        public List<FolderOptionViewModel> MyFolders { get; set; } = new();
        public List<FolderOptionViewModel> SavedFolders { get; set; } = new();
        public List<ClassFolderItemViewModel> ClassFolders { get; set; } = new();
        public int CurrentUserId { get; set; }
        public bool IsOwner { get; set; }
        public bool IsMember { get; set; }
        public bool IsAdmin { get; set; }
        public bool CanViewPrivateContent { get; set; }
        public bool CanManageClass { get; set; }
        public bool CanJoinClass { get; set; }
    }

    public class ClassSummaryViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public int OwnerId { get; set; }
        public string? Description { get; set; }
        public bool HasPassword { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class ClassMemberItemViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
    }

    public class FolderOptionViewModel
    {
        public int FolderId { get; set; }
        public string FolderName { get; set; } = string.Empty;
    }

    public class ClassFolderItemViewModel
    {
        public int FolderId { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public int AddedByUserId { get; set; }
        public string AddedByUserName { get; set; } = string.Empty;
        public bool CanRemove { get; set; }
    }
}
