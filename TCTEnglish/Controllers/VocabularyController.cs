using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization; // [FIX-AI-AUTH]
using System.Security.Claims; // [FIX-AI-AUTH]
using TCTVocabulary.Models;

namespace TCTVocabulary.Controllers
{
    [Authorize] // [FIX-AI-AUTH]
    public class VocabularyController : Controller
    {
        private readonly DbflashcardContext _context;

        public VocabularyController(DbflashcardContext context)
        {
            _context = context;
        }

        // GET: Vocabulary/Index - Trang chủ danh sách Folder
        public async Task<IActionResult> Index()
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!); // [FIX-AI-AUTH]
            int sysId = SystemVocabularySeeder.GetSystemUserId(_context);

            var folders = await _context.Folders
                .Where(f => f.UserId == sysId)
                .Include(f => f.Sets)
                    .ThenInclude(s => s.Cards)
                        .ThenInclude(c => c.LearningProgresses.Where(lp => lp.UserId == currentUserId))
                .ToListAsync();

            // Tính toán thống kê thật
            var allCards = folders.SelectMany(f => f.Sets).SelectMany(s => s.Cards).ToList();
            ViewBag.TotalCards = allCards.Count;
            ViewBag.MasteredCards = allCards.Count(c => c.LearningProgresses.Any(lp => lp.Status == "Mastered"));
            ViewBag.DueToday = allCards.Count(c =>
                c.LearningProgresses.Any() &&
                c.LearningProgresses.Any(lp => lp.NextReviewDate != null && lp.NextReviewDate <= DateTime.UtcNow));
            
            // Lấy streak từ bảng User
            var user = await _context.Users.FindAsync(currentUserId);
            ViewBag.Streak = user?.Streak ?? 0;
            ViewBag.LongestStreak = user?.LongestStreak ?? 0;

            // [Feature: Continue_Learning] - Lấy set học gần nhất từ LearningProgress (chỉ lấy các set hệ thống)
            var lastProgress = await _context.LearningProgresses
                .Include(lp => lp.Card)
                    .ThenInclude(c => c.Set)
                .Where(lp => lp.UserId == currentUserId && lp.LastReviewedDate != null && lp.Card.Set.OwnerId == sysId)
                .OrderByDescending(lp => lp.LastReviewedDate)
                .FirstOrDefaultAsync();

            if (lastProgress?.Card?.Set != null)
            {
                var lastSet = lastProgress.Card.Set;
                var lastSetWithCards = await _context.Sets
                    .Include(s => s.Cards)
                        .ThenInclude(c => c.LearningProgresses.Where(lp => lp.UserId == currentUserId))
                    .FirstOrDefaultAsync(s => s.SetId == lastSet.SetId);
                ViewBag.LastStudiedSet = lastSetWithCards;
            }

            return View(folders);
        }

        // GET: Vocabulary/Detail/5 - Chi tiết bộ từ vựng
        public async Task<IActionResult> Detail(int setId)
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!); // [FIX-AI-AUTH]
            int sysId = SystemVocabularySeeder.GetSystemUserId(_context);

            var set = await _context.Sets
                .Include(s => s.Folder)
                    .ThenInclude(f => f.Sets)
                        .ThenInclude(rs => rs.Cards)
                .Include(s => s.Cards)
                    .ThenInclude(c => c.LearningProgresses.Where(lp => lp.UserId == currentUserId))
                .FirstOrDefaultAsync(s => s.SetId == setId && s.OwnerId == sysId);

            if (set == null) return NotFound();

            // Tính DueToday cho set này
            var detailCards = set.Cards?.ToList() ?? new List<Card>();
            ViewBag.DueToday = detailCards.Count(c =>
                c.LearningProgresses.Any() &&
                c.LearningProgresses.Any(lp => lp.NextReviewDate != null && lp.NextReviewDate <= DateTime.UtcNow));

            // [Feature: View_Count] - Tăng lượt truy cập khi vào Detail
            // [FIX-AI-AUTH] Chống spam ViewCount bằng Cookie
            string viewedCookieName = $"ViewedSet_{setId}"; // [FIX-AI-AUTH]
            if (!Request.Cookies.ContainsKey(viewedCookieName)) // [FIX-AI-AUTH]
            { // [FIX-AI-AUTH]
                set.ViewCount++; // [FIX-AI-AUTH]
                await _context.SaveChangesAsync(); // [FIX-AI-AUTH]
                Response.Cookies.Append(viewedCookieName, "true", new CookieOptions { Expires = DateTime.UtcNow.AddDays(1) }); // [FIX-AI-AUTH]
            } // [FIX-AI-AUTH]

            return View(set);
        }

        // GET: Vocabulary/Topics/5 - Danh sách các chủ đề trong bộ từ
        public async Task<IActionResult> Topics(int setId)
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!); // [FIX-AI-AUTH]
            int sysId = SystemVocabularySeeder.GetSystemUserId(_context);

            var set = await _context.Sets
                .Include(s => s.Cards)
                    .ThenInclude(c => c.LearningProgresses.Where(lp => lp.UserId == currentUserId))
                .FirstOrDefaultAsync(s => s.SetId == setId && s.OwnerId == sysId);

            if (set == null) return NotFound();

            return View(set);
        }

        // GET: Vocabulary/TopicDetail - Chi tiết một chủ đề
        public async Task<IActionResult> TopicDetail(int setId, string topic)
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!); // [FIX-AI-AUTH]
            int sysId = SystemVocabularySeeder.GetSystemUserId(_context);

            var set = await _context.Sets
                .Include(s => s.Cards)
                    .ThenInclude(c => c.LearningProgresses.Where(lp => lp.UserId == currentUserId))
                .FirstOrDefaultAsync(s => s.SetId == setId && s.OwnerId == sysId);

            if (set == null) return NotFound();

            ViewBag.TopicName = topic;
            return View(set);
        }

        // GET: Vocabulary/Study - Học từ vựng (có thể lọc theo topic)
        public async Task<IActionResult> Study(int setId, string topic = null, int index = 1, string mode = "all")
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!); // [FIX-AI-AUTH]
            int sysId = SystemVocabularySeeder.GetSystemUserId(_context);

            var set = await _context.Sets
                .Include(s => s.Cards)
                    .ThenInclude(c => c.LearningProgresses.Where(lp => lp.UserId == currentUserId))
                .FirstOrDefaultAsync(s => s.SetId == setId && s.OwnerId == sysId);

            if (set == null) return NotFound();

            bool isReview = mode == "review";

            // Nếu có topic, lọc cards theo topic
            var filteredCards = set.Cards?.ToList() ?? new List<Card>();

            if (!string.IsNullOrEmpty(topic))
            {
                filteredCards = filteredCards
                    .Where(c => (c.Topic ?? "Chưa phân loại") == topic)
                    .ToList();
            }

            // Nếu là review (SRS), chỉ lấy các thẻ đã học mà đến hạn ôn tập
            if (isReview)
            {
                filteredCards = filteredCards
                    .Where(c => c.LearningProgresses.Any() &&
                               c.LearningProgresses.Any(lp => lp.NextReviewDate != null && lp.NextReviewDate <= DateTime.UtcNow))
                    .ToList();
            }

            var resultSet = new Set
            {
                SetId = set.SetId,
                SetName = !string.IsNullOrEmpty(topic) ? $"{set.SetName} - {topic}" : set.SetName,
                Cards = filteredCards,
                Folder = set.Folder
            };

            ViewBag.CurrentIndex = index;
            ViewBag.TopicName = topic;
            ViewBag.IsReview = isReview;
            ViewBag.StudyMode = mode;
            ViewBag.StudyTotal = filteredCards.Count;

            // [Feature: View_Count] - Tăng lượt truy cập khi vào Study
            // [FIX-AI-AUTH] Chống spam ViewCount bằng Cookie
            string viewedCookieName = $"ViewedSet_{setId}"; // [FIX-AI-AUTH]
            if (!Request.Cookies.ContainsKey(viewedCookieName)) // [FIX-AI-AUTH]
            { // [FIX-AI-AUTH]
                set.ViewCount++; // [FIX-AI-AUTH]
                await _context.SaveChangesAsync(); // [FIX-AI-AUTH]
                Response.Cookies.Append(viewedCookieName, "true", new CookieOptions { Expires = DateTime.UtcNow.AddDays(1) }); // [FIX-AI-AUTH]
            } // [FIX-AI-AUTH]

            return View("Study", resultSet);
        }

        // GET: Vocabulary/FolderDetail/5 - Chi tiết Folder
        public async Task<IActionResult> FolderDetail(int folderId)
        {
            int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!); // [FIX-AI-AUTH]
            int sysId = SystemVocabularySeeder.GetSystemUserId(_context);

            var folder = await _context.Folders
                .Include(f => f.Sets)
                    .ThenInclude(s => s.Cards)
                        .ThenInclude(c => c.LearningProgresses.Where(lp => lp.UserId == currentUserId))
                .FirstOrDefaultAsync(f => f.FolderId == folderId && f.UserId == sysId);

            if (folder == null)
            {
                return NotFound();
            }

            ViewBag.FolderName = folder.FolderName;
            return View(folder.Sets);
        }
    }
}