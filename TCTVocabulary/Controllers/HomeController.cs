using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using TCTVocabulary.Models;
using TCTVocabulary.Models.ViewModels;

namespace TCTVocabulary.Controllers
{
    public class HomeController : Controller
    {
        private readonly DbflashcardContext _context;

        public HomeController(DbflashcardContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Folder()
        {
            var myFolders = _context.Folders
                                    .Where(f => f.UserId == 1 && f.ParentFolderId == null)
                                    .ToList();

            return View(myFolders);
        }
        [HttpPost]
        public IActionResult CreateFolder(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return RedirectToAction("Index");
            }

            var folder = new Folder
            {
                FolderName = folderName,
                UserId = 1,
                ParentFolderId = null
            };

            _context.Folders.Add(folder);
            _context.SaveChanges();

            return RedirectToAction(
     "FolderDetail",
     "Home",
     new { id = folder.FolderId });
        }
        public IActionResult FolderDetail(int id)
        {
            var folder = _context.Folders
                .Include(f => f.Sets)
                    .ThenInclude(s => s.Cards)
                .FirstOrDefault(f => f.FolderId == id);

            if (folder == null)
                return NotFound();

            var vm = new FolderDetailViewModel
            {
                Folder = folder,
                Sets = folder.Sets.ToList()
            };

            return View(vm);
        }
        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Login()
        {
            return View();
        }

        public IActionResult Register()
        {
            return View();
        }
        public IActionResult Landing() 
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
