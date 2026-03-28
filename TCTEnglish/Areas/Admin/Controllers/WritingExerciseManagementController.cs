using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Areas.Admin.ViewModels;
using TCTVocabulary.Models;

namespace TCTVocabulary.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class WritingExerciseManagementController : Controller
{
    private static readonly Regex ParagraphSplitRegex = new(
        @"(?:\r?\n\s*){2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SentenceBoundaryRegex = new(
        @"(?:(?<=[.!?])|(?<=[.!?][""”’')\]]))\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyList<WritingExerciseOptionViewModel> LevelOptions =
    [
        new() { Value = "beginner", Label = "Beginner" },
        new() { Value = "intermediate", Label = "Intermediate" },
        new() { Value = "advanced", Label = "Advanced" }
    ];

    private static readonly IReadOnlyList<WritingExerciseOptionViewModel> ContentTypeOptions =
    [
        new() { Value = "emails", Label = "Emails" },
        new() { Value = "diaries", Label = "Diaries" },
        new() { Value = "essays", Label = "Essays" },
        new() { Value = "articles", Label = "Articles" },
        new() { Value = "stories", Label = "Stories" },
        new() { Value = "reports", Label = "Reports" }
    ];

    private static readonly IReadOnlyList<WritingExerciseOptionViewModel> VisibilityOptions =
    [
        new() { Value = "all", Label = "Tất cả trạng thái" },
        new() { Value = "published", Label = "Đang hiển thị" },
        new() { Value = "hidden", Label = "Đang ẩn" }
    ];

    private readonly DbflashcardContext _context;
    private readonly ILogger<WritingExerciseManagementController> _logger;

    public WritingExerciseManagementController(
        DbflashcardContext context,
        ILogger<WritingExerciseManagementController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var exercises = await _context.WritingExercises
            .AsNoTracking()
            .OrderByDescending(exercise => exercise.Id)
            .Select(exercise => new
            {
                exercise.Id,
                exercise.Title,
                exercise.Level,
                exercise.ContentType,
                exercise.Topic,
                exercise.PreviewText,
                exercise.IsPublished,
                exercise.CreatedAt,
                SentenceCount = exercise.WritingExerciseSentences.Count,
                ParagraphBreakCount = exercise.WritingExerciseSentences.Count(sentence => sentence.BreakAfter)
            })
            .ToListAsync();

        var viewModel = new WritingExerciseIndexViewModel
        {
            Exercises = exercises.Select(exercise => new WritingExerciseListItemViewModel
            {
                Id = exercise.Id,
                Title = exercise.Title,
                Level = exercise.Level,
                LevelLabel = ResolveOptionLabel(LevelOptions, exercise.Level),
                ContentType = exercise.ContentType,
                ContentTypeLabel = ResolveOptionLabel(ContentTypeOptions, exercise.ContentType),
                Topic = exercise.Topic,
                PreviewText = exercise.PreviewText,
                SentenceCount = exercise.SentenceCount,
                ParagraphCount = exercise.ParagraphBreakCount > 0
                    ? exercise.ParagraphBreakCount
                    : (exercise.SentenceCount > 0 ? 1 : 0),
                IsPublished = exercise.IsPublished,
                CreatedAt = exercise.CreatedAt
            }).ToList(),
            LevelOptions = CloneOptions(LevelOptions),
            ContentTypeOptions = CloneOptions(ContentTypeOptions),
            TopicOptions = BuildTopicOptions(exercises.Select(exercise => exercise.Topic)),
            VisibilityOptions = CloneOptions(VisibilityOptions)
        };

        return View(viewModel);
    }

    public IActionResult Create()
    {
        var model = new WritingExerciseCreateViewModel
        {
            Topic = "General",
            IsPublished = true
        };

        PopulateFormOptions(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] WritingExerciseCreateViewModel model)
    {
        NormalizeFormModel(model);
        PopulateFormOptions(model);
        ValidateFormModel(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var exercise = new WritingExercise
            {
                Title = model.Title,
                Level = model.Level,
                ContentType = model.ContentType,
                Topic = string.IsNullOrWhiteSpace(model.Topic) ? "General" : model.Topic,
                PreviewText = model.PreviewText,
                IsPublished = model.IsPublished,
                CreatedAt = DateTime.UtcNow
            };

            _context.WritingExercises.Add(exercise);
            await _context.SaveChangesAsync();

            var sentences = BuildSentenceEntities(model.Sentences, exercise.Id);
            _context.WritingExerciseSentences.AddRange(sentences);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            TempData["SuccessMessage"] = $"Đã tạo bài viết \"{exercise.Title}\" với {sentences.Count} câu được tự tách.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create writing exercise {Title}", model.Title);
            ModelState.AddModelError(string.Empty, "Không thể tạo bài viết lúc này. Vui lòng thử lại.");
            return View(model);
        }
    }

    public async Task<IActionResult> Edit(int id)
    {
        var exercise = await _context.WritingExercises
            .AsNoTracking()
            .Include(item => item.WritingExerciseSentences)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (exercise == null)
        {
            return NotFound();
        }

        var orderedSentences = exercise.WritingExerciseSentences
            .OrderBy(sentence => sentence.SortOrder)
            .Select(sentence => new WritingExerciseSentenceInputViewModel
            {
                VietnameseText = sentence.VietnameseText,
                EnglishMeaning = sentence.EnglishMeaning,
                BreakAfter = sentence.BreakAfter
            })
            .ToList();

        var model = new WritingExerciseEditViewModel
        {
            Id = exercise.Id,
            Title = exercise.Title,
            Level = exercise.Level,
            ContentType = exercise.ContentType,
            Topic = exercise.Topic,
            PreviewText = exercise.PreviewText,
            FullVietnameseText = BuildFullText(orderedSentences, sentence => sentence.VietnameseText),
            FullEnglishText = BuildFullText(orderedSentences, sentence => sentence.EnglishMeaning),
            IsPublished = exercise.IsPublished,
            CreatedAt = exercise.CreatedAt,
            Sentences = orderedSentences
        };

        PopulateFormOptions(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [FromForm] WritingExerciseEditViewModel model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        NormalizeFormModel(model);
        PopulateFormOptions(model);
        ValidateFormModel(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var exercise = await _context.WritingExercises
                .Include(item => item.WritingExerciseSentences)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (exercise == null)
            {
                return NotFound();
            }

            exercise.Title = model.Title;
            exercise.Level = model.Level;
            exercise.ContentType = model.ContentType;
            exercise.Topic = string.IsNullOrWhiteSpace(model.Topic) ? "General" : model.Topic;
            exercise.PreviewText = model.PreviewText;
            exercise.IsPublished = model.IsPublished;

            _context.WritingExerciseSentences.RemoveRange(exercise.WritingExerciseSentences);
            await _context.SaveChangesAsync();

            var sentences = BuildSentenceEntities(model.Sentences, exercise.Id);
            _context.WritingExerciseSentences.AddRange(sentences);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật bài viết \"{exercise.Title}\".";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update writing exercise {ExerciseId}", id);
            ModelState.AddModelError(string.Empty, "Không thể cập nhật bài viết lúc này. Vui lòng thử lại.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromForm] int id)
    {
        var exercise = await _context.WritingExercises.FirstOrDefaultAsync(item => item.Id == id);
        if (exercise == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy bài viết.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _context.WritingExercises.Remove(exercise);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã xóa bài viết \"{exercise.Title}\" và toàn bộ câu con liên quan.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete writing exercise {ExerciseId}", id);
            TempData["ErrorMessage"] = "Không thể xóa bài viết lúc này. Vui lòng thử lại.";
        }

        return RedirectToAction(nameof(Index));
    }

    private static List<WritingExerciseSentence> BuildSentenceEntities(
        IEnumerable<WritingExerciseSentenceInputViewModel> sentences,
        int exerciseId)
    {
        return sentences
            .Select((sentence, index) => new WritingExerciseSentence
            {
                WritingExerciseId = exerciseId,
                SortOrder = index + 1,
                VietnameseText = sentence.VietnameseText,
                EnglishMeaning = sentence.EnglishMeaning,
                BreakAfter = sentence.BreakAfter
            })
            .ToList();
    }

    private static List<WritingExerciseOptionViewModel> BuildTopicOptions(IEnumerable<string?> topics)
    {
        var options = new List<WritingExerciseOptionViewModel>
        {
            new() { Value = "all", Label = "Tất cả chủ đề" }
        };

        options.AddRange(topics
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Select(topic => topic!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(topic => topic, StringComparer.OrdinalIgnoreCase)
            .Select(topic => new WritingExerciseOptionViewModel
            {
                Value = topic,
                Label = topic
            }));

        return options;
    }

    private static List<WritingExerciseOptionViewModel> CloneOptions(IEnumerable<WritingExerciseOptionViewModel> options)
    {
        return options
            .Select(option => new WritingExerciseOptionViewModel
            {
                Value = option.Value,
                Label = option.Label
            })
            .ToList();
    }

    private void PopulateFormOptions(WritingExerciseFormViewModel model)
    {
        model.LevelOptions = CloneOptions(LevelOptions);
        model.ContentTypeOptions = CloneOptions(ContentTypeOptions);
    }

    private static string ResolveOptionLabel(IEnumerable<WritingExerciseOptionViewModel> options, string? value)
    {
        var matchedOption = options.FirstOrDefault(option =>
            string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase));

        return matchedOption?.Label ?? value ?? string.Empty;
    }

    private void ValidateFormModel(WritingExerciseFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Title), "Tiêu đề bài viết là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(model.Topic))
        {
            ModelState.AddModelError(nameof(model.Topic), "Chủ đề là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(model.PreviewText))
        {
            ModelState.AddModelError(nameof(model.PreviewText), "Preview text là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(model.FullVietnameseText))
        {
            ModelState.AddModelError(nameof(model.FullVietnameseText), "Full text tiếng Việt là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(model.FullEnglishText))
        {
            ModelState.AddModelError(nameof(model.FullEnglishText), "Full text tiếng Anh tham chiếu là bắt buộc.");
        }

        if (!LevelOptions.Any(option => string.Equals(option.Value, model.Level, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(nameof(model.Level), "Trình độ không hợp lệ.");
        }

        if (!ContentTypeOptions.Any(option => string.Equals(option.Value, model.ContentType, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(nameof(model.ContentType), "Loại nội dung không hợp lệ.");
        }

        if (HasFieldErrors(nameof(model.FullVietnameseText)) || HasFieldErrors(nameof(model.FullEnglishText)))
        {
            model.Sentences = new List<WritingExerciseSentenceInputViewModel>();
            return;
        }

        if (!TryBuildSentenceInputs(model.FullVietnameseText, model.FullEnglishText, out var sentences, out var validationMessage))
        {
            ModelState.AddModelError(nameof(model.FullEnglishText), validationMessage);
            model.Sentences = new List<WritingExerciseSentenceInputViewModel>();
            return;
        }

        model.Sentences = sentences;
    }

    private bool HasFieldErrors(string fieldName)
    {
        return ModelState.TryGetValue(fieldName, out var entry)
            && entry.Errors.Count > 0;
    }

    private static void NormalizeFormModel(WritingExerciseFormViewModel model)
    {
        model.Title = model.Title?.Trim() ?? string.Empty;
        model.Level = model.Level?.Trim().ToLowerInvariant() ?? string.Empty;
        model.ContentType = model.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        model.Topic = model.Topic?.Trim() ?? string.Empty;
        model.PreviewText = model.PreviewText?.Trim() ?? string.Empty;
        model.FullVietnameseText = NormalizeTextBlock(model.FullVietnameseText);
        model.FullEnglishText = NormalizeTextBlock(model.FullEnglishText);
    }

    private static bool TryBuildSentenceInputs(
        string fullVietnameseText,
        string fullEnglishText,
        out List<WritingExerciseSentenceInputViewModel> sentences,
        out string validationMessage)
    {
        var vietnameseParagraphs = ParseParagraphs(fullVietnameseText);
        var englishParagraphs = ParseParagraphs(fullEnglishText);

        var flattenedVietnameseSentences = vietnameseParagraphs
            .SelectMany(paragraph => paragraph.Sentences.Select((sentence, index) => new
            {
                Text = sentence,
                BreakAfter = index == paragraph.Sentences.Count - 1
            }))
            .ToList();

        var flattenedEnglishSentences = englishParagraphs
            .SelectMany(paragraph => paragraph.Sentences)
            .ToList();

        if (flattenedVietnameseSentences.Count == 0)
        {
            sentences = new List<WritingExerciseSentenceInputViewModel>();
            validationMessage = "Không đọc được câu nào từ full text tiếng Việt. Hãy kiểm tra lại dấu câu.";
            return false;
        }

        if (flattenedEnglishSentences.Count == 0)
        {
            sentences = new List<WritingExerciseSentenceInputViewModel>();
            validationMessage = "Không đọc được câu nào từ full text tiếng Anh. Hãy kiểm tra lại dấu câu.";
            return false;
        }

        if (flattenedVietnameseSentences.Count != flattenedEnglishSentences.Count)
        {
            sentences = new List<WritingExerciseSentenceInputViewModel>();
            validationMessage =
                $"Tiếng Việt đang được tách thành {flattenedVietnameseSentences.Count} câu, nhưng tiếng Anh được tách thành {flattenedEnglishSentences.Count} câu. Hãy kiểm tra lại dấu chấm câu để hai bên khớp nhau.";
            return false;
        }

        sentences = flattenedVietnameseSentences
            .Select((sentence, index) => new WritingExerciseSentenceInputViewModel
            {
                VietnameseText = sentence.Text,
                EnglishMeaning = flattenedEnglishSentences[index],
                BreakAfter = sentence.BreakAfter
            })
            .ToList();

        validationMessage = string.Empty;
        return true;
    }

    private static List<WritingParagraphInput> ParseParagraphs(string text)
    {
        var normalizedText = NormalizeTextBlock(text);
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return new List<WritingParagraphInput>();
        }

        return ParagraphSplitRegex
            .Split(normalizedText)
            .Select(NormalizeParagraphText)
            .Where(paragraphText => !string.IsNullOrWhiteSpace(paragraphText))
            .Select(paragraphText => new WritingParagraphInput
            {
                Text = paragraphText,
                Sentences = SplitSentences(paragraphText)
            })
            .Where(paragraph => paragraph.Sentences.Count > 0)
            .ToList();
    }

    private static List<string> SplitSentences(string paragraphText)
    {
        return SentenceBoundaryRegex
            .Split(paragraphText)
            .Select(CollapseWhitespace)
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .ToList();
    }

    private static string BuildFullText(
        IEnumerable<WritingExerciseSentenceInputViewModel> sentences,
        Func<WritingExerciseSentenceInputViewModel, string> selector)
    {
        var paragraphs = new List<string>();
        var currentParagraph = new List<string>();

        foreach (var sentence in sentences)
        {
            var text = CollapseWhitespace(selector(sentence));
            if (!string.IsNullOrWhiteSpace(text))
            {
                currentParagraph.Add(text);
            }

            if (sentence.BreakAfter && currentParagraph.Count > 0)
            {
                paragraphs.Add(string.Join(" ", currentParagraph));
                currentParagraph.Clear();
            }
        }

        if (currentParagraph.Count > 0)
        {
            paragraphs.Add(string.Join(" ", currentParagraph));
        }

        return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
    }

    private static string NormalizeTextBlock(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();
    }

    private static string NormalizeParagraphText(string value)
    {
        return CollapseWhitespace(value.Replace('\n', ' '));
    }

    private static string CollapseWhitespace(string? value)
    {
        return string.Join(
            ' ',
            (value ?? string.Empty)
                .Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed class WritingParagraphInput
    {
        public string Text { get; set; } = string.Empty;
        public List<string> Sentences { get; set; } = new();
    }
}
