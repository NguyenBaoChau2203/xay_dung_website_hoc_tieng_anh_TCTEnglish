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
        // GetIndexViewModelAsync
        // ----------------------------------------------------------------

        public async Task<ListeningIndexViewModel> GetIndexViewModelAsync(string? level, string? topic, int? userId = null)
        {
            var query = _context.ListeningLessons
                .AsNoTracking()
                .Where(l => l.OwnerUserId == null && l.IsPublished);

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

            var viewModel = new ListeningIndexViewModel
            {
                LessonsByLevel = cardsByLevel,
                Topics = allTopics,
                Levels = levelMetas,
                SelectedLevel = level,
                SelectedTopic = topic,
            };

            if (!userId.HasValue) return viewModel;

            var currentRole = await GetNormalizedRoleAsync(userId.Value);
            var isLocked = currentRole == Roles.Standard;
            var myLessons = await _context.ListeningLessons
                .AsNoTracking()
                .Where(l => l.OwnerUserId == userId.Value)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new ListeningLessonCardViewModel
                {
                    Id = l.Id,
                    Title = l.Title,
                    Level = l.Level,
                    Topic = l.Topic,
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
                .ToListAsync();

            viewModel.MyLessons = myLessons;
            viewModel.IsMyLessonsSectionLocked = isLocked;

            return viewModel;
        }

        // ----------------------------------------------------------------
        // GetPracticeViewModelAsync
        // ----------------------------------------------------------------

        public async Task<ListeningPracticeViewModel?> GetPracticeViewModelAsync(int lessonId, int? userId)
        {
            var role = userId.HasValue ? await GetNormalizedRoleAsync(userId.Value) : Roles.Standard;

            var lesson = await _context.ListeningLessons
                .AsNoTracking()
                .Where(l => l.Id == lessonId && l.IsPublished && (l.OwnerUserId == null || l.OwnerUserId == userId))
                .Include(l => l.TranscriptLines)
                .Include(l => l.QuizQuestions)
                .Include(l => l.VocabItems)
                .FirstOrDefaultAsync();

            if (lesson == null)
                return null;

            if (lesson.OwnerUserId == userId && role == Roles.Standard)
            {
                return null; // Premium locked
            }

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
                var wasCompleted = progress.CompletedAt != null;
                if (progress.TranscriptCompleted && progress.QuizCompleted && progress.VocabReviewed
                    && progress.CompletedAt == null)
                {
                    progress.CompletedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                var isNewCompletion = !wasCompleted && progress.CompletedAt != null;
                if (isNewCompletion)
                {
                    var activityUpdate = _goalsService.BuildListeningCompletionActivityUpdate();
                    await _goalsService.RecordLearningActivityAsync(userId, activityUpdate);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveProgressAsync failed for userId={UserId}, lessonId={LessonId}", userId, lessonId);
                return false;
            }
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

            // Auto-generate AI Quiz without blocking
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
                    MaxOutputTokens = 800,
                    Temperature = 0.3f,
                    RequestTimeoutSeconds = 20
                };

                var reply = await _aiProvider.GenerateReplyAsync(messages, ct, aiOptions);
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
                return OperationResult.Invalid("Lỗi khi kết nối với hệ thống AI sinh câu hỏi.");
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

            try
            {
                foreach (var batch in batches)
                {
                    var englishLines = string.Join("\n", batch.Select((l, i) => $"{i + 1}. \"{l.Text}\""));
                    var promptText = $@"Dịch các câu tiếng Anh sau sang tiếng Việt. Trả về JSON array, mỗi phần tử là bản dịch tương ứng theo đúng thứ tự.
Chỉ trả JSON array of strings, không giải thích thêm.

{englishLines}";

                    var messages = new List<AiContextMessage> { new AiContextMessage("user", promptText) };
                    var options = new AiProviderRequestOptions { MaxOutputTokens = 1500, Temperature = 0.2f, RequestTimeoutSeconds = 30 };

                    var reply = await _aiProvider.GenerateReplyAsync(messages, ct, options);
                    var translations = ParseStringArrayJson(reply.Text);

                    if (translations.Count == batch.Count)
                    {
                        for (int i = 0; i < batch.Count; i++)
                        {
                            batch[i].VietnameseMeaning = translations[i];
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Gemini returned {Count} translations for {BatchCount} lines in lesson {Id}", translations.Count, batch.Count, lessonId);
                    }
                }

                await _context.SaveChangesAsync(ct);
                return OperationResult.Success();
            }
            catch (AiProviderException ex)
            {
                _logger.LogError(ex, "AiProvider error during translation for lesson {Id}", lessonId);
                return OperationResult.Invalid("Lỗi kết nối tới hệ thống AI dịch vụ. Vui lòng thử lại sau.");
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
