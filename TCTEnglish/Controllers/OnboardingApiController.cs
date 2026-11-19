using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Models;
using TCTEnglish.Services.Onboarding;

namespace TCTVocabulary.Controllers
{
    /// <summary>
    /// JSON endpoints of the first-run onboarding. Every call is scoped to the signed-in user; a set that is not theirs
    /// (or not an onboarding deck) is a 404, never a 403.
    /// </summary>
    [Authorize]
    [AutoValidateAntiforgeryToken]
    [Route("api/onboarding")]
    public class OnboardingApiController : BaseController
    {
        private const string BadRequestMessage = "Yêu cầu không hợp lệ.";
        private const string TooFastMessage = "Bạn đang thao tác quá nhanh. Vui lòng chờ ít phút rồi thử lại.";

        private readonly IOnboardingService _onboarding;
        private readonly IOnboardingRequestLimiter _limiter;

        public OnboardingApiController(IOnboardingService onboarding, IOnboardingRequestLimiter limiter)
        {
            _onboarding = onboarding;
            _limiter = limiter;
        }

        [HttpGet("state")]
        public async Task<IActionResult> State(CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var state = await _onboarding.GetStateAsync(userId, ct);
            return Json(new
            {
                shouldPrompt = state.ShouldPrompt,
                status = state.Status,
                mode = state.Status == OnboardingStatus.Pending ? "first" : "again",
                hasDecks = state.HasDecks,
                bannerVisible = state.BannerVisible
            });
        }

        [HttpPost("prompted")]
        public async Task<IActionResult> Prompted(CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            await _onboarding.MarkPromptedAsync(userId, ct);
            return NoContent();
        }

        [HttpPost("skip")]
        public async Task<IActionResult> Skip(CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            return Json(new { status = await _onboarding.SkipAsync(userId, ct) });
        }

        [HttpPost("banner/dismiss")]
        public async Task<IActionResult> DismissBanner(CancellationToken ct)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            await _onboarding.DismissBannerAsync(userId, ct);
            return NoContent();
        }

        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromBody] OnboardingChoicesDto? body, CancellationToken ct)
        {
            if (body == null)
            {
                return BadRequest(new { error = BadRequestMessage });
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _onboarding.BuildPreviewAsync(userId, body.ToChoices(), ct);
            if (!result.IsSuccess)
            {
                return Failure(result.Outcome, result.Error);
            }

            var preview = result.Value!;
            return Json(new
            {
                decks = preview.Decks.Select(deck => new
                {
                    key = deck.Key,
                    name = deck.Name,
                    goal = deck.Goal,
                    topics = deck.Topics.Select(key => new { key, name = OnboardingCatalog.Topic(key).SetName }),
                    cards = deck.Cards.Select(card => new
                    {
                        cardId = card.CardId,
                        term = card.Term,
                        meaning = card.Meaning,
                        imageUrl = card.ImageUrl,
                        cefr = card.Cefr
                    })
                }),
                total = preview.Total,
                days = preview.Days
            });
        }

        [HttpPost("complete")]
        public async Task<IActionResult> Complete([FromBody] OnboardingCompleteDto? body, CancellationToken ct)
        {
            if (body == null || !Guid.TryParse(body.RequestId, out var requestId) || requestId == Guid.Empty)
            {
                return BadRequest(new { error = BadRequestMessage });
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            if (!_limiter.TryAcquire(userId))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new { error = TooFastMessage });
            }

            var request = new CompleteRequest(
                requestId,
                body.ToChoices(),
                (body.DecksOff ?? new List<string>()).Select(x => (x ?? string.Empty).Trim()).ToList(),
                body.KnownCardIds ?? new List<int>());
            var result = await _onboarding.CompleteAsync(userId, request, ct);
            if (!result.IsSuccess)
            {
                return Failure(result.Outcome, result.Error);
            }

            var created = result.Value!;
            return Json(new
            {
                folderId = created.FolderId,
                folderUrl = $"/Sets/Folders/{created.FolderId}",
                decks = created.Decks.Select(deck => new { setId = deck.SetId, name = deck.Name, count = deck.Count, url = $"/Sets/{deck.SetId}" }),
                firstLearnUrl = created.Decks.Count == 0
                    ? null
                    : $"/Practice/learn?set={created.Decks[0].SetId}&n={created.Pace}&from=set"
            });
        }

        [HttpPost("refill/{setId:int}")]
        public async Task<IActionResult> Refill(int setId, [FromBody] OnboardingRefillDto? body, CancellationToken ct)
        {
            if (body == null || !Guid.TryParse(body.RequestId, out var requestId) || requestId == Guid.Empty)
            {
                return BadRequest(new { error = BadRequestMessage });
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            if (!_limiter.TryAcquire(userId))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new { error = TooFastMessage });
            }

            var result = await _onboarding.RefillAsync(userId, setId, body.Count, requestId, ct);
            if (!result.IsSuccess)
            {
                return Failure(result.Outcome, result.Error);
            }

            return Json(new { added = result.Value!.Added, setUrl = $"/Sets/{setId}", exhausted = result.Value.Exhausted });
        }

        private IActionResult Failure(OnboardingOutcome outcome, string? error) => outcome switch
        {
            OnboardingOutcome.NotFound => NotFound(),
            OnboardingOutcome.Conflict => Conflict(new { error = error ?? BadRequestMessage }),
            _ => BadRequest(new { error = error ?? BadRequestMessage })
        };
    }
}
