using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TCTVocabulary.Services;

namespace TCTVocabulary.Controllers
{
    [Authorize]
    public class VocabularyController : BaseController
    {
        private readonly IStudyService _studyService;

        public VocabularyController(IStudyService studyService)
        {
            _studyService = studyService;
        }

        public async Task<IActionResult> Index()
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await _studyService.GetVocabularyIndexViewModelAsync(currentUserId);
            return View(viewModel);
        }

        public async Task<IActionResult> Detail(int setId)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await _studyService.GetVocabularySetDetailViewModelAsync(setId, currentUserId);
            if (viewModel == null)
            {
                return NotFound();
            }

            await TrackVocabularySetViewAsync(setId);
            return View(viewModel);
        }

        public async Task<IActionResult> Topics(int setId)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await _studyService.GetVocabularyTopicsViewModelAsync(setId, currentUserId);
            return viewModel == null ? NotFound() : View(viewModel);
        }

        public async Task<IActionResult> TopicDetail(int setId, string? topic)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await _studyService.GetVocabularyTopicDetailViewModelAsync(setId, currentUserId, topic);
            return viewModel == null ? NotFound() : View(viewModel);
        }

        public async Task<IActionResult> Study(int setId, string? topic = null, int index = 1, string mode = "all")
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await _studyService.GetVocabularyStudyViewModelAsync(setId, currentUserId, topic, index, mode);
            if (viewModel == null)
            {
                return NotFound();
            }

            await TrackVocabularySetViewAsync(setId);
            return View("Study", viewModel);
        }

        public async Task<IActionResult> FolderDetail(int folderId)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return RedirectToAction("Login", "Account");
            }

            var viewModel = await _studyService.GetVocabularyFolderDetailViewModelAsync(folderId, currentUserId);
            return viewModel == null ? NotFound() : View(viewModel);
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
}
