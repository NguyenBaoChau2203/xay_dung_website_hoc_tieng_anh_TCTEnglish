// Template: ViewModel
// Usage: Copy this template when creating a new ViewModel for TCTEnglish
// File location: TCTEnglish/ViewModels/{Feature}ViewModel.cs
//
// Instructions:
//   1. Replace {Feature} with your feature name
//   2. Add properties relevant to your feature
//   3. Use Data Annotations for input validation
//   4. Separate input ViewModels from display ViewModels when needed

using System.ComponentModel.DataAnnotations;

namespace TCTEnglish.ViewModels
{
    // ============================================================
    // Main ViewModel (used for both Create and Edit forms)
    // ============================================================
    public class {Feature}ViewModel
    {
        public int Id { get; set; }

        // Required string input:
        [Required(ErrorMessage = "Name is required.")]
        [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        // Optional string:
        [MaxLength(1000)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        // Enum/select:
        [Required]
        [Display(Name = "Level")]
        public string Level { get; set; } = "B1";

        // Number with range:
        [Range(1, 100, ErrorMessage = "Value must be between 1 and 100.")]
        [Display(Name = "Daily Target")]
        public int DailyTarget { get; set; } = 10;

        // Display-only (not from user input):
        public DateTime CreatedAt { get; set; }
        public string? OwnerName { get; set; }
        public int ItemCount { get; set; }
    }

    // ============================================================
    // List ViewModel (lightweight, used for index/list pages)
    // ============================================================
    public class {Feature}ListViewModel
    {
        public List<{Feature}ViewModel> Items { get; set; } = [];
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public string? SearchTerm { get; set; }
        public int TotalCount { get; set; }

        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    // ============================================================
    // Detail ViewModel (for detail/show pages with related data)
    // ============================================================
    public class {Feature}DetailViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsOwner { get; set; }  // Used to show/hide edit controls

        // Related data:
        public List<RelatedItemViewModel> Items { get; set; } = [];

        // Stats:
        public int TotalItems => Items.Count;
        public int CompletedItems => Items.Count(i => i.IsCompleted);
        public double CompletionPercentage =>
            TotalItems > 0 ? (double)CompletedItems / TotalItems * 100 : 0;
    }

    // ============================================================
    // Related item ViewModel (nested in detail)
    // ============================================================
    public class RelatedItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
    }
}
