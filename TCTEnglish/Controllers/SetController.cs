using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services.Sets;
using TCTEnglish.ViewModels.Sets;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Controllers
{
    /// <summary>
    /// Legacy /Home set routes (kept contract-compatible) live here; the personal-set workspace routes
    /// (/Sets, /s/{slug}) are in SetController.Personal.cs.
    /// </summary>
    [Authorize]
    [AutoValidateAntiforgeryToken]
    [Route("Home")]
    public partial class SetController : BaseController
    {
        private readonly DbflashcardContext _context;
        private readonly IFileStorageService _fileStorageService;
        private readonly IVocabSuggestService _vocabSuggestService;
        private readonly IPersonalSetService _personalSets;

        public SetController(
            DbflashcardContext context,
            IFileStorageService fileStorageService,
            IVocabSuggestService vocabSuggestService,
            IPersonalSetService personalSets)
        {
            _context = context;
            _fileStorageService = fileStorageService;
            _vocabSuggestService = vocabSuggestService;
            _personalSets = personalSets;
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
        public async Task<IActionResult> CreateSet(int? folderId, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            // A folder the user cannot manage is ignored here; the POST still rejects it with 404.
            var form = await _personalSets.GetNewSetFormAsync(userId, IsAdminUser(), folderId, ct)
                ?? await _personalSets.GetNewSetFormAsync(userId, IsAdminUser(), null, ct);
            return View(form);
        }

        [HttpPost("CreateSet")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSet(SetEditorViewModel model, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (model.FolderId.HasValue)
            {
                var folderExists = await GetManageableFoldersQuery(userId)
                    .AsNoTracking()
                    .AnyAsync(f => f.FolderId == model.FolderId.Value, ct);

                if (!folderExists)
                {
                    return NotFound();
                }
            }

            PopulateCardsFromLegacyRequest(model);
            var cardInputs = NormalizeAndValidateEditor(model);
            if (!ModelState.IsValid)
            {
                return View(await BuildInfoFormAsync(model, userId, ct));
            }

            var now = DateTime.UtcNow;
            var newSet = new Set
            {
                SetName = model.SetName,
                FolderId = model.FolderId,
                OwnerId = userId,
                Description = model.Description,
                // Learner-created sets are private by default; the model default (Public) is only for seeded/imported content.
                Visibility = TCTEnglish.Services.Sets.SetVisibility.Private,
                CreatedAt = now,
                UpdatedAt = now
            };

            try
            {
                await AddCardsAsync(newSet, cardInputs, now, ct);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.Cards), ex.Message);
                return View(await BuildInfoFormAsync(model, userId, ct));
            }

            _context.Sets.Add(newSet);
            await _context.SaveChangesAsync(ct);

            return model.FolderId.HasValue
                ? RedirectToAction(nameof(FolderController.FolderDetail), "Folder", new { id = model.FolderId.Value })
                : RedirectToAction("Folder", "Folder");
        }

        [HttpPost("DeleteSet")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSet(int setId, int folderId, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            // Progress -> cards -> subscriptions -> set, in one transaction (B2).
            var result = await _personalSets.DeleteSetAsync(setId, currentUserId, IsAdminUser(), ct: ct);
            if (result.Status != OperationStatus.Success)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(FolderController.FolderDetail), "Folder", new { id = folderId });
        }

        [HttpPost("RemoveSetFromFolder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveSetFromFolder(int setId, int folderId, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            var inFolder = await GetManageableSetsQuery(currentUserId)
                .AsNoTracking()
                .AnyAsync(s => s.SetId == setId && s.FolderId == folderId, ct);
            if (!inFolder)
            {
                return NotFound();
            }

            var result = await _personalSets.DeleteSetAsync(setId, currentUserId, IsAdminUser(), ct: ct);
            if (result.Status != OperationStatus.Success)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(FolderController.FolderDetail), "Folder", new { id = folderId });
        }

        [HttpGet("EditSet/{id:int}")]
        public async Task<IActionResult> EditSet(int id, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            var form = await _personalSets.GetSetInfoFormAsync(id, currentUserId, IsAdminUser(), ct);
            if (form == null)
            {
                return NotFound();
            }

            return View("Settings", form);
        }

        [HttpPost("EditSet")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSet(SetEditorViewModel model, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            if (!model.SetId.HasValue)
            {
                return NotFound();
            }

            var existing = await GetManageableSetsQuery(currentUserId)
                .AsNoTracking()
                .Where(s => s.SetId == model.SetId.Value)
                .Select(s => new { s.SetId, s.FolderId })
                .FirstOrDefaultAsync(ct);

            if (existing == null)
            {
                return NotFound();
            }

            model.FolderId = existing.FolderId;
            PopulateCardsFromLegacyRequest(model);
            var cardInputs = NormalizeAndValidateEditor(model);
            if (!ModelState.IsValid)
            {
                return View("Settings", await BuildInfoFormAsync(model, currentUserId, ct));
            }

            var rows = new List<LegacyCardRow>();
            try
            {
                foreach (var card in cardInputs)
                {
                    string? uploaded = null;
                    if (card.ImageFile != null && card.ImageFile.Length > 0)
                    {
                        uploaded = await _fileStorageService.SaveImageAsync(card.ImageFile, ImageUploadPolicies.CardImage, ct);
                    }

                    rows.Add(new LegacyCardRow
                    {
                        Term = card.Term,
                        Definition = card.Definition,
                        ImageUrl = card.ExistingImageUrl,
                        UploadedImageUrl = uploaded
                    });
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.Cards), ex.Message);
                return View("Settings", await BuildInfoFormAsync(model, currentUserId, ct));
            }

            // Non-destructive upsert: learning progress and card details survive an edit (B1).
            var result = await _personalSets.ApplyLegacyEditAsync(
                existing.SetId, model.SetName, model.Description, rows, currentUserId, IsAdminUser(), ct);
            if (result.Status != OperationStatus.Success)
            {
                return NotFound();
            }

            return existing.FolderId.HasValue
                ? RedirectToAction(nameof(FolderController.FolderDetail), "Folder", new { id = existing.FolderId.Value })
                : RedirectToAction("Folder", "Folder");
        }

        private async Task<SetInfoFormViewModel> BuildInfoFormAsync(SetEditorViewModel model, int userId, CancellationToken ct)
        {
            SetInfoFormViewModel? form = null;
            if (model.SetId.HasValue)
            {
                form = await _personalSets.GetSetInfoFormAsync(model.SetId.Value, userId, IsAdminUser(), ct);
            }

            form ??= await _personalSets.GetNewSetFormAsync(userId, IsAdminUser(), null, ct) ?? new SetInfoFormViewModel();
            form.SetName = model.SetName;
            form.Description = model.Description;
            form.FolderId = model.FolderId;
            return form;
        }

        private async Task AddCardsAsync(Set targetSet, IReadOnlyCollection<SetCardEditorItemViewModel> cards, DateTime now, CancellationToken ct)
        {
            foreach (var card in cards)
            {
                var imageUrl = card.ExistingImageUrl;
                if (card.ImageFile != null && card.ImageFile.Length > 0)
                {
                    imageUrl = await _fileStorageService.SaveImageAsync(card.ImageFile, ImageUploadPolicies.CardImage, ct);
                }
                else if (!string.IsNullOrWhiteSpace(imageUrl) && imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    // Only https Pixabay images are downloaded, with size/type limits (B3); anything else is dropped.
                    imageUrl = await _personalSets.ImportExternalImageAsync(imageUrl, ct);
                }

                targetSet.Cards.Add(new Card
                {
                    Term = card.Term,
                    Definition = card.Definition,
                    ImageUrl = imageUrl,
                    CreatedAt = now,
                    UpdatedAt = now
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
