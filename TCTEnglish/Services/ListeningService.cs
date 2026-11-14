using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTEnglish.Services.AI;

namespace TCTEnglish.Services
{
    public class ListeningService : IListeningService
    {
        private readonly DbflashcardContext _context;
        private readonly ILogger<ListeningService> _logger;
        private readonly IGoalsService _goalsService;
        private readonly IYoutubeTranscriptService _youtubeTranscriptService;
        private readonly IAiProviderClient _aiProvider;

        public ListeningService(
            DbflashcardContext context,
            ILogger<ListeningService> logger,
            IGoalsService goalsService,
            IYoutubeTranscriptService youtubeTranscriptService,
            IAiProviderClient aiProvider)
        {
            _context = context;
            _logger = logger;
            _goalsService = goalsService;
            _youtubeTranscriptService = youtubeTranscriptService;
            _aiProvider = aiProvider;
        }

        // ----------------------------------------------------------------
        // Access + completion helpers (plan L-R3, L-R5)
        // ----------------------------------------------------------------

        private sealed record LessonAccess(int Id, int? OwnerUserId, int TranscriptCount, int QuizCount, int VocabCount);

        private sealed record ProgressSnapshot(
            bool TranscriptCompleted, bool QuizCompleted, int? QuizScore, bool VocabReviewed,
            DateTime? CompletedAt, DateTime LastAccessedAt);

        /// <summary>
        /// The single rule that decides whether a user may use (read, grade, save progress for) a lesson.
        /// Usable = published and (public, or owned by this user). A private lesson is not usable by a
        /// Standard-role owner (the import feature is Premium). Returns null when not usable, so callers answer 404.
        /// </summary>
        private async Task<LessonAccess?> GetUsableLessonAsync(int lessonId, int? userId, CancellationToken ct = default)
        {
            var lesson = await _context.ListeningLessons
                .AsNoTracking()
                .Where(l => l.Id == lessonId && l.IsPublished
                    && (l.OwnerUserId == null || (userId.HasValue && l.OwnerUserId == userId.Value)))
                .Select(l => new LessonAccess(
                    l.Id,
                    l.OwnerUserId,
                    l.TranscriptLines.Count,
                    l.QuizQuestions.Count,
                    l.VocabItems.Count))
                .FirstOrDefaultAsync(ct);

            if (lesson == null)
                return null;

            if (lesson.OwnerUserId.HasValue && userId.HasValue
                && await GetNormalizedRoleAsync(userId.Value) == Roles.Standard)
            {
                return null; // Premium locked
            }

            return lesson;
        }

        /// <summary>A lesson is complete when every section that has content is done.</summary>
        private static bool IsLessonComplete(bool transcriptDone, bool quizDone, bool vocabDone,
            int transcriptCount, int quizCount, int vocabCount) =>
            (transcriptCount == 0 || transcriptDone) &&
            (quizCount == 0 || quizDone) &&
            (vocabCount == 0 || vocabDone) &&
            (transcriptCount + quizCount + vocabCount) > 0;

        private static bool IsLessonComplete(UserListeningProgress p, LessonAccess lesson) =>
            IsLessonComplete(p.TranscriptCompleted, p.QuizCompleted, p.VocabReviewed,
                lesson.TranscriptCount, lesson.QuizCount, lesson.VocabCount);

        /// <summary>Fills status/progress fields of a card from the user's progress row (null = not started).</summary>
        private static void ApplyProgress(ListeningLessonCardViewModel card, ProgressSnapshot? progress)
        {
            card.StepsTotal = (card.TranscriptLineCount > 0 ? 1 : 0)
                + (card.QuizQuestionCount > 0 ? 1 : 0)
                + (card.VocabItemCount > 0 ? 1 : 0);

            if (progress == null)
            {
                card.Status = "new";
                return;
            }

            card.TranscriptCompleted = progress.TranscriptCompleted;
            card.QuizCompleted = progress.QuizCompleted;
            card.QuizScore = progress.QuizScore;
            card.VocabReviewed = progress.VocabReviewed;

            card.StepsDone = (card.TranscriptLineCount > 0 && progress.TranscriptCompleted ? 1 : 0)
                + (card.QuizQuestionCount > 0 && progress.QuizCompleted ? 1 : 0)
                + (card.VocabItemCount > 0 && progress.VocabReviewed ? 1 : 0);

            card.IsCompleted = progress.CompletedAt != null
                || IsLessonComplete(progress.TranscriptCompleted, progress.QuizCompleted, progress.VocabReviewed,
                    card.TranscriptLineCount, card.QuizQuestionCount, card.VocabItemCount);
            card.Status = card.IsCompleted ? "done" : "progress";
        }

        // ----------------------------------------------------------------
        // GetIndexViewModelAsync
        // ----------------------------------------------------------------

        public async Task<ListeningIndexViewModel> GetIndexViewModelAsync(string? level, string? topic, int? userId = null, CancellationToken ct = default)
        {
            var publicLessons = _context.ListeningLessons
                .AsNoTracking()
                .Where(l => l.OwnerUserId == null && l.IsPublished);

            var query = publicLessons;

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
                    l.YoutubeId,
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
                .ToListAsync(ct);

            // Distinct topics from ALL published public lessons (pre-filter list for the topic filter).
            // Private lessons (topic "My Lessons") never appear here.
            var allTopics = await publicLessons
                .Select(l => l.Topic)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync(ct);

            var totalLessons = await publicLessons.CountAsync(ct);

            var cards = lessons
                .Select(l => new ListeningLessonCardViewModel
                {
                    Id = l.Id,
                    Title = l.Title,
                    Level = l.Level,
                    Topic = l.Topic,
                    YoutubeId = l.YoutubeId,
                    ThumbnailUrl = l.ThumbnailUrl,
                    Duration = l.Duration,
                    Speaker1Name = l.Speaker1Name,
                    Speaker2Name = l.Speaker2Name,
                    Speaker1Country = l.Speaker1Country,
                    Speaker2Country = l.Speaker2Country,
                    TranscriptLineCount = l.TranscriptLineCount,
                    QuizQuestionCount = l.QuizQuestionCount,
                    VocabItemCount = l.VocabItemCount,
                })
                .ToList();

            var viewModel = new ListeningIndexViewModel
            {
                Topics = allTopics,
                SelectedLevel = level,
                SelectedTopic = topic,
                TotalLessons = totalLessons,
                IsAuthenticated = userId.HasValue,
            };

            if (!userId.HasValue)
            {
                foreach (var card in cards) ApplyProgress(card, null);
                FinishIndexViewModel(viewModel, cards);
                return viewModel;
            }

            var uid = userId.Value;
            var currentRole = await GetNormalizedRoleAsync(uid);
            var isLocked = currentRole == Roles.Standard;
            viewModel.CanImport = currentRole is Roles.Admin or Roles.Premium;

            // One query for every progress row of the user on lessons they may see
            // (public published lessons and their own private ones).
            var progressRows = await _context.UserListeningProgresses
                .AsNoTracking()
                .Where(p => p.UserId == uid && p.Lesson.IsPublished
                    && (p.Lesson.OwnerUserId == null || p.Lesson.OwnerUserId == uid))
                .Select(p => new
                {
                    p.LessonId,
                    IsPrivate = p.Lesson.OwnerUserId != null,
                    p.TranscriptCompleted,
                    p.QuizCompleted,
                    p.QuizScore,
                    p.VocabReviewed,
                    p.CompletedAt,
                    p.LastAccessedAt,
                    TranscriptCount = p.Lesson.TranscriptLines.Count,
                    QuizCount = p.Lesson.QuizQuestions.Count,
                    VocabCount = p.Lesson.VocabItems.Count,
                })
                .ToListAsync(ct);

            var progressByLesson = progressRows.ToDictionary(
                p => p.LessonId,
                p => new ProgressSnapshot(p.TranscriptCompleted, p.QuizCompleted, p.QuizScore, p.VocabReviewed, p.CompletedAt, p.LastAccessedAt));

            foreach (var card in cards)
                ApplyProgress(card, progressByLesson.GetValueOrDefault(card.Id));

            // Overview numbers use every public lesson, never the level/topic filter.
            var publicProgress = progressRows.Where(p => !p.IsPrivate).ToList();
            viewModel.CompletedLessons = publicProgress.Count(p => p.CompletedAt != null
                || IsLessonComplete(p.TranscriptCompleted, p.QuizCompleted, p.VocabReviewed, p.TranscriptCount, p.QuizCount, p.VocabCount));
            viewModel.InProgressLessons = publicProgress.Count - viewModel.CompletedLessons;

            var myLessons = await _context.ListeningLessons
                .AsNoTracking()
                .Where(l => l.OwnerUserId == uid)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new ListeningLessonCardViewModel
                {
                    Id = l.Id,
                    Title = l.Title,
                    Level = l.Level,
                    Topic = l.Topic,
                    YoutubeId = l.YoutubeId,
                    ThumbnailUrl = l.ThumbnailUrl,
                    Duration = l.Duration,
                    TranscriptLineCount = l.TranscriptLines.Count,
                    QuizQuestionCount = l.QuizQuestions.Count,
                    VocabItemCount = l.VocabItems.Count,
                    IsPrivate = true,
                    IsLocked = isLocked,
                    ImportStatus = "ready", // For now we keep it simple
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync(ct);

            foreach (var card in myLessons)
                ApplyProgress(card, progressByLesson.GetValueOrDefault(card.Id));

            viewModel.MyLessons = myLessons;
            viewModel.IsMyLessonsSectionLocked = isLocked;

            // "Continue listening": the usable, unfinished lesson opened most recently.
            var usableIds = myLessons.Where(c => !c.IsLocked).Select(c => c.Id).ToHashSet();
            var continueRow = progressRows
                .Where(p => !p.IsPrivate || usableIds.Contains(p.LessonId))
                .Where(p => progressByLesson.TryGetValue(p.LessonId, out var s)
                    && s.CompletedAt == null
                    && !IsLessonComplete(s.TranscriptCompleted, s.QuizCompleted, s.VocabReviewed, p.TranscriptCount, p.QuizCount, p.VocabCount))
                .OrderByDescending(p => p.LastAccessedAt)
                .FirstOrDefault();

            if (continueRow != null)
            {
                var continueCard = cards.FirstOrDefault(c => c.Id == continueRow.LessonId)
                    ?? myLessons.FirstOrDefault(c => c.Id == continueRow.LessonId);

                if (continueCard == null)
                {
                    // A public lesson hidden by the active level/topic filter.
                    continueCard = await publicLessons
                        .Where(l => l.Id == continueRow.LessonId)
                        .Select(l => new ListeningLessonCardViewModel
                        {
                            Id = l.Id,
                            Title = l.Title,
                            Level = l.Level,
                            Topic = l.Topic,
                            YoutubeId = l.YoutubeId,
                            ThumbnailUrl = l.ThumbnailUrl,
                            Duration = l.Duration,
                            Speaker1Name = l.Speaker1Name,
                            Speaker2Name = l.Speaker2Name,
                            Speaker1Country = l.Speaker1Country,
                            Speaker2Country = l.Speaker2Country,
                            TranscriptLineCount = l.TranscriptLines.Count,
                            QuizQuestionCount = l.QuizQuestions.Count,
                            VocabItemCount = l.VocabItems.Count,
                        })
                        .FirstOrDefaultAsync(ct);

                    if (continueCard != null)
                        ApplyProgress(continueCard, progressByLesson.GetValueOrDefault(continueCard.Id));
                }

                viewModel.ContinueLesson = continueCard;
            }

            FinishIndexViewModel(viewModel, cards);
            return viewModel;
        }

        // CEFR levels in display order (the page itself builds its section headings from the same keys).
        private static readonly string[] LevelOrder = { "A1", "A2", "B1", "B2", "C1", "C2" };

        private static void FinishIndexViewModel(ListeningIndexViewModel viewModel, List<ListeningLessonCardViewModel> cards)
        {
            viewModel.LessonsByLevel = cards
                .GroupBy(c => c.Level)
                .ToDictionary(g => g.Key, g => g.ToList());
            viewModel.Levels = LevelOrder
                .Select(key => new ListeningLevelMetaViewModel
                {
                    Key = key,
                    LessonCount = viewModel.LessonsByLevel.TryGetValue(key, out var inLevel) ? inLevel.Count : 0
                })
                .ToList();
        }

        // ----------------------------------------------------------------
        // GetPracticeViewModelAsync
        // ----------------------------------------------------------------

        public async Task<ListeningPracticeViewModel?> GetPracticeViewModelAsync(int lessonId, int? userId, CancellationToken ct = default)
        {
            var access = await GetUsableLessonAsync(lessonId, userId, ct);
            if (access == null)
                return null;

            var role = userId.HasValue ? await GetNormalizedRoleAsync(userId.Value) : Roles.Standard;

            var lesson = await _context.ListeningLessons
                .AsNoTracking()
                .Where(l => l.Id == lessonId)
                .Include(l => l.TranscriptLines)
                .Include(l => l.QuizQuestions)
                .Include(l => l.VocabItems)
                .FirstOrDefaultAsync(ct);

            if (lesson == null)
                return null;

            // Load user progress (may be null for guests / first-time visitors)
            UserListeningProgress? progress = null;
            if (userId.HasValue)
            {
                progress = await _context.UserListeningProgresses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == userId.Value && p.LessonId == lessonId, ct);
            }

            var isPremiumOrAdmin = role is Roles.Admin or Roles.Premium;
            var isOwnLesson = lesson.OwnerUserId.HasValue && lesson.OwnerUserId == userId;

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

                IsAuthenticated = userId.HasValue,
                IsOwnLesson = isOwnLesson,
                IsCompleted = progress != null && (progress.CompletedAt != null || IsLessonComplete(progress, access)),
                CanUseSpeed = isPremiumOrAdmin,
                CanTranslate = isPremiumOrAdmin,
                // Quiz generation on a shared (public) lesson is Admin-only; Premium users keep it for their own lessons.
                CanUseAi = isPremiumOrAdmin && (isOwnLesson || role == Roles.Admin),
            };
        }

        // ----------------------------------------------------------------
        // EvaluateQuizAsync
        // ----------------------------------------------------------------

        public async Task<ListeningQuizResultViewModel?> EvaluateQuizAsync(ListeningQuizSubmitDto dto, int? userId = null, CancellationToken ct = default)
        {
            if (dto == null || dto.LessonId <= 0)
                return null;

            var access = await GetUsableLessonAsync(dto.LessonId, userId, ct);
            if (access == null)
                return null;

            var questions = await _context.ListeningQuizQuestions
                .AsNoTracking()
                .Where(q => q.LessonId == dto.LessonId)
                .OrderBy(q => q.OrderIndex)
                .ToListAsync(ct);

            if (!questions.Any())
                return null;

            var submitted = dto.Answers ?? new Dictionary<int, string>();
            var results = new List<ListeningQuizAnswerResult>();
            int correctCount = 0;

            // Only the lesson's own question ids are graded; any other key in the body is ignored.
            foreach (var q in questions)
            {
                submitted.TryGetValue(q.Id, out var rawAnswer);
                var userAnswer = NormalizeAnswerLetter(rawAnswer);
                bool isCorrect = userAnswer != null
                    && string.Equals(userAnswer, q.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
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

            var result = new ListeningQuizResultViewModel
            {
                LessonId = dto.LessonId,
                TotalQuestions = total,
                CorrectCount = correctCount,
                ScorePercent = scorePercent,
                Passed = scorePercent >= 60,
                Answers = results,
            };

            if (userId.HasValue)
            {
                try
                {
                    var progress = await ApplyProgressAsync(userId.Value, access, p =>
                    {
                        p.QuizCompleted = true;
                        p.QuizScore = Math.Max(p.QuizScore ?? 0, scorePercent);
                    }, ct);
                    result.BestScorePercent = progress.QuizScore;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The learner still gets the graded result; the score is simply not stored this time.
                    _logger.LogError(ex, "EvaluateQuizAsync could not store progress for userId={UserId}, lessonId={LessonId}", userId, dto.LessonId);
                }
            }

            return result;
        }

        /// <summary>Accepts only A-D (case-insensitive); anything else counts as unanswered.</summary>
        private static string? NormalizeAnswerLetter(string? answer)
        {
            var trimmed = answer?.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.Length != 1)
                return null;

            var upper = char.ToUpperInvariant(trimmed[0]);
            return upper is >= 'A' and <= 'D' ? upper.ToString() : null;
        }

        // ----------------------------------------------------------------
        // SaveProgressAsync
        // ----------------------------------------------------------------

        public async Task<bool> SaveProgressAsync(int userId, int lessonId, ListeningProgressUpdateDto dto)
        {
            try
            {
                // Same access rule as every other path: published and (public or own and still allowed).
                var access = await GetUsableLessonAsync(lessonId, userId);
                if (access == null)
                    return false;

                await ApplyProgressAsync(userId, access, progress =>
                {
                    // Selective update — only apply fields sent by the client
                    if (dto.TranscriptCompleted.HasValue)
                        progress.TranscriptCompleted = dto.TranscriptCompleted.Value;

                    if (dto.QuizCompleted.HasValue)
                        progress.QuizCompleted = dto.QuizCompleted.Value;

                    if (dto.QuizScore.HasValue)
                        progress.QuizScore = Math.Max(progress.QuizScore ?? 0, Math.Clamp(dto.QuizScore.Value, 0, 100));

                    if (dto.VocabReviewed.HasValue)
                        progress.VocabReviewed = dto.VocabReviewed.Value;
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveProgressAsync failed for userId={UserId}, lessonId={LessonId}", userId, lessonId);
                return false;
            }
        }

        /// <summary>
        /// Upserts the user's progress row, applies the completion rule (plan 4.4) and, on the transition
        /// from not-completed to completed, records exactly one Listening credit for Goals.
        /// </summary>
        private async Task<UserListeningProgress> ApplyProgressAsync(
            int userId, LessonAccess lesson, Action<UserListeningProgress> apply, CancellationToken ct = default)
        {
            // Anti-IDOR: ownership is enforced by using the server-side userId
            var progress = await _context.UserListeningProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == lesson.Id, ct);

            if (progress == null)
            {
                progress = new UserListeningProgress
                {
                    UserId = userId,
                    LessonId = lesson.Id,
                    LastAccessedAt = DateTime.UtcNow,
                };
                _context.UserListeningProgresses.Add(progress);
            }

            apply(progress);
            progress.LastAccessedAt = DateTime.UtcNow;

            var wasCompleted = progress.CompletedAt != null;
            if (!wasCompleted && IsLessonComplete(progress, lesson))
            {
                progress.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(ct);

            var isNewCompletion = !wasCompleted && progress.CompletedAt != null;
            if (isNewCompletion)
            {
                var activityUpdate = _goalsService.BuildListeningCompletionActivityUpdate();
                await _goalsService.RecordLearningActivityAsync(userId, activityUpdate);
            }

            return progress;
        }

        // ----------------------------------------------------------------
        // Private Methods for Import/Delete 
        // ----------------------------------------------------------------

        public async Task<ListeningImportResult> CreateOwnedLessonAsync(int userId, string youtubeUrl, System.Threading.CancellationToken ct = default)
        {
            var role = await GetNormalizedRoleAsync(userId);
            if (role is not (Roles.Admin or Roles.Premium))
            {
                return ListeningImportResult.UpgradeRequired("Vui lòng nâng cấp lên Premium để sử dụng tính năng này.");
            }

            var normalizedYoutubeId = YoutubeUrlHelper.NormalizeYoutubeId(youtubeUrl);
            if (normalizedYoutubeId is null)
            {
                return ListeningImportResult.Invalid("YouTube URL không hợp lệ.");
            }

            var duplicateExists = await _context.ListeningLessons
                .AsNoTracking()
                .AnyAsync(v => v.OwnerUserId == userId && v.YoutubeId == normalizedYoutubeId, ct);

            if (duplicateExists)
            {
                return ListeningImportResult.Invalid("Video này đã có trong mục Bài nghe của tôi.");
            }

            var transcriptResult = await _youtubeTranscriptService.GetTranscriptForSpeakingImportAsync(normalizedYoutubeId, ct);
            if (!transcriptResult.IsEnglishUsable || transcriptResult.Sentences.Count == 0)
            {
                return ListeningImportResult.Invalid("Không thể lấy transcript tiếng Anh hợp lệ từ video này.");
            }

            var metadata = await _youtubeTranscriptService.GetVideoMetadataAsync(normalizedYoutubeId);
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.Title))
            {
                return ListeningImportResult.Invalid("Không thể tải metadata của video YouTube.");
            }

            var title = metadata.Title.Trim();
            if (title.Length > 255) title = title[..255];

            var strategy = _context.Database.CreateExecutionStrategy();
            var createdLessonId = 0;

            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(ct);

                var lesson = new ListeningLesson
                {
                    Title = title,
                    Level = "B1", // Default level
                    Topic = "My Lessons",
                    YoutubeId = normalizedYoutubeId,
                    ThumbnailUrl = metadata.ThumbnailUrl ?? YoutubeUrlHelper.BuildDefaultThumbnailUrl(normalizedYoutubeId),
                    Duration = await _youtubeTranscriptService.GetVideoDurationAsync(normalizedYoutubeId),
                    Speaker1Name = "Speaker",
                    IsPublished = true,
                    OwnerUserId = userId,
                    TranscriptSource = transcriptResult.TranscriptSource,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ListeningLessons.Add(lesson);
                await _context.SaveChangesAsync(ct);

                var transcriptLines = transcriptResult.Sentences.Select((s, index) => new ListeningTranscriptLine
                {
                    LessonId = lesson.Id,
                    OrderIndex = index,
                    Speaker = "Speaker",
                    Text = s.Text,
                    VietnameseMeaning = string.Empty,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime
                }).ToList();

                _context.ListeningTranscriptLines.AddRange(transcriptLines);
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                createdLessonId = lesson.Id;
            });

            _logger.LogInformation("User {UserId} imported private listening lesson {LessonId} ({YoutubeId}).", userId, createdLessonId, normalizedYoutubeId);

            // Auto-generate an AI quiz. The import request WAITS for it (bounded by the AI provider timeout);
            // a failure is logged and the lesson is still created without a quiz.
            try 
            { 
                 await GenerateQuizFromTranscriptAsync(createdLessonId, userId, ct); 
            }
            catch (Exception ex) 
            { 
                 _logger.LogWarning(ex, "Auto quiz generation failed for lesson {Id}", createdLessonId); 
            }

            return ListeningImportResult.Success(createdLessonId);
        }

        public async Task<OperationResult> DeleteOwnedLessonAsync(int userId, int lessonId)
        {
            var lesson = await _context.ListeningLessons
                .FirstOrDefaultAsync(l => l.Id == lessonId && l.OwnerUserId == userId);

            if (lesson == null)
            {
                return OperationResult.NotFound();
            }

            var role = await GetNormalizedRoleAsync(userId);
            if (role is not (Roles.Admin or Roles.Premium))
            {
                return OperationResult.Invalid("Vui lòng nâng cấp lên Premium để sử dụng tính năng này.");
            }

            _context.ListeningLessons.Remove(lesson);
            await _context.SaveChangesAsync();
            return OperationResult.Success();
        }

        public async Task<OperationResult> GenerateQuizFromTranscriptAsync(int lessonId, int userId, CancellationToken ct = default)
        {
            var role = await GetNormalizedRoleAsync(userId);
            if (role is not (Roles.Admin or Roles.Premium))
            {
                return OperationResult.Invalid("Vui lòng nâng cấp lên Premium để sử dụng tính năng AI.");
            }

            var lesson = await _context.ListeningLessons
                .Include(l => l.TranscriptLines)
                .Include(l => l.QuizQuestions)
                .FirstOrDefaultAsync(l => l.Id == lessonId, ct);

            if (lesson == null)
                return OperationResult.NotFound("Bài nghe không tồn tại.");

            if (lesson.OwnerUserId.HasValue && lesson.OwnerUserId != userId)
                return OperationResult.Invalid("Bạn không có quyền truy cập bài nghe này.");

            // Shared (public) lessons: only an Admin may write AI content onto them.
            if (lesson.OwnerUserId == null && role != Roles.Admin)
                return OperationResult.Invalid("Chỉ quản trị viên mới tạo được quiz cho bài nghe chung.");

            if (lesson.QuizQuestions.Any())
                return OperationResult.Invalid("Bài nghe đã có quiz.");

            if (lesson.TranscriptLines.Count == 0)
                return OperationResult.Invalid("Bài nghe chưa có phụ đề để sinh quiz.");

            var transcriptText = string.Join("\n", lesson.TranscriptLines
                .OrderBy(t => t.OrderIndex)
                .Select(t => $"{t.Speaker}: {t.Text}"));

            var promptText = $@"Dựa trên đoạn hội thoại/transcript tiếng Anh sau, hãy tạo 5 câu hỏi trắc nghiệm (A/B/C/D) để kiểm tra khả năng nghe hiểu.
Transcript:
{transcriptText}
Trả về JSON (không markdown) với format:
[
{{
""question"": ""Câu hỏi bằng tiếng Anh"",
""optionA"": ""đáp án A"",
""optionB"": ""đáp án B"",
""optionC"": ""đáp án C"",
""optionD"": ""đáp án D"",
""correctAnswer"": ""A"",
""explanation"": ""Giải thích ngắn bằng tiếng Việt""
}}
]
Yêu cầu:
Câu hỏi bám sát nội dung transcript
Đáp án nhiễu hợp lý, không quá dễ loại
Trộn đều các dạng: main idea, detail, inference
Chỉ trả JSON, không giải thích thêm";

            try
            {
                var messages = new List<AiContextMessage>
                {
                    new AiContextMessage("user", promptText)
                };

                var aiOptions = new AiProviderRequestOptions
                {
                    MaxOutputTokens = 4096,
                    Temperature = 0.3f,
                    RequestTimeoutSeconds = 20
                };

                var reply = await _aiProvider.GenerateReplyAsync(userId, messages, ct, aiOptions);
                var parsedQuestions = ParseQuizJson(reply.Text);

                if (parsedQuestions.Count == 0)
                    return OperationResult.Invalid("AI không tạo được câu hỏi nào hợp lệ.");

                var newQuestions = parsedQuestions.Select((q, i) => new ListeningQuizQuestion
                {
                    LessonId = lessonId,
                    OrderIndex = i,
                    QuestionText = q.Question ?? string.Empty,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    CorrectAnswer = q.CorrectAnswer?.ToUpper() ?? "A",
                    Explanation = q.Explanation
                }).ToList();

                _context.ListeningQuizQuestions.AddRange(newQuestions);
                await _context.SaveChangesAsync(ct);

                return OperationResult.Success();
            }
            catch (AiProviderException ex)
            {
                _logger.LogError(ex, "Lỗi từ AI Provider khi sinh quiz cho lesson {LessonId}", lessonId);
                return OperationResult.Invalid(AiProviderErrorMessages.For(ex.ErrorCode));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi sinh quiz cho lesson {LessonId}", lessonId);
                return OperationResult.Invalid("Không thể tạo quiz lúc này.");
            }
        }

        private List<ParsedQuizQuestion> ParseQuizJson(string jsonText)
        {
            var clean = jsonText.Replace("```json", "").Replace("```", "").Trim();
            try
            {
                return JsonSerializer.Deserialize<List<ParsedQuizQuestion>>(clean, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch
            {
                return new List<ParsedQuizQuestion>();
            }
        }

        private class ParsedQuizQuestion
        {
            public string? Question { get; set; }
            public string? OptionA { get; set; }
            public string? OptionB { get; set; }
            public string? OptionC { get; set; }
            public string? OptionD { get; set; }
            public string? CorrectAnswer { get; set; }
            public string? Explanation { get; set; }
        }

        private async Task<string> GetNormalizedRoleAsync(int userId)
        {
            var role = await _context.Users
                .AsNoTracking()
                .Where(user => user.UserId == userId)
                .Select(user => user.Role)
                .FirstOrDefaultAsync();

            return Roles.Normalize(role);
        }

        public async Task<OperationResult> TranslateTranscriptAsync(int lessonId, int userId, CancellationToken ct = default)
        {
            var roleName = await GetNormalizedRoleAsync(userId);
            if (roleName is not (Roles.Admin or Roles.Premium))
            {
                return OperationResult.Invalid("Vui lòng nâng cấp lên Premium để sử dụng tính năng dịch bằng AI.");
            }

            var lesson = await _context.ListeningLessons
                .Include(l => l.TranscriptLines)
                .FirstOrDefaultAsync(l => l.Id == lessonId, ct);

            if (lesson == null)
                return OperationResult.NotFound("Bài nghe không tồn tại.");

            // Standard permission check: only owners or admins for private lessons. 
            // Public lessons (no owner) can be translated by any Premium user.
            if (lesson.OwnerUserId.HasValue && lesson.OwnerUserId != userId && roleName != Roles.Admin)
                return OperationResult.Invalid("Bạn không có quyền dịch bài nghe này.");

            var lines = lesson.TranscriptLines.OrderBy(l => l.OrderIndex).ToList();
            if (lines.Count == 0)
                return OperationResult.Invalid("Bài nghe chưa có transcript để dịch.");

            // Only translate if needed
            if (lines.All(l => !string.IsNullOrWhiteSpace(l.VietnameseMeaning)))
                return OperationResult.Success();

            const int BatchSize = 30;
            var batches = lines.Select((x, i) => new { Index = i, Value = x })
                               .GroupBy(x => x.Index / BatchSize)
                               .Select(x => x.Select(v => v.Value).ToList())
                               .ToList();

            var pendingTranslations = new List<(ListeningTranscriptLine Line, string Translation)>();
            try
            {
                foreach (var batch in batches)
                {
                    var englishLines = string.Join("\n", batch.Select((l, i) => $"{i + 1}. \"{l.Text}\""));
                    var promptText = $@"Dịch các câu tiếng Anh sau sang tiếng Việt. Trả về JSON array, mỗi phần tử là bản dịch tương ứng theo đúng thứ tự.
Chỉ trả JSON array of strings, không giải thích thêm.

{englishLines}";

                    var messages = new List<AiContextMessage> { new AiContextMessage("user", promptText) };
                    var options = new AiProviderRequestOptions { MaxOutputTokens = 6144, Temperature = 0.2f, RequestTimeoutSeconds = 30 };

                    var reply = await _aiProvider.GenerateReplyAsync(userId, messages, ct, options);
                    var translations = ParseStringArrayJson(reply.Text);

                    if (translations.Count == batch.Count)
                    {
                        for (int i = 0; i < batch.Count; i++)
                        {
                            pendingTranslations.Add((batch[i], translations[i]));
                        }
                    }
                    else
                    {
                        _logger.LogWarning("OpenAI returned {Count} translations for {BatchCount} lines in lesson {Id}", translations.Count, batch.Count, lessonId);
                        return OperationResult.Invalid("AI trả về thiếu bản dịch. Chưa lưu thay đổi; vui lòng thử lại.");
                    }
                }

                foreach (var (line, translation) in pendingTranslations)
                    line.VietnameseMeaning = translation;
                await _context.SaveChangesAsync(ct);
                return OperationResult.Success();
            }
            catch (AiProviderException ex)
            {
                _logger.LogError(ex, "AiProvider error during translation for lesson {Id}", lessonId);
                return OperationResult.Invalid(AiProviderErrorMessages.For(ex.ErrorCode));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unknown error during translation for lesson {Id}", lessonId);
                return OperationResult.Invalid("Có lỗi xảy ra trong quá trình dịch bài nghe.");
            }
        }

        private List<string> ParseStringArrayJson(string jsonText)
        {
            var clean = jsonText.Replace("```json", "").Replace("```", "").Trim();
            try
            {
                return JsonSerializer.Deserialize<List<string>>(clean, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
