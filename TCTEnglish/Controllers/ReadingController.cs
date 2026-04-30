using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTEnglish.ViewModels;
using System.Text.Json;

namespace TCTEnglish.Controllers
{
    public class ReadingController : Controller
    {
        private readonly DbflashcardContext _context;
        private readonly IGoalsService _goalsService;

        public ReadingController(DbflashcardContext context, IGoalsService goalsService)
        {
            _context = context;
            _goalsService = goalsService;
        }

        // Trang học chi tiết
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

        // Xử lý nộp bài AJAX
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

        private bool TryGetCurrentUserId(out int userId)
        {
            userId = 0;
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out userId);
        }
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
    }
}