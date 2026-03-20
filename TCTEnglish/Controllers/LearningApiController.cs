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
        private readonly IStreakService _streakService;
        private readonly ILogger<LearningApiController> _logger;

        public LearningApiController(
            DbflashcardContext context,
            IStreakService streakService,
            ILogger<LearningApiController> logger)
        {
            _context = context;
            _streakService = streakService;
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

            var streak = await _streakService.UpdateStreakAsync(currentUserId);

            _logger.LogInformation(
                "Learning record updated for user {userId}, card {cardId}, status {status}, repetitionCount {repetitionCount}, nextReviewDate {nextReviewDate}, isNewProgress {isNewProgress}, streak {streak}",
                currentUserId,
                request.CardId,
                progress.Status,
                progress.RepetitionCount,
                progress.NextReviewDate,
                isNewProgress,
                streak);

            return Ok(new { success = true, nextReviewDate = progress.NextReviewDate, streak });
        }
    }

    public class LearningRecordRequest
    {
        public int CardId { get; set; }
        public string MasteryLevel { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
}
