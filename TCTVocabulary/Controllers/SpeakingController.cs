using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            var playlists = await _context.SpeakingPlaylists
                .Include(p => p.SpeakingVideos)
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
                        Duration = v.Duration,
                        ThumbnailUrl = v.ThumbnailUrl ?? $"https://img.youtube.com/vi/{v.YoutubeId}/hqdefault.jpg",
                        SentenceCount = v.SpeakingSentences?.Count ?? 0
                    }).ToList()
                }).ToList()
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
