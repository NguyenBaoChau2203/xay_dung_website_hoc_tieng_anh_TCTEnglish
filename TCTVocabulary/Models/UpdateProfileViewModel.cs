using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TCTVocabulary.Models
{
    public class UpdateProfileViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = null!;

        public string? CurrentAvatarUrl { get; set; }

        [Display(Name = "Ảnh đại diện mới")]
        public IFormFile? AvatarFile { get; set; }
    }
}
