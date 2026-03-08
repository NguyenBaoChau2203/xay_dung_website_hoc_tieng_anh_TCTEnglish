using System.ComponentModel.DataAnnotations;
using TCTVocabulary.Models;

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
        public int OnlineUsers { get; set; }
        public int OfflineUsers { get; set; }
        public int BlockedUsers { get; set; }
    }

    // Row-level projection — never expose raw User entity
    public class UserRowViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = string.Empty;
        public UserStatus Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int FolderCount { get; set; }
        public int SetCount { get; set; }
        public string? LockReason { get; set; }
        public DateTime? LockExpiry { get; set; }
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

        public UserStatus Status { get; set; }
    }

    // Block user request DTO
    public class BlockUserRequest
    {
        public int UserId { get; set; }

        [Required(ErrorMessage = "Lý do khóa không được để trống.")]
        [StringLength(1000)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        public string Duration { get; set; } = string.Empty;
    }

    // Unlock user request DTO
    public class UnlockUserRequest
    {
        public int UserId { get; set; }
    }
}
