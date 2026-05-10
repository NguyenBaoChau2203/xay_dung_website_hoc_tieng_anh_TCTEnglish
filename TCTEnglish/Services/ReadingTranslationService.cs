using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TCTEnglish.Models;
using TCTEnglish.Services.AI;
using TCTVocabulary.Models;
using System.Text.Json;

namespace TCTVocabulary.Services;

public sealed class ReadingTranslationService : IReadingTranslationService
{
    private const int ApprovalThreshold = 70;

    private const string EvaluationSystemPrompt = """
        You evaluate English-to-Vietnamese translations of reading passages for Vietnamese ESL learners.
        The learner reads an English passage and writes a Vietnamese translation.
        Evaluate accuracy, naturalness, and completeness.
        Return strict JSON with these keys only:
        score, feedback, isApproved
        - "score": integer 0-100 representing translation quality.
        - "feedback": a short Vietnamese paragraph (2-4 sentences) with specific, constructive comments about what was good and what can be improved.
        - "isApproved": true if score >= 70, false otherwise.
        Do not include markdown, code fences, or extra keys.
        """;

    private readonly DbflashcardContext _context;
    private readonly IAiProviderClient _providerClient;
    private readonly ILogger<ReadingTranslationService> _logger;

    public ReadingTranslationService(
        DbflashcardContext context,
        IAiProviderClient providerClient,
        ILogger<ReadingTranslationService> logger)
    {
        _context = context;
        _providerClient = providerClient;
        _logger = logger;
    }

    // ─── GetMyTranslation ─────────────────────────────────────────────────────
    public async Task<ReadingUserTranslation?> GetMyTranslationAsync(int userId, int passageId)
    {
        return await _context.ReadingUserTranslations
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId && t.ReadingPassageId == passageId);
    }

    // ─── GetPublicTranslations ────────────────────────────────────────────────
    public async Task<List<ReadingTranslationListItem>> GetPublicTranslationsAsync(int passageId)
    {
        return await _context.ReadingUserTranslations
            .AsNoTracking()
            .Where(t => t.ReadingPassageId == passageId && t.IsPublic && t.IsAiApproved == true)
            .OrderByDescending(t => t.LikeCount)
            .ThenByDescending(t => t.CreatedAtUtc)
            .Select(t => new ReadingTranslationListItem(
                t.Id,
                t.User.FullName ?? "Ẩn danh",
                t.User.AvatarUrl,
                t.LikeCount,
                t.DislikeCount,
                t.CreatedAtUtc))
            .ToListAsync();
    }

    // ─── SubmitTranslation ────────────────────────────────────────────────────
    public async Task<ReadingTranslationResult> SubmitTranslationAsync(
        int userId, int passageId, string? translatedTitle, string translatedContentJson)
    {
        var passage = await _context.ReadingPassages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == passageId);

        if (passage == null)
            return ReadingTranslationResult.Fail("Bài đọc không tồn tại.");

        // Validate JSON
        try
        {
            using var doc = JsonDocument.Parse(translatedContentJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return ReadingTranslationResult.Fail("Dữ liệu dịch không hợp lệ.");
        }
        catch (JsonException)
        {
            return ReadingTranslationResult.Fail("Dữ liệu dịch không hợp lệ.");
        }

        // Call AI evaluation
        var (aiScore, aiFeedback, isApproved) = await EvaluateWithAiAsync(passage, translatedTitle, translatedContentJson);

        // Find or create
        var existing = await _context.ReadingUserTranslations
            .FirstOrDefaultAsync(t => t.UserId == userId && t.ReadingPassageId == passageId);

        if (existing != null)
        {
            existing.TranslatedTitle = translatedTitle;
            existing.TranslatedContent = translatedContentJson;
            existing.AiScore = aiScore;
            existing.AiFeedback = aiFeedback;
            existing.IsAiApproved = isApproved;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            // Nếu chỉnh sửa thì reset public status nếu AI không duyệt
            if (isApproved != true)
                existing.IsPublic = false;
        }
        else
        {
            existing = new ReadingUserTranslation
            {
                UserId = userId,
                ReadingPassageId = passageId,
                TranslatedTitle = translatedTitle,
                TranslatedContent = translatedContentJson,
                AiScore = aiScore,
                AiFeedback = aiFeedback,
                IsAiApproved = isApproved
            };
            _context.ReadingUserTranslations.Add(existing);
        }

        await _context.SaveChangesAsync();

        return new ReadingTranslationResult
        {
            Success = true,
            Message = isApproved == true
                ? "Bản dịch của bạn đã được AI chấp nhận! Bạn có thể chia sẻ cho cộng đồng."
                : "Bản dịch chưa đạt yêu cầu. Hãy tham khảo nhận xét của AI và thử lại.",
            AiScore = aiScore,
            AiFeedback = aiFeedback,
            IsAiApproved = isApproved,
            IsPublic = existing.IsPublic,
            TranslationId = existing.Id
        };
    }

    // ─── PublishTranslation ───────────────────────────────────────────────────
    public async Task<ReadingTranslationResult> PublishTranslationAsync(int userId, int translationId)
    {
        var translation = await _context.ReadingUserTranslations
            .FirstOrDefaultAsync(t => t.Id == translationId && t.UserId == userId);

        if (translation == null) return ReadingTranslationResult.Fail("Không tìm thấy bản dịch.");
        if (translation.IsAiApproved != true) return ReadingTranslationResult.Fail("Bản dịch chưa được AI duyệt.");

        translation.IsPublic = !translation.IsPublic;
        translation.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return ReadingTranslationResult.Ok(translation.IsPublic
            ? "Bản dịch đã được chia sẻ cho cộng đồng."
            : "Bản dịch đã được ẩn khỏi cộng đồng.");
    }

    // ─── DeleteTranslation ────────────────────────────────────────────────────
    public async Task<ReadingTranslationResult> DeleteTranslationAsync(int userId, int translationId)
    {
        var translation = await _context.ReadingUserTranslations
            .FirstOrDefaultAsync(t => t.Id == translationId && t.UserId == userId);

        if (translation == null) return ReadingTranslationResult.Fail("Không tìm thấy bản dịch.");

        _context.ReadingUserTranslations.Remove(translation);
        await _context.SaveChangesAsync();

        return ReadingTranslationResult.Ok("Đã xóa bản dịch.");
    }

    // ─── GetTranslationDetail ─────────────────────────────────────────────────
    public async Task<ReadingTranslationDetailDto?> GetTranslationDetailAsync(int translationId, int? currentUserId)
    {
        var translation = await _context.ReadingUserTranslations
            .AsNoTracking()
            .Include(t => t.ReadingPassage)
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == translationId);

        if (translation == null) return null;

        int? currentUserVote = null;
        if (currentUserId.HasValue)
        {
            var vote = await _context.ReadingTranslationVotes
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.TranslationId == translationId && v.UserId == currentUserId.Value);
            currentUserVote = vote != null ? (int)vote.VoteType : null;
        }

        return new ReadingTranslationDetailDto(
            translation.Id,
            translation.ReadingPassageId,
            translation.ReadingPassage.Title,
            translation.ReadingPassage.Content,
            translation.ReadingPassage.ImageUrl,
            translation.TranslatedTitle,
            translation.TranslatedContent,
            translation.AiScore,
            translation.AiFeedback,
            translation.IsAiApproved,
            translation.LikeCount,
            translation.DislikeCount,
            translation.User.FullName ?? "Ẩn danh",
            translation.User.AvatarUrl,
            translation.UserId,
            currentUserVote);
    }

    // ─── VoteTranslation ──────────────────────────────────────────────────────
    public async Task<ReadingTranslationResult> VoteTranslationAsync(int userId, int translationId, TranslationVoteType voteType)
    {
        var translation = await _context.ReadingUserTranslations
            .FirstOrDefaultAsync(t => t.Id == translationId);

        if (translation == null) return ReadingTranslationResult.Fail("Không tìm thấy bản dịch.");
        if (translation.UserId == userId) return ReadingTranslationResult.Fail("Bạn không thể bình chọn bản dịch của chính mình.");

        var existingVote = await _context.ReadingTranslationVotes
            .FirstOrDefaultAsync(v => v.TranslationId == translationId && v.UserId == userId);

        if (existingVote != null)
        {
            if (existingVote.VoteType == voteType)
            {
                // Undo vote
                _context.ReadingTranslationVotes.Remove(existingVote);
            }
            else
            {
                existingVote.VoteType = voteType;
                existingVote.CreatedAtUtc = DateTime.UtcNow;
            }
        }
        else
        {
            _context.ReadingTranslationVotes.Add(new ReadingTranslationVote
            {
                TranslationId = translationId,
                UserId = userId,
                VoteType = voteType
            });
        }

        await _context.SaveChangesAsync();

        // Recalculate counts
        translation.LikeCount = await _context.ReadingTranslationVotes
            .CountAsync(v => v.TranslationId == translationId && v.VoteType == TranslationVoteType.Like);
        translation.DislikeCount = await _context.ReadingTranslationVotes
            .CountAsync(v => v.TranslationId == translationId && v.VoteType == TranslationVoteType.Dislike);

        await _context.SaveChangesAsync();

        return new ReadingTranslationResult
        {
            Success = true,
            LikeCount = translation.LikeCount,
            DislikeCount = translation.DislikeCount
        };
    }

    // ─── AI Evaluation (private) ──────────────────────────────────────────────
    private async Task<(int? score, string? feedback, bool? isApproved)> EvaluateWithAiAsync(
        ReadingPassage passage, string? translatedTitle, string translatedContentJson)
    {
        try
        {
            // Build readable translation text for AI from JSON
            var translationText = BuildReadableTranslation(translatedTitle, translatedContentJson);

            var prompt = $"""
                Evaluate this English-to-Vietnamese translation.

                Original English title:
                {passage.Title}

                Original English content:
                {passage.Content}

                Learner's Vietnamese translation:
                {translationText}

                Requirements:
                - Judge by meaning accuracy first, then naturalness and completeness.
                - Score from 0-100.
                - If score >= {ApprovalThreshold}, set isApproved to true.
                - Write feedback in Vietnamese, 2-4 sentences.
                - Return strict JSON only with keys: "score", "feedback", "isApproved"
                """;

            var messages = new List<AiContextMessage>
            {
                new("system", EvaluationSystemPrompt),
                new("user", prompt)
            };

            var reply = await _providerClient.GenerateReplyAsync(0, messages, CancellationToken.None);

            if (string.IsNullOrWhiteSpace(reply.Text))
            {
                _logger.LogWarning("AI returned empty response for translation evaluation.");
                return (null, null, null);
            }

            return ParseAiResult(reply.Text);
        }
        catch (AiProviderException ex)
        {
            _logger.LogWarning(ex, "AI evaluation failed for passage {PassageId}: {ErrorCode}", passage.Id, ex.ErrorCode);
            return (null, "Hệ thống AI tạm thời không khả dụng. Vui lòng thử lại sau.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during AI evaluation for passage {PassageId}", passage.Id);
            return (null, "Lỗi hệ thống khi đánh giá bản dịch.", null);
        }
    }

    private static string BuildReadableTranslation(string? translatedTitle, string translatedContentJson)
    {
        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrWhiteSpace(translatedTitle))
            sb.AppendLine($"Tiêu đề: {translatedTitle}");

        try
        {
            using var doc = JsonDocument.Parse(translatedContentJson);
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var original = element.GetProperty("original").GetString() ?? "";
                var translated = element.GetProperty("translated").GetString() ?? "";
                sb.AppendLine($"[EN] {original}");
                sb.AppendLine($"[VI] {translated}");
                sb.AppendLine();
            }
        }
        catch
        {
            sb.AppendLine(translatedContentJson);
        }

        return sb.ToString();
    }

    private (int? score, string? feedback, bool? isApproved) ParseAiResult(string aiContent)
    {
        try
        {
            var normalized = NormalizeJsonPayload(aiContent);
            using var doc = JsonDocument.Parse(normalized);
            var root = doc.RootElement;

            int? score = root.TryGetProperty("score", out var scoreProp) && scoreProp.ValueKind == JsonValueKind.Number
                ? scoreProp.GetInt32()
                : null;

            string? feedback = root.TryGetProperty("feedback", out var feedbackProp) && feedbackProp.ValueKind == JsonValueKind.String
                ? feedbackProp.GetString()
                : null;

            bool? isApproved = score.HasValue ? score.Value >= ApprovalThreshold : null;

            return (score, feedback, isApproved);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI evaluation result: {Content}", aiContent);
            return (null, null, null);
        }
    }

    private static string NormalizeJsonPayload(string aiContent)
    {
        var trimmed = aiContent.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return trimmed;

        var firstNewLine = trimmed.IndexOf('\n');
        if (firstNewLine < 0) return trimmed;

        var body = trimmed[(firstNewLine + 1)..].Trim();
        if (body.EndsWith("```", StringComparison.Ordinal))
            body = body[..^3].Trim();

        return body;
    }
}
