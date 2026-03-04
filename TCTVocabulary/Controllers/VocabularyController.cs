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

        // GET: /Vocabulary/Index
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

        // GET: /Vocabulary/Detail/5
        public async Task<IActionResult> Detail(int setId)
        {
            int sysId = SystemVocabularySeeder.GetSystemUserId(_context);

            var set = await _context.Sets
                .Where(s => s.SetId == setId && s.OwnerId == sysId)
                .Include(s => s.Cards)
                .Include(s => s.Folder)
                .FirstOrDefaultAsync();

            if (set == null)
                return NotFound();

            return View(set);
        }
    }
}
