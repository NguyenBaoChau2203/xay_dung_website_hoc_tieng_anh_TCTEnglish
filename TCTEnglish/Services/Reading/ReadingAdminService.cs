using Microsoft.EntityFrameworkCore;
using TCTEnglish.Areas.Admin.ViewModels;
using TCTEnglish.Models.Reading;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Reading;

public sealed class ReadingAdminService : IReadingAdminService
{
    private readonly DbflashcardContext _db;
    private readonly IFileStorageService _files;

    public ReadingAdminService(DbflashcardContext db, IFileStorageService files)
    {
        _db = db;
        _files = files;
    }

    public async Task<List<ReadingPassageListItemViewModel>> ListAsync(string? search, string? status, CancellationToken ct = default)
    {
        var query = _db.ReadingPassages.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.Title.Contains(search) || (p.Topic != null && p.Topic.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => p.Status == status);
        }

        return await query
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .Select(p => new ReadingPassageListItemViewModel
            {
                Id = p.Id,
                Title = p.Title,
                Topic = p.Topic,
                Level = p.Level,
                QuestionCount = p.Questions.Count,
                CreatedByUserId = p.CreatedByUserId,
                CreatedByName = p.CreatedByUser != null ? p.CreatedByUser.FullName ?? p.CreatedByUser.Email : null,
                UpdatedAt = p.UpdatedAt ?? p.CreatedAt,
                Status = p.Status,
                IsPublished = p.IsPublished
            })
            .ToListAsync(ct);
    }

    public Task<ReadingPassage?> GetForEditAsync(int id, CancellationToken ct = default) =>
        _db.ReadingPassages
            .Include(p => p.Paragraphs)
            .Include(p => p.Questions).ThenInclude(q => q.Options)
            .Include(p => p.VocabularyItems).ThenInclude(v => v.Paragraph)
            .Include(p => p.ReviewLogs)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<ReadingPassage> CreateDraftAsync(ReadingEditorViewModel model, int actorUserId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var imageUrl = await ResolveImageUrlAsync(null, model, ct);
        var passage = new ReadingPassage
        {
            Title = model.Title.Trim(),
            Description = model.Description,
            Topic = model.Topic,
            Level = CefrLevels.Normalize(model.Level),
            EstimatedTimeMinutes = model.EstimatedTimeMinutes,
            Content = model.Content ?? "",
            ImageUrl = imageUrl,
            ExternalReadingKey = model.ExternalReadingKey,
            Status = ReadingStatuses.Draft,
            IsPublished = false,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        ApplyParagraphs(passage, model);
        _db.ReadingPassages.Add(passage);
        await _db.SaveChangesAsync(ct);

        if (model.QuestionsBound || model.Questions.Count > 0)
            await ReplaceQuestionsAsync(passage.Id, model.Questions, ct);
        if (model.VocabularyBound || model.Vocabulary.Count > 0)
            await ReplaceVocabularyAsync(passage.Id, model.Vocabulary, ct);

        return (await GetForEditAsync(passage.Id, ct))!;
    }

    public async Task UpdateDraftAsync(int id, ReadingEditorViewModel model, int actorUserId, CancellationToken ct = default)
    {
        var passage = await GetForEditAsync(id, ct) ?? throw new InvalidOperationException("Reading not found.");
        EnsureEditable(passage);

        passage.Title = model.Title.Trim();
        passage.Description = model.Description;
        passage.Topic = model.Topic;
        passage.Level = CefrLevels.Normalize(model.Level);
        passage.EstimatedTimeMinutes = model.EstimatedTimeMinutes;
        passage.Content = model.Content ?? "";
        passage.ImageUrl = await ResolveImageUrlAsync(passage.ImageUrl, model, ct);
        passage.ExternalReadingKey = model.ExternalReadingKey;
        passage.UpdatedAt = DateTime.UtcNow;
        if (passage.Status == ReadingStatuses.RevisionRequired)
            passage.Status = ReadingStatuses.Draft;

        ApplyParagraphs(passage, model);
        await _db.SaveChangesAsync(ct);

        if (model.QuestionsBound)
            await ReplaceQuestionsAsync(id, model.Questions, ct);
        if (model.VocabularyBound)
            await ReplaceVocabularyAsync(id, model.Vocabulary, ct);
    }

    public async Task ReplaceQuestionsAsync(int passageId, IReadOnlyList<ReadingQuestionEditorViewModel> questions, CancellationToken ct = default)
    {
        var passage = await _db.ReadingPassages
            .Include(p => p.Questions).ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(p => p.Id == passageId, ct)
            ?? throw new InvalidOperationException("Reading not found.");

        EnsureEditable(passage);

        _db.ReadingOptions.RemoveRange(passage.Questions.SelectMany(q => q.Options));
        _db.ReadingQuestions.RemoveRange(passage.Questions);
        await _db.SaveChangesAsync(ct);

        var order = 0;
        foreach (var q in questions.OrderBy(x => x.OrderIndex))
        {
            if (string.IsNullOrWhiteSpace(q.QuestionText)) continue;
            var type = ReadingQuestionTypes.IsValid(q.QuestionType) ? q.QuestionType : ReadingQuestionTypes.MultipleChoice;
            var entity = new ReadingQuestion
            {
                PassageId = passageId,
                ExternalQuestionKey = q.ExternalQuestionKey,
                QuestionText = q.QuestionText.Trim(),
                QuestionType = type,
                OrderIndex = q.OrderIndex > 0 ? q.OrderIndex : ++order,
                Points = q.Points > 0 ? q.Points : 1,
                ParagraphReference = q.ParagraphReference,
                CorrectAnswerText = q.CorrectAnswerText,
                Explanation = q.Explanation,
                EvidenceText = q.EvidenceText,
                EvidenceParagraphId = q.EvidenceParagraphId,
                Paraphrase = q.Paraphrase,
                Options = q.Options.Select((o, i) => new ReadingOption
                {
                    ExternalOptionKey = o.ExternalOptionKey,
                    OptionText = o.OptionText,
                    IsCorrect = o.IsCorrect,
                    MatchKey = o.MatchKey,
                    MatchSide = o.MatchSide,
                    OptionOrder = o.OptionOrder > 0 ? o.OptionOrder : i + 1
                }).ToList()
            };
            _db.ReadingQuestions.Add(entity);
        }

        passage.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReplaceVocabularyAsync(int passageId, IReadOnlyList<ReadingVocabularyEditorViewModel> items, CancellationToken ct = default)
    {
        var passage = await _db.ReadingPassages
            .Include(p => p.VocabularyItems)
            .Include(p => p.Paragraphs)
            .FirstOrDefaultAsync(p => p.Id == passageId, ct)
            ?? throw new InvalidOperationException("Reading not found.");

        EnsureEditable(passage);
        _db.ReadingVocabularyItems.RemoveRange(passage.VocabularyItems);

        var paragraphsByOrder = passage.Paragraphs
            .GroupBy(p => p.ParagraphOrder)
            .ToDictionary(g => g.Key, g => g.First().Id);

        foreach (var item in items.Where(v => !string.IsNullOrWhiteSpace(v.Word)))
        {
            int? paragraphId = item.ParagraphId;
            if (!paragraphId.HasValue && item.ParagraphOrder.HasValue &&
                paragraphsByOrder.TryGetValue(item.ParagraphOrder.Value, out var mappedId))
            {
                paragraphId = mappedId;
            }

            _db.ReadingVocabularyItems.Add(new ReadingVocabularyItem
            {
                ReadingPassageId = passageId,
                ExternalVocabularyKey = item.ExternalVocabularyKey,
                Word = item.Word.Trim(),
                Ipa = item.Ipa,
                PartOfSpeech = item.PartOfSpeech,
                EnglishDefinition = item.EnglishDefinition,
                VietnameseMeaning = item.VietnameseMeaning,
                Example = item.Example,
                ParagraphId = paragraphId
            });
        }

        passage.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SubmitForReviewAsync(int passageId, int actorUserId, CancellationToken ct = default)
    {
        var passage = await GetForEditAsync(passageId, ct) ?? throw new InvalidOperationException("Reading not found.");
        if (passage.CreatedByUserId.HasValue && passage.CreatedByUserId != actorUserId && !await IsAdminActor(actorUserId, ct))
            throw new InvalidOperationException("Only the author can submit for review.");

        var errors = ValidateForReview(passage);
        if (errors.Count > 0)
        {
            await TransitionAsync(passage, ReadingStatuses.ValidationFailed, actorUserId, string.Join("; ", errors), ct);
            throw new InvalidOperationException(string.Join("; ", errors));
        }

        await TransitionAsync(passage, ReadingStatuses.PendingReview, actorUserId, null, ct);
    }

    public async Task RequestRevisionAsync(int passageId, int actorUserId, string reason, CancellationToken ct = default)
    {
        var passage = await GetForEditAsync(passageId, ct) ?? throw new InvalidOperationException("Reading not found.");
        await EnsureReviewerAsync(passage, actorUserId, ct);
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Revision reason is required.");
        await TransitionAsync(passage, ReadingStatuses.RevisionRequired, actorUserId, reason, ct);
    }

    public async Task RejectAsync(int passageId, int actorUserId, string reason, CancellationToken ct = default)
    {
        var passage = await GetForEditAsync(passageId, ct) ?? throw new InvalidOperationException("Reading not found.");
        await EnsureReviewerAsync(passage, actorUserId, ct);
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Rejection reason is required.");
        await TransitionAsync(passage, ReadingStatuses.Rejected, actorUserId, reason, ct);
    }

    public async Task ApproveAsync(int passageId, int actorUserId, CancellationToken ct = default)
    {
        var passage = await GetForEditAsync(passageId, ct) ?? throw new InvalidOperationException("Reading not found.");
        await EnsureReviewerAsync(passage, actorUserId, ct);
        var errors = ValidateForReview(passage);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join("; ", errors));
        await TransitionAsync(passage, ReadingStatuses.Approved, actorUserId, null, ct);
    }

    public async Task PublishAsync(int passageId, int actorUserId, CancellationToken ct = default)
    {
        var passage = await GetForEditAsync(passageId, ct) ?? throw new InvalidOperationException("Reading not found.");
        if (passage.Status != ReadingStatuses.Approved)
            throw new InvalidOperationException("Only approved readings can be published.");

        passage.Status = ReadingStatuses.Published;
        passage.SyncPublishedFlag();
        passage.PublishedAt = DateTime.UtcNow;
        passage.PublishedByUserId = actorUserId;
        passage.UpdatedAt = DateTime.UtcNow;
        _db.ReadingReviewLogs.Add(new ReadingReviewLog
        {
            ReadingPassageId = passage.Id,
            ActorUserId = actorUserId,
            FromStatus = ReadingStatuses.Approved,
            ToStatus = ReadingStatuses.Published,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(int passageId, int actorUserId, CancellationToken ct = default)
    {
        var passage = await GetForEditAsync(passageId, ct) ?? throw new InvalidOperationException("Reading not found.");
        var from = passage.Status;
        passage.Status = ReadingStatuses.Archived;
        passage.SyncPublishedFlag();
        passage.UpdatedAt = DateTime.UtcNow;
        _db.ReadingReviewLogs.Add(new ReadingReviewLog
        {
            ReadingPassageId = passage.Id,
            ActorUserId = actorUserId,
            FromStatus = from,
            ToStatus = ReadingStatuses.Archived,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int passageId, CancellationToken ct = default)
    {
        var passage = await _db.ReadingPassages
            .Include(p => p.Questions).ThenInclude(q => q.Options)
            .Include(p => p.UserReadingHistories)
            .FirstOrDefaultAsync(p => p.Id == passageId, ct);
        if (passage == null) return;
        var imageUrl = passage.ImageUrl;
        _db.ReadingPassages.Remove(passage);
        await _db.SaveChangesAsync(ct);
        _files.TryDeleteLocalFile(imageUrl, ImageUploadPolicies.ReadingPassage.PublicUrlPrefix);
    }

    public Task<string> SavePassageImageAsync(IFormFile file, CancellationToken ct = default) =>
        _files.SaveImageAsync(file, ImageUploadPolicies.ReadingPassage, ct);

    private async Task<string?> ResolveImageUrlAsync(
        string? currentUrl,
        ReadingEditorViewModel model,
        CancellationToken ct)
    {
        if (model.ImageFile != null && model.ImageFile.Length > 0)
        {
            var saved = await SavePassageImageAsync(model.ImageFile, ct);
            if (!string.Equals(currentUrl, saved, StringComparison.OrdinalIgnoreCase))
                _files.TryDeleteLocalFile(currentUrl, ImageUploadPolicies.ReadingPassage.PublicUrlPrefix);
            return saved;
        }

        if (model.RemoveImage)
        {
            _files.TryDeleteLocalFile(currentUrl, ImageUploadPolicies.ReadingPassage.PublicUrlPrefix);
            return null;
        }

        if (string.IsNullOrWhiteSpace(model.ImageUrl))
        {
            if (!string.IsNullOrWhiteSpace(currentUrl))
                _files.TryDeleteLocalFile(currentUrl, ImageUploadPolicies.ReadingPassage.PublicUrlPrefix);
            return null;
        }

        var typed = NormalizeImageUrl(model.ImageUrl)
            ?? throw new InvalidOperationException(
                "URL ảnh phải bắt đầu bằng /images/, /uploads/, http:// hoặc https://.");

        if (!string.Equals(currentUrl, typed, StringComparison.OrdinalIgnoreCase))
            _files.TryDeleteLocalFile(currentUrl, ImageUploadPolicies.ReadingPassage.PublicUrlPrefix);

        return typed;
    }

    public static string? NormalizeImageUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = raw.Trim();
        if (value.StartsWith("/images/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return value.Length <= 500 ? value : value[..500];
        }

        return null;
    }

    public IReadOnlyList<string> ValidateForReview(ReadingPassage passage)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(passage.Title)) errors.Add("Title is required.");
        if (!CefrLevels.IsValid(passage.Level)) errors.Add("Level must be A1–C2.");
        if (string.IsNullOrWhiteSpace(passage.Content) && passage.Paragraphs.Count == 0)
            errors.Add("Passage content is required.");
        if (passage.Questions.Count == 0) errors.Add("At least one question is required.");

        foreach (var q in passage.Questions)
        {
            if (string.IsNullOrWhiteSpace(q.QuestionText))
                errors.Add($"Question {q.OrderIndex}: content required.");
            if (!ReadingQuestionTypes.IsValid(q.QuestionType))
                errors.Add($"Question {q.OrderIndex}: invalid type.");

            switch (q.QuestionType)
            {
                case ReadingQuestionTypes.MultipleChoice:
                case ReadingQuestionTypes.TrueFalse:
                    if (q.Options.Count < 2) errors.Add($"Question {q.OrderIndex}: needs options.");
                    if (!q.Options.Any(o => o.IsCorrect)) errors.Add($"Question {q.OrderIndex}: correct answer required.");
                    break;
                case ReadingQuestionTypes.GapFill:
                case ReadingQuestionTypes.ShortAnswer:
                    if (string.IsNullOrWhiteSpace(q.CorrectAnswerText))
                        errors.Add($"Question {q.OrderIndex}: correct answer text required.");
                    break;
                case ReadingQuestionTypes.Matching:
                    errors.AddRange(ValidateMatchingOptions(q.OrderIndex, q.Options));
                    break;
            }

            if (string.IsNullOrWhiteSpace(q.Explanation))
                errors.Add($"Question {q.OrderIndex}: explanation required for review.");
        }

        return errors;
    }

    private static void ApplyParagraphs(ReadingPassage passage, ReadingEditorViewModel model)
    {
        passage.Paragraphs.Clear();
        var paragraphs = model.Paragraphs
            .Where(p => !string.IsNullOrWhiteSpace(p.ParagraphContent))
            .OrderBy(p => p.ParagraphOrder)
            .ToList();

        if (paragraphs.Count == 0 && !string.IsNullOrWhiteSpace(model.Content))
        {
            var parts = model.Content.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                paragraphs.Add(new ReadingParagraphEditorViewModel
                {
                    ParagraphOrder = i + 1,
                    ParagraphContent = parts[i].Trim()
                });
            }
        }

        foreach (var p in paragraphs)
        {
            passage.Paragraphs.Add(new ReadingParagraph
            {
                ExternalParagraphKey = p.ExternalParagraphKey,
                ParagraphOrder = p.ParagraphOrder,
                ParagraphContent = p.ParagraphContent.Trim()
            });
        }

        if (paragraphs.Count > 0)
            passage.Content = string.Join("\n\n", paragraphs.Select(p => p.ParagraphContent.Trim()));
    }

    private async Task TransitionAsync(ReadingPassage passage, string toStatus, int actorUserId, string? reason, CancellationToken ct)
    {
        var from = passage.Status;
        passage.Status = toStatus;
        passage.SyncPublishedFlag();
        passage.UpdatedAt = DateTime.UtcNow;
        _db.ReadingReviewLogs.Add(new ReadingReviewLog
        {
            ReadingPassageId = passage.Id,
            ActorUserId = actorUserId,
            FromStatus = from,
            ToStatus = toStatus,
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    private static void EnsureEditable(ReadingPassage passage)
    {
        if (passage.Status is ReadingStatuses.PendingReview or ReadingStatuses.Approved or ReadingStatuses.Published or ReadingStatuses.Archived)
            throw new InvalidOperationException($"Cannot edit reading in status {passage.Status}. Move to revision/draft first.");
    }

    public static IEnumerable<string> ValidateMatchingOptions(
        int orderIndex,
        IEnumerable<ReadingOption> options)
    {
        var list = options.ToList();
        if (list.Count < 2)
        {
            yield return $"Question {orderIndex}: matching pairs required.";
            yield break;
        }

        foreach (var opt in list)
        {
            var side = opt.MatchSide?.Trim();
            if (!string.Equals(side, "Left", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(side, "Right", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"Question {orderIndex}: each matching option needs MatchSide Left or Right.";
                yield break;
            }

            if (string.IsNullOrWhiteSpace(opt.MatchKey))
            {
                yield return $"Question {orderIndex}: each matching option needs MatchKey.";
                yield break;
            }
        }

        var groups = list.GroupBy(o => o.MatchKey!.Trim(), StringComparer.OrdinalIgnoreCase).ToList();
        if (groups.Count == 0)
        {
            yield return $"Question {orderIndex}: matching pairs required.";
            yield break;
        }

        foreach (var group in groups)
        {
            var left = group.Count(o => string.Equals(o.MatchSide?.Trim(), "Left", StringComparison.OrdinalIgnoreCase));
            var right = group.Count(o => string.Equals(o.MatchSide?.Trim(), "Right", StringComparison.OrdinalIgnoreCase));
            if (left != 1 || right != 1)
            {
                yield return $"Question {orderIndex}: MatchKey '{group.Key}' needs exactly one Left and one Right.";
            }
        }
    }

    /// <summary>Editor ViewModel options (Excel / form) before they become entities.</summary>
    public static IEnumerable<string> ValidateMatchingEditorOptions(
        int orderIndex,
        IEnumerable<ReadingOptionEditorViewModel> options)
    {
        var entities = options.Select(o => new ReadingOption
        {
            OptionText = o.OptionText,
            MatchKey = o.MatchKey,
            MatchSide = o.MatchSide,
            IsCorrect = o.IsCorrect,
            OptionOrder = o.OptionOrder
        });
        return ValidateMatchingOptions(orderIndex, entities);
    }

    /// <summary>
    /// Reviewer must act on PendingReview. Non-admin authors cannot self-review;
    /// Admin may self-review so a single local Admin can approve imports they created.
    /// </summary>
    private async Task EnsureReviewerAsync(ReadingPassage passage, int actorUserId, CancellationToken ct)
    {
        if (passage.Status != ReadingStatuses.PendingReview)
            throw new InvalidOperationException("Reading is not pending review.");

        if (!passage.CreatedByUserId.HasValue || passage.CreatedByUserId.Value != actorUserId)
            return;

        if (await IsAdminActor(actorUserId, ct))
            return;

        throw new InvalidOperationException("Authors cannot review their own readings.");
    }

    private async Task<bool> IsAdminActor(int userId, CancellationToken ct)
    {
        var role = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);
        return Roles.Normalize(role) == Roles.Admin;
    }
}
