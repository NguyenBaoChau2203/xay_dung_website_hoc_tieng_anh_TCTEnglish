using System.ComponentModel.DataAnnotations;

namespace TCTVocabulary.ViewModels
{
    public class SecuritySettingsViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập Email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string Email { get; set; } = null!;

        [Display(Name = "Mật khẩu hiện tại")]
        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }

        [Display(Name = "Mật khẩu mới")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
        public string? NewPassword { get; set; }

        [Display(Name = "Xác nhận Mật khẩu mới")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string? ConfirmPassword { get; set; }

        public bool IsGoogleLinked { get; set; }
        public bool IsFacebookLinked { get; set; }
        public string? ProofCode { get; set; }
        public string? ReturnSurface { get; set; }
        public string? PendingEmail { get; set; }
        public string PasswordStateText { get; set; } = string.Empty;
        public string EmailVerificationText { get; set; } = string.Empty;
        public bool DailyReminderEnabled { get; set; }
        public int ReminderHour { get; set; } = 20;
        public int ReminderMinute { get; set; }
        public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
        public bool PushOptIn { get; set; }
        public bool WeeklyEmailOptIn { get; set; }
        public string PushState { get; set; } = "Chưa bật";
        public string EmailState { get; set; } = "Chưa bật";
        public bool PushConfigured { get; set; }

        /// <summary>
        /// SOCIAL_LOGIN_ marker suggests an external-created account, but is not proof of a linked Google identity.
        /// </summary>
        public bool HasUnconfirmedSocialLoginMarker { get; set; }
    }
}
