using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTVocabulary.Services;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    [Route("Goals")]
    public class GoalsController : BaseController
    {
        private const string GoalEditorFieldName = $"{nameof(GoalsViewModel.GoalEditor)}.{nameof(UpdateGoalInputViewModel.DailyGoal)}";
        private readonly IGoalsService _goalsService;

        public GoalsController(IGoalsService goalsService)
        {
            _goalsService = goalsService;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var model = await _goalsService.GetGoalsAsync(GetCurrentUserId());
            return model == null ? NotFound() : View(model);
        }

        [HttpPost("UpdateGoal")]
        public async Task<IActionResult> UpdateGoal(
            [Bind(Prefix = nameof(GoalsViewModel.GoalEditor))] UpdateGoalInputViewModel input)
        {
            var userId = GetCurrentUserId();

            if (!ModelState.IsValid)
            {
                return await ReturnEditorViewAsync(userId, input);
            }

            var result = await _goalsService.UpdateGoalAsync(userId, input.DailyGoal);
            if (result.Status == OperationStatus.Success)
            {
                TempData["SuccessMessage"] = "Cập nhật mục tiêu ngày thành công.";
                return RedirectToAction(nameof(Index));
            }

            if (result.Status == OperationStatus.NotFound)
            {
                return NotFound();
            }

            ModelState.AddModelError(
                GoalEditorFieldName,
                result.ErrorMessage ?? "Không thể cập nhật mục tiêu ngày lúc này.");

            return await ReturnEditorViewAsync(userId, input);
        }

        private async Task<IActionResult> ReturnEditorViewAsync(int userId, UpdateGoalInputViewModel input)
        {
            var model = await _goalsService.GetGoalsAsync(userId);
            if (model == null)
            {
                return NotFound();
            }

            model.GoalEditor = input;
            model.ShowGoalEditor = true;

            return View(nameof(Index), model);
        }
    }
}
