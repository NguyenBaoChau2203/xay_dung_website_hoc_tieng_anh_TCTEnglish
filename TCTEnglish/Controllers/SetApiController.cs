using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Services.Sets;
using TCTEnglish.ViewModels.Sets;
using TCTVocabulary.Services;

namespace TCTVocabulary.Controllers
{
    /// <summary>JSON endpoints for the personal set workspace. No access → 404 (never 403).</summary>
    [Authorize]
    [AutoValidateAntiforgeryToken]
    [Route("api/sets")]
    public class SetApiController : BaseController
    {
        private readonly IPersonalSetService _sets;
        private readonly IFileStorageService _fileStorageService;

        public SetApiController(IPersonalSetService sets, IFileStorageService fileStorageService)
        {
            _sets = sets;
            _fileStorageService = fileStorageService;
        }

        [HttpGet("{id:int}/cards")]
        public async Task<IActionResult> Cards(int id, [FromQuery] CardSearchQuery query, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var page = await _sets.SearchCardsAsync(id, query ?? new CardSearchQuery(), userId, IsAdminUser(), ct);
            return page == null ? NotFound() : Json(page);
        }

        [HttpPost("{id:int}/cards/bulk")]
        public async Task<IActionResult> Bulk(int id, [FromBody] CardBulkRequest? request, CancellationToken ct)
        {
            if (request == null)
            {
                return BadRequest(new { error = "Yêu cầu không hợp lệ." });
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _sets.BulkCardsAsync(id, request, userId, IsAdminUser(), ct);
            return ToActionResult(result);
        }

        [HttpPost("{id:int}/cards/batch")]
        public async Task<IActionResult> Batch(int id, [FromBody] CardBatchRequest? request, CancellationToken ct)
        {
            if (request?.Cards == null)
            {
                return BadRequest(new { error = "Yêu cầu không hợp lệ." });
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _sets.CreateCardsBatchAsync(id, request.Cards, userId, IsAdminUser(), ct);
            return ToActionResult(result);
        }

        [HttpGet("duplicates")]
        public async Task<IActionResult> Duplicates([FromQuery] string? term, [FromQuery] int? excludeCardId, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var hits = await _sets.FindDuplicatesAsync(term ?? string.Empty, userId, excludeCardId, ct);
            return Json(hits);
        }

        [HttpPost("card-image")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> CardImage(IFormFile? file, CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out _))
            {
                return Unauthorized();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "Chọn một ảnh để tải lên." });
            }

            try
            {
                var url = await _fileStorageService.SaveImageAsync(file, ImageUploadPolicies.CardImage, ct);
                return Json(new { url });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("cards/{cardId:int}/suspend")]
        public async Task<IActionResult> Suspend(int cardId, [FromBody] SuspendRequest? request, CancellationToken ct)
        {
            if (request == null)
            {
                return BadRequest(new { error = "Yêu cầu không hợp lệ." });
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _sets.SetSuspendedAsync(cardId, request.Suspended, userId, IsAdminUser(), ct);
            if (result.Status != OperationStatus.Success)
            {
                return NotFound();
            }

            return Json(new { suspended = result.Value });
        }

        [HttpPost("{id:int}/share-link")]
        public async Task<IActionResult> ShareLink(int id, [FromBody] ShareLinkRequest? request, CancellationToken ct)
        {
            if (request == null)
            {
                return BadRequest(new { error = "Yêu cầu không hợp lệ." });
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _sets.SetShareLinkAsync(id, request.Enabled, request.Regenerate, userId, IsAdminUser(), ct);
            if (result.Status == OperationStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == OperationStatus.Invalid)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            if (result.Value == null)
            {
                return Json(new { });
            }

            var url = Url.Action("Shared", "Set", new { slug = result.Value }, Request.Scheme);
            return Json(new { slug = result.Value, url });
        }

        private IActionResult ToActionResult<T>(OperationResult<T> result)
        {
            return result.Status switch
            {
                OperationStatus.Success => Json(result.Value),
                OperationStatus.NotFound when !string.IsNullOrEmpty(result.ErrorMessage) => NotFound(new { error = result.ErrorMessage }),
                OperationStatus.NotFound => NotFound(),
                _ => BadRequest(new { error = result.ErrorMessage ?? "Yêu cầu không hợp lệ.", fieldErrors = result.FieldErrors }),
            };
        }
    }
}
