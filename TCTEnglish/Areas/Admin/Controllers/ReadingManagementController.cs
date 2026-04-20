using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Areas.Admin.ViewModels;
using TCTVocabulary.Models;

namespace TCTEnglish.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ReadingManagementController : Controller
    {
        private readonly DbflashcardContext _context;

        public ReadingManagementController(DbflashcardContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. DANH SÁCH & TÌM KIẾM
        // ==========================================
        public async Task<IActionResult> Index(string searchString)
        {
            var query = _context.ReadingPassages
                .Include(p => p.Questions)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(p => p.Title.Contains(searchString) || p.Topic.Contains(searchString));
            }

            var passages = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
            ViewBag.SearchString = searchString;

            return View(passages);
        }

        // ==========================================
        // 2. TẠO BÀI ĐỌC MỚI
        // ==========================================
        public IActionResult Create()
        {
            return View(new ReadingCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReadingCreateViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var reading = new ReadingPassage
            {
                Title = model.Title,
                Content = model.Content,
                Level = model.Level,
                Topic = model.Topic,
                ImageUrl = model.ImageUrl,
                IsPublished = model.IsPublished,
                CreatedAt = DateTime.Now
            };

            _context.ReadingPassages.Add(reading);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Tạo bài đọc thành công! Hãy thêm câu hỏi bên dưới.";
            return RedirectToAction(nameof(ManageQuestions), new { passageId = reading.Id });
        }

        // ==========================================
        // 3. CHỈNH SỬA BÀI ĐỌC (EDIT)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var passage = await _context.ReadingPassages.FindAsync(id);
            if (passage == null) return NotFound();

            var model = new ReadingCreateViewModel
            {
                Title = passage.Title,
                Content = passage.Content,
                Level = passage.Level,
                Topic = passage.Topic,
                ImageUrl = passage.ImageUrl,
                IsPublished = passage.IsPublished
            };
            ViewBag.Id = id;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ReadingCreateViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var passage = await _context.ReadingPassages.FindAsync(id);
            if (passage == null) return NotFound();

            passage.Title = model.Title;
            passage.Content = model.Content;
            passage.Level = model.Level;
            passage.Topic = model.Topic;
            passage.ImageUrl = model.ImageUrl;
            passage.IsPublished = model.IsPublished;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Cập nhật bài đọc thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // 4. BẬT/TẮT HIỂN THỊ NHANH (TOGGLE)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> TogglePublish(int id)
        {
            var passage = await _context.ReadingPassages.FindAsync(id);
            if (passage != null)
            {
                passage.IsPublished = !passage.IsPublished;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // 5. XÓA BÀI ĐỌC (DELETE)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            // Tải bài đọc cùng với các quan hệ phụ thuộc để xóa sạch (Cascade)
            var passage = await _context.ReadingPassages
                .Include(p => p.Questions)
                    .ThenInclude(q => q.Options)
                .Include(p => p.UserReadingHistories)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (passage == null) return NotFound();

            _context.ReadingPassages.Remove(passage);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa bài đọc và các dữ liệu liên quan.";
            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // 6. QUẢN LÝ CÂU HỎI & ĐÁP ÁN (MANAGE QUESTIONS)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> ManageQuestions(int passageId)
        {
            var passage = await _context.ReadingPassages
                .Include(p => p.Questions)
                    .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(p => p.Id == passageId);

            if (passage == null) return NotFound();

            var vm = new ManageQuestionsViewModel
            {
                PassageId = passage.Id,
                PassageTitle = passage.Title,
                NewOrderIndex = passage.Questions.Count + 1,
                ExistingQuestions = passage.Questions.OrderBy(q => q.OrderIndex).Select(q => new ReadingQuestionItem
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    OrderIndex = q.OrderIndex,
                    Options = q.Options.Select(o => o.OptionText).ToList(),
                    CorrectOptionText = q.Options.FirstOrDefault(o => o.IsCorrect)?.OptionText
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddQuestion(ManageQuestionsViewModel model)
        {
            if (string.IsNullOrEmpty(model.NewQuestionText))
            {
                TempData["Error"] = "Vui lòng nhập nội dung câu hỏi!";
                return RedirectToAction(nameof(ManageQuestions), new { passageId = model.PassageId });
            }

            var question = new ReadingQuestion
            {
                PassageId = model.PassageId,
                QuestionText = model.NewQuestionText,
                OrderIndex = model.NewOrderIndex,
                Options = new List<ReadingOption>
                {
                    new ReadingOption { OptionText = model.OptionA, IsCorrect = model.CorrectOption == "A" },
                    new ReadingOption { OptionText = model.OptionB, IsCorrect = model.CorrectOption == "B" },
                    new ReadingOption { OptionText = model.OptionC, IsCorrect = model.CorrectOption == "C" },
                    new ReadingOption { OptionText = model.OptionD, IsCorrect = model.CorrectOption == "D" }
                }
            };

            _context.ReadingQuestions.Add(question);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã thêm câu hỏi mới thành công!";
            return RedirectToAction(nameof(ManageQuestions), new { passageId = model.PassageId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteQuestion(int questionId, int passageId)
        {
            var question = await _context.ReadingQuestions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question != null)
            {
                _context.ReadingQuestions.Remove(question);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa câu hỏi.";
            }

            return RedirectToAction(nameof(ManageQuestions), new { passageId = passageId });
        }
    }
}