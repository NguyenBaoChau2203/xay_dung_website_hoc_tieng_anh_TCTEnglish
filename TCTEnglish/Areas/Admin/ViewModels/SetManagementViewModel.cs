using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TCTVocabulary.Areas.Admin.ViewModels
{
    public class SetManagementIndexViewModel
    {
        public List<SetListItemViewModel> Sets { get; set; } = new();
        public List<FolderFilterOption> Folders { get; set; } = new();
        public string? SearchToken { get; set; }
        public int? FolderFilter { get; set; }
        public string? OwnerFilter { get; set; } // "all" | "system" | "user"
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
    }

    public class SetListItemViewModel
    {
        public int SetId { get; set; }
        public string SetName { get; set; } = null!;
        public string? Description { get; set; }
        public string OwnerName { get; set; } = null!;
        public string OwnerEmail { get; set; } = null!;
        public int OwnerId { get; set; }
        public string? FolderName { get; set; }
        public int? FolderId { get; set; }
        public int CardCount { get; set; }
        public int ViewCount { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool IsSystemSet { get; set; } // true nếu OwnerEmail == "system@tct.local"
    }

    public class FolderFilterOption
    {
        public int FolderId { get; set; }
        public string FolderName { get; set; } = null!;
        public string OwnerName { get; set; } = null!;
    }

    public class SetCreateEditViewModel
    {
        public int? SetId { get; set; } // null = create

        [Required(ErrorMessage = "Tên bộ từ vựng không được để trống")]
        [Display(Name = "Tên bộ từ vựng")]
        public string SetName { get; set; } = null!;

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Thư mục")]
        public int? FolderId { get; set; }

        [Display(Name = "Chủ sở hữu")]
        public int? OwnerId { get; set; }

        public List<FolderFilterOption> AvailableFolders { get; set; } = new();
        public List<OwnerOption> AvailableOwners { get; set; } = new();
    }

    public class OwnerOption
    {
        public int UserId { get; set; }
        public string DisplayName { get; set; } = null!; // "FullName (email)"
    }

    public class SetCardsViewModel
    {
        public int SetId { get; set; }
        public string SetName { get; set; } = null!;
        public string? FolderName { get; set; }
        public List<CardItemViewModel> Cards { get; set; } = new();
    }

    public class CardItemViewModel
    {
        public int CardId { get; set; }
        public string Term { get; set; } = null!;
        public string Definition { get; set; } = null!;
        public string? ImageUrl { get; set; }
        public string? Phonetic { get; set; }
        public string? Example { get; set; }
        public string? ExampleTranslation { get; set; }
        public string? Topic { get; set; }
    }

    public class CardCreateEditViewModel
    {
        public int? CardId { get; set; } // null = create
        public int SetId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập thuật ngữ.")]
        [MaxLength(255)]
        public string Term { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập định nghĩa.")]
        public string Definition { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        [MaxLength(100)]
        public string? Phonetic { get; set; }

        public string? Example { get; set; }
        public string? ExampleTranslation { get; set; }

        [MaxLength(100)]
        public string? Topic { get; set; }

        // For display context
        public string? SetName { get; set; }
    }
}
