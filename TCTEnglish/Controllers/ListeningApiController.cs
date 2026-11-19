using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Services.Listening;

namespace TCTVocabulary.Controllers
{
    /// <summary>
    /// JSON endpoints of the Listening practice page: tap-a-word lookup and "save to my set". The controller only maps
    /// results to HTTP; every rule lives in <see cref="IListeningWordService"/>. A lesson the caller may not use is 404.
    /// </summary>
    [AutoValidateAntiforgeryToken]
    [Route("api/listening")]
    public class ListeningApiController : BaseController
    {
        private readonly IListeningWordService _words;

        public ListeningApiController(IListeningWordService words)
        {
            _words = words;
        }

        /// <summary>Word lookup. Open to guests: lesson vocabulary is free; the rest is decided per role by the service.</summary>
        [HttpGet("word")]
        public async Task<IActionResult> Word([FromQuery] int lessonId, [FromQuery] string? term, CancellationToken ct)
        {
            Response.Headers.CacheControl = "no-store";
            int? userId = TryGetCurrentUserId(out var id) ? id : null;

            var result = await _words.LookupAsync(lessonId, term, userId, ct);
            switch (result.Status)
            {
                case ListeningWordLookupStatus.Found when result.Source == "locked":
                    return Json(new { source = result.Source, word = result.Word, lineTranslationOnly = true });
                case ListeningWordLookupStatus.Found:
                    return Json(new
                    {
                        source = result.Source,
                        word = result.Word,
                        definition = result.Definition,
                        example = result.Example,
                        phonetic = result.Phonetic,
                    });
                case ListeningWordLookupStatus.InvalidTerm:
                    return BadRequest(new { error = "Từ không hợp lệ." });
                case ListeningWordLookupStatus.NotFound:
                    return NotFound();
                case ListeningWordLookupStatus.RateLimited:
                    Response.Headers["Retry-After"] = result.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Bạn tra từ hơi nhanh, thử lại sau ít phút." });
                default:
                    return StatusCode(StatusCodes.Status502BadGateway, new { error = "Chưa tra được từ này. Thử lại sau." });
            }
        }

        /// <summary>Adds a word to the caller's own "Từ vựng Luyện nghe" set. The user id comes from the cookie, never from the body.</summary>
        [Authorize]
        [HttpPost("word/save")]
        public async Task<IActionResult> Save([FromBody] ListeningWordSaveRequest? request, CancellationToken ct)
        {
            Response.Headers.CacheControl = "no-store";
            if (request == null)
                return BadRequest(new { error = "Dữ liệu không hợp lệ." });

            if (!TryGetCurrentUserId(out var userId))
                return Unauthorized(new { error = "Bạn cần đăng nhập." });

            var result = await _words.SaveAsync(request.LessonId, request, userId, ct);
            return result.Status switch
            {
                ListeningWordSaveStatus.Saved => Json(new { saved = true, setUrl = result.SetUrl }),
                ListeningWordSaveStatus.Duplicate => Json(new { saved = false, duplicate = true, setUrl = result.SetUrl }),
                ListeningWordSaveStatus.Invalid => BadRequest(new { error = "Không lưu được: từ hoặc nghĩa chưa hợp lệ." }),
                ListeningWordSaveStatus.NotFound => NotFound(),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { error = "Chưa lưu được từ này. Thử lại sau." }),
            };
        }
    }
}
