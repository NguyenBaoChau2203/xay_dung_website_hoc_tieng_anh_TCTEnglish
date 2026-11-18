using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services;
using TCTEnglish.Services.Sets;
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
        private readonly TCTEnglish.Services.Reading.IReadingLearningService _readingLearning;
        private readonly DbflashcardContext _context;
        private readonly IPersonalSetService _personalSets;
        private readonly ILogger<StudyController>? _logger;

        public StudyController(
            IStudyService studyService,
            IWritingService writingService,
            IWritingRequestRateLimiter writingRequestRateLimiter,
            IListeningService listeningService,
            TCTEnglish.Services.Reading.IReadingLearningService readingLearning,
            DbflashcardContext context,
            IPersonalSetService personalSets,
            ILogger<StudyController>? logger = null)
        {
            _logger = logger;
            _personalSets = personalSets;
            _studyService = studyService;
            _writingService = writingService;
            _writingRequestRateLimiter = writingRequestRateLimiter;
            _listeningService = listeningService;
            _readingLearning = readingLearning;
            _context = context;
        }

        [Authorize]
        [HttpGet("Study/{id:int?}")]
        public async Task<IActionResult> Study(int id, string? mode = null)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            return await RenderStudyViewAsync(nameof(Study), id, userId, redirectToSetWhenEmpty: true, mode);
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

            // The card must exist and belong to a set the user may view (B7); otherwise nothing is written.
            var cardSetId = await _context.Cards
                .AsNoTracking()
                .Where(c => c.CardId == request.CardId)
                .Select(c => (int?)c.SetId)
                .FirstOrDefaultAsync();
            if (cardSetId == null || !await _personalSets.CanViewSetAsync(cardSetId.Value, userId, IsAdminUser()))
            {
                return NotFound();
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

            return await RenderStudyViewAsync(nameof(Speaking), id.Value, userId, redirectToSetWhenEmpty: true);
        }
        
        [HttpGet("Listening")]
        public async Task<IActionResult> Listening(string? level = null, string? topic = null, CancellationToken ct = default)
        {
            var userId = TryGetCurrentUserId(out var currentUserId) ? currentUserId : (int?)null;
            // The page filters client-side, so it needs every lesson; level/topic only pick the initial selection.
            var viewModel = await _listeningService.GetIndexViewModelAsync(null, null, userId, ct);
            viewModel.SelectedLevel = NormalizeListeningLevel(level);
            viewModel.SelectedTopic = !string.IsNullOrWhiteSpace(topic) && viewModel.Topics.Contains(topic) ? topic : null;
            return View(viewModel);
        }

        private static string? NormalizeListeningLevel(string? level)
        {
            var key = level?.Trim().ToUpperInvariant();
            return key is "A1" or "A2" or "B1" or "B2" or "C1" ? key : null;
        }

        [HttpGet("Listening/Practice/{id:int}")]
        public async Task<IActionResult> ListeningPractice(int id, CancellationToken ct = default)
        {
            TryGetCurrentUserId(out var userId);
            var viewModel = await _listeningService.GetPracticeViewModelAsync(id, userId == 0 ? null : userId, ct);
            return viewModel == null ? NotFound() : View(viewModel);
        }

        [Authorize]
        [HttpPost("Listening/EvaluateQuiz")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EvaluateListeningQuiz([FromBody] ListeningQuizSubmitDto? dto, CancellationToken ct = default)
        {
            if (dto == null || dto.LessonId <= 0)
                return BadRequest(new { error = "Dữ liệu không hợp lệ." });

            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { error = "Bạn cần đăng nhập." });

            // Access, grading and best-score persistence are decided by the service (404 when the lesson is not usable).
            var result = await _listeningService.EvaluateQuizAsync(dto, userId, ct);
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

            var readings = await _readingLearning.GetLibraryAsync(userId);
            var readingLevelGated = !IsAdminUser();
            ViewBag.ReadingLevelGated = readingLevelGated;
            ViewBag.StudentReadingLevel = readingLevelGated
                ? await _readingLearning.GetUserReadingLevelAsync(userId)
                : null;
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
            int page = 1,
            int myPage = 1)
        {
            var userId = TryGetCurrentUserId(out var currentUserId)
                ? currentUserId
                : (int?)null;
            var viewModel = await _writingService.GetWritingExerciseListViewModelAsync(level, contentType, topic, page, userId, status, myPage);
            if (userId.HasValue)
            {
                Response.Headers.CacheControl = "no-store";
                viewModel.RecentOwnerUserId = userId;
                try
                {
                    viewModel.RecentExercises = await _writingService.GetRecentExercisesAsync(userId.Value);
                }
                catch (Exception ex) when (ex is System.Data.Common.DbException or TimeoutException)
                {
                    _logger?.LogWarning(ex, "Writing recent history unavailable for user {UserId}", userId.Value);
                    viewModel.RecentLoadFailed = true;
                }
            }
            return View(viewModel);
        }

        [Authorize]
        [HttpGet("/api/writing/recent")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> RecentWritingExercises(CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
            return Json(new { success = true, ownerUserId = userId, data = await _writingService.GetRecentExercisesAsync(userId, ct: ct) });
        }

        [Authorize]
        [HttpPost("/api/writing/{exerciseId:int}/recent")]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> RecordWritingExerciseVisit(int exerciseId, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
            var openedAt = await _writingService.RecordExerciseVisitAsync(userId, exerciseId, ct);
            return openedAt.HasValue ? Json(new { success = true, lastOpenedAtUtc = openedAt.Value })
                : NotFound(new { error = "Không tìm thấy bài viết." });
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
        [RequestSizeLimit(65536)]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWritingExerciseFromAi(
            [FromBody] WritingCreateFromAiRequestViewModel? request,
            CancellationToken cancellationToken)
        {
            if (request == null || !ModelState.IsValid)
            {
                return BadRequest(new { error = "Thieu du lieu tao bai viet.", errorCode = "request_required" });
            }

            var result = await _writingService.CreateFromAiAsync(request, GetCurrentUserId(), cancellationToken);
            return WritingGenerationResponse(result);
        }

        [Authorize]
        [HttpGet("/api/writing/generation/capabilities")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult WritingGenerationCapabilities() => Json(new { success = true, contractVersion = 1,
            modes = new[] { "from_text", "from_brief" },
            templates = TCTEnglish.Services.Writing.Generation.WritingTemplateCatalog.Capabilities });

        [Authorize]
        [HttpGet("/api/writing/generation/quota")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> WritingGenerationQuota(CancellationToken ct)
        {
            var quota = await _writingService.GetWritingGenerationQuotaAsync(GetCurrentUserId(), ct);
            return quota == null ? NotFound() : Json(new { success = true, data = quota });
        }

        [Authorize]
        [HttpGet("/api/writing/generation/by-key/{key}")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> WritingGenerationByKey(string key, CancellationToken ct)
        {
            var result = await _writingService.GetWritingGenerationByKeyAsync(GetCurrentUserId(), key, ct);
            return result == null ? NotFound() : WritingGenerationResponse(result);
        }

        [Authorize]
        [HttpGet("/api/writing/generation/{operationId:guid}")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> WritingGenerationStatus(Guid operationId, CancellationToken ct)
        {
            var result = await _writingService.GetWritingGenerationAsync(GetCurrentUserId(), operationId, ct);
            return result == null ? NotFound() : WritingGenerationResponse(result);
        }

        [Authorize]
        [HttpPost("/api/writing/generation/{operationId:guid}/confirm-template")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(8192)]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ConfirmWritingTemplate(Guid operationId, [FromBody] WritingTemplateConfirmation? request, CancellationToken ct)
        {
            if (request == null || !ModelState.IsValid) return BadRequest(new { errorCode = "request_required" });
            return WritingGenerationResponse(await _writingService.ConfirmWritingTemplateAsync(GetCurrentUserId(), operationId, request, ct));
        }

        [Authorize]
        [HttpPost("/api/writing/generation/{operationId:guid}/cancel")]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> CancelWritingGeneration(Guid operationId, CancellationToken ct) =>
            WritingGenerationResponse(await _writingService.CancelWritingGenerationAsync(GetCurrentUserId(), operationId, ct));

        private IActionResult WritingGenerationResponse(WritingCreateFromAiResultViewModel result)
        {
            if (result.RetryAfterSeconds > 0) Response.Headers["Retry-After"] = result.RetryAfterSeconds.ToString();
            var status = result.Outcome switch
            {
                WritingCreateFromAiOutcome.Success or WritingCreateFromAiOutcome.TemplateOffer => 200,
                WritingCreateFromAiOutcome.Pending => 202,
                WritingCreateFromAiOutcome.QuotaExceeded => 429,
                WritingCreateFromAiOutcome.Forbidden => 403,
                WritingCreateFromAiOutcome.Invalid => 400,
                WritingCreateFromAiOutcome.Conflict => 409,
                WritingCreateFromAiOutcome.NotFound => 404,
                _ => 503
            };
            return StatusCode(status, new { success = result.Outcome == WritingCreateFromAiOutcome.Success,
                error = result.ErrorMessage, errorCode = result.ErrorCode, retryAfterSeconds = result.RetryAfterSeconds,
                contractVersion = 1, data = result });
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

            Response.Headers.CacheControl = "no-store";
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

            if (!ModelState.IsValid || request.UserAnswer.Length > TCTEnglish.Services.Writing.WritingAssessmentPolicy.MaxAcceptedAnswerLength)
            {
                return BadRequest(new { error = "Câu trả lời không hợp lệ hoặc vượt quá 1.000 ký tự." });
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
                userId,
                HttpContext.RequestAborted);

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
            bool redirectToSetWhenEmpty = false,
            string? mode = null)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            // Read access to the set (B4): legacy sets are Public, folder/class/saved access is included.
            var isAdmin = IsAdminUser();
            if (!await _personalSets.CanViewSetAsync(setId, currentUserId, isAdmin))
            {
                return NotFound();
            }

            var isDue = string.Equals(mode, "due", StringComparison.OrdinalIgnoreCase);
            var viewModel = await _studyService.GetStudyViewModelAsync(setId, userId, isDue ? "due" : null);
            if (viewModel == null)
            {
                return NotFound();
            }

            if (redirectToSetWhenEmpty && !viewModel.Cards.Any())
            {
                // /Sets/{id} always exists, unlike FolderDetail/0 for sets without a folder (B6).
                if (isDue)
                {
                    TempData["SetMessage"] = "Bạn đã ôn xong bộ này. Quay lại sau nhé.";
                }

                return RedirectToAction(nameof(SetController.Detail), "Set", new { id = setId });
            }

            viewModel.CanEdit = await _personalSets.CanEditSetAsync(setId, currentUserId, isAdmin);
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
