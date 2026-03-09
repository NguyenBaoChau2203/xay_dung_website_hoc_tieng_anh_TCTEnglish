// Template: Controller
// Usage: Copy this template when creating a new Controller in TCTEnglish
// File location: TCTEnglish/Controllers/{Feature}Controller.cs
//
// Instructions:
//   1. Replace {Feature} with your feature name (e.g., Playlist, Goal, Report)
//   2. Replace {feature} with camelCase version (e.g., playlist, goal, report)
//   3. Replace I{Feature}Service with your actual service interface
//   4. Replace {Feature}ViewModel with your actual ViewModel
//   5. Add/remove actions as needed
//   6. Register service in Program.cs: builder.Services.AddScoped<I{Feature}Service, {Feature}Service>();

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Models;
using TCTEnglish.Services;
using TCTEnglish.ViewModels;

namespace TCTEnglish.Controllers
{
    [Authorize]  // Change to [Authorize(Roles = "Admin")] for admin-only
    public class {Feature}Controller : BaseController
    {
        private readonly DbflashcardContext _context;
        private readonly I{Feature}Service _{feature}Service;
        private readonly ILogger<{Feature}Controller> _logger;

        public {Feature}Controller(
            DbflashcardContext context,
            I{Feature}Service {feature}Service,
            ILogger<{Feature}Controller> logger)
        {
            _context = context;
            _{feature}Service = {feature}Service;
            _logger = logger;
        }

        // GET: /{Feature}
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            var viewModel = await _{feature}Service.GetAllAsync(userId);
            return View(viewModel);
        }

        // GET: /{Feature}/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            var viewModel = await _{feature}Service.GetByIdAsync(id, userId);
            if (viewModel == null) return NotFound();
            return View(viewModel);
        }

        // GET: /{Feature}/Create
        public IActionResult Create()
        {
            return View(new {Feature}ViewModel());
        }

        // POST: /{Feature}/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create({Feature}ViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var userId = GetCurrentUserId();
            var result = await _{feature}Service.CreateAsync(model, userId);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "An error occurred.");
                return View(model);
            }

            TempData["Success"] = "{Feature} created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /{Feature}/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetCurrentUserId();
            var viewModel = await _{feature}Service.GetByIdAsync(id, userId);
            if (viewModel == null) return NotFound();
            return View(viewModel);
        }

        // POST: /{Feature}/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, {Feature}ViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var userId = GetCurrentUserId();
            var result = await _{feature}Service.UpdateAsync(id, model, userId);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Update failed.");
                return View(model);
            }

            TempData["Success"] = "{Feature} updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /{Feature}/Delete/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();
            var result = await _{feature}Service.DeleteAsync(id, userId);

            if (!result.Success)
            {
                TempData["Error"] = result.Error ?? "Delete failed.";
            }
            else
            {
                TempData["Success"] = "{Feature} deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
