using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Areas.Admin.ViewModels;
using TCTVocabulary.Models;

namespace TCTVocabulary.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = Roles.Admin)]
    public class DashboardController : Controller
    {
        private readonly DbflashcardContext _context;

        public DashboardController(DbflashcardContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var sevenDaysAgo = DateTime.Now.AddDays(-7);

            // FIX: Single round-trip with AsNoTracking for read-only KPI aggregation
            var totalUsers = await _context.Users.AsNoTracking().CountAsync();
            var totalClasses = await _context.Classes.AsNoTracking().CountAsync();
            var totalSets = await _context.Sets.AsNoTracking().CountAsync();

            var newUsersLast7Days = await _context.Users.AsNoTracking()
                .CountAsync(u => u.CreatedAt >= sevenDaysAgo);
            var newClassesLast7Days = await _context.Classes.AsNoTracking()
                .CountAsync(c => c.CreatedAt >= sevenDaysAgo);
            var newSetsLast7Days = await _context.Sets.AsNoTracking()
                .CountAsync(s => s.CreatedAt >= sevenDaysAgo);

            // FIX: Project into ViewModel via .Select() — never pass raw entities to views
            var recentUsers = await _context.Users.AsNoTracking()
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .Select(u => new RecentUserViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName ?? "N/A",
                    Email = u.Email,
                    Role = u.Role ?? Roles.Student,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            var viewModel = new AdminDashboardViewModel
            {
                TotalUsers = totalUsers,
                TotalClasses = totalClasses,
                TotalSets = totalSets,
                NewUsersLast7Days = newUsersLast7Days,
                NewClassesLast7Days = newClassesLast7Days,
                NewSetsLast7Days = newSetsLast7Days,
                RecentUsers = recentUsers
            };

            return View(viewModel);
        }
    }
}
