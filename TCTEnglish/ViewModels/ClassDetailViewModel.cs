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
        public bool IsAssistant { get; set; }   // thêm dòng này
        public bool IsMember { get; set; }
        public bool IsAdmin { get; set; }

        public bool CanViewPrivateContent { get; set; }
        public bool CanManageClass { get; set; }
        public bool CanJoinClass { get; set; }
        public List<ClassJoinRequestItemViewModel> JoinRequests { get; set; } = new();
        public bool CanChatWhenLocked { get; set; } // thêm dòng này
    }

    public class ClassSummaryViewModel
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public int OwnerId { get; set; }
        public string? Description { get; set; }
        public bool HasPassword { get; set; }
        public string? ImageUrl { get; set; }
        public bool RequiresApproval { get; set; }
        public bool IsChatLocked { get; set; }
        public bool AllowMemberToPost { get; set; }
    }

    public class ClassMemberItemViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        // THÊM PHẦN ROLE
        // =========================
        public TCTVocabulary.Models.ClassRole Role { get; set; }

        public bool IsMuted { get; set; }

        public DateTime JoinedAt { get; set; }
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
    // NEW: ViewModel cho yêu cầu tham gia lớp
    public class ClassJoinRequestItemViewModel
    {
        public int RequestId { get; set; }

        public int UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public string? RequestMessage { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
