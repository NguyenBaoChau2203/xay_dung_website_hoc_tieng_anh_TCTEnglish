using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Controllers
{
    [AutoValidateAntiforgeryToken]
    [Route("Home")]
    public class FolderController : BaseController
    {
        private readonly DbflashcardContext _context;

        public FolderController(DbflashcardContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpGet("Folder")]
        [ActionName("Folder")]
        public async Task<IActionResult> Index()
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            var vm = await BuildFolderPageViewModelAsync(currentUserId);
            return View("Folder", vm);
        }

        [Authorize]
        [HttpGet("FolderDetail/{id:int?}")]
        public async Task<IActionResult> FolderDetail(int id)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var vm = await BuildFolderDetailViewModelAsync(id, userId);
            if (vm == null)
            {
                return NotFound();
            }

            return View(nameof(FolderDetail), vm);
        }

        [Authorize]
        [HttpPost("CreateFolder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFolder(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return RedirectToAction("Folder", "Folder");
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var folder = new Folder
            {
                FolderName = folderName.Trim(),
                UserId = userId,
                ParentFolderId = null
            };

            _context.Folders.Add(folder);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(FolderDetail), "Folder", new { id = folder.FolderId });
        }

        [Authorize]
        [HttpPost("DeleteFolder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFolder(int id)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            var folder = await GetManageableFoldersQuery(currentUserId)
                .Include(f => f.Sets)
                .FirstOrDefaultAsync(f => f.FolderId == id);

            if (folder == null)
            {
                return NotFound();
            }

            _context.Folders.Remove(folder);
            await _context.SaveChangesAsync();

            return RedirectToAction("Folder", "Folder");
        }

        [Authorize]
        [HttpPost("UpdateFolderName")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateFolderName(int folderId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                return RedirectToAction(nameof(FolderDetail), "Folder", new { id = folderId });
            }

            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            var folder = await GetManageableFoldersQuery(currentUserId)
                .FirstOrDefaultAsync(f => f.FolderId == folderId);

            if (folder == null)
            {
                return NotFound();
            }

            folder.FolderName = newName.Trim();
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(FolderDetail), "Folder", new { id = folderId });
        }

        [Authorize]
        [HttpPost("SaveFolder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveFolder(int folderId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var folderExists = await _context.Folders
                .AsNoTracking()
                .AnyAsync(f => f.FolderId == folderId);

            if (!folderExists)
            {
                return NotFound();
            }

            var existed = await _context.SavedFolders
                .AsNoTracking()
                .AnyAsync(sf => sf.UserId == userId && sf.FolderId == folderId);

            if (existed)
            {
                return RedirectToAction(nameof(FolderDetail), "Folder", new { id = folderId });
            }

            _context.SavedFolders.Add(new SavedFolder
            {
                UserId = userId,
                FolderId = folderId
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(FolderDetail), "Folder", new { id = folderId });
        }

        [Authorize]
        [HttpPost("UnsaveFolder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnsaveFolder(int folderId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var saved = await _context.SavedFolders
                .FirstOrDefaultAsync(sf => sf.UserId == userId && sf.FolderId == folderId);

            if (saved != null)
            {
                _context.SavedFolders.Remove(saved);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(FolderDetail), "Folder", new { id = folderId });
        }

        private async Task<FolderDetailViewModel?> BuildFolderDetailViewModelAsync(int folderId, int userId)
        {
            var folder = await _context.Folders
                .AsNoTracking()
                .Where(f => f.FolderId == folderId)
                .Select(f => new FolderSummaryViewModel
                {
                    FolderId = f.FolderId,
                    UserId = f.UserId,
                    FolderName = f.FolderName,
                    CreatorName = f.User.FullName ?? string.Empty
                })
                .FirstOrDefaultAsync();

            if (folder == null)
            {
                return null;
            }

            var sets = await _context.Sets
                .AsNoTracking()
                .Where(s => s.FolderId == folderId)
                .OrderByDescending(s => s.SetId)
                .Select(s => new FolderSetItemViewModel
                {
                    SetId = s.SetId,
                    SetName = s.SetName,
                    CardCount = s.Cards.Count
                })
                .ToListAsync();

            var isOwner = folder.UserId == userId;
            var isSaved = await _context.SavedFolders
                .AsNoTracking()
                .AnyAsync(sf => sf.UserId == userId && sf.FolderId == folderId);

            return new FolderDetailViewModel
            {
                Folder = folder,
                Sets = sets,
                CurrentUserId = userId,
                IsSaved = isSaved,
                IsOwner = isOwner,
                CanManage = isOwner || IsAdminUser()
            };
        }

        private async Task<FolderPageViewModel> BuildFolderPageViewModelAsync(int currentUserId)
        {
            var myFolders = await _context.Folders
                .AsNoTracking()
                .Where(f => f.UserId == currentUserId && f.ParentFolderId == null)
                .OrderByDescending(f => f.FolderId)
                .Select(f => new FolderCardViewModel
                {
                    FolderId = f.FolderId,
                    FolderName = f.FolderName,
                    SetCount = f.Sets.Count,
                    CreatorName = "You"
                })
                .ToListAsync();

            var savedFolders = await _context.SavedFolders
                .AsNoTracking()
                .Where(sf => sf.UserId == currentUserId && sf.Folder != null)
                .OrderByDescending(sf => sf.FolderId)
                .Select(sf => new FolderCardViewModel
                {
                    FolderId = sf.Folder!.FolderId,
                    FolderName = sf.Folder.FolderName,
                    SetCount = sf.Folder.Sets.Count,
                    CreatorName = sf.Folder.User.FullName ?? "User"
                })
                .ToListAsync();

            return new FolderPageViewModel
            {
                MyFolders = myFolders,
                SavedFolders = savedFolders
            };
        }

        private IQueryable<Folder> GetManageableFoldersQuery(int currentUserId)
        {
            var folders = _context.Folders.AsQueryable();

            return IsAdminUser()
                ? folders
                : folders.Where(f => f.UserId == currentUserId);
        }
    }
}
