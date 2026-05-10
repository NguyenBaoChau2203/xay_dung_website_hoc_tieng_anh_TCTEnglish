using System;
using System.Collections.Generic;
using TCTVocabulary.Models;

namespace TCTEnglish.Models;

public class ReadingUserTranslation
{
    public int Id { get; set; }

    public int ReadingPassageId { get; set; }

    public int UserId { get; set; }

    /// <summary>Bản dịch tiêu đề bài đọc (tuỳ chọn)</summary>
    public string? TranslatedTitle { get; set; }

    /// <summary>Nội dung bản dịch toàn bài – lưu dạng JSON: [{"original":"...","translated":"..."}]</summary>
    public string TranslatedContent { get; set; } = null!;

    /// <summary>Điểm AI đánh giá (0-100). Null = chưa đánh giá.</summary>
    public int? AiScore { get; set; }

    /// <summary>Nhận xét của AI</summary>
    public string? AiFeedback { get; set; }

    /// <summary>AI chấp nhận bản dịch (true = đạt, false = không đạt, null = chưa đánh giá)</summary>
    public bool? IsAiApproved { get; set; }

    /// <summary>Người dùng đã chọn public bản dịch cho cộng đồng</summary>
    public bool IsPublic { get; set; } = false;

    /// <summary>Tổng số like (cache, sync từ ReadingTranslationVotes)</summary>
    public int LikeCount { get; set; } = 0;

    /// <summary>Tổng số dislike (cache)</summary>
    public int DislikeCount { get; set; } = 0;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // ─── Navigation ───────────────────────────────────────────────────────────
    public virtual ReadingPassage ReadingPassage { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual ICollection<ReadingTranslationVote> Votes { get; set; } = new List<ReadingTranslationVote>();
}
