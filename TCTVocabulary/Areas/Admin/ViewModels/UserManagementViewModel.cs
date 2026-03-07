using System.ComponentModel.DataAnnotations;

namespace TCTVocabulary.Areas.Admin.ViewModels
{
    // Main page ViewModel (list + filter state)
    public class UserManagementViewModel
    {
        public List<UserRowViewModel> Users { get; set; } = new();

        // Filter / search state
        public string? SearchQuery { get; set; }
        public string? RoleFilter { get; set; }
        public string? StatusFilter { get; set; }

        // KPI summary
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int DisabledUsers { get; set; }
    }

    // Row-level projection — never expose raw User entity
    public class UserRowViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int FolderCount { get; set; }
        public int SetCount { get; set; }
    }

    // Edit modal ViewModel
    public class EditUserViewModel
    {
        public int UserId { get; set; }

        [Required(ErrorMessage = "Họ tên không được để trống.")]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vai trò không được để trống.")]
        public string Role { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
