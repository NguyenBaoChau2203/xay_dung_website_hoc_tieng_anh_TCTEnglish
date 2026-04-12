using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;

namespace TCTEnglish.Services
{
    public class ListeningService : IListeningService
    {
        // CEFR levels in display order
        private static readonly string[] LevelOrder = { "A1", "A2", "B1", "B2", "C1" };

        private static readonly Dictionary<string, (string Title, string Description, string IconClass, string AccentColor)> LevelMeta =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["A1"] = ("Beginner", "Basic everyday expressions and simple phrases", "bi bi-1-circle", "#4CAF50"),
                ["A2"] = ("Elementary", "Common situations and simple language", "bi bi-2-circle", "#8BC34A"),
                ["B1"] = ("Intermediate", "Main points on familiar topics", "bi bi-3-circle", "#2196F3"),
                ["B2"] = ("Upper-Intermediate", "Complex texts and technical discussions", "bi bi-4-circle", "#9C27B0"),
                ["C1"] = ("Advanced", "Flexible and effective language use", "bi bi-5-circle", "#FF5722"),
            };

        private readonly DbflashcardContext _context;
        private readonly ILogger<ListeningService> _logger;

        public ListeningService(DbflashcardContext context, ILogger<ListeningService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ----------------------------------------------------------------
        // GetIndexViewModelAsync
        // ----------------------------------------------------------------

        public async Task<ListeningIndexViewModel> GetIndexViewModelAsync(string? level, string? topic)
        {
            var query = _context.ListeningLessons
                .AsNoTracking()
                .Where(l => l.IsPublished);

            if (!string.IsNullOrWhiteSpace(level))
                query = query.Where(l => l.Level == level);

            if (!string.IsNullOrWhiteSpace(topic))
                query = query.Where(l => l.Topic == topic);

            var lessons = await query
                .OrderBy(l => l.CreatedAt)
                .Select(l => new
                {
                    l.Id,
                    l.Title,
                    l.Level,
                    l.Topic,
                    l.ThumbnailUrl,
                    l.Duration,
                    l.Speaker1Name,
                    l.Speaker2Name,
                    l.Speaker1Country,
                    l.Speaker2Country,
                    TranscriptLineCount = l.TranscriptLines.Count,
                    QuizQuestionCount = l.QuizQuestions.Count,
                    VocabItemCount = l.VocabItems.Count,
                })
                .ToListAsync();

            // Distinct topics from ALL published lessons (pre-filter for topic dropdown)
            var allTopics = await _context.ListeningLessons
                .AsNoTracking()
                .Where(l => l.IsPublished)
                .Select(l => l.Topic)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            var cardsByLevel = lessons
                .GroupBy(l => l.Level)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(l => new ListeningLessonCardViewModel
                    {
                        Id = l.Id,
                        Title = l.Title,
                        Level = l.Level,
                        Topic = l.Topic,
                        ThumbnailUrl = l.ThumbnailUrl,
                        Duration = l.Duration,
                        Speaker1Name = l.Speaker1Name,
                        Speaker2Name = l.Speaker2Name,
                        Speaker1Country = l.Speaker1Country,
                        Speaker2Country = l.Speaker2Country,
                        TranscriptLineCount = l.TranscriptLineCount,
                        QuizQuestionCount = l.QuizQuestionCount,
                        VocabItemCount = l.VocabItemCount,
                    }).ToList()
                );

            // Level metadata in canonical order
            var levelMetas = LevelOrder
                .Select(key => BuildLevelMeta(key, cardsByLevel))
                .ToList();

            return new ListeningIndexViewModel
            {
                LessonsByLevel = cardsByLevel,
                Topics = allTopics,
                Levels = levelMetas,
                SelectedLevel = level,
                SelectedTopic = topic,
            };
        }

        // ----------------------------------------------------------------
        // GetPracticeViewModelAsync
        // ----------------------------------------------------------------

        public async Task<ListeningPracticeViewModel?> GetPracticeViewModelAsync(int lessonId, int? userId)
        {
            var lesson = await _context.ListeningLessons
                .AsNoTracking()
                .Where(l => l.Id == lessonId && l.IsPublished)
                .Include(l => l.TranscriptLines)
                .Include(l => l.QuizQuestions)
                .Include(l => l.VocabItems)
                .FirstOrDefaultAsync();

            if (lesson == null)
                return null;

            // Load user progress (may be null for guests / first-time visitors)
            UserListeningProgress? progress = null;
            if (userId.HasValue)
            {
                progress = await _context.UserListeningProgresses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == userId.Value && p.LessonId == lessonId);
            }

            return new ListeningPracticeViewModel
            {
                LessonId = lesson.Id,
                Title = lesson.Title,
                Level = lesson.Level,
                Topic = lesson.Topic,
                YoutubeId = lesson.YoutubeId,
                AudioUrl = lesson.AudioUrl,
                ThumbnailUrl = lesson.ThumbnailUrl,
                Duration = lesson.Duration,
                Speaker1Name = lesson.Speaker1Name,
                Speaker2Name = lesson.Speaker2Name,
                Speaker1Country = lesson.Speaker1Country,
                Speaker2Country = lesson.Speaker2Country,

                TranscriptLines = lesson.TranscriptLines
                    .OrderBy(t => t.OrderIndex)
                    .Select(t => new ListeningTranscriptLineViewModel
                    {
                        Id = t.Id,
                        OrderIndex = t.OrderIndex,
                        Speaker = t.Speaker,
                        Text = t.Text,
                        VietnameseMeaning = t.VietnameseMeaning,
                        StartTime = t.StartTime,
                        EndTime = t.EndTime,
                    }).ToList(),

                // Quiz questions: CorrectAnswer intentionally omitted from ViewModel
                QuizQuestions = lesson.QuizQuestions
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
                    }).ToList(),

                VocabItems = lesson.VocabItems
                    .OrderBy(v => v.OrderIndex)
                    .Select(v => new ListeningVocabItemViewModel
                    {
                        Id = v.Id,
                        OrderIndex = v.OrderIndex,
                        Word = v.Word,
                        Definition = v.Definition,
                        ExampleSentence = v.ExampleSentence,
                        ImageUrl = v.ImageUrl,
                    }).ToList(),

                TranscriptCompleted = progress?.TranscriptCompleted ?? false,
                QuizCompleted = progress?.QuizCompleted ?? false,
                QuizScore = progress?.QuizScore,
                VocabReviewed = progress?.VocabReviewed ?? false,
                CompletedAt = progress?.CompletedAt,
            };
        }

        // ----------------------------------------------------------------
        // EvaluateQuizAsync
        // ----------------------------------------------------------------

        public async Task<ListeningQuizResultViewModel?> EvaluateQuizAsync(ListeningQuizSubmitDto dto)
        {
            if (dto == null || dto.LessonId <= 0)
                return null;

            var questions = await _context.ListeningQuizQuestions
                .AsNoTracking()
                .Where(q => q.LessonId == dto.LessonId)
                .OrderBy(q => q.OrderIndex)
                .ToListAsync();

            if (!questions.Any())
                return null;

            var results = new List<ListeningQuizAnswerResult>();
            int correctCount = 0;

            foreach (var q in questions)
            {
                dto.Answers.TryGetValue(q.Id, out var userAnswer);
                bool isCorrect = string.Equals(userAnswer, q.CorrectAnswer, StringComparison.OrdinalIgnoreCase);
                if (isCorrect) correctCount++;

                results.Add(new ListeningQuizAnswerResult
                {
                    QuestionId = q.Id,
                    OrderIndex = q.OrderIndex,
                    QuestionText = q.QuestionText,
                    UserAnswer = userAnswer,
                    CorrectAnswer = q.CorrectAnswer,
                    IsCorrect = isCorrect,
                    Explanation = q.Explanation,
                });
            }

            int total = questions.Count;
            int scorePercent = total > 0 ? (int)Math.Round((double)correctCount / total * 100) : 0;

            return new ListeningQuizResultViewModel
            {
                LessonId = dto.LessonId,
                TotalQuestions = total,
                CorrectCount = correctCount,
                ScorePercent = scorePercent,
                Passed = scorePercent >= 60,
                Answers = results,
            };
        }

        // ----------------------------------------------------------------
        // SaveProgressAsync
        // ----------------------------------------------------------------

        public async Task<bool> SaveProgressAsync(int userId, int lessonId, ListeningProgressUpdateDto dto)
        {
            try
            {
                // Verify lesson exists (also prevents saving progress for non-existent lessons)
                bool lessonExists = await _context.ListeningLessons
                    .AnyAsync(l => l.Id == lessonId && l.IsPublished);

                if (!lessonExists)
                    return false;

                // Anti-IDOR: ownership is enforced by using the server-side userId
                var progress = await _context.UserListeningProgresses
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == lessonId);

                if (progress == null)
                {
                    // Insert new record
                    progress = new UserListeningProgress
                    {
                        UserId = userId,
                        LessonId = lessonId,
                        LastAccessedAt = DateTime.UtcNow,
                    };
                    _context.UserListeningProgresses.Add(progress);
                }

                // Selective update — only apply fields sent by the client
                if (dto.TranscriptCompleted.HasValue)
                    progress.TranscriptCompleted = dto.TranscriptCompleted.Value;

                if (dto.QuizCompleted.HasValue)
                    progress.QuizCompleted = dto.QuizCompleted.Value;

                if (dto.QuizScore.HasValue)
                    progress.QuizScore = dto.QuizScore.Value;

                if (dto.VocabReviewed.HasValue)
                    progress.VocabReviewed = dto.VocabReviewed.Value;

                progress.LastAccessedAt = DateTime.UtcNow;

                // Mark fully completed when all three sections done
                if (progress.TranscriptCompleted && progress.QuizCompleted && progress.VocabReviewed
                    && progress.CompletedAt == null)
                {
                    progress.CompletedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveProgressAsync failed for userId={UserId}, lessonId={LessonId}", userId, lessonId);
                return false;
            }
        }

        // ----------------------------------------------------------------
        // Private helpers
        // ----------------------------------------------------------------

        private static ListeningLevelMetaViewModel BuildLevelMeta(
            string key,
            Dictionary<string, List<ListeningLessonCardViewModel>> cardsByLevel)
        {
            LevelMeta.TryGetValue(key, out var meta);
            cardsByLevel.TryGetValue(key, out var cards);

            return new ListeningLevelMetaViewModel
            {
                Key = key,
                Title = meta.Title ?? key,
                Description = meta.Description ?? string.Empty,
                IconClass = meta.IconClass ?? "bi bi-headphones",
                AccentColor = meta.AccentColor ?? "#607D8B",
                LessonCount = cards?.Count ?? 0,
            };
        }
    }
}
