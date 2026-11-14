using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TCTVocabulary.Services;
using TCTVocabulary.Services.Vocabulary;

namespace TCTVocabulary.Controllers
{
    /// <summary>
    /// System Vocabulary: a home page, a course page and a lesson screen.
    /// Legacy URLs (FolderDetail, Topics, TopicDetail) keep answering and render the new views.
    /// A folder or set that is not owned by the system user is indistinguishable from a missing one (404).
    /// </summary>
    [Authorize]
    public class VocabularyController : BaseController
    {
        private readonly IStudyService _studyService;

        public VocabularyController(IStudyService studyService)
        {
            _studyService = studyService;
        }

        // "/Vocabulary/Index" stays the canonical URL (Order 0, so generated links keep it);
        // "/Vocabulary" was a 404 and is added as an alias.
        [HttpGet("/Vocabulary/Index", Order = 0)]
        [HttpGet("/Vocabulary", Order = 1)]
        public async Task<IActionResult> Index()
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await _studyService.GetVocabularyHomeAsync(currentUserId);
            return viewModel == null ? NotFound() : View("Index", viewModel);
        }

        public async Task<IActionResult> FolderDetail(int folderId)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await _studyService.GetVocabularyHomeAsync(currentUserId, folderId);
            return viewModel == null ? NotFound() : View("Index", viewModel);
        }

        public async Task<IActionResult> Detail(int setId)
        {
            return await RenderCourseAsync(setId, "lessons", null, trackView: true);
        }

        public async Task<IActionResult> Topics(int setId)
        {
            return await RenderCourseAsync(setId, "lessons", null, trackView: false);
        }

        public async Task<IActionResult> TopicDetail(int setId, string? topic)
        {
            return await RenderCourseAsync(setId, "words", topic, trackView: false);
        }

        // "index" is accepted for old links and ignored.
        public async Task<IActionResult> Study(int setId, int? lesson = null, string? topic = null, int index = 1, string? mode = null)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            var review = string.Equals(mode, "review", StringComparison.OrdinalIgnoreCase);
            var viewModel = await _studyService.GetVocabularyLessonAsync(setId, currentUserId, lesson, topic, review);
            if (viewModel == null)
            {
                return NotFound();
            }

            await TrackVocabularySetViewAsync(setId);
            return View("Study", viewModel);
        }

        public async Task<IActionResult> Review(int? setId = null)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (setId.HasValue)
            {
                var courseReview = await _studyService.GetVocabularyLessonAsync(setId.Value, currentUserId, null, null, review: true);
                return courseReview == null ? NotFound() : View("Study", courseReview);
            }

            var viewModel = await _studyService.GetVocabularyReviewAsync(currentUserId);
            return View("Study", viewModel);
        }

        /// <summary>
        /// Records one system word's outcome of a lesson or review session. The client sends only the outcome
        /// (again, hard, good or known); the server computes the level and the date. System cards only.
        /// </summary>
        [HttpPost("/Vocabulary/Review/Record")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordReview([FromBody] VocabularyReviewRecordRequest? request)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid
                || request == null
                || request.CardId <= 0
                || !SpacedRepetitionScheduler.TryParseOutcome(request.Outcome, out var outcome))
            {
                return BadRequest();
            }

            var result = await _studyService.RecordVocabularyReviewAsync(currentUserId, request.CardId, outcome);
            return result == null ? NotFound() : Ok(result);
        }

        private async Task<IActionResult> RenderCourseAsync(int setId, string activeTab, string? topic, bool trackView)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await _studyService.GetVocabularyCourseAsync(setId, currentUserId);
            if (viewModel == null)
            {
                return NotFound();
            }

            viewModel.ActiveTab = activeTab;
            viewModel.TopicFilter = string.IsNullOrWhiteSpace(topic) ? null : topic.Trim();

            if (trackView)
            {
                await TrackVocabularySetViewAsync(setId);
            }

            return View("Detail", viewModel);
        }

        private async Task TrackVocabularySetViewAsync(int setId)
        {
            var viewedCookieName = GetViewedCookieName(setId);
            if (Request.Cookies.ContainsKey(viewedCookieName))
            {
                return;
            }

            var incremented = await _studyService.TryIncrementVocabularySetViewCountAsync(setId);
            if (!incremented)
            {
                return;
            }

            Response.Cookies.Append(
                viewedCookieName,
                "true",
                new CookieOptions { Expires = DateTime.UtcNow.AddDays(1) });
        }

        private static string GetViewedCookieName(int setId)
        {
            return $"ViewedSet_{setId}";
        }
    }

    public sealed class VocabularyReviewRecordRequest
    {
        public int CardId { get; set; }
        public string? Outcome { get; set; }
    }
}
