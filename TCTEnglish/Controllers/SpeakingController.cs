using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TCTVocabulary.Models;
using TCTVocabulary.ViewModel;

namespace TCTVocabulary.Controllers
{
    [Authorize]
    public class SpeakingController : Controller
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
                .Include(p => p.SpeakingVideos)
                    .ThenInclude(v => v.SpeakingSentences)
                .ToListAsync();

            var viewModel = new SpeakingIndexViewModel
            {
                Playlists = playlists.Select(p => new SpeakingPlaylistViewModel
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
                        SentenceCount = v.SpeakingSentences?.Count ?? 0
                    }).ToList()
                }).ToList(),

                // Assign the new properties
                Topics = topics,
                VideosByLevel = videosByLevel
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Practice(int id)
        {
            // FIX: Use TryParse instead of Parse to avoid InvalidOperationException if claim is missing
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int currentUserId))
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
    }
}
