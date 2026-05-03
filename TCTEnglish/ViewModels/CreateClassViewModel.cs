namespace TCTVocabulary.ViewModels
{
    public class CreateClassViewModel
    {
        public string ClassName { get; set; } = null!;
        public string? Description { get; set; }
        public string? Password { get; set; }
        public IFormFile? Avatar { get; set; }

        // --- Các tính năng mới ---
        public bool RequiresApproval { get; set; } // Phê duyệt thành viên
        public bool IsChatLocked { get; set; }    // Khóa chat toàn lớp[cite: 1]
        public bool AllowMemberToPost { get; set; } // Quyền thêm Folder[cite: 1]
    }
}
