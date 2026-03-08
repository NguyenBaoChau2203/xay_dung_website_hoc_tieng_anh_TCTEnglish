using System.ComponentModel.DataAnnotations;

namespace TCTVocabulary.Models
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập Email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string Email { get; set; } = null!;

        [Display(Name = "Mật khẩu hiện tại (nhập nếu muốn đổi mật khẩu)")]
        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }

        [Display(Name = "Mật khẩu mới")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
        public string? NewPassword { get; set; }

        [Display(Name = "Xác nhận Mật khẩu mới")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string? ConfirmNewPassword { get; set; }
    }
}
