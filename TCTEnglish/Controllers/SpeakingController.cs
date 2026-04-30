using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using TCTVocabulary.Services;
using TCTVocabulary.Models;

using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Controllers
{
    [Authorize]
    public class SpeakingController : BaseController
    {
        private const double SpeakingSentencePassScore = 70d;
        private const double SpeakingVideoCompletionRatio = 0.7d;

        private readonly DbflashcardContext _context;
        private readonly ISpeakingService _speakingService;
        private readonly IGoalsService _goalsService;
        private readonly ILogger<SpeakingController> _logger;

        public SpeakingController(
            DbflashcardContext context,
            IYoutubeTranscriptService transcriptService,
            ILogger<SpeakingService> speakingServiceLogger,
            IGoalsService goalsService,
            ILogger<SpeakingController> logger)
        {
            _context = context;
            _speakingService = new SpeakingService(context, transcriptService, speakingServiceLogger);
            _goalsService = goalsService;
            _logger = logger;
        }

        [HttpGet("/Speaking")]
        public async Task<IActionResult> Index()
        {
            var currentUserId = TryGetCurrentUserId(out var userId) ? userId : (int?)null;
            var viewModel = await _speakingService.GetSpeakingIndexViewModelAsync(currentUserId);

            return View(viewModel);
        }

        [HttpGet("/Speaking/Practice/{id:int}")]
        public async Task<IActionResult> Practice(int id)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
                return RedirectToAction("Login", "Account");

            var viewModel = await _speakingService.GetSpeakingPracticeViewModelAsync(id, currentUserId);
            if (viewModel == null)
                return NotFound();

            return View(viewModel);
        }

        [HttpPost("/Speaking/My/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMyVideo([FromForm] string youtubeUrl, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { error = "Chưa đăng nhập." });

            var result = await _speakingService.CreateOwnedVideoAsync(userId, youtubeUrl, ct);

            if (result.RequiresUpgrade)
            {
                return BadRequest(new
                {
                    error = result.ErrorMessage,
                    code = "premium_required"
                });
            }

            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Ok(new
            {
                success = true,
                videoId = result.VideoId
            });
        }

        [HttpPost("/Speaking/My/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMyVideo([FromForm] int id)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { error = "Chưa đăng nhập." });

            var result = await _speakingService.DeleteOwnedVideoAsync(userId, id);

            return result.Status switch
            {
                OperationStatus.Success => Ok(new { success = true }),
                OperationStatus.NotFound => NotFound(),
                _ => BadRequest(new
                {
                    error = result.ErrorMessage,
                    code = "premium_required"
                })
            };
        }

        // ────────────────────────────────────────────────────────────
        //  API — Save Speaking Progress (Upsert: giữ điểm cao nhất)
        // ────────────────────────────────────────────────────────────
        [HttpPost("api/speaking/{sentenceId}/progress")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSpeakingProgress(int sentenceId, [FromBody] SpeakingProgressDto dto)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { error = "Chưa đăng nhập." });

            // 2. Validate điểm số (0–100)
            if (dto.TotalScore < 0 || dto.TotalScore > 100 ||
                dto.AccuracyScore < 0 || dto.AccuracyScore > 100 ||
                dto.FluencyScore < 0 || dto.FluencyScore > 100 ||
                dto.CompletenessScore < 0 || dto.CompletenessScore > 100)
            {
                return BadRequest(new { error = "Điểm số phải từ 0 đến 100." });
            }

            // 3. Kiểm tra câu tồn tại, quyền truy cập video cha, và VideoId
            var sentenceAccess = await _context.SpeakingSentences
                .AsNoTracking()
                .Where(s => s.Id == sentenceId)
                .Select(s => new
                {
                    s.Id,
                    s.VideoId,
                    OwnerUserId = s.SpeakingVideo.OwnerUserId
                })
                .FirstOrDefaultAsync();

            if (sentenceAccess == null)
                return NotFound(new { error = "Câu không tồn tại." });

            if (sentenceAccess.OwnerUserId.HasValue && sentenceAccess.OwnerUserId.Value != userId)
                return NotFound();

            if (sentenceAccess.OwnerUserId == userId)
            {
                var role = await _context.Users
                    .AsNoTracking()
                    .Where(user => user.UserId == userId)
                    .Select(user => user.Role)
                    .FirstOrDefaultAsync();

                if (Roles.Normalize(role) == Roles.Standard)
                    return NotFound();
            }

            // 4. Upsert — giữ điểm cao nhất
            var existing = await _context.UserSpeakingProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.SentenceId == sentenceId);

            var utcNow = DateTime.UtcNow;

            if (existing != null)
            {
                // Chỉ cập nhật nếu điểm mới cao hơn
                if (dto.TotalScore > existing.TotalScore)
                {
                    existing.TotalScore = dto.TotalScore;
                    existing.AccuracyScore = dto.AccuracyScore;
                    existing.FluencyScore = dto.FluencyScore;
                    existing.CompletenessScore = dto.CompletenessScore;
                    existing.PracticedAt = utcNow;
                }
            }
            else
            {
                var progress = new UserSpeakingProgress
                {
                    UserId = userId,
                    SentenceId = sentenceId,
                    TotalScore = dto.TotalScore,
                    AccuracyScore = dto.AccuracyScore,
                    FluencyScore = dto.FluencyScore,
                    CompletenessScore = dto.CompletenessScore,
                    PracticedAt = utcNow
                };
                _context.UserSpeakingProgresses.Add(progress);
            }

            await _context.SaveChangesAsync();

            var totalSentenceCount = await _context.SpeakingSentences
                .AsNoTracking()
                .CountAsync(s => s.VideoId == sentenceAccess.VideoId);

            var requiredSentenceCount = Math.Max(1, (int)Math.Ceiling(totalSentenceCount * SpeakingVideoCompletionRatio));

            var passedSentenceCount = await _context.UserSpeakingProgresses
                .AsNoTracking()
                .Where(p => p.UserId == userId
                    && p.SpeakingSentence.VideoId == sentenceAccess.VideoId
                    && p.TotalScore >= SpeakingSentencePassScore)
                .Select(p => p.SentenceId)
                .Distinct()
                .CountAsync();

            var reachedVideoCompletion = passedSentenceCount >= requiredSentenceCount;
            var firstTimeVideoCompletion = await UpsertSpeakingVideoCompletionAsync(
                userId,
                sentenceAccess.VideoId,
                passedSentenceCount,
                requiredSentenceCount,
                reachedVideoCompletion,
                utcNow);

            var speakingXpEarned = 0;
            var currentStreak = 0;
            if (firstTimeVideoCompletion)
            {
                var speakingRewardUpdate = _goalsService.BuildSpeakingCompletionActivityUpdate();
                var rewardResult = await _goalsService.RecordLearningActivityAsync(userId, speakingRewardUpdate);
                if (rewardResult.Status == OperationStatus.Success)
                {
                    speakingXpEarned = speakingRewardUpdate.XpEarned;
                    currentStreak = rewardResult.Streak;
                }
                else
                {
                    _logger.LogWarning(
                        "Speaking completion reward failed for user {userId}, video {videoId}, status {status}",
                        userId,
                        sentenceAccess.VideoId,
                        rewardResult.Status);
                }
            }

            return Ok(new
            {
                success = true,
                sentenceId,
                videoId = sentenceAccess.VideoId,
                totalScore = dto.TotalScore,
                accuracyScore = dto.AccuracyScore,
                fluencyScore = dto.FluencyScore,
                completenessScore = dto.CompletenessScore,
                passedSentenceCount,
                requiredSentenceCount,
                isVideoCompleted = reachedVideoCompletion,
                firstTimeVideoCompletion,
                xpEarned = speakingXpEarned,
                streak = currentStreak
            });
        }

        private async Task<bool> UpsertSpeakingVideoCompletionAsync(
            int userId,
            int videoId,
            int completedSentenceCount,
            int requiredSentenceCount,
            bool reachedVideoCompletion,
            DateTime evaluatedAtUtc)
        {
            await EnsureSpeakingVideoCompletionRowAsync(userId, videoId, requiredSentenceCount, evaluatedAtUtc);

            if (reachedVideoCompletion)
            {
                var completionTransitionRows = await _context.UserSpeakingVideoCompletions
                    .Where(completion =>
                        completion.UserId == userId
                        && completion.VideoId == videoId
                        && !completion.IsCompleted)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(completion => completion.CompletedSentenceCount, _ => completedSentenceCount)
                        .SetProperty(completion => completion.RequiredSentenceCount, _ => requiredSentenceCount)
                        .SetProperty(completion => completion.LastEvaluatedAt, _ => evaluatedAtUtc)
                        .SetProperty(completion => completion.IsCompleted, _ => true)
                        .SetProperty(completion => completion.CompletedAt, _ => evaluatedAtUtc));

                if (completionTransitionRows > 0)
                {
                    return true;
                }
            }

            await _context.UserSpeakingVideoCompletions
                .Where(completion => completion.UserId == userId && completion.VideoId == videoId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(completion => completion.CompletedSentenceCount, _ => completedSentenceCount)
                    .SetProperty(completion => completion.RequiredSentenceCount, _ => requiredSentenceCount)
                    .SetProperty(completion => completion.LastEvaluatedAt, _ => evaluatedAtUtc));

            return false;
        }

        private async Task EnsureSpeakingVideoCompletionRowAsync(
            int userId,
            int videoId,
            int requiredSentenceCount,
            DateTime evaluatedAtUtc)
        {
            var completionExists = await _context.UserSpeakingVideoCompletions
                .AsNoTracking()
                .AnyAsync(completion => completion.UserId == userId && completion.VideoId == videoId);

            if (completionExists)
            {
                return;
            }

            var completion = new UserSpeakingVideoCompletion
            {
                UserId = userId,
                VideoId = videoId,
                CompletedSentenceCount = 0,
                RequiredSentenceCount = requiredSentenceCount,
                IsCompleted = false,
                LastEvaluatedAt = evaluatedAtUtc
            };

            _context.UserSpeakingVideoCompletions.Add(completion);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _context.Entry(completion).State = EntityState.Detached;
            }
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        {
            // MySQL/MariaDB error 1062 = Duplicate entry for key
            var mysqlException = FindException<MySqlException>(exception);
            if (mysqlException is { Number: 1062 })
            {
                return true;
            }

            var sqliteException = FindException(
                exception,
                candidate => string.Equals(
                    candidate.GetType().FullName,
                    "Microsoft.Data.Sqlite.SqliteException",
                    StringComparison.Ordinal));

            return sqliteException != null && GetExceptionIntProperty(sqliteException, "SqliteErrorCode") == 19;
        }

        private static TException? FindException<TException>(Exception exception)
            where TException : Exception
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is TException typedException)
                {
                    return typedException;
                }
            }

            return null;
        }

        private static Exception? FindException(Exception exception, Func<Exception, bool> predicate)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (predicate(current))
                {
                    return current;
                }
            }

            return null;
        }

        private static int? GetExceptionIntProperty(Exception exception, string propertyName)
        {
            var property = exception.GetType().GetProperty(propertyName);
            if (property?.PropertyType != typeof(int))
            {
                return null;
            }

            return (int?)property.GetValue(exception);
        }

    }

    // ── DTO ──────────────────────────────────────────────────────────
    public class SpeakingProgressDto
    {
        public double TotalScore { get; set; }
        public double AccuracyScore { get; set; }
        public double FluencyScore { get; set; }
        public double CompletenessScore { get; set; }
    }
}
