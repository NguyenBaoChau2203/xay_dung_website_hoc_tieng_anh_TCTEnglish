using Microsoft.EntityFrameworkCore;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;

namespace TCTVocabulary.Services;

public partial class WritingService
{
    public async Task<OperationResult> DeleteOwnedExerciseAsync(int exerciseId, int userId)
    {
        if (exerciseId <= 0)
        {
            return OperationResult.NotFound();
        }

        var accessContext = await ResolveWritingAccessContextAsync(userId);
        var exercise = await _context.WritingExercises
            .FirstOrDefaultAsync(item => item.Id == exerciseId && item.UserId == userId);

        if (exercise == null)
        {
            return OperationResult.NotFound();
        }

        if (!accessContext.CanAccessOwnedPrivateExercises)
        {
            return OperationResult.Invalid("Bài viết này đang bị khóa theo gói hiện tại. Hãy nâng cấp để tiếp tục.");
        }

        var hasAttemptHistory = await _context.UserWritingAttempts
            .AsNoTracking()
            .AnyAsync(item => item.WritingExerciseId == exerciseId);

        if (hasAttemptHistory)
        {
            return OperationResult.Invalid("Bài viết đã có lịch sử luyện nên hiện chưa thể xóa.");
        }

        _context.WritingExercises.Remove(exercise);
        await _context.SaveChangesAsync();
        return OperationResult.Success();
    }

    private async Task<WritingAccessContext> ResolveWritingAccessContextAsync(int userId)
    {
        var normalizedRole = await GetNormalizedWritingRoleAsync(userId);
        var isAdmin = string.Equals(normalizedRole, Roles.Admin, StringComparison.Ordinal);
        var canAccessOwnedPrivateExercises = isAdmin
            || string.Equals(normalizedRole, Roles.Premium, StringComparison.Ordinal);

        return new WritingAccessContext(
            userId,
            normalizedRole,
            isAdmin,
            canAccessOwnedPrivateExercises,
            canAccessOwnedPrivateExercises);
    }

    private async Task<List<WritingExerciseCardViewModel>> LoadOwnerWritingExerciseCardsAsync(
        string levelKey,
        string contentTypeKey,
        int userId)
    {
        return await _context.WritingExercises
            .AsNoTracking()
            .Where(exercise => exercise.UserId == userId
                && exercise.Level == levelKey
                && exercise.ContentType == contentTypeKey)
            .OrderByDescending(exercise => exercise.CreatedAt)
            .ThenByDescending(exercise => exercise.Id)
            .Select(exercise => new WritingExerciseCardViewModel
            {
                Id = exercise.Id,
                Title = exercise.Title,
                PreviewText = exercise.PreviewText,
                Topic = exercise.Topic,
                SentenceCount = exercise.WritingExerciseSentences.Count(),
                CreatedAtDisplay = FormatWritingTimestamp(exercise.CreatedAt),
                CanDelete = !exercise.UserWritingAttempts.Any()
            })
            .ToListAsync();
    }

    private async Task<WritingPracticeExerciseRow?> ResolveWritingExerciseAccessAsync(int exerciseId, int userId)
    {
        var accessContext = await ResolveWritingAccessContextAsync(userId);

        return await _context.WritingExercises
            .AsNoTracking()
            .Where(exercise => exercise.Id == exerciseId)
            .Where(exercise =>
                accessContext.IsAdmin
                || (exercise.UserId == null && exercise.IsPublished)
                || (exercise.UserId == userId && accessContext.CanAccessOwnedPrivateExercises))
            .Select(exercise => new WritingPracticeExerciseRow
            {
                Id = exercise.Id,
                Title = exercise.Title,
                Level = exercise.Level,
                ContentType = exercise.ContentType,
                Topic = exercise.Topic,
                PreviewText = exercise.PreviewText
            })
            .FirstOrDefaultAsync();
    }

    private async Task<string> GetNormalizedWritingRoleAsync(int userId)
    {
        var role = await _context.Users
            .AsNoTracking()
            .Where(user => user.UserId == userId)
            .Select(user => user.Role)
            .FirstOrDefaultAsync();

        return Roles.Normalize(role);
    }

    private sealed record WritingAccessContext(
        int UserId,
        string NormalizedRole,
        bool IsAdmin,
        bool CanAccessOwnedPrivateExercises,
        bool CanCreateFromAi);
}
