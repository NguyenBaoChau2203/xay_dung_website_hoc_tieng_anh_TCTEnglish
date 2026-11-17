using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models.Reading;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Reading;

public sealed class ReadingLearningService : IReadingLearningService
{
    private readonly DbflashcardContext _db;
    private readonly IGoalsService _goalsService;

    public ReadingLearningService(DbflashcardContext db, IGoalsService goalsService)
    {
        _db = db;
        _goalsService = goalsService;
    }

    public async Task<string> GetUserReadingLevelAsync(int userId, CancellationToken ct = default)
    {
        var level = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.ReadingLevel)
            .FirstOrDefaultAsync(ct);
        return CefrLevels.Normalize(level);
    }

    public async Task<List<ReadingListViewModel>> GetLibraryAsync(int userId, CancellationToken ct = default)
    {
        var bypassLevelGate = await IsAdminUserAsync(userId, ct);
        string[]? allowed = null;
        if (!bypassLevelGate)
        {
            var studentLevel = await GetUserReadingLevelAsync(userId, ct);
            var maxOrdinal = CefrLevels.Ordinal(studentLevel);
            allowed = CefrLevels.All.Take(maxOrdinal + 1).ToArray();
        }

        var query = _db.ReadingPassages.AsNoTracking()
            .Where(r => r.IsPublished && r.Status == ReadingStatuses.Published);
        if (allowed != null)
            query = query.Where(r => allowed.Contains(r.Level));

        return await query
            .Select(r => new ReadingListViewModel
            {
                Id = r.Id,
                Title = r.Title,
                ImageUrl = r.ImageUrl,
                Level = r.Level,
                Topic = r.Topic,
                Description = r.Description,
                EstimatedTimeMinutes = r.EstimatedTimeMinutes,
                QuestionCount = r.Questions.Count,
                IsCompleted = r.UserReadingHistories.Any(u => u.UserId == userId && u.IsCompleted),
                IsInProgress = r.UserReadingHistories.Any(u => u.UserId == userId && !u.IsCompleted)
            })
            .ToListAsync(ct);
    }

    public async Task<ReadingStudyViewModel?> GetStudyAsync(int userId, int passageId, CancellationToken ct = default)
    {
        var bypassLevelGate = await IsAdminUserAsync(userId, ct);
        var passage = await _db.ReadingPassages.AsNoTracking()
            .Include(p => p.Paragraphs)
            .Include(p => p.Questions).ThenInclude(q => q.Options)
            .Include(p => p.VocabularyItems).ThenInclude(v => v.Paragraph)
            .FirstOrDefaultAsync(p => p.Id == passageId && p.IsPublished && p.Status == ReadingStatuses.Published, ct);

        if (passage == null)
            return null;

        if (!bypassLevelGate)
        {
            var studentLevel = await GetUserReadingLevelAsync(userId, ct);
            if (!CefrLevels.CanAccess(studentLevel, passage.Level))
                return null;
        }

        var history = await _db.UserReadingHistories
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ReadingPassageId == passageId, ct);
        if (history == null)
        {
            _db.UserReadingHistories.Add(new UserReadingHistory
            {
                UserId = userId,
                ReadingPassageId = passageId,
                ViewedAt = DateTime.UtcNow,
                IsCompleted = false
            });
            await _db.SaveChangesAsync(ct);
        }

        var openAttempt = await _db.ReadingAttempts.AsNoTracking()
            .Include(a => a.Answers)
            .Where(a => a.UserId == userId && a.ReadingPassageId == passageId && !a.IsSubmitted)
            .OrderByDescending(a => a.StartedAt)
            .FirstOrDefaultAsync(ct);

        return new ReadingStudyViewModel
        {
            Id = passage.Id,
            Title = passage.Title,
            Content = passage.Content,
            ImageUrl = passage.ImageUrl,
            Level = passage.Level,
            Topic = passage.Topic,
            EstimatedTimeMinutes = passage.EstimatedTimeMinutes,
            AttemptId = openAttempt?.Id,
            Mode = openAttempt?.Mode ?? "Practice",
            TimeLimitSeconds = openAttempt?.TimeLimitSeconds,
            Paragraphs = passage.Paragraphs.OrderBy(p => p.ParagraphOrder)
                .Select(p => new ReadingParagraphViewModel
                {
                    Id = p.Id,
                    Order = p.ParagraphOrder,
                    Content = p.ParagraphContent
                }).ToList(),
            Questions = passage.Questions.OrderBy(q => q.OrderIndex).Select(q => new QuestionViewModel
            {
                Id = q.Id,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType,
                Points = q.Points,
                Options = q.Options.OrderBy(o => o.OptionOrder).Select(o => new OptionViewModel
                {
                    Id = o.Id,
                    OptionText = o.OptionText,
                    MatchKey = o.MatchKey,
                    MatchSide = o.MatchSide
                }).ToList(),
                SavedAnswerText = openAttempt?.Answers.FirstOrDefault(a => a.QuestionId == q.Id)?.AnswerText,
                SavedSelectedOptionId = openAttempt?.Answers.FirstOrDefault(a => a.QuestionId == q.Id)?.SelectedOptionId,
                SavedMatchingJson = openAttempt?.Answers.FirstOrDefault(a => a.QuestionId == q.Id)?.MatchingJson,
                IsFlagged = openAttempt?.Answers.FirstOrDefault(a => a.QuestionId == q.Id)?.IsFlagged ?? false
            }).ToList(),
            Vocabulary = passage.VocabularyItems.Select(v => new ReadingVocabViewModel
            {
                Id = v.Id,
                Word = v.Word,
                Ipa = v.Ipa,
                PartOfSpeech = v.PartOfSpeech,
                EnglishDefinition = v.EnglishDefinition,
                VietnameseMeaning = v.VietnameseMeaning,
                Example = v.Example,
                ParagraphNo = v.Paragraph?.ParagraphOrder
            }).ToList()
        };
    }

    public async Task<ReadingAttempt> StartAttemptAsync(int userId, int passageId, string mode, int? timeLimitSeconds, CancellationToken ct = default)
    {
        var study = await GetStudyAsync(userId, passageId, ct)
            ?? throw new InvalidOperationException("Reading not accessible.");

        var existing = await _db.ReadingAttempts
            .FirstOrDefaultAsync(a => a.UserId == userId && a.ReadingPassageId == passageId && !a.IsSubmitted, ct);
        if (existing != null) return existing;

        var attempt = new ReadingAttempt
        {
            UserId = userId,
            ReadingPassageId = passageId,
            Mode = string.Equals(mode, "Timed", StringComparison.OrdinalIgnoreCase) ? "Timed" : "Practice",
            StartedAt = DateTime.UtcNow,
            TimeLimitSeconds = string.Equals(mode, "Timed", StringComparison.OrdinalIgnoreCase)
                ? timeLimitSeconds ?? (study.EstimatedTimeMinutes ?? 20) * 60
                : null
        };
        _db.ReadingAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);
        return attempt;
    }

    public async Task SaveAnswerAsync(int userId, int attemptId, SaveReadingAnswerRequest request, CancellationToken ct = default)
    {
        var attempt = await _db.ReadingAttempts
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct)
            ?? throw new InvalidOperationException("Attempt not found.");
        if (attempt.IsSubmitted)
            throw new InvalidOperationException("Attempt already submitted.");

        var answer = attempt.Answers.FirstOrDefault(a => a.QuestionId == request.QuestionId);
        if (answer == null)
        {
            answer = new ReadingStudentAnswer
            {
                AttemptId = attemptId,
                QuestionId = request.QuestionId
            };
            _db.ReadingStudentAnswers.Add(answer);
        }

        answer.SelectedOptionId = request.SelectedOptionId;
        answer.AnswerText = request.AnswerText;
        answer.MatchingJson = request.MatchingJson;
        answer.IsFlagged = request.IsFlagged;
        answer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ReadingSubmitResultViewModel> SubmitAttemptAsync(int userId, int attemptId, CancellationToken ct = default)
    {
        var attempt = await _db.ReadingAttempts
            .Include(a => a.Answers)
            .Include(a => a.ReadingPassage).ThenInclude(p => p.Questions).ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct)
            ?? throw new InvalidOperationException("Attempt not found.");

        if (attempt.IsSubmitted)
            throw new InvalidOperationException("Attempt already submitted.");

        if (!attempt.ReadingPassage.IsPublished || attempt.ReadingPassage.Status != ReadingStatuses.Published)
            throw new InvalidOperationException("Reading is not published.");

        if (!await IsAdminUserAsync(userId, ct))
        {
            var studentLevel = await GetUserReadingLevelAsync(userId, ct);
            if (!CefrLevels.CanAccess(studentLevel, attempt.ReadingPassage.Level))
                throw new InvalidOperationException("Reading level not allowed.");
        }

        var details = new List<ReadingQuestionResultViewModel>();
        var score = 0;
        var maxScore = 0;
        var correctCount = 0;

        foreach (var question in attempt.ReadingPassage.Questions.OrderBy(q => q.OrderIndex))
        {
            maxScore += question.Points > 0 ? question.Points : 1;
            var answer = attempt.Answers.FirstOrDefault(a => a.QuestionId == question.Id);
            var isCorrect = ScoreQuestion(question, answer);
            if (answer != null) answer.IsCorrect = isCorrect;
            if (isCorrect)
            {
                correctCount++;
                score += question.Points > 0 ? question.Points : 1;
            }

            details.Add(new ReadingQuestionResultViewModel
            {
                QuestionId = question.Id,
                IsCorrect = isCorrect,
                CorrectOptionText = question.Options.FirstOrDefault(o => o.IsCorrect)?.OptionText,
                CorrectAnswerText = question.CorrectAnswerText,
                Explanation = question.Explanation,
                EvidenceText = question.EvidenceText,
                Paraphrase = question.Paraphrase,
                EvidenceParagraphId = question.EvidenceParagraphId
            });
        }

        attempt.IsSubmitted = true;
        attempt.SubmittedAt = DateTime.UtcNow;
        attempt.Score = score;
        attempt.MaxScore = maxScore;
        attempt.ElapsedSeconds = (int)(DateTime.UtcNow - attempt.StartedAt).TotalSeconds;

        var history = await _db.UserReadingHistories
            .FirstOrDefaultAsync(h => h.UserId == userId && h.ReadingPassageId == attempt.ReadingPassageId, ct);
        var isNewCompletion = history == null || !history.IsCompleted;
        if (history == null)
        {
            history = new UserReadingHistory
            {
                UserId = userId,
                ReadingPassageId = attempt.ReadingPassageId
            };
            _db.UserReadingHistories.Add(history);
        }

        history.IsCompleted = true;
        history.Score = correctCount;
        history.ViewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (isNewCompletion)
        {
            var activityUpdate = _goalsService.BuildReadingCompletionActivityUpdate();
            await _goalsService.RecordLearningActivityAsync(userId, activityUpdate);
        }

        return new ReadingSubmitResultViewModel
        {
            AttemptId = attempt.Id,
            Score = score,
            MaxScore = maxScore,
            CorrectCount = correctCount,
            TotalCount = attempt.ReadingPassage.Questions.Count,
            Details = details
        };
    }

    public async Task SaveAnnotationAsync(int userId, ReadingAnnotationRequest request, CancellationToken ct = default)
    {
        if (await GetStudyAsync(userId, request.PassageId, ct) == null)
            throw new InvalidOperationException("Reading not accessible.");

        _db.ReadingAnnotations.Add(new ReadingAnnotation
        {
            UserId = userId,
            ReadingPassageId = request.PassageId,
            AnnotationType = string.IsNullOrWhiteSpace(request.AnnotationType) ? "Highlight" : request.AnnotationType,
            StartOffset = request.StartOffset,
            EndOffset = request.EndOffset,
            SelectedText = request.SelectedText ?? "",
            NoteText = request.NoteText,
            Color = request.Color ?? "yellow",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAnnotationAsync(int userId, int annotationId, CancellationToken ct = default)
    {
        var item = await _db.ReadingAnnotations.FirstOrDefaultAsync(a => a.Id == annotationId && a.UserId == userId, ct);
        if (item == null) return;
        _db.ReadingAnnotations.Remove(item);
        await _db.SaveChangesAsync(ct);
    }

    public Task<List<ReadingAnnotation>> GetAnnotationsAsync(int userId, int passageId, CancellationToken ct = default) =>
        _db.ReadingAnnotations.AsNoTracking()
            .Where(a => a.UserId == userId && a.ReadingPassageId == passageId)
            .OrderBy(a => a.StartOffset)
            .ToListAsync(ct);

    public async Task<ReadingVocabularyItem?> LookupWordAsync(int passageId, string word, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(word)) return null;
        var normalized = word.Trim();
        return await _db.ReadingVocabularyItems.AsNoTracking()
            .FirstOrDefaultAsync(v => v.ReadingPassageId == passageId && v.Word.ToLower() == normalized.ToLower(), ct);
    }

    public async Task<StudentVocabulary> SaveToNotebookAsync(
        int userId,
        int? passageId,
        int? vocabItemId,
        string word,
        ReadingNotebookFields? fields = null,
        CancellationToken ct = default)
    {
        ReadingVocabularyItem? source = null;
        if (vocabItemId.HasValue)
        {
            source = await _db.ReadingVocabularyItems.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == vocabItemId.Value, ct);
        }
        else if (passageId.HasValue)
        {
            source = await LookupWordAsync(passageId.Value, word, ct);
        }

        var entry = new StudentVocabulary
        {
            UserId = userId,
            SourceReadingPassageId = passageId ?? source?.ReadingPassageId,
            SourceVocabularyItemId = source?.Id,
            Word = source?.Word ?? word.Trim(),
            Ipa = FirstNonEmpty(fields?.Ipa, source?.Ipa),
            PartOfSpeech = FirstNonEmpty(fields?.PartOfSpeech, source?.PartOfSpeech),
            EnglishDefinition = FirstNonEmpty(fields?.EnglishDefinition, source?.EnglishDefinition),
            VietnameseMeaning = FirstNonEmpty(fields?.VietnameseMeaning, source?.VietnameseMeaning),
            Example = FirstNonEmpty(fields?.Example, source?.Example),
            CreatedAt = DateTime.UtcNow
        };
        _db.StudentVocabularies.Add(entry);
        await _db.SaveChangesAsync(ct);
        return entry;
    }

    private static string? FirstNonEmpty(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred.Trim() : fallback;

    public Task<List<StudentVocabulary>> GetNotebookAsync(int userId, string? search, CancellationToken ct = default)
    {
        var query = _db.StudentVocabularies.AsNoTracking().Where(v => v.UserId == userId);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(v => v.Word.Contains(search) || (v.VietnameseMeaning != null && v.VietnameseMeaning.Contains(search)));
        return query.OrderByDescending(v => v.CreatedAt).ToListAsync(ct);
    }

    public async Task DeleteNotebookEntryAsync(int userId, int id, CancellationToken ct = default)
    {
        var item = await _db.StudentVocabularies.FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId, ct);
        if (item == null) return;
        _db.StudentVocabularies.Remove(item);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ReadingProgressViewModel> GetProgressAsync(int userId, CancellationToken ct = default)
    {
        var attempts = await _db.ReadingAttempts.AsNoTracking()
            .Include(a => a.ReadingPassage)
            .Include(a => a.Answers).ThenInclude(ans => ans.Question)
            .Where(a => a.UserId == userId && a.IsSubmitted)
            .OrderByDescending(a => a.SubmittedAt)
            .ToListAsync(ct);

        var completed = await _db.UserReadingHistories.AsNoTracking()
            .CountAsync(h => h.UserId == userId && h.IsCompleted, ct);

        var percents = attempts
            .Where(a => a.MaxScore is > 0)
            .Select(a => 100.0 * a.Score!.Value / a.MaxScore!.Value)
            .ToList();

        var byType = attempts
            .SelectMany(a => a.Answers.Where(ans => ans.IsCorrect.HasValue && ans.Question != null)
                .Select(ans => new { ans.Question!.QuestionType, Correct = ans.IsCorrect!.Value }))
            .GroupBy(x => x.QuestionType)
            .ToDictionary(
                g => g.Key,
                g => g.Count() == 0 ? 0 : 100.0 * g.Count(x => x.Correct) / g.Count());

        return new ReadingProgressViewModel
        {
            CompletedCount = completed,
            AverageScorePercent = percents.Count == 0 ? 0 : percents.Average(),
            TotalStudySeconds = attempts.Sum(a => a.ElapsedSeconds ?? 0),
            AccuracyPercent = percents.Count == 0 ? 0 : percents.Average(),
            History = attempts.Take(50).Select(a => new ReadingAttemptHistoryItem
            {
                AttemptId = a.Id,
                PassageId = a.ReadingPassageId,
                Title = a.ReadingPassage.Title,
                Level = a.ReadingPassage.Level,
                Score = a.Score,
                MaxScore = a.MaxScore,
                SubmittedAt = a.SubmittedAt
            }).ToList(),
            AccuracyByQuestionType = byType,
            ScoreOverTime = attempts
                .Where(a => a.SubmittedAt.HasValue && a.MaxScore is > 0)
                .OrderBy(a => a.SubmittedAt)
                .Select(a => new ReadingScorePoint
                {
                    Date = a.SubmittedAt!.Value,
                    Percent = 100.0 * a.Score!.Value / a.MaxScore!.Value
                }).ToList()
        };
    }

    private static bool ScoreQuestion(ReadingQuestion question, ReadingStudentAnswer? answer)
    {
        if (answer == null) return false;

        return question.QuestionType switch
        {
            ReadingQuestionTypes.MultipleChoice or ReadingQuestionTypes.TrueFalse =>
                question.Options.Any(o => o.IsCorrect && o.Id == answer.SelectedOptionId),
            ReadingQuestionTypes.GapFill or ReadingQuestionTypes.ShortAnswer =>
                NormalizeText(answer.AnswerText) == NormalizeText(question.CorrectAnswerText),
            ReadingQuestionTypes.Matching => ScoreMatching(question, answer.MatchingJson),
            _ => false
        };
    }

    private static bool ScoreMatching(ReadingQuestion question, string? matchingJson)
    {
        if (string.IsNullOrWhiteSpace(matchingJson)) return false;
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(matchingJson);
            if (map == null || map.Count == 0) return false;
            var expected = question.Options
                .Where(o => !string.IsNullOrWhiteSpace(o.MatchKey))
                .GroupBy(o => o.MatchKey!)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id.ToString()).OrderBy(x => x).ToArray());

            foreach (var pair in expected)
            {
                if (!map.TryGetValue(pair.Key, out var value)) return false;
                var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .OrderBy(x => x).ToArray();
                if (!parts.SequenceEqual(pair.Value)) return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeText(string? value) =>
        string.Join(' ', (value ?? string.Empty).Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private async Task<bool> IsAdminUserAsync(int userId, CancellationToken ct)
    {
        var role = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);
        return Roles.Normalize(role) == Roles.Admin;
    }
}
