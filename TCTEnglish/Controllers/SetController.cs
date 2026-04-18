using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    [Route("Home")]
    public class SetController : BaseController
    {
        private readonly DbflashcardContext _context;
        private readonly IFileStorageService _fileStorageService;
        private readonly IVocabSuggestService _vocabSuggestService;
        private readonly IHttpClientFactory _httpClientFactory;

        public SetController(
            DbflashcardContext context, 
            IFileStorageService fileStorageService,
            IVocabSuggestService vocabSuggestService,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _fileStorageService = fileStorageService;
            _vocabSuggestService = vocabSuggestService;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("SuggestVocab")]
        public async Task<IActionResult> SuggestVocab(string term, CancellationToken ct)
        {
            if (!(User.IsInRole(Roles.Premium) || User.IsInRole(Roles.Admin)))
            {
                return StatusCode(403, "Tính năng Gợi ý từ vựng chỉ dành cho tài khoản Premium.");
            }

            if (string.IsNullOrWhiteSpace(term))
            {
                return BadRequest("Vui lòng cung cấp từ khóa.");
            }

            var result = await _vocabSuggestService.SuggestAsync(term, ct);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage ?? "Không thể tìm thấy gợi ý.");
            }

            return Json(result);
        }

        [HttpGet("CreateSet")]
        public async Task<IActionResult> CreateSet(int? folderId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new SetEditorViewModel
            {
                FolderId = folderId
            };

            model.EnsureCardSlot();
            return View(model);
        }

        [HttpPost("CreateSet")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSet(SetEditorViewModel model)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (model.FolderId.HasValue)
            {
                var folderExists = await GetManageableFoldersQuery(userId)
                    .AsNoTracking()
                    .AnyAsync(f => f.FolderId == model.FolderId.Value);

                if (!folderExists)
                {
                    return NotFound();
                }
            }

            PopulateCardsFromLegacyRequest(model);
            var cardInputs = NormalizeAndValidateEditor(model);
            if (!ModelState.IsValid)
            {
                model.EnsureCardSlot();
                return View(model);
            }

            var newSet = new Set
            {
                SetName = model.SetName,
                FolderId = model.FolderId,
                OwnerId = userId,
                Description = model.Description,
                CreatedAt = DateTime.UtcNow
            };

            await AddCardsAsync(newSet, cardInputs);
            _context.Sets.Add(newSet);
            await _context.SaveChangesAsync();

            return model.FolderId.HasValue
                ? RedirectToAction(nameof(FolderController.FolderDetail), "Folder", new { id = model.FolderId.Value })
                : RedirectToAction("Folder", "Folder");
        }

        [HttpPost("DeleteSet")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSet(int setId, int folderId)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            var set = await GetManageableSetsQuery(currentUserId)
                .Include(s => s.Cards)
                .FirstOrDefaultAsync(s => s.SetId == setId);

            if (set == null)
            {
                return NotFound();
            }

            if (set.Cards.Count != 0)
            {
                _context.Cards.RemoveRange(set.Cards);
            }

            _context.Sets.Remove(set);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(FolderController.FolderDetail), "Folder", new { id = folderId });
        }

        [HttpPost("RemoveSetFromFolder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveSetFromFolder(int setId, int folderId)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            var set = await GetManageableSetsQuery(currentUserId)
                .Include(s => s.Cards)
                .FirstOrDefaultAsync(s => s.SetId == setId && s.FolderId == folderId);

            if (set == null)
            {
                return NotFound();
            }

            if (set.Cards.Count != 0)
            {
                _context.Cards.RemoveRange(set.Cards);
            }

            _context.Sets.Remove(set);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(FolderController.FolderDetail), "Folder", new { id = folderId });
        }

        [HttpGet("EditSet/{id:int}")]
        public async Task<IActionResult> EditSet(int id)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            var set = await GetManageableSetsQuery(currentUserId)
                .AsNoTracking()
                .Where(s => s.SetId == id)
                .Select(s => new SetEditorViewModel
                {
                    SetId = s.SetId,
                    FolderId = s.FolderId,
                    SetName = s.SetName,
                    Description = s.Description,
                    Cards = s.Cards
                        .OrderBy(c => c.CardId)
                        .Select(c => new SetCardEditorItemViewModel
                        {
                            Term = c.Term,
                            Definition = c.Definition,
                            ExistingImageUrl = c.ImageUrl
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (set == null)
            {
                return NotFound();
            }

            set.EnsureCardSlot();
            return View(set);
        }

        [HttpPost("EditSet")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSet(SetEditorViewModel model)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            if (!model.SetId.HasValue)
            {
                return NotFound();
            }

            var existingSet = await GetManageableSetsQuery(currentUserId)
                .Include(s => s.Cards)
                .FirstOrDefaultAsync(s => s.SetId == model.SetId.Value);

            if (existingSet == null)
            {
                return NotFound();
            }

            model.FolderId = existingSet.FolderId;
            PopulateCardsFromLegacyRequest(model);
            var cardInputs = NormalizeAndValidateEditor(model);
            if (!ModelState.IsValid)
            {
                model.EnsureCardSlot();
                return View(model);
            }

            existingSet.SetName = model.SetName;
            existingSet.Description = model.Description;

            var oldCardIds = existingSet.Cards.Select(c => c.CardId).ToList();
            if (oldCardIds.Count != 0)
            {
                var relatedProgresses = await _context.LearningProgresses
                    .Where(lp => oldCardIds.Contains(lp.CardId))
                    .ToListAsync();

                _context.LearningProgresses.RemoveRange(relatedProgresses);
            }

            _context.Cards.RemoveRange(existingSet.Cards);

            await AddCardsAsync(existingSet, cardInputs);
            await _context.SaveChangesAsync();

            return existingSet.FolderId.HasValue
                ? RedirectToAction(nameof(FolderController.FolderDetail), "Folder", new { id = existingSet.FolderId.Value })
                : RedirectToAction("Folder", "Folder");
        }

        private async Task AddCardsAsync(Set targetSet, IReadOnlyCollection<SetCardEditorItemViewModel> cards)
        {
            foreach (var card in cards)
            {
                var imageUrl = card.ExistingImageUrl;
                if (card.ImageFile != null && card.ImageFile.Length > 0)
                {
                    imageUrl = await SaveCardImageAsync(card.ImageFile);
                }
                else if (!string.IsNullOrWhiteSpace(imageUrl) && imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    // It's an external URL (e.g. from Pixabay), download and save
                    var localPath = await DownloadAndSaveExternalImageAsync(imageUrl);
                    if (localPath != null)
                    {
                        imageUrl = localPath;
                    }
                }

                targetSet.Cards.Add(new Card
                {
                    Term = card.Term,
                    Definition = card.Definition,
                    ImageUrl = imageUrl
                });
            }
        }

        private List<SetCardEditorItemViewModel> NormalizeAndValidateEditor(SetEditorViewModel model)
        {
            model.SetName = model.SetName?.Trim() ?? string.Empty;
            model.Description = NormalizeDescription(model.Description);
            model.Cards ??= new List<SetCardEditorItemViewModel>();

            if (string.IsNullOrWhiteSpace(model.SetName))
            {
                ModelState.AddModelError(nameof(model.SetName), "Vui lòng nhập tiêu đề học phần.");
            }

            var validCards = new List<SetCardEditorItemViewModel>();

            for (var i = 0; i < model.Cards.Count; i++)
            {
                var card = model.Cards[i] ?? new SetCardEditorItemViewModel();
                card.Term = card.Term?.Trim() ?? string.Empty;
                card.Definition = card.Definition?.Trim() ?? string.Empty;
                card.ExistingImageUrl = NormalizeDescription(card.ExistingImageUrl);
                model.Cards[i] = card;

                var hasTerm = !string.IsNullOrWhiteSpace(card.Term);
                var hasDefinition = !string.IsNullOrWhiteSpace(card.Definition);
                var hasAnyInput = hasTerm || hasDefinition || card.ImageFile != null || !string.IsNullOrWhiteSpace(card.ExistingImageUrl);

                if (!hasAnyInput)
                {
                    continue;
                }

                if (!hasTerm)
                {
                    ModelState.AddModelError($"Cards[{i}].Term", $"Thẻ {i + 1}: vui lòng nhập thuật ngữ.");
                }

                if (!hasDefinition)
                {
                    ModelState.AddModelError($"Cards[{i}].Definition", $"Thẻ {i + 1}: vui lòng nhập định nghĩa.");
                }

                if (hasTerm && hasDefinition)
                {
                    validCards.Add(card);
                }
            }

            if (validCards.Count == 0)
            {
                ModelState.AddModelError(nameof(model.Cards), "Vui lòng thêm ít nhất một thẻ hoàn chỉnh.");
            }

            return validCards;
        }

        private void PopulateCardsFromLegacyRequest(SetEditorViewModel model)
        {
            model.Cards ??= new List<SetCardEditorItemViewModel>();

            var hasModernCardInput = model.Cards.Any(card =>
                card != null &&
                (!string.IsNullOrWhiteSpace(card.Term)
                 || !string.IsNullOrWhiteSpace(card.Definition)
                 || card.ImageFile != null
                 || !string.IsNullOrWhiteSpace(card.ExistingImageUrl)));

            if (hasModernCardInput)
            {
                return;
            }

            var legacyTerms = Request.Form["Terms"];
            var legacyDefinitions = Request.Form["Definitions"];
            var legacyImageUrls = Request.Form["ExistingImageUrls"];
            var totalCards = Math.Max(legacyTerms.Count, legacyDefinitions.Count);
            totalCards = Math.Max(totalCards, legacyImageUrls.Count);

            if (totalCards == 0)
            {
                return;
            }

            model.Cards = new List<SetCardEditorItemViewModel>(totalCards);

            for (var i = 0; i < totalCards; i++)
            {
                model.Cards.Add(new SetCardEditorItemViewModel
                {
                    Term = i < legacyTerms.Count ? legacyTerms[i]?.ToString() ?? string.Empty : string.Empty,
                    Definition = i < legacyDefinitions.Count ? legacyDefinitions[i]?.ToString() ?? string.Empty : string.Empty,
                    ExistingImageUrl = i < legacyImageUrls.Count ? legacyImageUrls[i]?.ToString() : null,
                    ImageFile = Request.Form.Files[$"ImageFile_{i}"]
                });
            }
        }

        private Task<string> SaveCardImageAsync(IFormFile imageFile)
        {
            return _fileStorageService.SaveImageAsync(imageFile, ImageUploadPolicies.CardImage);
        }

        private async Task<string?> DownloadAndSaveExternalImageAsync(string externalUrl)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var imageBytes = await client.GetByteArrayAsync(externalUrl);
                
                var ext = ".jpg";
                var fileName = Guid.NewGuid().ToString("N") + ext;
                
                var relativePath = $"/images/cards/{fileName}";
                var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "cards", fileName);
                
                var dir = Path.GetDirectoryName(absolutePath);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await System.IO.File.WriteAllBytesAsync(absolutePath, imageBytes);
                return relativePath;
            }
            catch
            {
                return null;
            }
        }

        private static string? NormalizeDescription(string? description)
        {
            return string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim();
        }

        private IQueryable<Folder> GetManageableFoldersQuery(int currentUserId)
        {
            var folders = _context.Folders.AsQueryable();

            return IsAdminUser()
                ? folders
                : folders.Where(f => f.UserId == currentUserId);
        }

        private IQueryable<Set> GetManageableSetsQuery(int currentUserId)
        {
            var sets = _context.Sets.AsQueryable();

            return IsAdminUser()
                ? sets
                : sets.Where(s => s.OwnerId == currentUserId);
        }
    }
}
