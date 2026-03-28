using System.ComponentModel.DataAnnotations;

namespace TCTVocabulary.Areas.Admin.ViewModels;

public sealed class WritingExerciseIndexViewModel
{
    public List<WritingExerciseListItemViewModel> Exercises { get; set; } = new();
    public List<WritingExerciseOptionViewModel> LevelOptions { get; set; } = new();
    public List<WritingExerciseOptionViewModel> ContentTypeOptions { get; set; } = new();
    public List<WritingExerciseOptionViewModel> TopicOptions { get; set; } = new();
    public List<WritingExerciseOptionViewModel> VisibilityOptions { get; set; } = new();
}

public sealed class WritingExerciseListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string LevelLabel { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string ContentTypeLabel { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string PreviewText { get; set; } = string.Empty;
    public int SentenceCount { get; set; }
    public int ParagraphCount { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class WritingExerciseOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class WritingExerciseFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tiêu đề bài viết là bắt buộc.")]
    [Display(Name = "Tiêu đề")]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Trình độ là bắt buộc.")]
    [Display(Name = "Trình độ")]
    [StringLength(50)]
    public string Level { get; set; } = "beginner";

    [Required(ErrorMessage = "Loại nội dung là bắt buộc.")]
    [Display(Name = "Loại nội dung")]
    [StringLength(50)]
    public string ContentType { get; set; } = "emails";

    [Required(ErrorMessage = "Chủ đề là bắt buộc.")]
    [Display(Name = "Chủ đề")]
    [StringLength(100)]
    public string Topic { get; set; } = "General";

    [Required(ErrorMessage = "Preview text là bắt buộc.")]
    [Display(Name = "Preview text")]
    [StringLength(1000)]
    public string PreviewText { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full text tiếng Việt là bắt buộc.")]
    [Display(Name = "Full text tiếng Việt")]
    public string FullVietnameseText { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full text tiếng Anh tham chiếu là bắt buộc.")]
    [Display(Name = "Full text tiếng Anh")]
    public string FullEnglishText { get; set; } = string.Empty;

    [Display(Name = "Hiển thị cho học viên")]
    public bool IsPublished { get; set; } = true;

    public List<WritingExerciseSentenceInputViewModel> Sentences { get; set; } = new();
    public List<WritingExerciseOptionViewModel> LevelOptions { get; set; } = new();
    public List<WritingExerciseOptionViewModel> ContentTypeOptions { get; set; } = new();
}

public sealed class WritingExerciseCreateViewModel : WritingExerciseFormViewModel
{
}

public sealed class WritingExerciseEditViewModel : WritingExerciseFormViewModel
{
    public DateTime CreatedAt { get; set; }
}

public sealed class WritingExerciseSentenceInputViewModel
{
    public string VietnameseText { get; set; } = string.Empty;
    public string EnglishMeaning { get; set; } = string.Empty;
    public bool BreakAfter { get; set; }
}
