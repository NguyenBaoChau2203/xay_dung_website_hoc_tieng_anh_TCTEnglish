using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTVocabulary.Controllers
{
    /// <summary>AI autofill and image search for the set composer. Premium or Admin only (same gate as SuggestVocab).</summary>
    [Authorize]
    [AutoValidateAntiforgeryToken]
    [Route("api/vocab")]
    public class VocabApiController : BaseController
    {
        private readonly IVocabSuggestService _vocab;

        public VocabApiController(IVocabSuggestService vocab)
        {
            _vocab = vocab;
        }

        private bool IsPremium => User.IsInRole(Roles.Premium) || User.IsInRole(Roles.Admin);

        private IActionResult PremiumRequired() => StatusCode(403, new
        {
            error = "Tính năng gợi ý bằng AI chỉ dành cho tài khoản Premium.",
            premiumRequired = true
        });

        [HttpPost("enrich")]
        public async Task<IActionResult> Enrich([FromBody] EnrichRequest? request, CancellationToken ct)
        {
            if (!IsPremium)
            {
                return PremiumRequired();
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Term))
            {
                return BadRequest(new { error = "Nhập từ trước khi gợi ý." });
            }

            var response = await _vocab.EnrichAsync(request.Term, request.MeaningHint, request.Context, HttpContext.RequestAborted);
            return response.Success
                ? Json(response.Result)
                : BadRequest(new { error = response.ErrorMessage ?? "Không thể lấy gợi ý từ vựng lúc này." });
        }

        [HttpPost("enrich-batch")]
        public async Task<IActionResult> EnrichBatch([FromBody] EnrichBatchRequest? request, CancellationToken ct)
        {
            if (!IsPremium)
            {
                return PremiumRequired();
            }

            if (request?.Items == null || request.Items.Count < 1 || request.Items.Count > 10)
            {
                return BadRequest(new { error = "Mỗi lần gợi ý từ 1 đến 10 từ." });
            }

            var results = await _vocab.EnrichBatchAsync(
                request.Items.Select(i => new VocabEnrichBatchRequest { Term = i.Term, MeaningHint = i.MeaningHint }).ToList(),
                HttpContext.RequestAborted);

            return Json(new
            {
                results = results.Select(r => new { term = r.Term, ok = r.Ok, result = r.Result, error = r.Error })
            });
        }

        [HttpGet("images")]
        public async Task<IActionResult> Images([FromQuery] string? q, CancellationToken ct)
        {
            if (!IsPremium)
            {
                return PremiumRequired();
            }

            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length > 60)
            {
                return BadRequest(new { error = "Nhập từ khóa ảnh (tối đa 60 ký tự)." });
            }

            var hits = await _vocab.SearchImagesAsync(q, 8, HttpContext.RequestAborted);
            return Json(hits);
        }

        public sealed class EnrichRequest
        {
            public string? Term { get; set; }
            public string? MeaningHint { get; set; }
            public string? Context { get; set; }
        }

        public sealed class EnrichBatchRequest
        {
            public List<EnrichBatchItem>? Items { get; set; }
        }

        public sealed class EnrichBatchItem
        {
            public string? Term { get; set; }
            public string? MeaningHint { get; set; }
        }
    }
}
