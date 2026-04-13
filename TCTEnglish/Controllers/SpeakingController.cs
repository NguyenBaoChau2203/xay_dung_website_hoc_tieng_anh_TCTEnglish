using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        private readonly DbflashcardContext _context;
        private readonly ISpeakingService _speakingService;

        public SpeakingController(
            DbflashcardContext context,
            IYoutubeTranscriptService transcriptService,
            ILogger<SpeakingService> speakingServiceLogger)
        {
            _context = context;
            _speakingService = new SpeakingService(context, transcriptService, speakingServiceLogger);
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

            // 3. Kiểm tra câu tồn tại + quyền truy cập video cha
            var sentenceAccess = await _context.SpeakingSentences
                .AsNoTracking()
                .Where(s => s.Id == sentenceId)
                .Select(s => new
                {
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

            if (existing != null)
            {
                // Chỉ cập nhật nếu điểm mới cao hơn
                if (dto.TotalScore > existing.TotalScore)
                {
                    existing.TotalScore = dto.TotalScore;
                    existing.AccuracyScore = dto.AccuracyScore;
                    existing.FluencyScore = dto.FluencyScore;
                    existing.CompletenessScore = dto.CompletenessScore;
                    existing.PracticedAt = DateTime.UtcNow;
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
                    PracticedAt = DateTime.UtcNow
                };
                _context.UserSpeakingProgresses.Add(progress);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                sentenceId,
                totalScore = dto.TotalScore,
                accuracyScore = dto.AccuracyScore,
                fluencyScore = dto.FluencyScore,
                completenessScore = dto.CompletenessScore
            });
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
