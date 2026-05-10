using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TCTEnglish.Models;
using TCTEnglish.ViewModels;
using TCTVocabulary.Controllers;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Controllers
{
    public class ReadingController : BaseController
    {
        private readonly DbflashcardContext _context;
        private readonly IGoalsService _goalsService;
        private readonly IReadingTranslationService _translationService;

        public ReadingController(
            DbflashcardContext context,
            IGoalsService goalsService,
            IReadingTranslationService translationService)
        {
            _context = context;
            _goalsService = goalsService;
            _translationService = translationService;
        }

        // ─── Trang học chi tiết ───────────────────────────────────────────────
        [HttpGet("Reading/Study/{id}")]
        public async Task<IActionResult> Study(int id)
        {
            if (!TryGetCurrentUserId(out var userId)) return RedirectToAction("Login", "Account");

            var passage = await _context.ReadingPassages
                .Include(p => p.Questions).ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (passage == null) return NotFound();

            // Đảm bảo có bản ghi lịch sử để hiện "In Progress"
            var history = await _context.UserReadingHistories
                .FirstOrDefaultAsync(x => x.UserId == userId && x.ReadingPassageId == id);

            if (history == null)
            {
                history = new UserReadingHistory
                {
                    UserId = userId,
                    ReadingPassageId = id,
                    ViewedAt = DateTime.Now,
                    IsCompleted = false
                };
                _context.UserReadingHistories.Add(history);
                await _context.SaveChangesAsync();
            }

            var model = new ReadingStudyViewModel
            {
                Id = passage.Id,
                Title = passage.Title,
                Content = passage.Content,
                ImageUrl = passage.ImageUrl,
                Questions = passage.Questions.OrderBy(q => q.OrderIndex).Select(q => new QuestionViewModel
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    Options = q.Options.Select(o => new OptionViewModel { Id = o.Id, OptionText = o.OptionText }).ToList()
                }).ToList()
            };
            return View("~/Views/Study/ReadingStudy.cshtml", model);
        }

        // ─── Xử lý nộp bài AJAX ──────────────────────────────────────────────
        [HttpPost("Reading/SubmitReading")]
        public async Task<IActionResult> SubmitReading(int passageId, Dictionary<int, int> answers)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            var questions = await _context.ReadingQuestions
                .Include(q => q.Options)
                .Where(q => q.PassageId == passageId).ToListAsync();

            int correctCount = 0;
            var details = new List<object>();

            foreach (var q in questions)
            {
                answers.TryGetValue(q.Id, out int selectedId);
                var correctOption = q.Options.FirstOrDefault(o => o.IsCorrect);
                bool isCorrect = (correctOption != null && correctOption.Id == selectedId);
                if (isCorrect) correctCount++;

                details.Add(new
                {
                    questionId = q.Id,
                    isCorrect = isCorrect,
                    correctOptionText = correctOption?.OptionText
                });
            }

            // Cập nhật Database thành COMPLETED
            var history = await _context.UserReadingHistories
                .FirstOrDefaultAsync(h => h.UserId == userId && h.ReadingPassageId == passageId);
            if (history != null)
            {
                var isNewCompletion = !history.IsCompleted;
                history.IsCompleted = true; // Chuyển trạng thái
                history.Score = correctCount;
                history.ViewedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                if (isNewCompletion)
                {
                    var activityUpdate = _goalsService.BuildReadingCompletionActivityUpdate();
                    await _goalsService.RecordLearningActivityAsync(userId, activityUpdate);
                }
            }

            return Json(new { success = true, correctCount, totalCount = questions.Count, details });
        }

        // ─── Dịch Google ──────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Translate(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Json(new { translation = "" });

            try
            {
                using var client = new HttpClient();
                // Thêm User-Agent để tránh bị API chặn
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

                var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair=en|vi";
                var response = await client.GetStringAsync(url);

                using JsonDocument doc = JsonDocument.Parse(response);
                var responseData = doc.RootElement.GetProperty("responseData");

                // MyMemory có thể trả về thông báo giới hạn nếu gọi quá nhanh
                var translated = responseData.GetProperty("translatedText").GetString();

                return Json(new { translation = translated });
            }
            catch
            {
                return Json(new { translation = "" });
            }
        }

        // ─── Translation API: Lấy bản dịch hiện tại của user ─────────────────
        [Authorize]
        [HttpGet("Reading/MyTranslation/{passageId}")]
        public async Task<IActionResult> MyTranslation(int passageId)
        {
            var userId = GetCurrentUserId();
            var translation = await _translationService.GetMyTranslationAsync(userId, passageId);

            if (translation == null)
                return Json(new { exists = false });

            return Json(new
            {
                exists = true,
                id = translation.Id,
                translatedTitle = translation.TranslatedTitle,
                translatedContent = translation.TranslatedContent,
                aiScore = translation.AiScore,
                aiFeedback = translation.AiFeedback,
                isAiApproved = translation.IsAiApproved,
                isPublic = translation.IsPublic
            });
        }

        // ─── Translation API: Danh sách bản dịch public ──────────────────────
        [HttpGet("Reading/Translations/{passageId}")]
        public async Task<IActionResult> Translations(int passageId)
        {
            var translations = await _translationService.GetPublicTranslationsAsync(passageId);
            return Json(new { items = translations });
        }

        // ─── Translation API: Submit bản dịch ────────────────────────────────
        [Authorize]
        [HttpPost("Reading/SubmitTranslation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitTranslation([FromBody] SubmitTranslationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TranslatedContent))
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });

            var userId = GetCurrentUserId();
            var result = await _translationService.SubmitTranslationAsync(
                userId, request.PassageId, request.TranslatedTitle, request.TranslatedContent);

            return Json(result);
        }

        // ─── Translation API: Publish/Unpublish ──────────────────────────────
        [Authorize]
        [HttpPost("Reading/PublishTranslation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublishTranslation([FromBody] PublishTranslationRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _translationService.PublishTranslationAsync(userId, request.TranslationId);
            return Json(result);
        }

        // ─── Translation API: Delete ─────────────────────────────────────────
        [Authorize]
        [HttpPost("Reading/DeleteTranslation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTranslation([FromBody] DeleteTranslationRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _translationService.DeleteTranslationAsync(userId, request.TranslationId);
            return Json(result);
        }

        // ─── Translation API: Vote ───────────────────────────────────────────
        [Authorize]
        [HttpPost("Reading/VoteTranslation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoteTranslation([FromBody] VoteTranslationRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _translationService.VoteTranslationAsync(
                userId, request.TranslationId, request.VoteType);
            return Json(result);
        }

        // ─── Trang xem chi tiết bản dịch cộng đồng ──────────────────────────
        [HttpGet("Reading/CommunityTranslation/{id}")]
        public async Task<IActionResult> CommunityTranslation(int id)
        {
            TryGetCurrentUserId(out var userId);
            var detail = await _translationService.GetTranslationDetailAsync(id, userId > 0 ? userId : null);
            if (detail == null) return NotFound();

            return View("~/Views/Reading/CommunityTranslation.cshtml", detail);
        }
    }

    // ─── Request DTOs ─────────────────────────────────────────────────────────
    public class SubmitTranslationRequest
    {
        public int PassageId { get; set; }
        public string? TranslatedTitle { get; set; }
        public string TranslatedContent { get; set; } = null!;
    }

    public class PublishTranslationRequest
    {
        public int TranslationId { get; set; }
    }

    public class DeleteTranslationRequest
    {
        public int TranslationId { get; set; }
    }

    public class VoteTranslationRequest
    {
        public int TranslationId { get; set; }
        public TranslationVoteType VoteType { get; set; }
    }
}