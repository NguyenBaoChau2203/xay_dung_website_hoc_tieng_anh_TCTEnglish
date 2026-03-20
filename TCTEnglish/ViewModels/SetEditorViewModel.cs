using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace TCTVocabulary.ViewModels
{
    public class SetEditorViewModel
    {
        public int? SetId { get; set; }

        public int? FolderId { get; set; }

        [Display(Name = "Tiêu đề")]
        public string SetName { get; set; } = string.Empty;

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        public List<SetCardEditorItemViewModel> Cards { get; set; } = new();

        public void EnsureCardSlot()
        {
            if (Cards.Count == 0)
            {
                Cards.Add(new SetCardEditorItemViewModel());
            }
        }
    }

    public class SetCardEditorItemViewModel
    {
        public string Term { get; set; } = string.Empty;

        public string Definition { get; set; } = string.Empty;

        public string? ExistingImageUrl { get; set; }

        public IFormFile? ImageFile { get; set; }

        public bool HasImage => !string.IsNullOrWhiteSpace(ExistingImageUrl);
    }
}
