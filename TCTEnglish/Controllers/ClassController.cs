using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTVocabulary.Services;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Controllers
{
    [AutoValidateAntiforgeryToken]
    [Route("Home")]
    public class ClassController : BaseController
    {
        private readonly IClassService _classService;
        private readonly ILogger<ClassController> _logger;

        public ClassController(IClassService classService, ILogger<ClassController> logger)
        {
            _classService = classService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("Class")]
        [ActionName("Class")]
        public async Task<IActionResult> Index()
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var vm = await _classService.GetClassPageAsync(userId);
            return View("Class", vm);
        }

        [Authorize]
        [HttpGet("CreateClass")]
        public IActionResult CreateClass()
        {
            return View();
        }

        [Authorize]
        [HttpPost("CreateClass")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClass(CreateClassViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(nameof(CreateClass), model);
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var classId = await _classService.CreateClassAsync(model, userId);
                return RedirectToAction(nameof(ClassDetail), "Class", new { id = classId });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.Avatar), ex.Message);
                return View(nameof(CreateClass), model);
            }
        }

        [Authorize]
        [HttpPost("EditClass")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditClass(int classId, string className, string? description)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                _logger.LogWarning("Unauthorized class edit attempt for class {classId}", classId);
                return Unauthorized();
            }

            var result = await _classService.UpdateClassAsync(classId, className, description, userId, IsAdminUser());
            return result.Status == OperationStatus.Success ? Ok() : NotFound(result.ErrorMessage);
        }

        [Authorize]
        [HttpPost("DeleteClass")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteClass(int classId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                _logger.LogWarning("Unauthorized class delete attempt for class {classId}", classId);
                return Unauthorized();
            }

            var result = await _classService.DeleteClassAsync(classId, userId, IsAdminUser());
            return result.Status == OperationStatus.Success ? Ok() : NotFound(result.ErrorMessage);
        }

        [Authorize]
        [HttpGet("ClassDetail/{id:int?}")]
        public async Task<IActionResult> ClassDetail(int id)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var vm = await _classService.GetClassDetailAsync(id, userId, IsAdminUser());
            if (vm == null)
            {
                return NotFound();
            }

            return View(nameof(ClassDetail), vm);
        }

        [Authorize]
        [HttpPost("AddFolderToClass")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFolderToClass(int classId, int folderId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                _logger.LogWarning("Unauthorized add folder attempt for class {classId}, folder {folderId}", classId, folderId);
                return Unauthorized();
            }

            var result = await _classService.AddFolderToClassAsync(classId, folderId, userId, IsAdminUser());

            return result.Status switch
            {
                OperationStatus.Success => Ok(),
                OperationStatus.Invalid => BadRequest(result.ErrorMessage),
                _ => NotFound(result.ErrorMessage)
            };
        }

        [Authorize]
        [HttpPost("RemoveFolderFromClass")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFolderFromClass(int classId, int folderId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                _logger.LogWarning("Unauthorized remove folder attempt for class {classId}, folder {folderId}", classId, folderId);
                return Unauthorized();
            }

            var result = await _classService.RemoveFolderFromClassAsync(classId, folderId, userId, IsAdminUser());
            return result.Status == OperationStatus.Success ? Ok() : NotFound(result.ErrorMessage);
        }

        [Authorize]
        [HttpPost("KickMember")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KickMember(int classId, int userId)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                _logger.LogWarning("Unauthorized kick member attempt for class {classId}, member {userId}", classId, userId);
                return Unauthorized();
            }

            var result = await _classService.KickMemberAsync(classId, userId, currentUserId, IsAdminUser());
            if (result.Status == OperationStatus.Success)
            {
                _logger.LogInformation("Member {memberUserId} kicked from class {classId} by user {userId}", userId, classId, currentUserId);
            }

            return result.Status switch
            {
                OperationStatus.Success => Ok(),
                OperationStatus.Invalid => BadRequest(result.ErrorMessage),
                _ => NotFound(result.ErrorMessage)
            };
        }

        [Authorize]
        [HttpGet("SearchClass")]
        public async Task<IActionResult> SearchClass(string keyword)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                _logger.LogWarning("Unauthorized class search attempt.");
                return Unauthorized();
            }

            var classes = await _classService.SearchClassesAsync(keyword, userId);
            return Ok(classes);
        }

        [Authorize]
        [HttpPost("JoinClass")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinClass(int classId, string? password)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _classService.JoinClassAsync(classId, password, userId);
            if (result.Status == OperationStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == OperationStatus.Invalid)
            {
                return BadRequest(result.ErrorMessage);
            }

            return Json(new
            {
                redirectUrl = Url.Action(nameof(ClassDetail), "Class", new { id = classId })
            });
        }

        [Authorize]
        [HttpPost("LeaveClass")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveClass(int classId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                _logger.LogWarning("Unauthorized leave class attempt for class {classId}", classId);
                return Unauthorized();
            }

            await _classService.LeaveClassAsync(classId, userId);
            return Ok();
        }
    }
}
