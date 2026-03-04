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

        // Bước 1: Trang chủ - Danh sách Folder (Index)
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

        // Bước 2: Detail trang từ vựng của User
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

        // Giữ lại Study phòng hờ User có chỗ nào còn link tới (Mặc định giờ UI trỏ đi Study/Index)
        public async Task<IActionResult> Study(int id)
        {
            int sysId = SystemVocabularySeeder.GetSystemUserId(_context);

            var set = await _context.Sets
                .Include(s => s.Cards)
                .FirstOrDefaultAsync(s => s.SetId == id && s.OwnerId == sysId);

            if (set == null) return NotFound();

            return View(set);
        }
    }
}
