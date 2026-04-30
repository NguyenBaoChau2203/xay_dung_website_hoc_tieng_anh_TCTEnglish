using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Controllers
{
   
    [AutoValidateAntiforgeryToken]
    [Route("Home")]
    public class StudyController : BaseController
    {
        private readonly IStudyService _studyService;
        private readonly IWritingService _writingService;
        private readonly IWritingRequestRateLimiter _writingRequestRateLimiter;
        private readonly IListeningService _listeningService;
        private readonly DbflashcardContext _context;

        public StudyController(
            IStudyService studyService,
            IWritingService writingService,
            IWritingRequestRateLimiter writingRequestRateLimiter,
            IListeningService listeningService,
            DbflashcardContext context)
        {
            _studyService = studyService;
            _writingService = writingService;
            _writingRequestRateLimiter = writingRequestRateLimiter;
            _listeningService = listeningService;
            _context = context;
        }

        [Authorize]
        [HttpGet("Study/{id:int?}")]
        public async Task<IActionResult> Study(int id)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            return await RenderStudyViewAsync(nameof(Study), id, userId, redirectToFolderWhenEmpty: true);
        }

        [Authorize]
        [HttpPost("UpdateCardProgress")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCardProgress([FromBody] UpdateCardProgressRequest request)
        {
            if (request == null)
            {
                return BadRequest();
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _studyService.UpdateCardProgressAsync(request.CardId, request.IsKnown, userId);
            if (result.Status != OperationStatus.Success)
            {
                return result.Status == OperationStatus.Invalid
                    ? BadRequest(result.ErrorMessage)
                    : NotFound(result.ErrorMessage);
            }

            return Json(new { success = true });
        }

        [Authorize]
        [HttpGet("Speaking/{id:int?}")]
        public async Task<IActionResult> Speaking(int? id)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (id == null || id == 0)
            {
                return RedirectToAction("Folder", "Folder");
            }

            return await RenderStudyViewAsync(nameof(Speaking), id.Value, userId, redirectToFolderWhenEmpty: true);
        }
        
        [HttpGet("Listening")]
        public async Task<IActionResult> Listening(string? level = null, string? topic = null)
        {
            var userId = TryGetCurrentUserId(out var currentUserId) ? currentUserId : (int?)null;
            var viewModel = await _listeningService.GetIndexViewModelAsync(level, topic, userId);
            return View(viewModel);
        }

        [HttpGet("Listening/Practice/{id:int}")]
        public async Task<IActionResult> ListeningPractice(int id)
        {
            TryGetCurrentUserId(out var userId);
            var viewModel = await _listeningService.GetPracticeViewModelAsync(id, userId == 0 ? null : userId);
            return viewModel == null ? NotFound() : View(viewModel);
        }

        [Authorize]
        [HttpPost("Listening/EvaluateQuiz")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EvaluateListeningQuiz([FromBody] ListeningQuizSubmitDto? dto)
        {
            if (dto == null || dto.LessonId <= 0)
                return BadRequest(new { error = "Dữ liệu không hợp lệ." });

            var result = await _listeningService.EvaluateQuizAsync(dto);
            return result == null
                ? NotFound(new { error = "Không tìm thấy bài nghe." })
                : Json(new { success = true, data = result });
        }

        [Authorize]
        [HttpPost("Listening/SaveProgress/{lessonId:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveListeningProgress(int lessonId, [FromBody] ListeningProgressUpdateDto? dto)
        {
            if (dto == null)
                return BadRequest(new { error = "Dữ liệu không hợp lệ." });

            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { error = "Bạn cần đăng nhập." });

            // Anti-IDOR: userId comes from server-side claims, never from the request body.
            var saved = await _listeningService.SaveProgressAsync(userId, lessonId, dto);
            return saved
                ? Json(new { success = true })
                : NotFound(new { error = "Không tìm thấy bài nghe." });
        }

        [Authorize]
        [HttpPost("Listening/My/Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMyListeningLesson([FromForm] string youtubeUrl, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(youtubeUrl))
            {
                return BadRequest(new { success = false, message = "URL không được để trống." });
            }

            var result = await _listeningService.CreateOwnedLessonAsync(userId, youtubeUrl, ct);

            if (result.RequiresUpgrade)
            {
                return StatusCode(403, new { success = false, message = result.ErrorMessage, requiresUpgrade = true });
            }

            if (!result.IsSuccess)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage });
            }

            return Json(new { success = true, lessonId = result.LessonId });
        }

        [Authorize]
        [HttpPost("Listening/My/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMyListeningLesson([FromForm] int lessonId)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var result = await _listeningService.DeleteOwnedLessonAsync(userId, lessonId);

            if (result.Status == OperationStatus.NotFound) return NotFound();
            if (result.Status == OperationStatus.Invalid) return StatusCode(403, new { success = false, message = result.ErrorMessage });

            return Json(new { success = true });
        }

        [Authorize]
        [HttpPost("Listening/My/GenerateQuiz")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateListeningQuiz([FromForm] int lessonId, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var result = await _listeningService.GenerateQuizFromTranscriptAsync(lessonId, userId, ct);

            if (result.Status == OperationStatus.NotFound) return NotFound();
            if (result.Status == OperationStatus.Invalid) return BadRequest(new { success = false, message = result.ErrorMessage ?? "Lỗi tạo quiz." });

            return Ok(new { success = true });
        }

        [Authorize]
        [HttpPost("Listening/Translate/{lessonId:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TranslateTranscript(int lessonId, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var result = await _listeningService.TranslateTranscriptAsync(lessonId, userId, ct);

            if (result.Status == OperationStatus.NotFound) return NotFound();
            if (result.Status == OperationStatus.Invalid) return BadRequest(new { success = false, message = result.ErrorMessage ?? "Lỗi dịch transcript." });

            return Ok(new { success = true });
        }

        [HttpGet("Grammar")]
        public IActionResult Grammar()
        {
            return View();
        }

        [HttpGet("Reading")]
        public async Task<IActionResult> Reading()
        {
            if (!TryGetCurrentUserId(out var userId))
                return RedirectToAction("Login", "Account");

            // Lưu ý: Đảm bảo _context đã được khai báo và inject trong Constructor của StudyController
            var readings = await _context.ReadingPassages
                .Where(r => r.IsPublished)
                .Select(r => new ReadingListViewModel
                {
                    Id = r.Id,
                    Title = r.Title,
                    ImageUrl = r.ImageUrl,
                    Level = r.Level,
                    // Thêm logic kiểm tra lịch sử học tập
                    IsCompleted = r.UserReadingHistories
                        .Any(u => u.UserId == userId && u.IsCompleted),
                    IsInProgress = r.UserReadingHistories
                        .Any(u => u.UserId == userId && !u.IsCompleted)
                })
                .ToListAsync();

            // Sử dụng tên View cụ thể để tránh nhầm lẫn
            return View("Reading", readings);
        }

        [AllowAnonymous]
        [HttpGet("Writing")]
        public async Task<IActionResult> Writing(string? level = null)
        {
            var viewModel = await _writingService.GetWritingIndexViewModelAsync(level);
            return View(viewModel);
        }

        [AllowAnonymous]
        [HttpGet("Writing/Exercises")]
        public async Task<IActionResult> WritingExercises(
            string? level = null,
            string? contentType = null,
            string? topic = null,
            string? status = null,
            int page = 1)
        {
            var userId = TryGetCurrentUserId(out var currentUserId)
                ? currentUserId
                : (int?)null;
            var viewModel = await _writingService.GetWritingExerciseListViewModelAsync(level, contentType, topic, page, userId, status);
            return View(viewModel);
        }

        [AllowAnonymous]
        [HttpGet("Writing/Exercises/Data")]
        public async Task<IActionResult> WritingExercisesData(
            string? level = null,
            string? contentType = null,
            string? topic = null,
            string? status = null)
        {
            var userId = TryGetCurrentUserId(out var currentUserId)
                ? currentUserId
                : (int?)null;
            var data = await _writingService.GetWritingExerciseDataAsync(level, contentType, topic, userId, status);
            return Json(data);
        }

        [Authorize]
        [HttpPost("Writing/Exercises/CreateFromAi")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWritingExerciseFromAi(
            [FromBody] WritingCreateFromAiRequestViewModel? request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(new { error = "Thieu du lieu tao bai viet.", errorCode = "request_required" });
            }

            var result = await _writingService.CreateFromAiAsync(request, GetCurrentUserId(), cancellationToken);
            if (result.Outcome == WritingCreateFromAiOutcome.Success)
            {
                return Json(new
                {
                    success = true,
                    data = new
                    {
                        exerciseId = result.ExerciseId,
                        sentenceCount = result.SentenceCount,
                        level = result.Level,
                        contentType = result.ContentType,
                        isReplay = result.IsReplay
                    }
                });
            }

            if (result.Outcome == WritingCreateFromAiOutcome.QuotaExceeded)
            {
                if (result.RetryAfterSeconds > 0)
                {
                    Response.Headers["Retry-After"] = result.RetryAfterSeconds.ToString();
                }

                return StatusCode(429, new { error = result.ErrorMessage, errorCode = result.ErrorCode, retryAfterSeconds = result.RetryAfterSeconds });
            }

            if (result.Outcome == WritingCreateFromAiOutcome.Forbidden)
            {
                return StatusCode(403, new { error = result.ErrorMessage, errorCode = result.ErrorCode });
            }

            if (result.Outcome == WritingCreateFromAiOutcome.Invalid)
            {
                return BadRequest(new { error = result.ErrorMessage, errorCode = result.ErrorCode });
            }

            return StatusCode(503, new { error = result.ErrorMessage, errorCode = result.ErrorCode });
        }

        [Authorize]
        [HttpPost("Writing/Exercises/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOwnedWritingExercise(
            int exerciseId,
            string? level = null,
            string? contentType = null,
            string? topic = null,
            string? status = null,
            int page = 1)
        {
            var result = await _writingService.DeleteOwnedExerciseAsync(exerciseId, GetCurrentUserId());
            if (result.Status == OperationStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == OperationStatus.Invalid)
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "Khong the xoa bai viet luc nay.";
            }
            else
            {
                TempData["SuccessMessage"] = "Da xoa bai viet cua ban.";
            }

            return RedirectToAction(nameof(WritingExercises), new
            {
                level,
                contentType,
                topic,
                status,
                page
            });
        }

        [Authorize]
        [HttpGet("Writing/Practice")]
        public async Task<IActionResult> WritingPractice(
            string? level = null,
            string? contentType = null,
            string? topic = null,
            string? status = null,
            int page = 1,
            int? exerciseId = null)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await _writingService.GetWritingPracticeViewModelAsync(
                level,
                contentType,
                topic,
                page,
                exerciseId,
                userId,
                status);

            return viewModel == null ? NotFound() : View(viewModel);
        }

        [Authorize]
        [HttpGet("Writing/Practice/Data")]
        public async Task<IActionResult> WritingPracticeData(int exerciseId)
        {
            if (exerciseId <= 0)
            {
                return BadRequest();
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var data = await _writingService.GetWritingPracticeDataAsync(exerciseId, userId);
            return data == null ? NotFound() : Json(data);
        }

        [Authorize]
        [HttpGet("Writing/Practice/Hint")]
        public async Task<IActionResult> WritingHint(int exerciseId, int sentenceId)
        {
            if (exerciseId <= 0 || sentenceId <= 0)
            {
                return BadRequest();
            }

            var userId = GetCurrentUserId();
            if (!_writingRequestRateLimiter.TryConsumeHint(
                    userId,
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    out var retryAfterSeconds))
            {
                return BuildWritingRateLimitResult(
                    "Ban dang yeu cau goi y qua nhanh. Vui long thu lai sau.",
                    retryAfterSeconds);
            }

            var data = await _writingService.GetWritingSentenceHintAsync(exerciseId, sentenceId, userId);
            return data == null ? NotFound() : Json(data);
        }

        [Authorize]
        [HttpPost("Writing/Practice/Evaluate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EvaluateWritingSentence([FromBody] WritingSentenceEvaluationRequestViewModel? request)
        {
            if (request == null)
            {
                return BadRequest(new { error = "Thiếu dữ liệu chấm bài." });
            }

            if (request.ExerciseId <= 0 || request.SentenceId <= 0)
            {
                return BadRequest(new { error = "Cần cung cấp bài tập và câu hợp lệ." });
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(new { error = "Bạn cần đăng nhập để luyện viết." });
            }

            if (string.IsNullOrWhiteSpace(request.UserAnswer))
            {
                return BadRequest(new { error = "Vui lòng nhập một câu tiếng Anh trước khi gửi bài." });
            }

            if (!_writingRequestRateLimiter.TryConsumeEvaluation(
                    userId,
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    out var retryAfterSeconds))
            {
                return BuildWritingRateLimitResult(
                    "Ban dang gui bai qua nhanh. Vui long thu lai sau.",
                    retryAfterSeconds);
            }

            var evaluation = await _writingService.EvaluateWritingSentenceAsync(
                request.ExerciseId,
                request.SentenceId,
                request.UserAnswer,
                userId);

            if (evaluation == null)
            {
                return NotFound(new { error = "Không tìm thấy câu viết mà bạn yêu cầu." });
            }

            return Json(new { success = true, data = evaluation });
        }

        [Authorize]
        [HttpGet("WriteMode/{id:int?}")]
        public async Task<IActionResult> WriteMode(int id)
        {
            return await RenderStudyViewAsync(nameof(WriteMode), id);
        }

        [Authorize]
        [HttpGet("QuizMode/{id:int?}")]
        public async Task<IActionResult> QuizMode(int id)
        {
            return await RenderStudyViewAsync(nameof(QuizMode), id);
        }

        [Authorize]
        [HttpGet("MatchingMode/{id:int?}")]
        public async Task<IActionResult> MatchingMode(int id)
        {
            return await RenderStudyViewAsync(nameof(MatchingMode), id);
        }

        private async Task<IActionResult> RenderStudyViewAsync(
            string viewName,
            int setId,
            int? userId = null,
            bool redirectToFolderWhenEmpty = false)
        {
            var viewModel = await _studyService.GetStudyViewModelAsync(setId, userId);
            if (viewModel == null)
            {
                return NotFound();
            }

            if (redirectToFolderWhenEmpty && !viewModel.Cards.Any())
            {
                return RedirectToAction(nameof(FolderController.FolderDetail), "Folder", new { id = viewModel.Set.FolderId });
            }

            return View(viewName, viewModel);
        }

        private IActionResult BuildWritingRateLimitResult(string errorMessage, int retryAfterSeconds)
        {
            Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
            return StatusCode(429, new
            {
                error = errorMessage,
                retryAfterSeconds
            });
        }
    }

    public class UpdateCardProgressRequest
    {
        public int CardId { get; set; }
        public bool IsKnown { get; set; }
    }
}
