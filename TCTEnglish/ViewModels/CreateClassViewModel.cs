using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TCTVocabulary.ViewModels
{
    public class CreateClassViewModel
    {
        [Required]
        public string ClassName { get; set; } = null!;

        public string? Description { get; set; }
        public string? Password { get; set; }
        public IFormFile? Avatar { get; set; }

        public bool RequiresApproval { get; set; }
        public bool IsChatLocked { get; set; }
        public bool AllowMemberToPost { get; set; }

        /// <summary>True = discoverable; false = private (code/link only).</summary>
        public bool IsPublic { get; set; } = true;

        /// <summary>Unique join code; generated server-side when empty.</summary>
        [StringLength(16)]
        public string? JoinCode { get; set; }
    }
}
