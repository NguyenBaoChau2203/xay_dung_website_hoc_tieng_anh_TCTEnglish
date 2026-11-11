using System.Data;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Accounts;

public sealed class AccountDeletionResult
{
    public bool Succeeded { get; init; }
    public bool Removed { get; init; }
    public string? Error { get; init; }
    public static AccountDeletionResult Ok(bool removed) => new() { Succeeded = true, Removed = removed };
    public static AccountDeletionResult Fail(string error) => new() { Error = error };
}

public interface IAccountDeletionService
{
    Task<AccountDeletionResult> DeleteAsync(int userId, string? confirmText, string? currentPassword, string? proofCode, CancellationToken cancellationToken = default);
}

public interface IAccountFileCleanupProcessor
{
    Task<int> RunOnceAsync(CancellationToken cancellationToken = default);
}

public sealed class AccountDeletionService : IAccountDeletionService
{
    public const string ConfirmText = "CONFIRM";
    private readonly DbflashcardContext _context;
    private readonly IAccountSecurityService _security;
    private readonly IAvatarUploadService _avatars;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(
        DbflashcardContext context,
        IAccountSecurityService security,
        IAvatarUploadService avatars,
        TimeProvider timeProvider,
        ILogger<AccountDeletionService> logger)
    {
        _context = context;
        _security = security;
        _avatars = avatars;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<AccountDeletionResult> DeleteAsync(
        int userId,
        string? confirmText,
        string? currentPassword,
        string? proofCode,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        return _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) _context.ChangeTracker.Clear();
            return await DeleteCoreAsync(userId, confirmText, currentPassword, proofCode, cancellationToken);
        });
    }

    private async Task<AccountDeletionResult> DeleteCoreAsync(int userId, string? confirmText,
        string? currentPassword, string? proofCode, CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmText?.Trim(), ConfirmText, StringComparison.Ordinal))
        {
            return AccountDeletionResult.Fail("Hãy gõ đúng CONFIRM. Nút trên trình duyệt không thay cho kiểm tra của máy chủ.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Users SET SecurityVersion = SecurityVersion WHERE Role = {Roles.Admin}",
            cancellationToken);

        var user = await _context.Users.FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken);
        if (user == null || user.AnonymizedAt != null)
        {
            return AccountDeletionResult.Fail("Không tìm thấy tài khoản.");
        }

        if (string.Equals(user.Role, Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            var otherAdmins = await _context.Users.CountAsync(
                row => row.Role == Roles.Admin && row.UserId != userId && row.AnonymizedAt == null
                    && (row.Status != UserStatus.Blocked || (row.LockExpiry != null && row.LockExpiry <= _timeProvider.GetUtcNow().UtcDateTime)),
                cancellationToken);
            if (otherAdmins == 0)
            {
                return AccountDeletionResult.Fail("Không thể xóa quản trị viên cuối cùng.");
            }
        }

        if (!await _security.HasFreshProofAsync(user, currentPassword, proofCode, AccountProofPurpose.Deletion, cancellationToken))
        {
            return AccountDeletionResult.Fail("Cần mật khẩu hiện tại hoặc mã xác nhận còn hiệu lực. Dấu SOCIAL_LOGIN không đủ để xóa tài khoản.");
        }

        foreach (var entry in _context.ChangeTracker.Entries<AccountSecurityChallenge>().ToList())
        {
            entry.State = EntityState.Detached;
        }

        var blocker = await FindSharedBlockerAsync(userId, cancellationToken);
        if (blocker != null)
        {
            return AccountDeletionResult.Fail(blocker);
        }

        var avatarUrl = user.AvatarUrl;
        try
        {
            await DeletePersonalRowsAsync(userId, cancellationToken);
            await DeleteSoleOwnedContentAsync(userId, cancellationToken);
            _context.ChangeTracker.Clear();
            user = await _context.Users.FirstAsync(row => row.UserId == userId, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Account deletion rolled back for user {UserId}", userId);
            return AccountDeletionResult.Fail("Chưa xóa được vì còn ràng buộc dữ liệu. Tài khoản và ảnh vẫn được giữ.");
        }

        var keepLedger = await _context.PaymentOrders.AnyAsync(row => row.UserId == userId, cancellationToken)
            || await _context.PaymentAdminActions.AnyAsync(row => row.AdminUserId == userId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            _context.AccountFileCleanupJobs.Add(new AccountFileCleanupJob
            {
                FormerUserId = userId,
                PublicUrl = avatarUrl,
                NextAttemptAtUtc = _timeProvider.GetUtcNow().UtcDateTime
            });
        }

        if (keepLedger)
        {
            Anonymize(user);
        }
        else
        {
            _context.Users.Remove(user);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Account deletion rolled back for user {UserId}", userId);
            return AccountDeletionResult.Fail("Chưa xóa được vì còn ràng buộc dữ liệu. Tài khoản và ảnh vẫn được giữ.");
        }

        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            _avatars.DeleteOwnedAvatar(avatarUrl);
        }

        return AccountDeletionResult.Ok(removed: !keepLedger);
    }

    private void Anonymize(User user)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        user.Email = $"deleted+{user.UserId}@users.invalid";
        user.FullName = null;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"));
        user.AvatarUrl = null;
        user.ResetPasswordToken = null;
        user.ResetPasswordTokenExpiry = null;
        user.Role = Roles.Standard;
        user.Status = UserStatus.Blocked;
        user.LockReason = "account-deleted";
        user.LockExpiry = null;
        user.EmailVerifiedAt = null;
        user.AnonymizedAt = now;
        user.SecurityVersion += 1;
        user.Streak = null;
        user.LongestStreak = null;
        user.LastStudyDate = null;
        user.Goal = null;
        user.CreatedAt = null;
    }

    private async Task<string?> FindSharedBlockerAsync(int userId, CancellationToken cancellationToken)
    {
        var ownedClassIds = await _context.Classes.Where(row => row.OwnerId == userId).Select(row => row.ClassId).ToListAsync(cancellationToken);
        if (await _context.ClassMembers.AnyAsync(row => ownedClassIds.Contains(row.ClassId) && row.UserId != userId, cancellationToken))
        {
            return "Bạn đang sở hữu lớp còn thành viên khác. Hãy chuyển hoặc giải tán lớp trước khi xóa tài khoản.";
        }

        if (await _context.ClassFolders.AnyAsync(row => row.AddedByUserId == userId && !ownedClassIds.Contains(row.ClassId), cancellationToken))
        {
            return "Bạn đã gắn học liệu vào lớp của người khác. Nhờ chủ lớp gỡ liên kết đó trước khi xóa tài khoản.";
        }

        var folderIds = await _context.Folders.Where(row => row.UserId == userId).Select(row => row.FolderId).ToListAsync(cancellationToken);
        if (await _context.Folders.AnyAsync(row => row.UserId != userId && row.ParentFolderId != null && folderIds.Contains(row.ParentFolderId.Value), cancellationToken))
            return "Thư mục của bạn còn thư mục con của người khác. Hãy xử lý liên kết dùng chung trước.";
        if (await _context.SavedFolders.AnyAsync(row => folderIds.Contains(row.FolderId) && row.UserId != userId, cancellationToken))
        {
            return "Thư mục của bạn đang được người khác lưu. Không xóa tài khoản khi còn phụ thuộc dùng chung.";
        }

        var setIds = await _context.Sets.Where(row => row.OwnerId == userId).Select(row => row.SetId).ToListAsync(cancellationToken);
        var cardIds = await _context.Cards.Where(row => setIds.Contains(row.SetId)).Select(row => row.CardId).ToListAsync(cancellationToken);
        if (await _context.LearningProgresses.AnyAsync(row => cardIds.Contains(row.CardId) && row.UserId != userId, cancellationToken))
        {
            return "Bộ thẻ của bạn đang có tiến độ của người khác. Không xóa tiến độ của họ.";
        }

        var videoIds = await _context.SpeakingVideos.Where(row => row.OwnerUserId == userId).Select(row => row.Id).ToListAsync(cancellationToken);
        var sentenceIds = await _context.SpeakingSentences.Where(row => videoIds.Contains(row.VideoId)).Select(row => row.Id).ToListAsync(cancellationToken);
        if (await _context.SpeakingAssessments.AnyAsync(row => sentenceIds.Contains(row.SentenceId) && row.UserId != userId, cancellationToken)
            || await _context.UserSpeakingVideoCompletions.AnyAsync(row => videoIds.Contains(row.VideoId) && row.UserId != userId, cancellationToken))
            return "Video nói của bạn còn kết quả luyện tập của người khác.";
        if (await _context.UserSpeakingProgresses.AnyAsync(row => sentenceIds.Contains(row.SentenceId) && row.UserId != userId, cancellationToken))
        {
            return "Video nói của bạn đang có tiến độ của người khác.";
        }

        var exerciseIds = await _context.WritingExercises.Where(row => row.UserId == userId).Select(row => row.Id).ToListAsync(cancellationToken);
        if (await _context.UserWritingExerciseProgresses.AnyAsync(row => exerciseIds.Contains(row.WritingExerciseId) && row.UserId != userId, cancellationToken)
            || await _context.UserWritingSentenceProgresses.AnyAsync(row => exerciseIds.Contains(row.WritingExerciseId) && row.UserId != userId, cancellationToken))
            return "Bài viết của bạn còn tiến độ luyện tập của người khác.";
        if (await _context.UserWritingAttempts.AnyAsync(row => exerciseIds.Contains(row.WritingExerciseId) && row.UserId != userId, cancellationToken))
        {
            return "Bài viết của bạn đang có bài làm của người khác.";
        }

        var lessonIds = await _context.ListeningLessons.Where(row => row.OwnerUserId == userId).Select(row => row.Id).ToListAsync(cancellationToken);
        if (await _context.UserListeningProgresses.AnyAsync(row => lessonIds.Contains(row.LessonId) && row.UserId != userId, cancellationToken))
        {
            return "Bài nghe của bạn đang có tiến độ của người khác.";
        }

        return null;
    }

    private async Task DeletePersonalRowsAsync(int userId, CancellationToken cancellationToken)
    {
        await _context.LearningActivitySegments.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.LearningActivitySessions.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.UserGoalTargetRevisions.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.AccountSecurityChallenges.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.UserNotificationPreferences.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.WebPushSubscriptions.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.OutboundDeliveries.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.UserFeatureTimeLogs.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.UserDailyActivities.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.UserBadges.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.UserGoals.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.LearningProgresses.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.LearningCompletionCredits.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.LearningNotificationOutboxEvents.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.Notifications.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        var conversationIds = await _context.AiConversations.Where(row => row.UserId == userId).Select(row => row.Id).ToListAsync(cancellationToken);
        await _context.AiMessages.Where(row => conversationIds.Contains(row.ConversationId)).ExecuteDeleteAsync(cancellationToken);
        await _context.AiConversations.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.AiRequestLogs.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.AiUserDailyQuotas.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.AiQuotaReservationLedgers.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.WritingGenerationLogs.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.UserSpeakingProgresses.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.SpeakingAssessments.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.UserSpeakingVideoCompletions.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.UserWritingAttempts.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.UserWritingExerciseProgresses.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.UserWritingSentenceProgresses.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.UserReadingHistories.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.ReadingTranslationVotes.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.ReadingUserTranslations.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.UserListeningProgresses.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.SavedFolders.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.ClassMessages.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.ClassMembers.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.ClassJoinRequests.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.ClassBlacklists.Where(row => row.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.SpeakingImportJobs.Where(row => row.OwnerUserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _context.WritingAcceptedAnswers.Where(row => row.ApprovedByUserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ApprovedByUserId, (int?)null), cancellationToken);
    }

    private async Task DeleteSoleOwnedContentAsync(int userId, CancellationToken cancellationToken)
    {
        var classIds = await _context.Classes.Where(row => row.OwnerId == userId).Select(row => row.ClassId).ToListAsync(cancellationToken);
        if (classIds.Count > 0)
        {
            await _context.Classes.Where(row => classIds.Contains(row.ClassId)).ExecuteDeleteAsync(cancellationToken);
        }

        var setIds = await _context.Sets.Where(row => row.OwnerId == userId).Select(row => row.SetId).ToListAsync(cancellationToken);
        if (setIds.Count > 0)
        {
            await _context.Cards.Where(row => setIds.Contains(row.SetId)).ExecuteDeleteAsync(cancellationToken);
            await _context.Sets.Where(row => setIds.Contains(row.SetId)).ExecuteDeleteAsync(cancellationToken);
        }

        var folders = await _context.Folders.Where(row => row.UserId == userId)
            .Select(row => new { row.FolderId, row.ParentFolderId })
            .ToListAsync(cancellationToken);
        var remaining = folders.Select(row => row.FolderId).ToHashSet();
        while (remaining.Count > 0)
        {
            var leaves = remaining.Where(id => !folders.Any(row => row.ParentFolderId == id && remaining.Contains(row.FolderId))).ToList();
            if (leaves.Count == 0)
            {
                break;
            }

            await _context.Folders.Where(row => leaves.Contains(row.FolderId)).ExecuteDeleteAsync(cancellationToken);
            foreach (var id in leaves)
            {
                remaining.Remove(id);
            }
        }

        var videoIds = await _context.SpeakingVideos.Where(row => row.OwnerUserId == userId).Select(row => row.Id).ToListAsync(cancellationToken);
        if (videoIds.Count > 0)
        {
            await _context.SpeakingSentences.Where(row => videoIds.Contains(row.VideoId)).ExecuteDeleteAsync(cancellationToken);
            await _context.SpeakingVideos.Where(row => videoIds.Contains(row.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        var exerciseIds = await _context.WritingExercises.Where(row => row.UserId == userId).Select(row => row.Id).ToListAsync(cancellationToken);
        if (exerciseIds.Count > 0)
        {
            await _context.WritingExerciseSentences.Where(row => exerciseIds.Contains(row.WritingExerciseId)).ExecuteDeleteAsync(cancellationToken);
            await _context.WritingExercises.Where(row => exerciseIds.Contains(row.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        var lessonIds = await _context.ListeningLessons.Where(row => row.OwnerUserId == userId).Select(row => row.Id).ToListAsync(cancellationToken);
        if (lessonIds.Count > 0)
        {
            await _context.ListeningTranscriptLines.Where(row => lessonIds.Contains(row.LessonId)).ExecuteDeleteAsync(cancellationToken);
            await _context.ListeningQuizQuestions.Where(row => lessonIds.Contains(row.LessonId)).ExecuteDeleteAsync(cancellationToken);
            await _context.ListeningVocabItems.Where(row => lessonIds.Contains(row.LessonId)).ExecuteDeleteAsync(cancellationToken);
            await _context.ListeningLessons.Where(row => lessonIds.Contains(row.Id)).ExecuteDeleteAsync(cancellationToken);
        }
    }
}

public sealed class AccountFileCleanupProcessor : IAccountFileCleanupProcessor
{
    private readonly DbflashcardContext _context;
    private readonly IAvatarUploadService _avatars;
    private readonly TimeProvider _timeProvider;

    public AccountFileCleanupProcessor(DbflashcardContext context, IAvatarUploadService avatars, TimeProvider timeProvider)
    {
        _context = context;
        _avatars = avatars;
        _timeProvider = timeProvider;
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var jobs = await _context.AccountFileCleanupJobs
            .Where(row => row.Status == AccountFileCleanupStatuses.Pending && row.NextAttemptAtUtc <= now && row.AttemptCount < 5)
            .OrderBy(row => row.Id)
            .Take(20)
            .ToListAsync(cancellationToken);
        var done = 0;
        foreach (var job in jobs)
        {
            job.AttemptCount += 1;
            var deleted = _avatars.DeleteOwnedAvatar(job.PublicUrl);
            if (deleted || job.AttemptCount >= 5)
            {
                job.Status = deleted ? AccountFileCleanupStatuses.Done : AccountFileCleanupStatuses.Failed;
                if (deleted) done++;
            }
            else
            {
                job.NextAttemptAtUtc = now.AddMinutes(5);
            }
        }

        if (jobs.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return done;
    }
}
