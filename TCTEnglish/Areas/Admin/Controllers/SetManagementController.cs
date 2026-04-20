using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TCTVocabulary.Areas.Admin.ViewModels;
using TCTVocabulary.Models;

namespace TCTVocabulary.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = Roles.Admin)]
    public class SetManagementController : Controller
    {
        private readonly DbflashcardContext _context;
        private const int PageSize = 20;
        private const string SystemEmail = "system@tct.local";

        public SetManagementController(DbflashcardContext context)
        {
            _context = context;
        }

        // 1) GET Index
        public async Task<IActionResult> Index(string? q, int? folderId, string? ownerFilter, int page = 1)
        {
            var query = _context.Sets
                .Include(s => s.Owner)
                .Include(s => s.Folder)
                .AsNoTracking();

            // Filter Search Token
            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(s => s.SetName.Contains(q) || (s.Description != null && s.Description.Contains(q)));
            }

            // Filter Folder
            if (folderId.HasValue)
            {
                query = query.Where(s => s.FolderId == folderId);
            }

            // Filter Owner
            if (ownerFilter == "system")
            {
                query = query.Where(s => s.Owner.Email == SystemEmail);
            }
            else if (ownerFilter == "user")
            {
                query = query.Where(s => s.Owner.Email != SystemEmail);
            }

            var totalItems = await query.CountAsync();
            var sets = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(s => new SetListItemViewModel
                {
                    SetId = s.SetId,
                    SetName = s.SetName,
                    Description = s.Description,
                    OwnerName = s.Owner.FullName ?? s.Owner.Email,
                    OwnerEmail = s.Owner.Email,
                    OwnerId = s.OwnerId,
                    FolderName = s.Folder != null ? s.Folder.FolderName : null,
                    FolderId = s.FolderId,
                    CardCount = s.Cards.Count,
                    ViewCount = s.ViewCount,
                    CreatedAt = s.CreatedAt,
                    IsSystemSet = s.Owner.Email == SystemEmail
                })
                .ToListAsync();

            var folders = await _context.Folders
                .Include(f => f.User)
                .AsNoTracking()
                .Select(f => new FolderFilterOption
                {
                    FolderId = f.FolderId,
                    FolderName = f.FolderName,
                    OwnerName = f.User.FullName ?? f.User.Email
                })
                .ToListAsync();

            var model = new SetManagementIndexViewModel
            {
                Sets = sets,
                Folders = folders,
                SearchToken = q,
                FolderFilter = folderId,
                OwnerFilter = ownerFilter ?? "all",
                CurrentPage = page,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)PageSize)
            };

            return View(model);
        }

        // 2) GET Create
        public async Task<IActionResult> Create()
        {
            var folders = await _context.Folders
                .Include(f => f.User)
                .AsNoTracking()
                .Select(f => new FolderFilterOption
                {
                    FolderId = f.FolderId,
                    FolderName = f.FolderName,
                    OwnerName = f.User.FullName ?? f.User.Email
                })
                .ToListAsync();

            var users = await _context.Users
                .AsNoTracking()
                .Select(u => new OwnerOption
                {
                    UserId = u.UserId,
                    DisplayName = $"{(string.IsNullOrEmpty(u.FullName) ? "No Name" : u.FullName)} ({u.Email})"
                })
                .ToListAsync();

            var model = new SetCreateEditViewModel
            {
                AvailableFolders = folders,
                AvailableOwners = users
            };

            return View(model);
        }

        // 3) POST Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SetCreateEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Reload data for view
                model.AvailableFolders = await _context.Folders
                    .Include(f => f.User)
                    .AsNoTracking()
                    .Select(f => new FolderFilterOption
                    {
                        FolderId = f.FolderId,
                        FolderName = f.FolderName,
                        OwnerName = f.User.FullName ?? f.User.Email
                    })
                    .ToListAsync();

                model.AvailableOwners = await _context.Users
                    .AsNoTracking()
                    .Select(u => new OwnerOption
                    {
                        UserId = u.UserId,
                        DisplayName = $"{(string.IsNullOrEmpty(u.FullName) ? "No Name" : u.FullName)} ({u.Email})"
                    })
                    .ToListAsync();

                return View(model);
            }

            int ownerId = model.OwnerId ?? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var set = new Set
            {
                SetName = model.SetName,
                Description = model.Description,
                FolderId = model.FolderId,
                OwnerId = ownerId,
                CreatedAt = DateTime.UtcNow,
                ViewCount = 0
            };

            _context.Sets.Add(set);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã tạo bộ từ vựng mới thành công.";
            return RedirectToAction(nameof(Index));
        }

        // 4) GET Edit
        public async Task<IActionResult> Edit(int id)
        {
            var set = await _context.Sets
                .Include(s => s.Owner)
                .Include(s => s.Folder)
                .FirstOrDefaultAsync(s => s.SetId == id);

            if (set == null) return NotFound();

            var folders = await _context.Folders
                .Include(f => f.User)
                .AsNoTracking()
                .Select(f => new FolderFilterOption
                {
                    FolderId = f.FolderId,
                    FolderName = f.FolderName,
                    OwnerName = f.User.FullName ?? f.User.Email
                })
                .ToListAsync();

            var users = await _context.Users
                .AsNoTracking()
                .Select(u => new OwnerOption
                {
                    UserId = u.UserId,
                    DisplayName = $"{(string.IsNullOrEmpty(u.FullName) ? "No Name" : u.FullName)} ({u.Email})"
                })
                .ToListAsync();

            var model = new SetCreateEditViewModel
            {
                SetId = set.SetId,
                SetName = set.SetName,
                Description = set.Description,
                FolderId = set.FolderId,
                OwnerId = set.OwnerId,
                AvailableFolders = folders,
                AvailableOwners = users
            };

            return View(model);
        }

        // 5) POST Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SetCreateEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableFolders = await _context.Folders
                    .Include(f => f.User)
                    .AsNoTracking()
                    .Select(f => new FolderFilterOption
                    {
                        FolderId = f.FolderId,
                        FolderName = f.FolderName,
                        OwnerName = f.User.FullName ?? f.User.Email
                    })
                    .ToListAsync();

                model.AvailableOwners = await _context.Users
                    .AsNoTracking()
                    .Select(u => new OwnerOption
                    {
                        UserId = u.UserId,
                        DisplayName = $"{(string.IsNullOrEmpty(u.FullName) ? "No Name" : u.FullName)} ({u.Email})"
                    })
                    .ToListAsync();

                return View(model);
            }

            if (!model.SetId.HasValue) return BadRequest();

            var set = await _context.Sets.FirstOrDefaultAsync(s => s.SetId == model.SetId.Value);

            if (set == null) return NotFound();

            set.SetName = model.SetName;
            set.Description = model.Description;
            set.FolderId = model.FolderId;
            // KHÔNG đổi OwnerId theo yêu cầu

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã cập nhật bộ từ vựng thành công.";
            return RedirectToAction(nameof(Index));
        }

        // 6) POST Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var set = await _context.Sets
                .Include(s => s.Cards)
                .FirstOrDefaultAsync(s => s.SetId == id);

            if (set == null) return NotFound();

            var cardIds = set.Cards.Select(c => c.CardId).ToList();

            // Xóa LearningProgress liên quan đến các Card của Set này
            var progress = _context.LearningProgresses.Where(lp => cardIds.Contains(lp.CardId));
            _context.LearningProgresses.RemoveRange(progress);

            // Xóa Cards
            _context.Cards.RemoveRange(set.Cards);

            // Xóa Set
            _context.Sets.Remove(set);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa bộ từ vựng thành công.";
            return RedirectToAction(nameof(Index));
        }

        // 7) GET Cards
        public async Task<IActionResult> Cards(int id)
        {
            var set = await _context.Sets
                .Include(s => s.Cards)
                .Include(s => s.Folder)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SetId == id);

            if (set == null) return NotFound();

            var model = new SetCardsViewModel
            {
                SetId = set.SetId,
                SetName = set.SetName,
                FolderName = set.Folder?.FolderName,
                Cards = set.Cards
                    .OrderBy(c => c.CardId)
                    .Select(c => new CardItemViewModel
                    {
                        CardId = c.CardId,
                        Term = c.Term,
                        Definition = c.Definition,
                        ImageUrl = c.ImageUrl,
                        Phonetic = c.Phonetic,
                        Example = c.Example,
                        ExampleTranslation = c.ExampleTranslation,
                        Topic = c.Topic
                    })
                    .ToList()
            };

            return View(model);
        }

        // 8) POST DeleteCard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCard(int cardId, int setId)
        {
            var card = await _context.Cards.FirstOrDefaultAsync(c => c.CardId == cardId && c.SetId == setId);

            if (card == null) return NotFound();

            // Xóa LearningProgress liên quan đến Card
            var progress = _context.LearningProgresses.Where(lp => lp.CardId == cardId);
            _context.LearningProgresses.RemoveRange(progress);

            // Xóa Card
            _context.Cards.Remove(card);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa thẻ từ vựng thành công.";
            return RedirectToAction(nameof(Cards), new { id = setId });
        }

        // 9) GET AddCard
        public async Task<IActionResult> AddCard(int setId)
        {
            var set = await _context.Sets.AsNoTracking().FirstOrDefaultAsync(s => s.SetId == setId);
            if (set == null) return NotFound();

            var model = new CardCreateEditViewModel
            {
                SetId = setId,
                SetName = set.SetName
            };

            return View(model);
        }

        // 10) POST AddCard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCard(CardCreateEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var set = await _context.Sets.AsNoTracking().FirstOrDefaultAsync(s => s.SetId == model.SetId);
                model.SetName = set?.SetName;
                return View(model);
            }

            var setExists = await _context.Sets.AnyAsync(s => s.SetId == model.SetId);
            if (!setExists) return NotFound();

            var card = new Card
            {
                SetId = model.SetId,
                Term = model.Term,
                Definition = model.Definition,
                ImageUrl = model.ImageUrl,
                Phonetic = model.Phonetic,
                Example = model.Example,
                ExampleTranslation = model.ExampleTranslation,
                Topic = model.Topic
            };

            _context.Cards.Add(card);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm thẻ mới thành công.";
            return RedirectToAction(nameof(Cards), new { id = model.SetId });
        }

        // 11) GET EditCard
        public async Task<IActionResult> EditCard(int id)
        {
            var card = await _context.Cards
                .Include(c => c.Set)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CardId == id);

            if (card == null) return NotFound();

            var model = new CardCreateEditViewModel
            {
                CardId = card.CardId,
                SetId = card.SetId,
                Term = card.Term,
                Definition = card.Definition,
                ImageUrl = card.ImageUrl,
                Phonetic = card.Phonetic,
                Example = card.Example,
                ExampleTranslation = card.ExampleTranslation,
                Topic = card.Topic,
                SetName = card.Set.SetName
            };

            return View(model);
        }

        // 12) POST EditCard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCard(CardCreateEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var set = await _context.Sets.AsNoTracking().FirstOrDefaultAsync(s => s.SetId == model.SetId);
                model.SetName = set?.SetName;
                return View(model);
            }

            if (!model.CardId.HasValue) return BadRequest();

            var card = await _context.Cards.FirstOrDefaultAsync(c => c.CardId == model.CardId.Value);
            if (card == null) return NotFound();

            card.Term = model.Term;
            card.Definition = model.Definition;
            card.ImageUrl = model.ImageUrl;
            card.Phonetic = model.Phonetic;
            card.Example = model.Example;
            card.ExampleTranslation = model.ExampleTranslation;
            card.Topic = model.Topic;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã cập nhật thẻ thành công.";
            return RedirectToAction(nameof(Cards), new { id = model.SetId });
        }
    }
}
