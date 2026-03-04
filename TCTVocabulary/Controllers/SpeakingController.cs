using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TCTVocabulary.Models;
using TCTVocabulary.ViewModel;

namespace TCTVocabulary.Controllers
{
    public class SpeakingController : Controller
    {
        private readonly DbflashcardContext _context;

        public SpeakingController(DbflashcardContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Fetch all videos (with sentences for count)
            var allVideos = await _context.SpeakingVideos
                .Include(v => v.SpeakingSentences)
                .ToListAsync();

            // 2. Extract distinct topics for the filter UI
            var topics = allVideos
                .Where(v => !string.IsNullOrEmpty(v.Topic))
                .Select(v => v.Topic)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            // 3. Group videos by Level (A1, A2, B1, B2) with consistent ordering
            var levelOrder = new[] { "A1", "A2", "B1", "B2" };
            var videosByLevel = new Dictionary<string, List<SpeakingVideoViewModel>>();

            foreach (var level in levelOrder)
            {
                var videosInLevel = allVideos
                    .Where(v => v.Level == level)
                    .Select(v => new SpeakingVideoViewModel
                    {
                        Id = v.Id,
                        Title = v.Title,
                        YoutubeId = v.YoutubeId,
                        Level = v.Level,
                        Topic = v.Topic,
                        Duration = v.Duration,
                        ThumbnailUrl = v.ThumbnailUrl ?? $"https://img.youtube.com/vi/{v.YoutubeId}/hqdefault.jpg",
                        SentenceCount = v.SpeakingSentences?.Count ?? 0
                    })
                    .ToList();

                if (videosInLevel.Any())
                {
                    videosByLevel[level] = videosInLevel;
                }
            }

            // 4. Also keep the existing Playlists data for backward compatibility
            var playlists = await _context.SpeakingPlaylists
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
            var video = await _context.SpeakingVideos
                .Include(v => v.SpeakingSentences)
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
                    .Select(s => new SpeakingSentenceViewModel
                    {
                        Id = s.Id,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        Text = s.Text,
                        VietnameseMeaning = s.VietnameseMeaning
                    }).ToList()
            };

            return View(viewModel);
        }
    }
}
