using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TCTVocabulary.Models;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Controllers
{
    [Authorize]
    public class SpeakingController : BaseController
    {
        private readonly DbflashcardContext _context;

        public SpeakingController(DbflashcardContext context)
        {
            _context = context;
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

            // 3. Kiểm tra câu tồn tại
            var sentenceExists = await _context.SpeakingSentences
                .AnyAsync(s => s.Id == sentenceId);

            if (!sentenceExists)
                return NotFound(new { error = "Câu không tồn tại." });

            // 4. Upsert — giữ điểm cao nhất
            var existing = await _context.UserSpeakingProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.SentenceId == sentenceId);

            if (existing != null)
            {
                // Chỉ cập nhật nếu điểm mới cao hơn
                if (dto.TotalScore > existing.TotalScore)
                {
                    existing.TotalScore = dto.TotalScore;
                    existing.AccuracyScore = dto.AccuracyScore;
                    existing.FluencyScore = dto.FluencyScore;
                    existing.CompletenessScore = dto.CompletenessScore;
                    existing.PracticedAt = DateTime.UtcNow;
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
                    PracticedAt = DateTime.UtcNow
                };
                _context.UserSpeakingProgresses.Add(progress);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                sentenceId,
                totalScore = dto.TotalScore,
                accuracyScore = dto.AccuracyScore,
                fluencyScore = dto.FluencyScore,
                completenessScore = dto.CompletenessScore
            });
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
