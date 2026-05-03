namespace TCTVocabulary.Models
{
    public partial class Class
    {
        public int ClassId { get; set; }

        public string ClassName { get; set; } = null!;

        public int OwnerId { get; set; }

        // Mật khẩu lớp (hash)
        public string? PasswordHash { get; set; }

        public bool HasPassword { get; set; }

        // Avatar lớp
        public string? ImageUrl { get; set; }

        // Mô tả lớp
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; }

        // ===== Navigation =====
        public virtual User Owner { get; set; } = null!;

        public virtual ICollection<ClassMessage> ClassMessages { get; set; } = new List<ClassMessage>();
        public virtual ICollection<ClassFolder> ClassFolders { get; set; }
            = new List<ClassFolder>();
        public virtual ICollection<ClassMember> ClassMembers { get; set; }
    = new List<ClassMember>();
        // Khóa chat toàn bộ thành viên (chỉ Trưởng/Phó được chat)
        public bool IsChatLocked { get; set; } = false;

        // Cho phép thành viên tự do thêm Folder hay không
        public bool AllowMemberToPost { get; set; } = false;

        // Danh sách đen
        public virtual ICollection<ClassBlacklist> BlacklistedUsers { get; set; } = new List<ClassBlacklist>();
        // Tùy chọn: Phải được duyệt mới được vào lớp
        public bool RequiresApproval { get; set; } = false;

        // Navigation tới các yêu cầu đang chờ
        public virtual ICollection<ClassJoinRequest> JoinRequests { get; set; }
            = new List<ClassJoinRequest>();
    }
}
