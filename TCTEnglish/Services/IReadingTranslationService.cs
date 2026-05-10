using TCTEnglish.Models;

namespace TCTVocabulary.Services;

/// <summary>
/// Đánh giá bản dịch bài đọc bằng AI và quản lý CRUD bản dịch.
/// </summary>
public interface IReadingTranslationService
{
    /// <summary>Lấy bản dịch của user hiện tại cho 1 passage (hoặc null).</summary>
    Task<ReadingUserTranslation?> GetMyTranslationAsync(int userId, int passageId);

    /// <summary>Lấy danh sách bản dịch public của 1 passage.</summary>
    Task<List<ReadingTranslationListItem>> GetPublicTranslationsAsync(int passageId);

    /// <summary>Lưu/cập nhật bản dịch và gọi AI chấm điểm.</summary>
    Task<ReadingTranslationResult> SubmitTranslationAsync(int userId, int passageId, string? translatedTitle, string translatedContentJson);

    /// <summary>Public bản dịch (nếu AI đã duyệt).</summary>
    Task<ReadingTranslationResult> PublishTranslationAsync(int userId, int translationId);

    /// <summary>Xóa bản dịch của user.</summary>
    Task<ReadingTranslationResult> DeleteTranslationAsync(int userId, int translationId);

    /// <summary>Lấy chi tiết 1 bản dịch (cho trang CommunityTranslation).</summary>
    Task<ReadingTranslationDetailDto?> GetTranslationDetailAsync(int translationId, int? currentUserId);

    /// <summary>Bình chọn (like/dislike) bản dịch.</summary>
    Task<ReadingTranslationResult> VoteTranslationAsync(int userId, int translationId, TranslationVoteType voteType);
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record ReadingTranslationListItem(
    int Id,
    string UserFullName,
    string? UserAvatarUrl,
    int LikeCount,
    int DislikeCount,
    DateTime CreatedAtUtc);

public record ReadingTranslationDetailDto(
    int Id,
    int PassageId,
    string PassageTitle,
    string PassageContent,
    string? PassageImageUrl,
    string? TranslatedTitle,
    string TranslatedContent,
    int? AiScore,
    string? AiFeedback,
    bool? IsAiApproved,
    int LikeCount,
    int DislikeCount,
    string UserFullName,
    string? UserAvatarUrl,
    int UserId,
    int? CurrentUserVote);

public class ReadingTranslationResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int? AiScore { get; init; }
    public string? AiFeedback { get; init; }
    public bool? IsAiApproved { get; init; }
    public bool? IsPublic { get; init; }
    public int? TranslationId { get; init; }
    public int? LikeCount { get; init; }
    public int? DislikeCount { get; init; }

    public static ReadingTranslationResult Ok(string? message = null) =>
        new() { Success = true, Message = message };

    public static ReadingTranslationResult Fail(string message) =>
        new() { Success = false, Message = message };
}
