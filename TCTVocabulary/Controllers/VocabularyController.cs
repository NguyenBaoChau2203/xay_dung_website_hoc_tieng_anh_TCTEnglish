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
            int sysId = SystemVocabularySeeder.GetSystemUserId(_context);

            var folders = await _context.Folders
                .Where(f => f.UserId == sysId)
                .Include(f => f.Sets)
                    .ThenInclude(s => s.Cards)
                .ToListAsync();

            return View(folders);
        }

        // GET: Vocabulary/Detail/5 - Chi tiết bộ từ vựng
        public async Task<IActionResult> Detail(int setId)
        {
            int sysId = SystemVocabularySeeder.GetSystemUserId(_context);

            var set = await _context.Sets
                .Include(s => s.Folder)
                    .ThenInclude(f => f.Sets)
                        .ThenInclude(rs => rs.Cards)
                .Include(s => s.Cards)
                    .ThenInclude(c => c.LearningProgresses)
                .FirstOrDefaultAsync(s => s.SetId == setId && s.OwnerId == sysId);

            if (set == null) return NotFound();

            return View(set);
        }

        // GET: Vocabulary/Topics/5 - Danh sách các chủ đề trong bộ từ
        public async Task<IActionResult> Topics(int setId)
        {
            int sysId = SystemVocabularySeeder.GetSystemUserId(_context);

            var set = await _context.Sets
                .Include(s => s.Cards)
                    .ThenInclude(c => c.LearningProgresses)
                .FirstOrDefaultAsync(s => s.SetId == setId && s.OwnerId == sysId);

            if (set == null) return NotFound();

            return View(set);
        }

        // GET: Vocabulary/TopicDetail - Chi tiết một chủ đề
        public async Task<IActionResult> TopicDetail(int setId, string topic)
        {
            int sysId = SystemVocabularySeeder.GetSystemUserId(_context);

            var set = await _context.Sets
                .Include(s => s.Cards)
                    .ThenInclude(c => c.LearningProgresses)
                .FirstOrDefaultAsync(s => s.SetId == setId && s.OwnerId == sysId);

            if (set == null) return NotFound();

            ViewBag.TopicName = topic;
            return View(set);
        }

        // GET: Vocabulary/Study - Học từ vựng (có thể lọc theo topic)
        public async Task<IActionResult> Study(int setId, string topic = null, int index = 1, bool review = false)
        {
            int sysId = SystemVocabularySeeder.GetSystemUserId(_context);

            var set = await _context.Sets
                .Include(s => s.Cards)
                    .ThenInclude(c => c.LearningProgresses)
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
            int sysId = SystemVocabularySeeder.GetSystemUserId(_context);

            var folder = await _context.Folders
                .Include(f => f.Sets)
                    .ThenInclude(s => s.Cards)
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