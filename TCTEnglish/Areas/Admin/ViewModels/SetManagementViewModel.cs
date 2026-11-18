using System.ComponentModel.DataAnnotations;
using TCTEnglish.Services.Admin;

namespace TCTVocabulary.Areas.Admin.ViewModels
{
    /// <summary>List page of vocabulary sets. Rows and figures come from IAdminVocabularyService.</summary>
    public class SetManagementIndexViewModel
    {
        public AdminSetListResult List { get; set; } = new();
        public AdminSetFigures Figures { get; set; } = new(0, 0, 0, 0);
        public string? Q { get; set; }
        /// <summary>"", "system" or "user" (query name kept as ownerFilter for old links).</summary>
        public string OwnerFilter { get; set; } = "";
        public int? FolderId { get; set; }
        public string Visibility { get; set; } = "";
        public string Sort { get; set; } = "newest";
    }

    /// <summary>Create / edit form of a system set. Posted fields are only these three.</summary>
    public class SetFormViewModel
    {
        public int? SetId { get; set; }

        [Display(Name = "Tên bộ từ vựng")]
        public string? Name { get; set; }

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Thư mục")]
        public int? FolderId { get; set; }

        // Display only (never posted back into the service).
        public bool IsSystem { get; set; } = true;
        public string OwnerName { get; set; } = "";
        public string Exposure { get; set; } = "Public";
        public IReadOnlyList<AdminFolderOption> Folders { get; set; } = Array.Empty<AdminFolderOption>();
        public IReadOnlyDictionary<string, string> Errors { get; set; } = new Dictionary<string, string>();
        public string? Summary { get; set; }
    }

    public class SetCardsViewModel
    {
        public AdminCardListResult Cards { get; set; } = new();
        public string? Q { get; set; }
        public string? Topic { get; set; }
        public string Quality { get; set; } = "";
    }

    /// <summary>Add / edit card form with per-field messages and the learner-card preview.</summary>
    public class CardFormViewModel
    {
        public int? CardId { get; set; }
        public int SetId { get; set; }
        public string SetName { get; set; } = "";
        public AdminCardInput Input { get; set; } = new();
        public IReadOnlyDictionary<string, string> Errors { get; set; } = new Dictionary<string, string>();
        public string? Summary { get; set; }
    }
}
