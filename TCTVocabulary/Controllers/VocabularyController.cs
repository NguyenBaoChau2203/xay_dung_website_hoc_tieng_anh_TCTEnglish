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
            ViewBag.DueToday = allCards.Count(c => c.LearningProgresses.Any(lp => lp.NextReviewDate <= DateTime.Now));
            
            // Lấy streak từ bảng User
            var user = await _context.Users.FindAsync(currentUserId);
            ViewBag.Streak = user?.Streak ?? 0;

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
        public async Task<IActionResult> Study(int setId, string topic = null, int index = 1, bool review = false)
        {
            int currentUserId = 1;
            int sysId = SystemVocabularySeeder.GetSystemUserId(_context);

            var set = await _context.Sets
                .Include(s => s.Cards)
                    .ThenInclude(c => c.LearningProgresses.Where(lp => lp.UserId == currentUserId))
                .FirstOrDefaultAsync(s => s.SetId == setId && s.OwnerId == sysId);

            if (set == null) return NotFound();

            // Nếu có topic, lọc cards theo topic
            if (!string.IsNullOrEmpty(topic))
            {
                var cardsInTopic = set.Cards?
                    .Where(c => (c.Topic ?? "Chưa phân loại") == topic)
                    .ToList();

                // Nếu là review, chỉ lấy các thẻ đã học
                if (review && cardsInTopic != null)
                {
                    cardsInTopic = cardsInTopic
                        .Where(c => c.LearningProgresses != null &&
                                   c.LearningProgresses.Any(lp => lp.Status == "Learned" || lp.Status == "Reviewing" || lp.Status == "Mastered"))
                        .ToList();
                }

                var topicSet = new Set
                {
                    SetId = set.SetId,
                    SetName = $"{set.SetName} - {topic}",
                    Cards = cardsInTopic
                };

                ViewBag.CurrentIndex = index;
                ViewBag.TopicName = topic;
                ViewBag.IsReview = review;
                return View("Study", topicSet);
            }

            ViewBag.CurrentIndex = index;
            return View(set);
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