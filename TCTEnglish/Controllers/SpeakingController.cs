using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCTEnglish.Services.Speaking;
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
        private readonly ISpeakingAssessmentService _speakingAssessmentService;
        private readonly IGoalsService _goalsService;
        private readonly TCTEnglish.Services.Learning.ICompletionCreditService _completionCreditService;
        private readonly ISpeakingImportJobService _speakingImportJobService;
        private readonly ILogger<SpeakingController> _logger;

        public SpeakingController(
            DbflashcardContext context,
            IYoutubeTranscriptService transcriptService,
            ILogger<SpeakingService> speakingServiceLogger,
            ISpeakingAssessmentService speakingAssessmentService,
            IGoalsService goalsService,
            TCTEnglish.Services.Learning.ICompletionCreditService completionCreditService,
            ISpeakingImportJobService speakingImportJobService,
            ILogger<SpeakingController> logger)
        {
            _context = context;
            _speakingService = new SpeakingService(context, transcriptService, speakingServiceLogger);
            _speakingAssessmentService = speakingAssessmentService;
            _goalsService = goalsService;
            _completionCreditService = completionCreditService;
            _speakingImportJobService = speakingImportJobService;
            _logger = logger;
        }

        [HttpGet("/Speaking")]
        [HttpGet("/Speaking/Index")]
        public async Task<IActionResult> Index()
        {
            var currentUserId = TryGetCurrentUserId(out var userId) ? userId : (int?)null;
            var viewModel = await _speakingService.GetSpeakingIndexViewModelAsync(currentUserId);

            return View(viewModel);
        }

        [HttpGet("/Speaking/Practice")]
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

        /// <summary>Async import when captions fetch may be slow — returns 202 + job status URL.</summary>
        [HttpPost("/api/speaking/imports")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnqueueSpeakingImport([FromForm] string youtubeUrl, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { error = "Chưa đăng nhập." });

            var enqueue = await _speakingImportJobService.EnqueueYoutubeImportAsync(userId, youtubeUrl, ct);
            if (enqueue.RequiresUpgrade)
            {
                return BadRequest(new { error = enqueue.ErrorMessage, code = "premium_required" });
            }

            if (enqueue.IsConflict)
            {
                return Conflict(new { error = enqueue.ErrorMessage, code = "source_conflict" });
            }

            if (enqueue.IsInvalid)
            {
                return BadRequest(new { error = enqueue.ErrorMessage });
            }

            return Accepted(new
            {
                success = true,
                jobId = enqueue.JobPublicId,
                statusUrl = enqueue.StatusUrl
            });
        }

        [HttpPost("/api/speaking/imports/subtitles")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(2 * 1024 * 1024)]
        public async Task<IActionResult> EnqueueSpeakingSubtitleImport(
            [FromForm] string youtubeUrl,
            IFormFile? subtitle,
            CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { error = "Chưa đăng nhập." });

            if (subtitle == null || subtitle.Length <= 0)
            {
                return BadRequest(new { error = "Thiếu file phụ đề." });
            }

            if (subtitle.Length > 1024 * 1024)
            {
                return BadRequest(new { error = "Subtitle vượt quá 1 MiB." });
            }

            string text;
            using (var reader = new StreamReader(subtitle.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                text = await reader.ReadToEndAsync(ct);
            }

            var enqueue = await _speakingImportJobService.EnqueueSubtitleImportAsync(userId, youtubeUrl, text, ct);
            if (enqueue.RequiresUpgrade)
            {
                return BadRequest(new { error = enqueue.ErrorMessage, code = "premium_required" });
            }

            if (enqueue.IsConflict)
            {
                return Conflict(new { error = enqueue.ErrorMessage, code = "source_conflict" });
            }

            if (enqueue.IsInvalid)
            {
                return BadRequest(new { error = enqueue.ErrorMessage });
            }

            return Accepted(new
            {
                success = true,
                jobId = enqueue.JobPublicId,
                statusUrl = enqueue.StatusUrl
            });
        }

        [HttpGet("/api/speaking/imports/{jobId:guid}")]
        public async Task<IActionResult> GetSpeakingImportStatus(Guid jobId, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { error = "Chưa đăng nhập." });

            var job = await _speakingImportJobService.GetOwnedJobAsync(userId, jobId, ct);
            if (job == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                success = true,
                jobId = job.PublicId,
                status = job.Status,
                videoId = job.ResultVideoId,
                error = job.LastError
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
        //  API — Server assessment (multipart WAV) then commit via progress
        // ────────────────────────────────────────────────────────────
        [HttpPost("api/speaking/{sentenceId}/assess")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(2 * 1024 * 1024)]
        public async Task<IActionResult> AssessSpeaking(int sentenceId, IFormFile? audio, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { error = "Chưa đăng nhập.", code = "unauthorized" });

            if (audio == null || audio.Length <= 0)
            {
                return BadRequest(new { error = "Thiếu file âm thanh.", code = "missing_audio" });
            }

            await using var stream = audio.OpenReadStream();
            var outcome = await _speakingAssessmentService.AssessAsync(
                userId,
                sentenceId,
                stream,
                audio.ContentType,
                cancellationToken);

            if (!outcome.Success)
            {
                if (outcome.RetryAfterSeconds is int retryAfter)
                {
                    Response.Headers.RetryAfter = retryAfter.ToString();
                }

                return StatusCode(outcome.StatusCode, new
                {
                    success = false,
                    error = outcome.Message,
                    code = outcome.ErrorCode
                });
            }

            return Ok(new
            {
                success = true,
                assessmentId = outcome.AssessmentId,
                status = outcome.Status,
                textMatchScore = outcome.TextMatchScore,
                speechDetected = outcome.SpeechDetected,
                transcriptPreview = outcome.TranscriptPreview,
                fluencyAvailable = outcome.FluencyAvailable,
                completenessAvailable = outcome.CompletenessAvailable,
                canCommitProgress = outcome.CanCommitProgress,
                message = outcome.Message
            });
        }

        // ────────────────────────────────────────────────────────────
        //  API — Save Speaking Progress (requires server assessment receipt)
        // ────────────────────────────────────────────────────────────
        [HttpPost("api/speaking/{sentenceId}/progress")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSpeakingProgress(int sentenceId, [FromBody] SpeakingProgressDto dto, CancellationToken cancellationToken)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { error = "Chưa đăng nhập." });

            var sentenceAccess = await _context.SpeakingSentences
                .AsNoTracking()
                .Where(s => s.Id == sentenceId)
                .Select(s => new
                {
                    s.Id,
                    s.VideoId,
                    OwnerUserId = s.SpeakingVideo.OwnerUserId
                })
                .FirstOrDefaultAsync(cancellationToken);

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
                    .FirstOrDefaultAsync(cancellationToken);

                if (Roles.Normalize(role) == Roles.Standard)
                    return NotFound();
            }

            if (dto.AssessmentId == Guid.Empty)
            {
                return Conflict(new
                {
                    success = false,
                    error = "Cần assessment phía server trước khi lưu tiến độ.",
                    code = "assessment_required"
                });
            }

            var assessment = await _speakingAssessmentService.GetUsableAssessmentAsync(
                userId,
                sentenceId,
                dto.AssessmentId,
                cancellationToken);

            if (assessment == null)
            {
                return Conflict(new
                {
                    success = false,
                    error = "Assessment không hợp lệ, hết hạn hoặc không thuộc câu này.",
                    code = "assessment_invalid"
                });
            }

            // Client-issued numeric scores are ignored; server assessment is authoritative.
            var totalScore = assessment.TextMatchScore;
            var accuracyScore = assessment.TextMatchScore;
            const double fluencyScore = 0d;
            const double completenessScore = 0d;

            await using var completionTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
            var existing = await _context.UserSpeakingProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.SentenceId == sentenceId, cancellationToken);

            var utcNow = DateTime.UtcNow;

            if (existing != null)
            {
                if (totalScore > existing.TotalScore)
                {
                    existing.TotalScore = totalScore;
                    existing.AccuracyScore = accuracyScore;
                    existing.FluencyScore = fluencyScore;
                    existing.CompletenessScore = completenessScore;
                    existing.PracticedAt = utcNow;
                }
            }
            else
            {
                _context.UserSpeakingProgresses.Add(new UserSpeakingProgress
                {
                    UserId = userId,
                    SentenceId = sentenceId,
                    TotalScore = totalScore,
                    AccuracyScore = accuracyScore,
                    FluencyScore = fluencyScore,
                    CompletenessScore = completenessScore,
                    PracticedAt = utcNow
                });
            }

            assessment.Consumed = true;
            assessment.ConsumedAtUtc = utcNow;

            await _context.SaveChangesAsync(cancellationToken);

            var totalSentenceCount = await _context.SpeakingSentences
                .AsNoTracking()
                .CountAsync(s => s.VideoId == sentenceAccess.VideoId, cancellationToken);

            var requiredSentenceCount = Math.Max(1, (int)Math.Ceiling(totalSentenceCount * SpeakingVideoCompletionRatio));

            var passedSentenceCount = await _context.UserSpeakingProgresses
                .AsNoTracking()
                .Where(p => p.UserId == userId
                    && p.SpeakingSentence.VideoId == sentenceAccess.VideoId
                    && p.TotalScore >= SpeakingSentencePassScore)
                .Select(p => p.SentenceId)
                .Distinct()
                .CountAsync(cancellationToken);

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
                var creditResult = await _completionCreditService.TryApplySpeakingVideoCompletionAsync(
                    userId,
                    sentenceAccess.VideoId,
                    speakingRewardUpdate,
                    cancellationToken);
                if (creditResult.Applied)
                {
                    speakingXpEarned = creditResult.XpEarned;
                    currentStreak = creditResult.Streak;
                }
                else if (!creditResult.Replay && creditResult.Status != OperationStatus.Success)
                {
                    await completionTransaction.RollbackAsync(cancellationToken);
                    return StatusCode(StatusCodes.Status500InternalServerError, new
                    {
                        success = false,
                        code = "completion_persist_failed",
                        error = "Không lưu được phần thưởng hoàn thành. Hãy thử lại."
                    });
                }
            }

            await completionTransaction.CommitAsync(cancellationToken);
            return Ok(new
            {
                success = true,
                sentenceId,
                videoId = sentenceAccess.VideoId,
                assessmentId = assessment.PublicId,
                totalScore,
                accuracyScore,
                fluencyScore,
                completenessScore,
                fluencyAvailable = false,
                completenessAvailable = false,
                assessmentSource = "server-text-match",
                passedSentenceCount,
                requiredSentenceCount,
                isVideoCompleted = reachedVideoCompletion,
                firstTimeVideoCompletion,
                xpEarned = speakingXpEarned,
                streak = currentStreak
            });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await completionTransaction.RollbackAsync(CancellationToken.None);
                _logger.LogError(ex, "Speaking completion unit failed for user {UserId} sentence {SentenceId}", userId, sentenceId);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    code = "completion_persist_failed",
                    error = "Không lưu được tiến độ. Hãy thử lại."
                });
            }
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
        /// <summary>Server assessment receipt. Required. Client scores are ignored.</summary>
        public Guid AssessmentId { get; set; }

        // Legacy fields ignored when AssessmentId is present (kept for JSON shape compatibility).
        public double TotalScore { get; set; }
        public double AccuracyScore { get; set; }
        public double FluencyScore { get; set; }
        public double CompletenessScore { get; set; }
    }
}
