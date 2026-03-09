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
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

            // FIX: Aggregated KPI counts for single round-trip per entity
            var userStats = await _context.Users.AsNoTracking()
                .GroupBy(x => 1)
                .Select(g => new { 
                    Total = g.Count(), 
                    NewItems = g.Count(u => u.CreatedAt >= sevenDaysAgo) 
                })
                .FirstOrDefaultAsync();

            var classStats = await _context.Classes.AsNoTracking()
                .GroupBy(x => 1)
                .Select(g => new { 
                    Total = g.Count(), 
                    NewItems = g.Count(c => c.CreatedAt >= sevenDaysAgo) 
                })
                .FirstOrDefaultAsync();

            var setStats = await _context.Sets.AsNoTracking()
                .GroupBy(x => 1)
                .Select(g => new { 
                    Total = g.Count(), 
                    NewItems = g.Count(s => s.CreatedAt >= sevenDaysAgo) 
                })
                .FirstOrDefaultAsync();

            var totalUsers = userStats?.Total ?? 0;
            var newUsersLast7Days = userStats?.NewItems ?? 0;
            var totalClasses = classStats?.Total ?? 0;
            var newClassesLast7Days = classStats?.NewItems ?? 0;
            var totalSets = setStats?.Total ?? 0;
            var newSetsLast7Days = setStats?.NewItems ?? 0;

            // FIX: Project into ViewModel via .Select() — never pass raw entities to views
            var recentUsers = await _context.Users.AsNoTracking()
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .Select(u => new RecentUserViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName ?? "N/A",
                    Email = u.Email,
                    Role = u.Role ?? Roles.Standard,
                    Status = u.Status,
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
