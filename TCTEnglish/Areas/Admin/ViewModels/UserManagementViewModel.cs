using System.ComponentModel.DataAnnotations;
using TCTVocabulary.Models;
using TCTVocabulary.Realtime;

namespace TCTVocabulary.Areas.Admin.ViewModels
{
    public class UserManagementViewModel
    {
        public List<UserRowViewModel> Users { get; set; } = new();
        public string? SearchQuery { get; set; }
        public string? RoleFilter { get; set; }
        public string? StatusFilter { get; set; }
        public int TotalUsers { get; set; }
        public int OnlineUsers { get; set; }
        public int OfflineUsers { get; set; }
        public int BlockedUsers { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 20;
        public int TotalFilteredCount { get; set; }
    }

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
        private string NormalizedRole => Roles.Normalize(Role);
        public string RoleBadgeClass => NormalizedRole switch
        {
            Roles.Admin => "badge bg-danger",
            Roles.Premium => "badge bg-primary",
            _ => "badge bg-secondary"
        };
        public string RoleIconClass => NormalizedRole switch
        {
            Roles.Admin => "bi-shield-lock-fill",
            Roles.Premium => "bi-gem",
            _ => "bi-person-fill"
        };
        public string StatusBadgeClass => Status switch
        {
            UserStatus.Online => "badge bg-success",
            UserStatus.Blocked => "badge bg-danger",
            _ => "badge bg-secondary"
        };
        public string StatusIconClass => Status switch
        {
            UserStatus.Online => "bi-check-circle-fill",
            UserStatus.Blocked => "bi-x-circle-fill",
            _ => "bi-moon-fill"
        };
        public string StatusLabel => Status switch
        {
            UserStatus.Online => "Online",
            UserStatus.Blocked => "Blocked",
            _ => "Offline"
        };
        public bool CanUnlock => Status == UserStatus.Blocked;
    }

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

    public class UpdateUserRequest
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
    }

    public class UserRowUpdateViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public UserStatus Status { get; set; }
        private string NormalizedRole => Roles.Normalize(Role);
        public string RoleBadgeClass => NormalizedRole switch
        {
            Roles.Admin => "badge bg-danger",
            Roles.Premium => "badge bg-primary",
            _ => "badge bg-secondary"
        };
        public string RoleIconClass => NormalizedRole switch
        {
            Roles.Admin => "bi-shield-lock-fill",
            Roles.Premium => "bi-gem",
            _ => "bi-person-fill"
        };
        public string StatusBadgeClass => Status switch
        {
            UserStatus.Online => "badge bg-success",
            UserStatus.Blocked => "badge bg-danger",
            _ => "badge bg-secondary"
        };
        public string StatusIconClass => Status switch
        {
            UserStatus.Online => "bi-check-circle-fill",
            UserStatus.Blocked => "bi-x-circle-fill",
            _ => "bi-moon-fill"
        };
        public string StatusLabel => Status switch
        {
            UserStatus.Online => "Online",
            UserStatus.Blocked => "Blocked",
            _ => "Offline"
        };
        public bool CanUnlock => Status == UserStatus.Blocked;
    }

    public class BlockUserRequest
    {
        public int UserId { get; set; }

        [Required(ErrorMessage = "Lý do khóa không được để trống.")]
        [StringLength(1000)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        public string Duration { get; set; } = string.Empty;
    }

    public class UnlockUserRequest
    {
        public int UserId { get; set; }
    }

    public class UpdateStatusRequest
    {
        public int UserId { get; set; }
        public UserStatus Status { get; set; }
    }

    public class PresenceSummaryViewModel
    {
        public int TotalUsers { get; set; }
        public int OnlineUsers { get; set; }
        public int OfflineUsers { get; set; }
        public int BlockedUsers { get; set; }
    }

    public class UserPresenceSnapshotViewModel
    {
        public List<AdminUserStatusChangedMessage> StatusUpdates { get; set; } = new();
        public PresenceSummaryViewModel Summary { get; set; } = new();
        public int TotalFilteredCount { get; set; }
    }
}
