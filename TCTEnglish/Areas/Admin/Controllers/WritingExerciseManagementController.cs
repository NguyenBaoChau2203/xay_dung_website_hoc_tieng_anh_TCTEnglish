using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services.Writing;
using TCTVocabulary.Areas.Admin.ViewModels;
using TCTVocabulary.Controllers;
using TCTVocabulary.Models;

namespace TCTVocabulary.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class WritingExerciseManagementController : BaseController
{
    private static readonly Regex ParagraphSplitRegex = new(
        @"(?:\r?\n\s*){2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AdminSentenceBoundaryRegex = new(
        @"(?:(?<=[.!?;:\u2026])|(?<=[.!?;:\u2026]['""\)\]\u2019\u201D]))\s+",
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

    public async Task<IActionResult> Index(string? search, string? level, string? contentType, string? topic, string? visibility, int page = 1)
    {
        var all = _context.WritingExercises.AsNoTracking();
        var topicNames = await all.Select(e => e.Topic).Distinct().OrderBy(t => t).ToListAsync();
        var totalExercises = await all.CountAsync();
        var published = await all.CountAsync(e => e.IsPublished);
        var sentenceTotal = await _context.WritingExerciseSentences.CountAsync();
        var query = all;
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(e => e.Title.Contains(search) || e.Topic.Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(level)) query = query.Where(e => e.Level == level);
        if (!string.IsNullOrWhiteSpace(contentType)) query = query.Where(e => e.ContentType == contentType);
        if (!string.IsNullOrWhiteSpace(topic)) query = query.Where(e => e.Topic == topic);
        if (visibility == "published") query = query.Where(e => e.IsPublished);
        if (visibility == "hidden") query = query.Where(e => !e.IsPublished);
        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / 20d));
        page = Math.Clamp(page, 1, pages);
        var exercises = await query
            .OrderByDescending(exercise => exercise.Id)
            .Skip((page - 1) * 20).Take(20)
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
            Search = search ?? "", Level = level ?? "", ContentType = contentType ?? "", Topic = topic ?? "", Visibility = visibility ?? "",
            CurrentPage = page, TotalPages = pages, TotalCount = total, TotalExercises = totalExercises,
            PublishedCount = published, ActiveTopics = topicNames.Count(t => !string.IsNullOrWhiteSpace(t)), TotalSentences = sentenceTotal,
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
            TopicOptions = BuildTopicOptions(topicNames),
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
        var adminId = GetCurrentUserId();
        NormalizeFormModel(model);
        PopulateFormOptions(model);
        ValidateFormModel(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            WritingExercise? exercise = null;
            List<WritingExerciseSentence> sentences = new();

            await ExecuteWritingTransactionAsync(async () =>
            {
                exercise = new WritingExercise
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

                sentences = BuildSentenceEntities(model.Sentences, exercise.Id);
                _context.WritingExerciseSentences.AddRange(sentences);
                await _context.SaveChangesAsync();
            });

            var createdExercise = exercise ?? throw new InvalidOperationException("Writing exercise create transaction completed without an exercise.");
            TempData["SuccessMessage"] = $"Đã tạo bài viết \"{createdExercise.Title}\" với {sentences.Count} câu được tự tách.";
            _logger.LogInformation(
                "Admin {AdminId} created WritingExercise {ExerciseId} ({Title}) with {SentenceCount} sentences at {TimeUtc}",
                adminId,
                createdExercise.Id,
                createdExercise.Title,
                sentences.Count,
                DateTime.UtcNow);

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Admin {AdminId} failed {Area}/{Controller}/{Action} while creating writing exercise {Title}",
                adminId,
                "Admin",
                nameof(WritingExerciseManagementController),
                nameof(Create),
                model.Title);
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
                Id = sentence.Id,
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
            Sentences = orderedSentences,
            SentenceVariantPanels = await BuildSentenceVariantPanelsAsync(exercise.Id)
        };

        PopulateFormOptions(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [FromForm] WritingExerciseEditViewModel model)
    {
        var adminId = GetCurrentUserId();
        if (id != model.Id)
        {
            return NotFound();
        }

        NormalizeFormModel(model);
        PopulateFormOptions(model);
        ValidateFormModel(model);

        if (!ModelState.IsValid)
        {
            model.SentenceVariantPanels = await BuildSentenceVariantPanelsAsync(id);
            return View(model);
        }

        try
        {
            WritingExercise? exercise = null;
            var sentenceCount = model.Sentences.Count;

            await ExecuteWritingTransactionAsync(async () =>
            {
                await LockWritingExerciseRowAsync(id);
                _context.ChangeTracker.Clear();

                exercise = await _context.WritingExercises
                    .Include(item => item.WritingExerciseSentences)
                    .ThenInclude(sentence => sentence.WritingAcceptedAnswers)
                    .FirstOrDefaultAsync(item => item.Id == id);

                if (exercise == null)
                {
                    return;
                }

                exercise.Title = model.Title;
                exercise.Level = model.Level;
                exercise.ContentType = model.ContentType;
                exercise.Topic = string.IsNullOrWhiteSpace(model.Topic) ? "General" : model.Topic;
                exercise.PreviewText = model.PreviewText;
                exercise.IsPublished = model.IsPublished;

                if (!TrySyncWritingExerciseSentences(exercise, model.Sentences, out var syncError))
                {
                    throw new InvalidOperationException("variant_guard:" + syncError);
                }

                await _context.SaveChangesAsync();
            });

            if (exercise == null)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] = $"Đã cập nhật bài viết \"{exercise.Title}\".";
            _logger.LogInformation(
                "Admin {AdminId} updated WritingExercise {ExerciseId} ({Title}) with {SentenceCount} sentences and Published={IsPublished} at {TimeUtc}",
                adminId,
                exercise.Id,
                exercise.Title,
                sentenceCount,
                exercise.IsPublished,
                DateTime.UtcNow);
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("variant_guard:", StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.FullEnglishText), ex.Message["variant_guard:".Length..]);
            model.SentenceVariantPanels = await BuildSentenceVariantPanelsAsync(id);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Admin {AdminId} failed {Area}/{Controller}/{Action} for writing exercise {ExerciseId} ({Title})",
                adminId,
                "Admin",
                nameof(WritingExerciseManagementController),
                nameof(Edit),
                id,
                model.Title);
            ModelState.AddModelError(string.Empty, "Không thể cập nhật bài viết lúc này. Vui lòng thử lại.");
            model.SentenceVariantPanels = await BuildSentenceVariantPanelsAsync(id);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromForm] int id)
    {
        var adminId = GetCurrentUserId();
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
            _logger.LogInformation(
                "Admin {AdminId} deleted WritingExercise {ExerciseId} ({Title}) at {TimeUtc}",
                adminId,
                exercise.Id,
                exercise.Title,
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin {AdminId} failed to delete writing exercise {ExerciseId}", adminId, id);
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

    private async Task ExecuteWritingTransactionAsync(Func<Task> action)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            await action();
            await transaction.CommitAsync();
        });
    }

    /// <summary>
    /// Acquire a MariaDB row lock on the parent exercise before reading/writing sentence variants.
    /// SQLite test provider has no FOR UPDATE; CAS token still guards state there.
    /// </summary>
    private async Task LockWritingExerciseRowAsync(int exerciseId)
    {
        if (_context.Database.IsMySql())
        {
            var locked = await _context.Database
                .SqlQuery<int>($"SELECT `Id` AS `Value` FROM `WritingExercises` WHERE `Id` = {exerciseId} FOR UPDATE")
                .ToListAsync();
            if (locked.Count == 0)
            {
                throw new InvalidOperationException("missing_exercise");
            }

            return;
        }

        var exists = await _context.WritingExercises.AsNoTracking()
            .AnyAsync(item => item.Id == exerciseId);
        if (!exists)
        {
            throw new InvalidOperationException("missing_exercise");
        }
    }

    private bool TrySyncWritingExerciseSentences(
        WritingExercise exercise,
        IReadOnlyList<WritingExerciseSentenceInputViewModel> sentences,
        out string? errorMessage)
    {
        errorMessage = null;
        var existingSentences = exercise.WritingExerciseSentences
            .OrderBy(sentence => sentence.SortOrder)
            .ThenBy(sentence => sentence.Id)
            .ToList();

        var sharedCount = Math.Min(existingSentences.Count, sentences.Count);
        for (var index = 0; index < sharedCount; index++)
        {
            var existingSentence = existingSentences[index];
            var updatedSentence = sentences[index];
            var hasVariants = existingSentence.WritingAcceptedAnswers?.Count > 0;
            var contentChanged = !string.Equals(
                    existingSentence.VietnameseText,
                    updatedSentence.VietnameseText,
                    StringComparison.Ordinal)
                || !string.Equals(
                    existingSentence.EnglishMeaning,
                    updatedSentence.EnglishMeaning,
                    StringComparison.Ordinal);

            if (hasVariants && contentChanged)
            {
                errorMessage =
                    $"Câu {existingSentence.SortOrder} đang có đáp án biến thể đã duyệt. Hãy quản lý/xóa biến thể trước khi đổi nội dung câu trong full text.";
                return false;
            }
        }

        if (existingSentences.Count > sentences.Count)
        {
            var trailingSentences = existingSentences
                .Skip(sentences.Count)
                .ToList();

            if (trailingSentences.Any(sentence => sentence.WritingAcceptedAnswers?.Count > 0))
            {
                errorMessage =
                    "Không thể xóa/ghép câu đang có đáp án biến thể đã duyệt. Hãy xóa biến thể có chủ ý trước khi rút ngắn passage.";
                return false;
            }

            foreach (var trailingSentence in trailingSentences)
            {
                exercise.WritingExerciseSentences.Remove(trailingSentence);
            }

            _context.WritingExerciseSentences.RemoveRange(trailingSentences);
        }

        for (var index = 0; index < sharedCount; index++)
        {
            var existingSentence = existingSentences[index];
            var updatedSentence = sentences[index];

            existingSentence.SortOrder = index + 1;
            existingSentence.VietnameseText = updatedSentence.VietnameseText;
            existingSentence.EnglishMeaning = updatedSentence.EnglishMeaning;
            existingSentence.BreakAfter = updatedSentence.BreakAfter;
        }

        for (var index = existingSentences.Count; index < sentences.Count; index++)
        {
            var newSentence = sentences[index];
            exercise.WritingExerciseSentences.Add(new WritingExerciseSentence
            {
                WritingExerciseId = exercise.Id,
                SortOrder = index + 1,
                VietnameseText = newSentence.VietnameseText,
                EnglishMeaning = newSentence.EnglishMeaning,
                BreakAfter = newSentence.BreakAfter
            });
        }

        return true;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAcceptedAnswers([FromForm] WritingAcceptedAnswersSaveViewModel model)
    {
        var adminId = GetCurrentUserId();
        if (model.ExerciseId <= 0 || model.SentenceId <= 0)
        {
            return NotFound();
        }

        var cleanedAnswers = (model.Answers ?? new List<string>())
            .Select(WritingAssessmentPolicy.CollapseWhitespace)
            .Where(answer => !string.IsNullOrWhiteSpace(answer))
            .ToList();

        if (cleanedAnswers.Count > WritingAssessmentPolicy.MaxAcceptedVariantsPerSentence)
        {
            return await RedisplayEditWithVariantErrorAsync(
                model.ExerciseId,
                model.SentenceId,
                cleanedAnswers,
                $"Mỗi câu chỉ được tối đa {WritingAssessmentPolicy.MaxAcceptedVariantsPerSentence} đáp án biến thể.");
        }

        if (cleanedAnswers.Any(answer => answer.Length > WritingAssessmentPolicy.MaxAcceptedAnswerLength))
        {
            return await RedisplayEditWithVariantErrorAsync(
                model.ExerciseId,
                model.SentenceId,
                cleanedAnswers,
                $"Mỗi đáp án biến thể tối đa {WritingAssessmentPolicy.MaxAcceptedAnswerLength} ký tự.");
        }

        try
        {
            var savedSortOrder = 0;
            var savedCount = 0;

            await ExecuteWritingTransactionAsync(async () =>
            {
                await LockWritingExerciseRowAsync(model.ExerciseId);
                _context.ChangeTracker.Clear();

                var tracked = await _context.WritingExerciseSentences
                    .Include(item => item.WritingAcceptedAnswers)
                    .FirstOrDefaultAsync(item =>
                        item.Id == model.SentenceId
                        && item.WritingExerciseId == model.ExerciseId);

                if (tracked == null)
                {
                    throw new InvalidOperationException("missing_sentence");
                }

                var currentToken = WritingAssessmentPolicy.BuildAcceptedAnswersStateToken(
                    model.ExerciseId,
                    tracked.Id,
                    tracked.VietnameseText,
                    tracked.EnglishMeaning,
                    tracked.WritingAcceptedAnswers);
                if (!string.Equals(currentToken, model.ExpectedStateToken ?? string.Empty, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("stale_variants");
                }

                var seenHashes = new HashSet<string>(StringComparer.Ordinal);
                var canonicalHash = WritingAssessmentPolicy.ComputeNormalizedHash(tracked.EnglishMeaning);
                var prepared = new List<(string Text, string Hash)>();

                foreach (var answer in cleanedAnswers)
                {
                    var hash = WritingAssessmentPolicy.ComputeNormalizedHash(answer);
                    if (string.Equals(hash, canonicalHash, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "variant_validation:Đáp án biến thể không được trùng đáp án chuẩn (EnglishMeaning) sau chuẩn hóa.");
                    }

                    if (!seenHashes.Add(hash))
                    {
                        throw new InvalidOperationException(
                            "variant_validation:Các đáp án biến thể không được trùng nhau sau chuẩn hóa.");
                    }

                    prepared.Add((answer, hash));
                }

                if (tracked.WritingAcceptedAnswers.Count > 0)
                {
                    _context.WritingAcceptedAnswers.RemoveRange(tracked.WritingAcceptedAnswers);
                }

                var now = DateTime.UtcNow;
                foreach (var item in prepared)
                {
                    tracked.WritingAcceptedAnswers.Add(new WritingAcceptedAnswer
                    {
                        WritingExerciseSentenceId = tracked.Id,
                        AnswerText = item.Text,
                        NormalizedHash = item.Hash,
                        ApprovedByUserId = adminId,
                        ApprovedAtUtc = now
                    });
                }

                await _context.SaveChangesAsync();
                savedSortOrder = tracked.SortOrder;
                savedCount = prepared.Count;
            });

            TempData["SuccessMessage"] = $"Đã cập nhật đáp án biến thể cho câu {savedSortOrder}.";
            _logger.LogInformation(
                "Admin {AdminId} saved {VariantCount} accepted answers for WritingExercise {ExerciseId} sentence {SentenceId}",
                adminId,
                savedCount,
                model.ExerciseId,
                model.SentenceId);
            return RedirectToAction(nameof(Edit), new { id = model.ExerciseId });
        }
        catch (InvalidOperationException ex) when (ex.Message == "stale_variants")
        {
            return await RedisplayEditWithVariantErrorAsync(
                model.ExerciseId,
                model.SentenceId,
                cleanedAnswers,
                "Danh sách đáp án biến thể hoặc nội dung câu đã thay đổi từ lúc bạn mở form. Hãy tải lại trang rồi lưu lại.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "missing_sentence" || ex.Message == "missing_exercise")
        {
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("variant_validation:", StringComparison.Ordinal))
        {
            return await RedisplayEditWithVariantErrorAsync(
                model.ExerciseId,
                model.SentenceId,
                cleanedAnswers,
                ex.Message["variant_validation:".Length..]);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Admin {AdminId} failed saving accepted answers for exercise {ExerciseId} sentence {SentenceId}",
                adminId,
                model.ExerciseId,
                model.SentenceId);
            return await RedisplayEditWithVariantErrorAsync(
                model.ExerciseId,
                model.SentenceId,
                cleanedAnswers,
                "Không thể lưu đáp án biến thể lúc này. Vui lòng thử lại.");
        }
    }

    private async Task<IActionResult> RedisplayEditWithVariantErrorAsync(
        int exerciseId,
        int sentenceId,
        IReadOnlyList<string> preservedAnswers,
        string errorMessage)
    {
        var exercise = await _context.WritingExercises
            .AsNoTracking()
            .Include(item => item.WritingExerciseSentences)
            .FirstOrDefaultAsync(item => item.Id == exerciseId);

        if (exercise == null)
        {
            return NotFound();
        }

        var orderedSentences = exercise.WritingExerciseSentences
            .OrderBy(sentence => sentence.SortOrder)
            .Select(sentence => new WritingExerciseSentenceInputViewModel
            {
                Id = sentence.Id,
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
            Sentences = orderedSentences,
            SentenceVariantPanels = await BuildSentenceVariantPanelsAsync(exercise.Id)
        };

        var panel = model.SentenceVariantPanels.FirstOrDefault(item => item.SentenceId == sentenceId);
        if (panel != null)
        {
            panel.Variants = preservedAnswers
                .Select(answer => new WritingAcceptedAnswerItemViewModel
                {
                    Id = 0,
                    AnswerText = answer,
                    ApprovedAtUtc = DateTime.UtcNow
                })
                .ToList();
            panel.VariantCount = panel.Variants.Count;
        }

        PopulateFormOptions(model);
        ModelState.AddModelError(string.Empty, errorMessage);
        ViewData["VariantSaveError"] = errorMessage;
        TempData["ErrorMessage"] = errorMessage;
        return View(nameof(Edit), model);
    }

    private async Task<List<WritingExerciseSentenceVariantPanelViewModel>> BuildSentenceVariantPanelsAsync(int exerciseId)
    {
        var sentences = await _context.WritingExerciseSentences
            .AsNoTracking()
            .Where(sentence => sentence.WritingExerciseId == exerciseId)
            .OrderBy(sentence => sentence.SortOrder)
            .Select(sentence => new
            {
                sentence.Id,
                sentence.SortOrder,
                sentence.VietnameseText,
                sentence.EnglishMeaning,
                Variants = sentence.WritingAcceptedAnswers
                    .OrderBy(answer => answer.Id)
                    .Select(answer => new WritingAcceptedAnswerItemViewModel
                    {
                        Id = answer.Id,
                        AnswerText = answer.AnswerText,
                        ApprovedAtUtc = answer.ApprovedAtUtc
                    })
                    .ToList()
            })
            .ToListAsync();

        return sentences.Select(sentence => new WritingExerciseSentenceVariantPanelViewModel
        {
            SentenceId = sentence.Id,
            SortOrder = sentence.SortOrder,
            VietnameseText = sentence.VietnameseText,
            CanonicalEnglishMeaning = sentence.EnglishMeaning,
            VariantCount = sentence.Variants.Count,
            Variants = sentence.Variants,
            ExpectedStateToken = WritingAssessmentPolicy.BuildAcceptedAnswersStateToken(
                exerciseId,
                sentence.Id,
                sentence.VietnameseText,
                sentence.EnglishMeaning,
                sentence.Variants.Select(variant => (
                    variant.Id,
                    WritingAssessmentPolicy.ComputeNormalizedHash(variant.AnswerText),
                    variant.ApprovedAtUtc.Ticks)))
        }).ToList();
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
        return AdminSentenceBoundaryRegex
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
