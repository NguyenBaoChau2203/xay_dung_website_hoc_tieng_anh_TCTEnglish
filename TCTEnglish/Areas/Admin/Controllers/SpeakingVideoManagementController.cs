using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTVocabulary.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = Roles.Admin)]
    public class SpeakingVideoManagementController : Controller
    {
        private readonly DbflashcardContext _context;
        private readonly IYoutubeTranscriptService _transcriptService;
        private readonly ILogger<SpeakingVideoManagementController> _logger;

        public SpeakingVideoManagementController(
            DbflashcardContext context, 
            IYoutubeTranscriptService transcriptService,
            ILogger<SpeakingVideoManagementController> logger)
        {
            _context = context;
            _transcriptService = transcriptService;
            _logger = logger;
        }

        // GET: Admin/SpeakingVideoManagement
        public async Task<IActionResult> Index()
        {
            var videos = await _context.SpeakingVideos
                .AsNoTracking()
                .OrderByDescending(v => v.Id)
                .Select(v => new
                {
                    v.Id,
                    v.Title,
                    v.YoutubeId,
                    v.Level,
                    v.Topic,
                    v.ThumbnailUrl,
                    v.Duration,
                    SentenceCount = v.SpeakingSentences.Count
                })
                .ToListAsync();

            return View(videos);
        }

        // GET: Admin/SpeakingVideoManagement/Create
        public IActionResult Create()
        {
            // Populate ViewBag with available Playlists
            ViewBag.Playlists = _context.SpeakingPlaylists.AsNoTracking().ToList();
            return View();
        }

        // POST: Admin/SpeakingVideoManagement/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] string title, [FromForm] string youtubeId, [FromForm] string level, [FromForm] string topic, [FromForm] int playlistId)
        {
            if (string.IsNullOrWhiteSpace(youtubeId) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(level))
            {
                ModelState.AddModelError("", "Title, YouTube ID, and Level are required.");
                ViewBag.Playlists = await _context.SpeakingPlaylists.AsNoTracking().ToListAsync();
                return View();
            }

            // Simple validation for Youtube ID format (usually 11 chars)
            if (youtubeId.Contains("youtube.com") || youtubeId.Contains("youtu.be"))
            {
                ModelState.AddModelError("youtubeId", "Vui lòng chỉ nhập Video ID (ví dụ: dQw4w9WgXcQ), không nhập toàn bộ link.");
                ViewBag.Playlists = await _context.SpeakingPlaylists.AsNoTracking().ToListAsync();
                return View();
            }

            try
            {
                // Call Transcript Service to get sentences
                var sentences = await _transcriptService.GetTranscriptAsync(youtubeId);
                
                if (sentences == null || !sentences.Any())
                {
                    ModelState.AddModelError("", "Không thể lấy được phụ đề hoặc âm thanh từ video này. Vui lòng thử video khác.");
                    ViewBag.Playlists = await _context.SpeakingPlaylists.AsNoTracking().ToListAsync();
                    return View();
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                
                try
                {
                    // 1. Create the SpeakingVideo
                    var video = new SpeakingVideo
                    {
                        Title = title,
                        YoutubeId = youtubeId,
                        Level = level,
                        Topic = topic ?? "General",
                        PlaylistId = playlistId,
                        // Default thumbnail can be constructed via Youtube ID
                        ThumbnailUrl = $"https://img.youtube.com/vi/{youtubeId}/hqdefault.jpg"
                    };

                    _context.SpeakingVideos.Add(video);
                    await _context.SaveChangesAsync(); // Get the generated Video ID

                    // 2. Assign VideoId to all derived sentences and add them
                    foreach (var sentence in sentences)
                    {
                        sentence.VideoId = video.Id;
                        _context.SpeakingSentences.Add(sentence);
                    }

                    await _context.SaveChangesAsync();
                    
                    // Commit the transaction
                    await transaction.CommitAsync();
                    
                    TempData["SuccessMessage"] = $"Tạo video thành công và trích xuất được {sentences.Count} câu thoại.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Lỗi khi lưu video và phụ đề vào database.");
                    ModelState.AddModelError("", $"Lỗi hệ thống khi lưu: {ex.Message}");
                    ViewBag.Playlists = await _context.SpeakingPlaylists.AsNoTracking().ToListAsync();
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi service xử lý Youtube: {YoutubeId}", youtubeId);
                ModelState.AddModelError("", $"Lỗi khi trích xuất dữ liệu Youtube: {ex.Message}");
                ViewBag.Playlists = await _context.SpeakingPlaylists.AsNoTracking().ToListAsync();
                return View();
            }
        }
    }
}
