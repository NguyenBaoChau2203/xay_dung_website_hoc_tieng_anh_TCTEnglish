using System;
using System.Collections.Generic;

namespace TCTEnglish.ViewModels;

/// <summary>Hiển thị 1 card bản dịch trong mục "Những bản dịch nổi bật"</summary>
public class ReadingTranslationCardViewModel
{
    public int Id { get; set; }
    public string AuthorName { get; set; } = null!;
    public string? AuthorAvatar { get; set; }
    public int LikeCount { get; set; }
    public int DislikeCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Vote của người dùng hiện tại (1=Like, -1=Dislike, 0=chưa vote)</summary>
    public int CurrentUserVote { get; set; } = 0;
    public string? TranslatedTitle { get; set; }
    public string TranslatedContent { get; set; } = null!;
}

/// <summary>Chi tiết đầy đủ 1 bản dịch (trả về khi click xem)</summary>
public class ReadingTranslationDetailViewModel : ReadingTranslationCardViewModel
{
    public int? AiScore { get; set; }
    public string? AiFeedback { get; set; }
    public bool? IsAiApproved { get; set; }
    public bool IsPublic { get; set; }
    public bool IsOwner { get; set; }
}

/// <summary>Request body khi tạo/cập nhật bản dịch</summary>
public class SubmitTranslationRequest
{
    public int PassageId { get; set; }
    public string? TranslatedTitle { get; set; }
    public string TranslatedContent { get; set; } = null!;
}

/// <summary>Kết quả đánh giá của AI</summary>
public class AiTranslationEvaluationResult
{
    public bool IsApproved { get; set; }
    public int Score { get; set; }
    public string Feedback { get; set; } = null!;
}

/// <summary>Request body khi vote like/dislike</summary>
public class VoteTranslationRequest
{
    public int TranslationId { get; set; }
    /// <summary>"like" hoặc "dislike"</summary>
    public string VoteType { get; set; } = null!;
}
