using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Services.Sets;
using TCTEnglish.ViewModels;
using TCTEnglish.ViewModels.Sets;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTVocabulary.Controllers
{
    /// <summary>Personal set workspace routes. Absolute routes override the class-level "Home" prefix.</summary>
    public partial class SetController
    {
        private const string MessageKey = "SetMessage";
        private const string ErrorKey = "SetError";

        private bool IsPremiumUser => User.IsInRole(Roles.Premium) || User.IsInRole(Roles.Admin);

        // ----------------------------------------------------------------- list

        [HttpGet("/Sets")]
        public async Task<IActionResult> Index(string? filter, CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            var model = await _personalSets.GetSetIndexAsync(userId, IsAdminUser(), filter, ct);
            return View(model);
        }

        // ----------------------------------------------------------------- create / settings / delete

        [HttpPost("/Sets/Create")]
        public async Task<IActionResult> CreateDeck(SetInfoFormViewModel model, CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            if (ModelState.IsValid)
            {
                var result = await _personalSets.CreateSetAsync(model, userId, IsAdminUser(), ct);
                if (result.Status == OperationStatus.Success)
                {
                    return RedirectToAction(nameof(AddCards), new { id = result.Value });
                }

                if (result.Status == OperationStatus.NotFound)
                {
                    return NotFound();
                }

                AddErrors(result.ErrorMessage, result.FieldErrors);
            }

            model.SetId = null;
            model.AvailableFolders = (await _personalSets.GetNewSetFormAsync(userId, IsAdminUser(), null, ct))?.AvailableFolders ?? new();
            return View(nameof(CreateSet), model);
        }

        [HttpGet("/Sets/{id:int}/Settings")]
        public async Task<IActionResult> Settings(int id, CancellationToken ct)
        {
            var form = await _personalSets.GetSetInfoFormAsync(id, GetCurrentUserId(), IsAdminUser(), ct);
            return form == null ? NotFound() : View(form);
        }

        [HttpPost("/Sets/{id:int}/Settings")]
        public async Task<IActionResult> SaveSettings(int id, SetInfoFormViewModel model, CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            var isAdmin = IsAdminUser();
            if (ModelState.IsValid)
            {
                var result = await _personalSets.UpdateSetInfoAsync(id, model, userId, isAdmin, ct);
                if (result.Status == OperationStatus.Success)
                {
                    TempData[MessageKey] = "Đã lưu bộ thẻ";
                    return RedirectToAction(nameof(Detail), new { id });
                }

                if (result.Status == OperationStatus.NotFound)
                {
                    return NotFound();
                }

                AddErrors(result.ErrorMessage, result.FieldErrors);
            }

            var form = await _personalSets.GetSetInfoFormAsync(id, userId, isAdmin, ct);
            if (form == null)
            {
                return NotFound();
            }

            model.SetId = id;
            model.AvailableFolders = form.AvailableFolders;
            model.WordCount = form.WordCount;
            model.CoverHue = form.CoverHue;
            return View(nameof(Settings), model);
        }

        [HttpPost("/Sets/{id:int}/Delete")]
        public async Task<IActionResult> DeleteDeck(int id, string? confirmName, CancellationToken ct)
        {
            var result = await _personalSets.DeleteSetAsync(id, GetCurrentUserId(), IsAdminUser(), confirmName ?? string.Empty, ct);
            if (result.Status == OperationStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == OperationStatus.Invalid)
            {
                TempData[ErrorKey] = "Tên xác nhận chưa khớp, bộ thẻ chưa bị xóa.";
                return RedirectToAction(nameof(Settings), new { id });
            }

            TempData[MessageKey] = "Đã xóa bộ thẻ";
            return RedirectToAction(nameof(Index));
        }

        // ----------------------------------------------------------------- detail / add / import / export

        [HttpGet("/Sets/{id:int}")]
        public async Task<IActionResult> Detail(int id, CancellationToken ct)
        {
            var model = await _personalSets.GetSetDetailAsync(id, GetCurrentUserId(), IsAdminUser(), ct);
            if (model == null)
            {
                return NotFound();
            }

            model.IsPremium = IsPremiumUser;
            if (model.ShareSlug != null)
            {
                model.ShareUrl = Url.Action(nameof(Shared), "Set", new { slug = model.ShareSlug }, Request.Scheme);
            }

            return View(model);
        }

        [HttpGet("/Sets/{id:int}/Add")]
        public async Task<IActionResult> AddCards(int id, string? term, CancellationToken ct)
        {
            var model = await _personalSets.GetComposeContextAsync(id, GetCurrentUserId(), IsAdminUser(), ct);
            if (model == null)
            {
                return NotFound();
            }

            model.IsPremium = IsPremiumUser;
            model.UserId = GetCurrentUserId();
            var initial = term?.Trim();
            model.InitialTerm = string.IsNullOrEmpty(initial) ? null : (initial.Length > 255 ? initial[..255] : initial);
            return View(model);
        }

        [HttpGet("/Sets/{id:int}/Import")]
        public async Task<IActionResult> Import(int id, CancellationToken ct)
        {
            var model = await _personalSets.GetImportContextAsync(id, GetCurrentUserId(), IsAdminUser(), ct);
            if (model == null)
            {
                return NotFound();
            }

            model.IsPremium = IsPremiumUser;
            return View(model);
        }

        [HttpGet("/Sets/{id:int}/Export")]
        public async Task<IActionResult> Export(int id, CancellationToken ct)
        {
            var file = await _personalSets.ExportCsvAsync(id, GetCurrentUserId(), IsAdminUser(), ct);
            return file == null
                ? NotFound()
                : File(file.Content, "text/csv; charset=utf-8", file.FileName);
        }

        // ----------------------------------------------------------------- cards

        [HttpGet("/Sets/Cards/{cardId:int}")]
        public async Task<IActionResult> CardDetail(int cardId, CancellationToken ct)
        {
            var model = await _personalSets.GetCardDetailAsync(cardId, GetCurrentUserId(), IsAdminUser(), ct);
            return model == null ? NotFound() : View(model);
        }

        [HttpGet("/Sets/Cards/{cardId:int}/Edit")]
        public async Task<IActionResult> EditCard(int cardId, CancellationToken ct)
        {
            var model = await _personalSets.GetCardFormAsync(cardId, GetCurrentUserId(), IsAdminUser(), ct);
            if (model == null)
            {
                return NotFound();
            }

            model.IsPremium = IsPremiumUser;
            return View(model);
        }

        [HttpPost("/Sets/Cards/{cardId:int}/Edit")]
        public async Task<IActionResult> EditCard(int cardId, CardFormViewModel model, CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            var isAdmin = IsAdminUser();
            var result = await _personalSets.UpdateCardAsync(cardId, model, userId, isAdmin, ct);
            if (result.Status == OperationStatus.Success)
            {
                TempData[MessageKey] = "Đã lưu thay đổi";
                return RedirectToAction(nameof(CardDetail), new { cardId });
            }

            if (result.Status == OperationStatus.NotFound)
            {
                return NotFound();
            }

            var form = await _personalSets.GetCardFormAsync(cardId, userId, isAdmin, ct);
            if (form == null)
            {
                return NotFound();
            }

            model.CardId = cardId;
            model.SetId = form.SetId;
            model.SetName = form.SetName;
            model.IsPremium = IsPremiumUser;
            model.FieldErrors = result.FieldErrors?.ToDictionary(p => p.Key, p => p.Value) ?? new Dictionary<string, string>();
            AddErrors(result.ErrorMessage, result.FieldErrors);
            return View(model);
        }

        [HttpPost("/Sets/Cards/{cardId:int}/Delete")]
        public async Task<IActionResult> DeleteCard(int cardId, CancellationToken ct)
        {
            var result = await _personalSets.DeleteCardAsync(cardId, GetCurrentUserId(), IsAdminUser(), ct);
            if (result.Status != OperationStatus.Success)
            {
                return NotFound();
            }

            TempData[MessageKey] = "Đã xóa từ";
            return RedirectToAction(nameof(Detail), new { id = result.Value });
        }

        // ----------------------------------------------------------------- sharing

        [AllowAnonymous]
        [HttpGet("/s/{slug}")]
        public async Task<IActionResult> Shared(string slug, CancellationToken ct)
        {
            int? userId = TryGetCurrentUserId(out var id) ? id : null;
            var model = await _personalSets.GetSharedSetAsync(slug, userId, ct);
            if (model == null)
            {
                return NotFound();
            }

            if (model.IsOwner)
            {
                return RedirectToAction(nameof(Detail), new { id = model.SetId });
            }

            return View(model);
        }

        [HttpPost("/Sets/Subscribe")]
        public async Task<IActionResult> Subscribe(int? setId, string? slug, CancellationToken ct)
        {
            var result = await _personalSets.SubscribeAsync(setId, slug, GetCurrentUserId(), IsAdminUser(), ct);
            if (result.Status != OperationStatus.Success)
            {
                return NotFound();
            }

            TempData[MessageKey] = "Đã lưu vào bộ thẻ của bạn";
            return RedirectToAction(nameof(Detail), new { id = result.Value });
        }

        [HttpPost("/Sets/{id:int}/Unsubscribe")]
        public async Task<IActionResult> Unsubscribe(int id, CancellationToken ct)
        {
            await _personalSets.UnsubscribeAsync(id, GetCurrentUserId(), ct);
            TempData[MessageKey] = "Đã bỏ theo học bộ thẻ";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("/Sets/Clone")]
        public async Task<IActionResult> Clone(int? setId, string? slug, CancellationToken ct)
        {
            var result = await _personalSets.CloneSetAsync(setId, slug, GetCurrentUserId(), IsAdminUser(), ct);
            if (result.Status == OperationStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == OperationStatus.Invalid)
            {
                TempData[ErrorKey] = result.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            TempData[MessageKey] = "Đã sao chép về bộ thẻ của bạn";
            return RedirectToAction(nameof(Detail), new { id = result.Value });
        }

        [HttpPost("/Sets/AddSamples")]
        public async Task<IActionResult> AddSamples(CancellationToken ct)
        {
            var result = await _personalSets.AddSampleSetsAsync(GetCurrentUserId(), ct);
            if (result.Status == OperationStatus.Success)
            {
                var added = result.Value!;
                TempData[MessageKey] = added.Sets == 0
                    ? "Bạn đã có các bộ thẻ mẫu rồi."
                    : $"Đã thêm {added.Sets} bộ thẻ mẫu ({added.Cards} từ).";
            }
            else
            {
                TempData[ErrorKey] = result.ErrorMessage;
            }

            return RedirectToAction(nameof(Index));
        }

        private void AddErrors(string? message, IReadOnlyDictionary<string, string>? fieldErrors)
        {
            if (fieldErrors != null && fieldErrors.Count > 0)
            {
                foreach (var pair in fieldErrors)
                {
                    ModelState.AddModelError(pair.Key, pair.Value);
                }
            }
            else if (!string.IsNullOrEmpty(message))
            {
                ModelState.AddModelError(string.Empty, message);
            }
        }
    }
}
