using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

namespace TCTVocabulary.Controllers
{
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
            int currentUserId = 1; // Tạm thời gán fix cứng
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
                !c.LearningProgresses.Any() ||
                c.LearningProgresses.Any(lp => lp.NextReviewDate == null || lp.NextReviewDate <= DateTime.Now));
            
            // Lấy streak từ bảng User
            var user = await _context.Users.FindAsync(currentUserId);
            ViewBag.Streak = user?.Streak ?? 0;
            ViewBag.LongestStreak = user?.LongestStreak ?? 0;

            return View(folders);
        }

        // GET: Vocabulary/Detail/5 - Chi tiết bộ từ vựng
        public async Task<IActionResult> Detail(int setId)
        {
            int currentUserId = 1;
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
                !c.LearningProgresses.Any() ||
                c.LearningProgresses.Any(lp => lp.NextReviewDate == null || lp.NextReviewDate <= DateTime.Now));

            return View(set);
        }

        // GET: Vocabulary/Topics/5 - Danh sách các chủ đề trong bộ từ
        public async Task<IActionResult> Topics(int setId)
        {
            int currentUserId = 1;
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
            int currentUserId = 1;
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
            int currentUserId = 1;
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

            // Nếu là review (SRS), chỉ lấy các thẻ đến hạn ôn tập hoặc chưa từng học
            if (isReview)
            {
                filteredCards = filteredCards
                    .Where(c => !c.LearningProgresses.Any() ||
                               c.LearningProgresses.Any(lp => lp.NextReviewDate == null || lp.NextReviewDate <= DateTime.Now))
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
            return View("Study", resultSet);
        }

        // GET: Vocabulary/FolderDetail/5 - Chi tiết Folder
        public async Task<IActionResult> FolderDetail(int folderId)
        {
            int currentUserId = 1;
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