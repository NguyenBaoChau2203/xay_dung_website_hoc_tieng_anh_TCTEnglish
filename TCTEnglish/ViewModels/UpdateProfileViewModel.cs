using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using TCTEnglish.ViewModels;

namespace TCTVocabulary.ViewModels
{
    public class UpdateProfileViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = null!;

        public string? CurrentAvatarUrl { get; set; }

        [Display(Name = "Ảnh đại diện mới")]
        public IFormFile? Avatar { get; set; }

        // Stats displayed on profile page
        public int StreakDays { get; set; }

        // Badges earned by the user (unlocked only)
        public List<GoalsBadgeViewModel> EarnedBadges { get; set; } = new();
    }
}
