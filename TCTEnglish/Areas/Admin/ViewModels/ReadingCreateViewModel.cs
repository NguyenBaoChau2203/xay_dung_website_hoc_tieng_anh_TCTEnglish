using System.ComponentModel.DataAnnotations;

namespace TCTEnglish.Areas.Admin.ViewModels
{
    public class ReadingCreateViewModel
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        public string Level { get; set; }

        public string? Topic { get; set; }

        public string? ImageUrl { get; set; }

        public bool IsPublished { get; set; } = true;
    }
}