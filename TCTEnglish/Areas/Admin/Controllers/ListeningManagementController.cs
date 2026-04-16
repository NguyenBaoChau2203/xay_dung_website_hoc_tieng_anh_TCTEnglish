using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Areas.Admin.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using System.Text.RegularExpressions;

namespace TCTVocabulary.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class ListeningManagementController : Controller
{
    private static readonly Regex YoutubeIdRegex = new("^[a-zA-Z0-9_-]{11}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly DbflashcardContext _context;
    private readonly IYoutubeTranscriptService _transcriptService;
    private readonly ILogger<ListeningManagementController> _logger;

    public ListeningManagementController(
        DbflashcardContext context,
        IYoutubeTranscriptService transcriptService,
        ILogger<ListeningManagementController> logger)
    {
        _context = context;
        _transcriptService = transcriptService;
        _logger = logger;
    }

    // GET: Admin/ListeningManagement
    public async Task<IActionResult> Index(string? q, string? level, int page = 1)
    {
        const int pageSize = 20;
        var query = _context.ListeningLessons.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(l => l.Title.Contains(q) || l.Topic.Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(level) && level != "All")
        {
            query = query.Where(l => l.Level == level);
        }

        var totalItems = await query.CountAsync();
        var lessons = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new ListeningLessonListItemViewModel
            {
                Id = l.Id,
                Title = l.Title,
                Level = l.Level,
                Topic = l.Topic,
                YoutubeId = l.YoutubeId,
                IsPublished = l.IsPublished,
                CreatedAt = l.CreatedAt,
                TranscriptCount = l.TranscriptLines.Count,
                QuizCount = l.QuizQuestions.Count,
                VocabCount = l.VocabItems.Count
            })
            .ToListAsync();

        var model = new ListeningLessonIndexViewModel
        {
            Lessons = lessons,
            SearchToken = q,
            LevelFilter = level,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };

        return View(model);
    }

    // GET: Admin/ListeningManagement/Create
    public IActionResult Create()
    {
        return View("CreateEdit", new ListeningLessonCreateEditViewModel { IsPublished = true });
    }

    // GET: Admin/ListeningManagement/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var lesson = await _context.ListeningLessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lesson == null) return NotFound();

        var model = new ListeningLessonCreateEditViewModel
        {
            Id = lesson.Id,
            Title = lesson.Title,
            Level = lesson.Level,
            Topic = lesson.Topic,
            YoutubeId = lesson.YoutubeId,
            AudioUrl = lesson.AudioUrl,
            ThumbnailUrl = lesson.ThumbnailUrl,
            Duration = lesson.Duration,
            Speaker1Name = lesson.Speaker1Name,
            Speaker1Country = lesson.Speaker1Country,
            Speaker2Name = lesson.Speaker2Name,
            Speaker2Country = lesson.Speaker2Country,
            IsPublished = lesson.IsPublished
        };

        return View("CreateEdit", model);
    }

    // POST: Admin/ListeningManagement/CreateEdit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEdit(ListeningLessonCreateEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        model.YoutubeId = NormalizeYoutubeId(model.YoutubeId);

        if (model.Id == 0)
        {
            // Create
            var lesson = new ListeningLesson
            {
                Title = model.Title.Trim(),
                Level = model.Level,
                Topic = model.Topic.Trim(),
                YoutubeId = model.YoutubeId,
                AudioUrl = model.AudioUrl,
                ThumbnailUrl = model.ThumbnailUrl ?? (string.IsNullOrEmpty(model.YoutubeId) ? null : $"https://img.youtube.com/vi/{model.YoutubeId}/hqdefault.jpg"),
                Duration = model.Duration,
                Speaker1Name = model.Speaker1Name,
                Speaker1Country = model.Speaker1Country,
                Speaker2Name = model.Speaker2Name,
                Speaker2Country = model.Speaker2Country,
                IsPublished = model.IsPublished,
                CreatedAt = DateTime.UtcNow
            };

            _context.ListeningLessons.Add(lesson);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã tạo bài học mới thành công.";
            return RedirectToAction(nameof(ManageTranscript), new { id = lesson.Id });
        }
        else
        {
            // Update
            var lesson = await _context.ListeningLessons.FirstOrDefaultAsync(l => l.Id == model.Id);
            if (lesson == null) return NotFound();

            lesson.Title = model.Title.Trim();
            lesson.Level = model.Level;
            lesson.Topic = model.Topic.Trim();
            lesson.YoutubeId = model.YoutubeId;
            lesson.AudioUrl = model.AudioUrl;
            lesson.ThumbnailUrl = model.ThumbnailUrl ?? (string.IsNullOrEmpty(model.YoutubeId) ? null : $"https://img.youtube.com/vi/{model.YoutubeId}/hqdefault.jpg");
            lesson.Duration = model.Duration;
            lesson.Speaker1Name = model.Speaker1Name;
            lesson.Speaker1Country = model.Speaker1Country;
            lesson.Speaker2Name = model.Speaker2Name;
            lesson.Speaker2Country = model.Speaker2Country;
            lesson.IsPublished = model.IsPublished;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã cập nhật bài học thành công.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: Admin/ListeningManagement/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var lesson = await _context.ListeningLessons.FirstOrDefaultAsync(l => l.Id == id);
        if (lesson == null) return NotFound();

        _context.ListeningLessons.Remove(lesson);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã xóa bài học thành công.";
        return RedirectToAction(nameof(Index));
    }

    // GET: Admin/ListeningManagement/ManageTranscript/5
    public async Task<IActionResult> ManageTranscript(int id)
    {
        var lesson = await _context.ListeningLessons
            .Include(l => l.TranscriptLines)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lesson == null) return NotFound();

        var model = new ListeningTranscriptManageViewModel
        {
            LessonId = lesson.Id,
            LessonTitle = lesson.Title,
            YoutubeId = lesson.YoutubeId,
            Lines = lesson.TranscriptLines
                .OrderBy(l => l.OrderIndex)
                .Select(l => new ListeningTranscriptLineViewModel
                {
                    Id = l.Id,
                    OrderIndex = l.OrderIndex,
                    Speaker = l.Speaker,
                    Text = l.Text,
                    VietnameseMeaning = l.VietnameseMeaning,
                    StartTime = l.StartTime,
                    EndTime = l.EndTime
                }).ToList()
        };

        return View(model);
    }

    // POST: Admin/ListeningManagement/ManageTranscript/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageTranscript(int id, List<ListeningTranscriptLineViewModel> lines)
    {
        var lesson = await _context.ListeningLessons
            .Include(l => l.TranscriptLines)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lesson == null) return NotFound();

        _context.ListeningTranscriptLines.RemoveRange(lesson.TranscriptLines);
        
        if (lines != null)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line.Text)) continue;

                _context.ListeningTranscriptLines.Add(new ListeningTranscriptLine
                {
                    LessonId = id,
                    OrderIndex = i + 1,
                    Speaker = string.IsNullOrWhiteSpace(line.Speaker) ? "Speaker 1" : line.Speaker.Trim(),
                    Text = line.Text.Trim(),
                    VietnameseMeaning = line.VietnameseMeaning?.Trim(),
                    StartTime = line.StartTime,
                    EndTime = line.EndTime
                });
            }
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã lưu transcript thành công.";
        return RedirectToAction(nameof(Index));
    }

    // POST: Admin/ListeningManagement/ImportTranscript/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportTranscript(int id)
    {
        var lesson = await _context.ListeningLessons.FirstOrDefaultAsync(l => l.Id == id);
        if (lesson == null || string.IsNullOrEmpty(lesson.YoutubeId)) 
            return BadRequest("Bài học không tồn tại hoặc không có Youtube ID");

        try
        {
            var sentences = await _transcriptService.GetTranscriptAsync(lesson.YoutubeId);
            if (sentences.Count == 0)
            {
                TempData["ErrorMessage"] = "Không thể lấy transcript từ video này.";
                return RedirectToAction(nameof(ManageTranscript), new { id });
            }

            // Remove existing
            var existing = _context.ListeningTranscriptLines.Where(l => l.LessonId == id);
            _context.ListeningTranscriptLines.RemoveRange(existing);

            // Detect speakers from lesson config or transcript text
            var defaultSpeaker1 = !string.IsNullOrWhiteSpace(lesson.Speaker1Name)
                ? lesson.Speaker1Name : "Speaker 1";
            var defaultSpeaker2 = !string.IsNullOrWhiteSpace(lesson.Speaker2Name)
                ? lesson.Speaker2Name : "Speaker 2";

            for (int i = 0; i < sentences.Count; i++)
            {
                var s = sentences[i];
                var (speaker, cleanedText) = DetectSpeaker(s.Text, defaultSpeaker1, defaultSpeaker2);

                _context.ListeningTranscriptLines.Add(new ListeningTranscriptLine
                {
                    LessonId = id,
                    OrderIndex = i + 1,
                    Speaker = speaker,
                    Text = cleanedText,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime
                });
            }

            // Update duration if not set
            if (string.IsNullOrEmpty(lesson.Duration))
            {
                lesson.Duration = await _transcriptService.GetVideoDurationAsync(lesson.YoutubeId);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã nhập thành công {sentences.Count} dòng transcript.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi import transcript cho bài học {Id}", id);
            TempData["ErrorMessage"] = "Lỗi khi kết nối với Youtube Service: " + ex.Message;
        }

        return RedirectToAction(nameof(ManageTranscript), new { id });
    }

    /// <summary>
    /// Detect speaker from transcript text patterns:
    ///   "[Speaker Name]:" / "Speaker Name:" / "Name:" at start of line.
    /// Returns (speaker, cleaned text without the speaker prefix).
    /// </summary>
    private static readonly Regex SpeakerPatternRegex = new(
        @"^\s*\[?([A-Za-z][A-Za-z\s]{0,30}?)\]?\s*:\s*",
        RegexOptions.Compiled);

    private static (string Speaker, string Text) DetectSpeaker(
        string text, string defaultSpeaker1, string defaultSpeaker2)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (defaultSpeaker1, text);

        var match = SpeakerPatternRegex.Match(text);
        if (match.Success)
        {
            var detectedName = match.Groups[1].Value.Trim();
            var remainingText = text[match.Length..].Trim();

            // Chỉ accept nếu phần còn lại không rỗng và tên hợp lý (1-4 từ)
            var nameWords = detectedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!string.IsNullOrWhiteSpace(remainingText) && nameWords.Length <= 4)
            {
                return (detectedName, remainingText);
            }
        }

        // Không detect được → trả default
        return (defaultSpeaker1, text);
    }

    // GET: Admin/ListeningManagement/ManageQuiz/5
    public async Task<IActionResult> ManageQuiz(int id)
    {
        var lesson = await _context.ListeningLessons
            .Include(l => l.QuizQuestions)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lesson == null) return NotFound();

        var model = new ListeningQuizManageViewModel
        {
            LessonId = lesson.Id,
            LessonTitle = lesson.Title,
            Questions = lesson.QuizQuestions
                .OrderBy(q => q.OrderIndex)
                .Select(q => new ListeningQuizQuestionViewModel
                {
                    Id = q.Id,
                    OrderIndex = q.OrderIndex,
                    QuestionText = q.QuestionText,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    CorrectAnswer = q.CorrectAnswer,
                    Explanation = q.Explanation
                }).ToList()
        };

        return View(model);
    }

    // POST: Admin/ListeningManagement/ManageQuiz/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageQuiz(int id, List<ListeningQuizQuestionViewModel> questions)
    {
        var lesson = await _context.ListeningLessons
            .Include(l => l.QuizQuestions)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lesson == null) return NotFound();

        _context.ListeningQuizQuestions.RemoveRange(lesson.QuizQuestions);

        if (questions != null)
        {
            for (int i = 0; i < questions.Count; i++)
            {
                var q = questions[i];
                if (string.IsNullOrWhiteSpace(q.QuestionText)) continue;

                _context.ListeningQuizQuestions.Add(new ListeningQuizQuestion
                {
                    LessonId = id,
                    OrderIndex = i + 1,
                    QuestionText = q.QuestionText.Trim(),
                    OptionA = q.OptionA.Trim(),
                    OptionB = q.OptionB.Trim(),
                    OptionC = q.OptionC?.Trim(),
                    OptionD = q.OptionD?.Trim(),
                    CorrectAnswer = q.CorrectAnswer,
                    Explanation = q.Explanation?.Trim()
                });
            }
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã lưu bộ câu hỏi Quiz thành công.";
        return RedirectToAction(nameof(Index));
    }

    // GET: Admin/ListeningManagement/ManageVocab/5
    public async Task<IActionResult> ManageVocab(int id)
    {
        var lesson = await _context.ListeningLessons
            .Include(l => l.VocabItems)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lesson == null) return NotFound();

        var model = new ListeningVocabManageViewModel
        {
            LessonId = lesson.Id,
            LessonTitle = lesson.Title,
            Items = lesson.VocabItems
                .OrderBy(v => v.OrderIndex)
                .Select(v => new ListeningVocabItemViewModel
                {
                    Id = v.Id,
                    OrderIndex = v.OrderIndex,
                    Word = v.Word,
                    Definition = v.Definition,
                    ExampleSentence = v.ExampleSentence,
                    ImageUrl = v.ImageUrl
                }).ToList()
        };

        return View(model);
    }

    // POST: Admin/ListeningManagement/ManageVocab/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageVocab(int id, List<ListeningVocabItemViewModel> items)
    {
        var lesson = await _context.ListeningLessons
            .Include(l => l.VocabItems)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lesson == null) return NotFound();

        _context.ListeningVocabItems.RemoveRange(lesson.VocabItems);

        if (items != null)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var v = items[i];
                if (string.IsNullOrWhiteSpace(v.Word)) continue;

                _context.ListeningVocabItems.Add(new ListeningVocabItem
                {
                    LessonId = id,
                    OrderIndex = i + 1,
                    Word = v.Word.Trim(),
                    Definition = v.Definition.Trim(),
                    ExampleSentence = v.ExampleSentence?.Trim(),
                    ImageUrl = v.ImageUrl?.Trim()
                });
            }
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã lưu danh sách từ vựng thành công.";
        return RedirectToAction(nameof(Index));
    }

    private static string? NormalizeYoutubeId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var value = input.Trim();
        if (YoutubeIdRegex.IsMatch(value)) return value;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var v = query["v"];
            if (!string.IsNullOrEmpty(v) && YoutubeIdRegex.IsMatch(v)) return v;

            if (uri.Host.Contains("youtu.be"))
            {
                var shortId = uri.AbsolutePath.Trim('/');
                if (YoutubeIdRegex.IsMatch(shortId)) return shortId;
            }
        }
        return value; // fallback
    }
}
