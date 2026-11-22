using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using TCTEnglish.Models.Reading;

namespace TCTEnglish.Areas.Admin.ViewModels;

public class ReadingPassageListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Topic { get; set; }
    public string Level { get; set; } = "A1";
    public int QuestionCount { get; set; }
    public string? CreatedByName { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Status { get; set; } = ReadingStatuses.Draft;
    public bool IsPublished { get; set; }
}

public class ReadingEditorViewModel
{
    public int? Id { get; set; }

    [Required]
    [StringLength(500)]
    public string Title { get; set; } = "";

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(200)]
    public string? Topic { get; set; }

    [Required]
    public string Level { get; set; } = "A1";

    public int? EstimatedTimeMinutes { get; set; }

    [Required]
    public string Content { get; set; } = "";

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>Optional upload; wins over <see cref="ImageUrl"/> when provided.</summary>
    [JsonIgnore]
    public IFormFile? ImageFile { get; set; }

    /// <summary>When true on edit, clears stored image and deletes local file if owned.</summary>
    [JsonIgnore]
    public bool RemoveImage { get; set; }

    public string? ExternalReadingKey { get; set; }

    /// <summary>
    /// ReadingCode as written in the Excel file before import remapping.
    /// Used so media ZIP files named {ReadingCode}.ext still match after auto-renumber.
    /// </summary>
    public string? ImportFileReadingCode { get; set; }

    public string Status { get; set; } = ReadingStatuses.Draft;

    public List<ReadingParagraphEditorViewModel> Paragraphs { get; set; } = new();

    public List<ReadingQuestionEditorViewModel> Questions { get; set; } = new();

    public List<ReadingVocabularyEditorViewModel> Vocabulary { get; set; } = new();

    /// <summary>
    /// When true, <see cref="Questions"/> from the form replace persisted questions.
    /// Prevents accidental wipe when the editor posts metadata only.
    /// </summary>
    public bool QuestionsBound { get; set; }

    /// <summary>
    /// When true, <see cref="Vocabulary"/> from the form replace persisted vocabulary.
    /// </summary>
    public bool VocabularyBound { get; set; }
}

public class ReadingParagraphEditorViewModel
{
    public int? Id { get; set; }
    public string? ExternalParagraphKey { get; set; }
    public int ParagraphOrder { get; set; }
    public string ParagraphContent { get; set; } = "";
}

public class ReadingQuestionEditorViewModel
{
    public int? Id { get; set; }
    public string? ExternalQuestionKey { get; set; }
    public int OrderIndex { get; set; }
    public string QuestionType { get; set; } = ReadingQuestionTypes.MultipleChoice;
    public string QuestionText { get; set; } = "";
    public string? ParagraphReference { get; set; }
    public int Points { get; set; } = 1;
    public string? CorrectAnswerText { get; set; }
    public string? Explanation { get; set; }
    public string? EvidenceText { get; set; }
    public int? EvidenceParagraphId { get; set; }
    /// <summary>Portable Excel ParagraphNo; resolved inside the destination passage.</summary>
    public int? EvidenceParagraphOrder { get; set; }
    public string? Paraphrase { get; set; }
    public List<ReadingOptionEditorViewModel> Options { get; set; } = new();
}

public class ReadingOptionEditorViewModel
{
    public int? Id { get; set; }
    public string? ExternalOptionKey { get; set; }
    public string OptionText { get; set; } = "";
    public bool IsCorrect { get; set; }
    public string? MatchKey { get; set; }
    public string? MatchSide { get; set; }
    public int OptionOrder { get; set; }
}

public class ReadingVocabularyEditorViewModel
{
    public int? Id { get; set; }
    public string? ExternalVocabularyKey { get; set; }
    public string Word { get; set; } = "";
    public string? Ipa { get; set; }
    public string? PartOfSpeech { get; set; }
    public string? EnglishDefinition { get; set; }
    public string? VietnameseMeaning { get; set; }
    public string? Example { get; set; }
    public int? ParagraphId { get; set; }
    /// <summary>Excel ParagraphNo; resolved to <see cref="ParagraphId"/> on save.</summary>
    public int? ParagraphOrder { get; set; }
}

public class ReadingReviewActionViewModel
{
    public int PassageId { get; set; }
    public string? Reason { get; set; }
}

/// <summary>UI helpers for Admin Reading Management (labels, badges, workflow visibility).</summary>
public static class ReadingAdminUi
{
    public static string StatusLabel(string? status) => status switch
    {
        ReadingStatuses.Draft => "Bản nháp",
        ReadingStatuses.ValidationFailed => "Không hợp lệ",
        ReadingStatuses.PendingReview => "Chờ duyệt",
        ReadingStatuses.RevisionRequired => "Cần sửa",
        ReadingStatuses.Rejected => "Từ chối",
        ReadingStatuses.Approved => "Đã duyệt",
        ReadingStatuses.Published => "Đã xuất bản",
        ReadingStatuses.Archived => "Lưu trữ",
        _ => string.IsNullOrWhiteSpace(status) ? "Không rõ" : status
    };

    public static string StatusModifier(string? status) =>
        string.IsNullOrWhiteSpace(status) ? "draft" : status.Trim().ToLowerInvariant();

    public static bool IsEditable(string? status) => status is
        ReadingStatuses.Draft or
        ReadingStatuses.ValidationFailed or
        ReadingStatuses.RevisionRequired or
        ReadingStatuses.Rejected or
        null or
        "";

    public static bool CanSubmitForReview(string? status) => status is
        ReadingStatuses.Draft or
        ReadingStatuses.ValidationFailed or
        ReadingStatuses.RevisionRequired or
        ReadingStatuses.Rejected;

    public static bool CanReview(string? status) => status == ReadingStatuses.PendingReview;

    public static bool CanPublish(string? status) => status == ReadingStatuses.Approved;

    public static string WorkflowHint(string? status) => status switch
    {
        ReadingStatuses.Draft => "Điền nội dung, lưu nháp rồi gửi kiểm duyệt khi sẵn sàng.",
        ReadingStatuses.ValidationFailed => "Bài chưa đạt điều kiện gửi duyệt — sửa nội dung rồi gửi lại.",
        ReadingStatuses.PendingReview => "Đang chờ duyệt. Có thể duyệt, yêu cầu sửa hoặc từ chối.",
        ReadingStatuses.RevisionRequired => "Reviewer yêu cầu chỉnh sửa — cập nhật rồi gửi lại kiểm duyệt.",
        ReadingStatuses.Rejected => "Bài đã bị từ chối — chỉnh sửa rồi gửi lại nếu cần.",
        ReadingStatuses.Approved => "Đã duyệt — xuất bản để học viên thấy bài.",
        ReadingStatuses.Published => "Bài đang hiển thị cho học viên.",
        ReadingStatuses.Archived => "Bài đã lưu trữ — không chỉnh sửa nội dung.",
        _ => "Theo dõi trạng thái và chọn thao tác phù hợp."
    };
}
