using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTVocabulary.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class ChatController : BaseController
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IClassService _classService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            IClassService classService,
            IFileStorageService fileStorageService,
            ILogger<ChatController> logger)
        {
            _classService = classService;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImage(IFormFile image, int classId)
        {
            if (image == null || image.Length == 0)
            {
                _logger.LogWarning("Invalid chat image upload request for class {classId}", classId);
                return BadRequest();
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                _logger.LogWarning("Unauthorized chat image upload attempt for class {classId}", classId);
                return Unauthorized();
            }

            var canAccessClass = await _classService.CanAccessClassAsync(classId, userId, IsAdminUser());
            if (!canAccessClass)
            {
                _logger.LogWarning("Access denied for chat image upload by user {userId} to class {classId}", userId, classId);
                return NotFound();
            }

            try
            {
                var imageUrl = await _fileStorageService.SaveImageAsync(image, ImageUploadPolicies.ChatImage);
                _logger.LogInformation(
                    "Chat image uploaded by user {userId} for class {classId}, sizeBytes {fileSizeBytes}",
                    userId,
                    classId,
                    image.Length);
                return Json(new { imageUrl });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Chat image upload rejected for user {userId}, class {classId}", userId, classId);
                return BadRequest(ex.Message);
            }
        }
    }
}
