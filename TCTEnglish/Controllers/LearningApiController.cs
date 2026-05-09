using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization; // [FIX-AI-AUTH]
using TCTVocabulary.Models;
using TCTVocabulary.Security;
using TCTVocabulary.Services;

namespace TCTVocabulary.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // [FIX-AI-AUTH]
    [AutoValidateAntiforgeryToken]
    public class LearningApiController : ControllerBase
    {
        private readonly DbflashcardContext _context;
        private readonly IGoalsService _goalsService;
        private readonly ILogger<LearningApiController> _logger;

        public LearningApiController(
            DbflashcardContext context,
            IGoalsService goalsService,
            ILogger<LearningApiController> logger)
        {
            _context = context;
            _goalsService = goalsService;
            _logger = logger;
        }

        [HttpPost("record")]
        public async Task<IActionResult> Record([FromBody] LearningRecordRequest request)
        {
            if (!User.TryGetUserId(out var currentUserId))
            {
                _logger.LogWarning("Unauthorized learning record request for card {cardId}", request.CardId);
                return Unauthorized();
            }

            _logger.LogInformation(
                "Learning record request received for user {userId}, card {cardId}, masteryLevel {masteryLevel}",
                currentUserId,
                request.CardId,
                request.MasteryLevel);

            var progress = await _context.LearningProgresses
                .FirstOrDefaultAsync(lp => lp.CardId == request.CardId && lp.UserId == currentUserId);

            var isNewProgress = progress == null;
            var previousStatus = progress?.Status;

            if (progress == null)
            {
                progress = new LearningProgress
                {
                    UserId = currentUserId,
                    CardId = request.CardId,
                    Status = "Learning",
                    WrongCount = 0,
                    RepetitionCount = 0
                };
                _context.LearningProgresses.Add(progress);
            }

            // Cập nhật ngày review cuối
            progress.LastReviewedDate = DateTime.UtcNow;

            // Logic SRS – dùng RepetitionCount để tính khoảng cách ôn tập
            var masteryLevel = request.MasteryLevel?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(masteryLevel))
            {
                _logger.LogWarning(
                    "Invalid mastery level for user {userId}, card {cardId}",
                    currentUserId,
                    request.CardId);
                return BadRequest("Invalid mastery level");
            }

            int reps = progress.RepetitionCount;
            switch (masteryLevel)
            {
                case "hard":
                    // Khó → ôn lại sau 10 phút, reset repetition
                    progress.Status = "Learning";
                    progress.NextReviewDate = DateTime.UtcNow.AddMinutes(1);
                    progress.WrongCount = (progress.WrongCount ?? 0) + 1;
                    progress.RepetitionCount = 0;
                    break;
                case "good":
                    // Tốt → khoảng cách tăng dần: 1 giờ → 12 giờ → 24 giờ
                    progress.Status = "Reviewing";
                    int goodHours = reps == 0 ? 1 : (reps == 1 ? 12 : 24);
                    progress.NextReviewDate = DateTime.UtcNow.AddHours(goodHours);
                    progress.RepetitionCount = reps + 1;
                    break;
                case "easy":
                case "perfect":
                    // Dễ → khoảng cách lớn hơn: 1 → 3 → 7 ngày
                    progress.Status = "Mastered";
                    int easyDays = reps == 0 ? 1 : (reps == 1 ? 3 : 7);
                    progress.NextReviewDate = DateTime.UtcNow.AddDays(easyDays);
                    progress.RepetitionCount = reps + 1;
                    break;
                default:
                    _logger.LogWarning(
                        "Unsupported mastery level {masteryLevel} for user {userId}, card {cardId}",
                        masteryLevel,
                        currentUserId,
                        request.CardId);
                return BadRequest("Invalid mastery level");
            }

            await _context.SaveChangesAsync();

            var activityUpdate = _goalsService.BuildVocabularyActivityUpdate(isNewProgress, previousStatus, progress.Status);
            var activityResult = await _goalsService.RecordLearningActivityAsync(currentUserId, activityUpdate);
            if (activityResult.Status == OperationStatus.NotFound)
            {
                _logger.LogWarning(
                    "Learning activity record aborted because user {userId} was not found",
                    currentUserId);
                return NotFound();
            }

            if (activityResult.Status == OperationStatus.Invalid)
            {
                _logger.LogWarning(
                    "Learning activity record was rejected for user {userId}, card {cardId}",
                    currentUserId,
                    request.CardId);
                return BadRequest("Unable to record learning activity");
            }

            var streak = activityResult.Streak;

            _logger.LogInformation(
                "Learning record updated for user {userId}, card {cardId}, status {status}, repetitionCount {repetitionCount}, nextReviewDate {nextReviewDate}, isNewProgress {isNewProgress}, xpEarned {xpEarned}, streak {streak}",
                currentUserId,
                request.CardId,
                progress.Status,
                progress.RepetitionCount,
                progress.NextReviewDate,
                isNewProgress,
                activityUpdate.XpEarned,
                streak);

            return Ok(new
            {
                success = true,
                nextReviewDate = progress.NextReviewDate,
                streak,
                xpEarned = activityUpdate.XpEarned
            });
        }

        // ── Track time per feature ─────────────────────────────────────────
        [HttpPost("track-time")]
        [IgnoreAntiforgeryToken] // sendBeacon không gửi được header CSRF
        public async Task<IActionResult> TrackFeatureTime([FromBody] TrackFeatureTimeRequest request)
        {
            if (!User.TryGetUserId(out var currentUserId))
                return Unauthorized();

            var allowed = new[] { "Flashcard", "Quiz", "Speaking", "Reading", "Listening", "Writing", "Grammar" };
            if (string.IsNullOrWhiteSpace(request.Feature) || !allowed.Contains(request.Feature))
            {
                _logger.LogWarning("Invalid feature '{Feature}' from user {UserId}", request.Feature, currentUserId);
                return BadRequest("Invalid feature name.");
            }

            // Tối thiểu 10s, tối đa 3600s (1 giờ) — tránh data rác
            var duration = Math.Clamp(request.DurationSeconds, 10, 3600);

            _context.UserFeatureTimeLogs.Add(new TCTVocabulary.Models.UserFeatureTimeLog
            {
                UserId = currentUserId,
                Feature = request.Feature,
                DurationSeconds = duration,
                LoggedDate = BusinessDateHelper.Today
            });
            await _context.SaveChangesAsync();

            _logger.LogDebug("Time logged: user {UserId}, {Feature}, {Duration}s", currentUserId, request.Feature, duration);
            return Ok(new { success = true });
        }
    }

    public class LearningRecordRequest
    {
        public int CardId { get; set; }
        public string MasteryLevel { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }

    public class TrackFeatureTimeRequest
    {
        public string Feature { get; set; } = string.Empty;
        /// <summary>Thời gian học tính bằng giây.</summary>
        public int DurationSeconds { get; set; }
    }
}
