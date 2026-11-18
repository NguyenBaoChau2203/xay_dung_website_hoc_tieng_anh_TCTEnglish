using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Services.Admin;
using TCTVocabulary.Areas.Admin.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Security;

namespace TCTVocabulary.Areas.Admin.Controllers
{
    /// <summary>
    /// Vocabulary sets in the admin. Rules live in <see cref="IAdminVocabularyService"/>:
    /// system sets are editable but not renamable, learner sets are view / make private / delete only.
    /// </summary>
    [Area("Admin")]
    [Authorize(Roles = Roles.Admin)]
    public class SetManagementController : Controller
    {
        private readonly IAdminVocabularyService _service;

        public SetManagementController(IAdminVocabularyService service)
        {
            _service = service;
        }

        // 1) GET Index
        public async Task<IActionResult> Index(string? q, int? folderId, string? ownerFilter, string? visibility, string? sort, int page = 1, CancellationToken ct = default)
        {
            var source = ownerFilter is "system" or "user" ? ownerFilter : "";
            var exposure = visibility is "Public" or "Link" or "Private" ? visibility : "";
            var sortKey = sort is "name" or "cards" or "views" ? sort : "newest";
            // The folder filter only makes sense for system sets (their folders), and the page disables it otherwise.
            int? folder = source == "system" ? folderId : null;

            var list = await _service.ListSetsAsync(new AdminSetQuery(source, folder, exposure, q, sortKey, page), ct);
            var figures = await _service.GetFiguresAsync(ct);

            return View(new SetManagementIndexViewModel
            {
                List = list,
                Figures = figures,
                Q = q,
                OwnerFilter = source,
                FolderId = folder,
                Visibility = exposure,
                Sort = sortKey,
            });
        }

        // 2) GET Create
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            var form = await _service.GetSetFormAsync(null, ct);
            return View(ToViewModel(form!));
        }

        // 3) POST Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SetFormViewModel model, CancellationToken ct)
        {
            if (!TryGetAdminId(out var adminId))
            {
                return Challenge();
            }

            var result = await _service.CreateSystemSetAsync(new AdminSetInput { Name = model.Name, Description = model.Description, FolderId = model.FolderId }, adminId, ct);
            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = "Đã tạo bộ từ vựng hệ thống. Hãy thêm thẻ cho bộ này.";
                return RedirectToAction(nameof(Cards), new { id = result.Value });
            }

            var form = await _service.GetSetFormAsync(null, ct);
            return View(WithErrors(model, form!, result.Message, result.FieldErrors));
        }

        // 4) GET Edit
        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            var form = await _service.GetSetFormAsync(id, ct);
            if (form == null)
            {
                return NotFound();
            }

            if (!form.IsSystem)
            {
                TempData["ErrorMessage"] = AdminVocabularyMessages.LearnerContentReadOnly;
                return RedirectToAction(nameof(Cards), new { id });
            }

            return View(ToViewModel(form));
        }

        // 5) POST Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SetFormViewModel model, CancellationToken ct)
        {
            if (!model.SetId.HasValue)
            {
                return BadRequest();
            }

            if (!TryGetAdminId(out var adminId))
            {
                return Challenge();
            }

            var result = await _service.UpdateSetAsync(model.SetId.Value, new AdminSetInput { Name = model.Name, Description = model.Description, FolderId = model.FolderId }, adminId, ct);
            switch (result.Status)
            {
                case AdminResultStatus.Success:
                    TempData["SuccessMessage"] = "Đã cập nhật bộ từ vựng.";
                    if (result.Warnings.Count > 0)
                    {
                        TempData["WarningMessage"] = string.Join(" ", result.Warnings);
                    }

                    return RedirectToAction(nameof(Cards), new { id = model.SetId.Value });
                case AdminResultStatus.NotFound:
                    return NotFound();
                case AdminResultStatus.Forbidden:
                    TempData["ErrorMessage"] = result.Message;
                    return RedirectToAction(nameof(Cards), new { id = model.SetId.Value });
                default:
                    var form = await _service.GetSetFormAsync(model.SetId, ct);
                    return form == null ? NotFound() : View(WithErrors(model, form, result.Message, result.FieldErrors));
            }
        }

        // 6) POST Delete (the admin has to type the exact set name)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? confirmName, CancellationToken ct)
        {
            if (!TryGetAdminId(out var adminId))
            {
                return Challenge();
            }

            var result = await _service.DeleteSetAsync(id, confirmName, adminId, ct);
            if (result.Status == AdminResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = "Đã xóa bộ từ vựng và dữ liệu học liên quan.";
            }
            else
            {
                TempData["ErrorMessage"] = result.Message ?? "Không xóa được bộ từ vựng.";
            }

            return RedirectToAction(nameof(Index));
        }

        // 7) POST SetVisibility (learner sets can only be made private)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetVisibility(int id, string visibility, CancellationToken ct)
        {
            if (!TryGetAdminId(out var adminId))
            {
                return Challenge();
            }

            var result = await _service.SetVisibilityAsync(id, visibility, adminId, ct);
            if (result.Status == AdminResultStatus.NotFound)
            {
                return NotFound();
            }

            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = "Đã đặt bộ thẻ về riêng tư và tắt liên kết chia sẻ.";
            }
            else
            {
                TempData["ErrorMessage"] = result.Message ?? "Không đổi được chế độ hiển thị.";
            }

            return RedirectToAction(nameof(Index));
        }

        // 8) GET Export (system sets only, in the shape of system-vocabulary.json)
        public async Task<IActionResult> Export(int id, CancellationToken ct)
        {
            var export = await _service.ExportSystemSetAsync(id, ct);
            if (export == null)
            {
                return NotFound();
            }

            return File(Encoding.UTF8.GetBytes(export.Json), "application/json; charset=utf-8", export.FileName);
        }

        // 9) GET Cards
        public async Task<IActionResult> Cards(int id, string? q, string? topic, string? quality, int page = 1, CancellationToken ct = default)
        {
            var qualityKey = quality is "complete" or "missing-example" or "missing-phonetic" or "missing-picture" ? quality : "";
            var cards = await _service.ListCardsAsync(id, new AdminCardQuery(q, topic, qualityKey, page), ct);
            if (cards == null)
            {
                return NotFound();
            }

            return View(new SetCardsViewModel { Cards = cards, Q = q, Topic = topic, Quality = qualityKey });
        }

        // 10) POST DeleteCard (the card knows its own set)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCard(int cardId, CancellationToken ct)
        {
            if (!TryGetAdminId(out var adminId))
            {
                return Challenge();
            }

            var result = await _service.DeleteCardAsync(cardId, adminId, ct);
            switch (result.Status)
            {
                case AdminResultStatus.NotFound:
                    return NotFound();
                case AdminResultStatus.Success:
                    TempData["SuccessMessage"] = "Đã xóa thẻ từ vựng.";
                    return RedirectToAction(nameof(Cards), new { id = result.Value });
                default:
                    TempData["ErrorMessage"] = result.Message ?? "Không xóa được thẻ.";
                    var form = await _service.GetCardFormAsync(cardId, null, ct);
                    return form == null ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Cards), new { id = form.SetId });
            }
        }

        // 11) GET AddCard
        public async Task<IActionResult> AddCard(int setId, CancellationToken ct)
        {
            var form = await _service.GetCardFormAsync(null, setId, ct);
            if (form == null)
            {
                return NotFound();
            }

            if (!form.IsSystem)
            {
                TempData["ErrorMessage"] = AdminVocabularyMessages.LearnerContentReadOnly;
                return RedirectToAction(nameof(Cards), new { id = setId });
            }

            return View(new CardFormViewModel { SetId = form.SetId, SetName = form.SetName, Input = form.Input });
        }

        // 12) POST AddCard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCard(int setId, AdminCardInput input, CancellationToken ct)
        {
            if (!TryGetAdminId(out var adminId))
            {
                return Challenge();
            }

            var result = await _service.AddCardAsync(setId, input, adminId, ct);
            switch (result.Status)
            {
                case AdminResultStatus.Success:
                    TempData["SuccessMessage"] = "Đã thêm thẻ mới.";
                    return RedirectToAction(nameof(Cards), new { id = setId });
                case AdminResultStatus.NotFound:
                    return NotFound();
                case AdminResultStatus.Forbidden:
                    TempData["ErrorMessage"] = result.Message;
                    return RedirectToAction(nameof(Cards), new { id = setId });
                default:
                    var form = await _service.GetCardFormAsync(null, setId, ct);
                    return form == null
                        ? NotFound()
                        : View(new CardFormViewModel { SetId = setId, SetName = form.SetName, Input = input, Errors = result.FieldErrors, Summary = result.Message });
            }
        }

        // 13) GET EditCard
        public async Task<IActionResult> EditCard(int id, CancellationToken ct)
        {
            var form = await _service.GetCardFormAsync(id, null, ct);
            if (form == null)
            {
                return NotFound();
            }

            if (!form.IsSystem)
            {
                TempData["ErrorMessage"] = AdminVocabularyMessages.LearnerContentReadOnly;
                return RedirectToAction(nameof(Cards), new { id = form.SetId });
            }

            return View(new CardFormViewModel { CardId = form.CardId, SetId = form.SetId, SetName = form.SetName, Input = form.Input });
        }

        // 14) POST EditCard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCard(int cardId, AdminCardInput input, CancellationToken ct)
        {
            if (!TryGetAdminId(out var adminId))
            {
                return Challenge();
            }

            var result = await _service.UpdateCardAsync(cardId, input, adminId, ct);
            switch (result.Status)
            {
                case AdminResultStatus.Success:
                    TempData["SuccessMessage"] = "Đã cập nhật thẻ.";
                    return RedirectToAction(nameof(Cards), new { id = result.Value });
                case AdminResultStatus.NotFound:
                    return NotFound();
                case AdminResultStatus.Forbidden:
                    TempData["ErrorMessage"] = result.Message;
                    var owner = await _service.GetCardFormAsync(cardId, null, ct);
                    return owner == null ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Cards), new { id = owner.SetId });
                default:
                    var form = await _service.GetCardFormAsync(cardId, null, ct);
                    return form == null
                        ? NotFound()
                        : View(new CardFormViewModel { CardId = cardId, SetId = form.SetId, SetName = form.SetName, Input = input, Errors = result.FieldErrors, Summary = result.Message });
            }
        }

        private bool TryGetAdminId(out int adminId) => User.TryGetUserId(out adminId);

        private static SetFormViewModel ToViewModel(AdminSetForm form) => new()
        {
            SetId = form.SetId,
            Name = form.Name,
            Description = form.Description,
            FolderId = form.FolderId,
            IsSystem = form.IsSystem,
            OwnerName = form.OwnerName,
            Exposure = form.Exposure,
            Folders = form.Folders,
        };

        private static SetFormViewModel WithErrors(SetFormViewModel posted, AdminSetForm form, string? summary, IReadOnlyDictionary<string, string> errors)
        {
            var model = ToViewModel(form);
            model.Name = posted.Name;
            model.Description = posted.Description;
            model.FolderId = posted.FolderId;
            model.Errors = errors;
            model.Summary = summary;
            return model;
        }
    }
}
