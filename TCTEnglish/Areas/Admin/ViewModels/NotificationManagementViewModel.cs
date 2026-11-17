using System.ComponentModel.DataAnnotations;

namespace TCTVocabulary.Areas.Admin.ViewModels;

public class CreateAnnouncementViewModel
{
    [Required(ErrorMessage = "Tiêu đề không được để trống")]
    [MaxLength(200, ErrorMessage = "Tiêu đề không vượt quá 200 ký tự")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nội dung không được để trống")]
    [MaxLength(1000, ErrorMessage = "Nội dung không vượt quá 1000 ký tự")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional path inside the site (checked by NotificationLinks), for example /Premium.</summary>
    [MaxLength(500, ErrorMessage = "Liên kết không vượt quá 500 ký tự")]
    public string? Url { get; set; }

    /// <summary>Made when the form opens; a double submit or a refresh sends the same batch again and is ignored.</summary>
    public Guid BatchId { get; set; }
}

public class AnnouncementHistoryViewModel
{
    public List<AnnouncementItemViewModel> Items { get; set; } = new();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
}

public class AnnouncementItemViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }               // UTC
    public string CreatedByName { get; set; } = string.Empty;
    public int RecipientCount { get; set; }
    public int ReadCount { get; set; }
    public string? Url { get; set; }
    public bool IsLegacy { get; set; }                    // written before batch keys existed
    public bool CopiesMayBeCleaned { get; set; }          // older than the retention period
}
