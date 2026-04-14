using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Controllers
{
    [Authorize]
    public class SpeakingController : BaseController
    {
        private const double SpeakingSentencePassScore = 70d;
        private const double SpeakingVideoCompletionRatio = 0.7d;

        private readonly DbflashcardContext _context;
        private readonly IGoalsService _goalsService;
        private readonly ILogger<SpeakingController> _logger;

        public SpeakingController(
            DbflashcardContext context,
            IGoalsService goalsService,
            ILogger<SpeakingController> logger)
        {
            _context = context;
            _goalsService = goalsService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Fetch all videos (Projected for optimization)
            var allVideos = await _context.SpeakingVideos
                .AsNoTracking()
                .Select(v => new SpeakingVideoViewModel
                {
                    Id = v.Id,
                    Title = v.Title,
                    YoutubeId = v.YoutubeId,
                    Level = v.Level,
                    Topic = v.Topic,
                    Duration = v.Duration,
                    ThumbnailUrl = v.ThumbnailUrl ?? $"https://img.youtube.com/vi/{v.YoutubeId}/hqdefault.jpg",
                    SentenceCount = v.SpeakingSentences.Count
                })
                .ToListAsync();

            // 2. Extract distinct topics directly from the database for optimization
            var topics = await _context.SpeakingVideos
                .Where(v => !string.IsNullOrEmpty(v.Topic))
                .Select(v => v.Topic)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            // 3. Group videos by Level (A1, A2, B1, B2, C1, C2) with consistent ordering
            var levelOrder = new[] { "A1", "A2", "B1", "B2", "C1", "C2" };
            var videosByLevel = new Dictionary<string, List<SpeakingVideoViewModel>>();

            foreach (var level in levelOrder)
            {
                var videosInLevel = allVideos
                    .Where(v => v.Level == level)
                    .ToList();

                if (videosInLevel.Any())
                {
                    videosByLevel[level] = videosInLevel;
                }
            }

            // 4. Also keep the existing Playlists data for backward compatibility
            var playlists = await _context.SpeakingPlaylists
                .AsNoTracking()
                .Select(p => new SpeakingPlaylistViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    ThumbnailUrl = p.ThumbnailUrl,
                    Videos = p.SpeakingVideos.Select(v => new SpeakingVideoViewModel
                    {
                        Id = v.Id,
                        Title = v.Title,
                        YoutubeId = v.YoutubeId,
                        Level = v.Level,
                        Topic = v.Topic,
                        Duration = v.Duration,
                        ThumbnailUrl = v.ThumbnailUrl ?? $"https://img.youtube.com/vi/{v.YoutubeId}/hqdefault.jpg",
                        SentenceCount = v.SpeakingSentences.Count
                    }).ToList()
                })
                .ToListAsync();

            var viewModel = new SpeakingIndexViewModel
            {
                Playlists = playlists,

                // Assign the new properties
                Topics = topics,
                VideosByLevel = videosByLevel
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Practice(int id)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
                return RedirectToAction("Login", "Account");

            var video = await _context.SpeakingVideos
                .AsNoTracking()
                .Include(v => v.SpeakingSentences)
                    .ThenInclude(s => s.UserSpeakingProgresses.Where(p => p.UserId == currentUserId)) // SECURE: Enforced UserId filter
                .FirstOrDefaultAsync(v => v.Id == id);

            if (video == null)
            {
                return NotFound();
            }

            var viewModel = new SpeakingPracticeViewModel
            {
                VideoId = video.Id,
                Title = video.Title,
                YoutubeId = video.YoutubeId,
                Sentences = video.SpeakingSentences
                    .OrderBy(s => s.StartTime)
                    .Select(s => 
                    {
                        var latestProgress = s.UserSpeakingProgresses
                            .OrderByDescending(p => p.PracticedAt)
                            .FirstOrDefault();

                        return new SpeakingSentenceViewModel
                        {
                            Id = s.Id,
                            StartTime = s.StartTime,
                            EndTime = s.EndTime,
                            Text = s.Text,
                            VietnameseMeaning = s.VietnameseMeaning,
                            TotalScore = latestProgress?.TotalScore,
                            AccuracyScore = latestProgress?.AccuracyScore,
                            FluencyScore = latestProgress?.FluencyScore,
                            CompletenessScore = latestProgress?.CompletenessScore
                        };
                    }).ToList()
            };

            return View(viewModel);
        }

        // ────────────────────────────────────────────────────────────
        //  API — Save Speaking Progress (Upsert: giữ điểm cao nhất)
        // ────────────────────────────────────────────────────────────
        [HttpPost("api/speaking/{sentenceId}/progress")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSpeakingProgress(int sentenceId, [FromBody] SpeakingProgressDto dto)
        {
            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { error = "Chưa đăng nhập." });

            // 2. Validate điểm số (0–100)
            if (dto.TotalScore < 0 || dto.TotalScore > 100 ||
                dto.AccuracyScore < 0 || dto.AccuracyScore > 100 ||
                dto.FluencyScore < 0 || dto.FluencyScore > 100 ||
                dto.CompletenessScore < 0 || dto.CompletenessScore > 100)
            {
                return BadRequest(new { error = "Điểm số phải từ 0 đến 100." });
            }

            // 3. Kiểm tra câu tồn tại và lấy VideoId
            var sentence = await _context.SpeakingSentences
                .AsNoTracking()
                .Where(s => s.Id == sentenceId)
                .Select(s => new { s.Id, s.VideoId })
                .FirstOrDefaultAsync();

            if (sentence == null)
                return NotFound(new { error = "Câu không tồn tại." });

            // 4. Upsert — giữ điểm cao nhất
            var existing = await _context.UserSpeakingProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.SentenceId == sentenceId);

            var utcNow = DateTime.UtcNow;

            if (existing != null)
            {
                // Chỉ cập nhật nếu điểm mới cao hơn
                if (dto.TotalScore > existing.TotalScore)
                {
                    existing.TotalScore = dto.TotalScore;
                    existing.AccuracyScore = dto.AccuracyScore;
                    existing.FluencyScore = dto.FluencyScore;
                    existing.CompletenessScore = dto.CompletenessScore;
                    existing.PracticedAt = utcNow;
                }
            }
            else
            {
                var progress = new UserSpeakingProgress
                {
                    UserId = userId,
                    SentenceId = sentenceId,
                    TotalScore = dto.TotalScore,
                    AccuracyScore = dto.AccuracyScore,
                    FluencyScore = dto.FluencyScore,
                    CompletenessScore = dto.CompletenessScore,
                    PracticedAt = utcNow
                };
                _context.UserSpeakingProgresses.Add(progress);
            }

            await _context.SaveChangesAsync();

            var totalSentenceCount = await _context.SpeakingSentences
                .AsNoTracking()
                .CountAsync(s => s.VideoId == sentence.VideoId);

            var requiredSentenceCount = Math.Max(1, (int)Math.Ceiling(totalSentenceCount * SpeakingVideoCompletionRatio));

            var passedSentenceCount = await _context.UserSpeakingProgresses
                .AsNoTracking()
                .Where(p => p.UserId == userId
                    && p.SpeakingSentence.VideoId == sentence.VideoId
                    && p.TotalScore >= SpeakingSentencePassScore)
                .Select(p => p.SentenceId)
                .Distinct()
                .CountAsync();

            var reachedVideoCompletion = passedSentenceCount >= requiredSentenceCount;
            var firstTimeVideoCompletion = await UpsertSpeakingVideoCompletionAsync(
                userId,
                sentence.VideoId,
                passedSentenceCount,
                requiredSentenceCount,
                reachedVideoCompletion,
                utcNow);

            var speakingXpEarned = 0;
            var currentStreak = 0;
            if (firstTimeVideoCompletion)
            {
                var speakingRewardUpdate = _goalsService.BuildSpeakingCompletionActivityUpdate();
                var rewardResult = await _goalsService.RecordLearningActivityAsync(userId, speakingRewardUpdate);
                if (rewardResult.Status == OperationStatus.Success)
                {
                    speakingXpEarned = speakingRewardUpdate.XpEarned;
                    currentStreak = rewardResult.Streak;
                }
                else
                {
                    _logger.LogWarning(
                        "Speaking completion reward failed for user {userId}, video {videoId}, status {status}",
                        userId,
                        sentence.VideoId,
                        rewardResult.Status);
                }
            }

            return Ok(new
            {
                success = true,
                sentenceId,
                videoId = sentence.VideoId,
                totalScore = dto.TotalScore,
                accuracyScore = dto.AccuracyScore,
                fluencyScore = dto.FluencyScore,
                completenessScore = dto.CompletenessScore,
                passedSentenceCount,
                requiredSentenceCount,
                isVideoCompleted = reachedVideoCompletion,
                firstTimeVideoCompletion,
                xpEarned = speakingXpEarned,
                streak = currentStreak
            });
        }

        private async Task<bool> UpsertSpeakingVideoCompletionAsync(
            int userId,
            int videoId,
            int completedSentenceCount,
            int requiredSentenceCount,
            bool reachedVideoCompletion,
            DateTime evaluatedAtUtc)
        {
            await EnsureSpeakingVideoCompletionRowAsync(userId, videoId, requiredSentenceCount, evaluatedAtUtc);

            if (reachedVideoCompletion)
            {
                var completionTransitionRows = await _context.UserSpeakingVideoCompletions
                    .Where(completion =>
                        completion.UserId == userId
                        && completion.VideoId == videoId
                        && !completion.IsCompleted)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(completion => completion.CompletedSentenceCount, _ => completedSentenceCount)
                        .SetProperty(completion => completion.RequiredSentenceCount, _ => requiredSentenceCount)
                        .SetProperty(completion => completion.LastEvaluatedAt, _ => evaluatedAtUtc)
                        .SetProperty(completion => completion.IsCompleted, _ => true)
                        .SetProperty(completion => completion.CompletedAt, _ => evaluatedAtUtc));

                if (completionTransitionRows > 0)
                {
                    return true;
                }
            }

            await _context.UserSpeakingVideoCompletions
                .Where(completion => completion.UserId == userId && completion.VideoId == videoId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(completion => completion.CompletedSentenceCount, _ => completedSentenceCount)
                    .SetProperty(completion => completion.RequiredSentenceCount, _ => requiredSentenceCount)
                    .SetProperty(completion => completion.LastEvaluatedAt, _ => evaluatedAtUtc));

            return false;
        }

        private async Task EnsureSpeakingVideoCompletionRowAsync(
            int userId,
            int videoId,
            int requiredSentenceCount,
            DateTime evaluatedAtUtc)
        {
            var completionExists = await _context.UserSpeakingVideoCompletions
                .AsNoTracking()
                .AnyAsync(completion => completion.UserId == userId && completion.VideoId == videoId);

            if (completionExists)
            {
                return;
            }

            var completion = new UserSpeakingVideoCompletion
            {
                UserId = userId,
                VideoId = videoId,
                CompletedSentenceCount = 0,
                RequiredSentenceCount = requiredSentenceCount,
                IsCompleted = false,
                LastEvaluatedAt = evaluatedAtUtc
            };

            _context.UserSpeakingVideoCompletions.Add(completion);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _context.Entry(completion).State = EntityState.Detached;
            }
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        {
            var sqlException = FindException<SqlException>(exception);
            if (sqlException is { Number: 2601 or 2627 })
            {
                return true;
            }

            var sqliteException = FindException(
                exception,
                candidate => string.Equals(
                    candidate.GetType().FullName,
                    "Microsoft.Data.Sqlite.SqliteException",
                    StringComparison.Ordinal));

            return sqliteException != null && GetExceptionIntProperty(sqliteException, "SqliteErrorCode") == 19;
        }

        private static TException? FindException<TException>(Exception exception)
            where TException : Exception
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is TException typedException)
                {
                    return typedException;
                }
            }

            return null;
        }

        private static Exception? FindException(Exception exception, Func<Exception, bool> predicate)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (predicate(current))
                {
                    return current;
                }
            }

            return null;
        }

        private static int? GetExceptionIntProperty(Exception exception, string propertyName)
        {
            var property = exception.GetType().GetProperty(propertyName);
            if (property?.PropertyType != typeof(int))
            {
                return null;
            }

            return (int?)property.GetValue(exception);
        }

    }

    // ── DTO ──────────────────────────────────────────────────────────
    public class SpeakingProgressDto
    {
        public double TotalScore { get; set; }
        public double AccuracyScore { get; set; }
        public double FluencyScore { get; set; }
        public double CompletenessScore { get; set; }
    }
}
