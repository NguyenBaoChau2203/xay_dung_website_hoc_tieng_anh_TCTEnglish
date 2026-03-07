using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Areas.Admin.ViewModels;
using TCTVocabulary.Models;

namespace TCTVocabulary.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = Roles.Admin)]
    public class UserManagementController : Controller
    {
        private readonly DbflashcardContext _context;

        public UserManagementController(DbflashcardContext context)
        {
            _context = context;
        }

        // GET: /Admin/UserManagement
        public async Task<IActionResult> Index(string? q, string? role, string? status)
        {
            // Base query — AsNoTracking for read-only listing
            var query = _context.Users.AsNoTracking().AsQueryable();

            // Search filter
            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(u =>
                    (u.FullName != null && u.FullName.Contains(q)) ||
                    u.Email.Contains(q));
            }

            // Role filter
            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => u.Role == role);
            }

            // Status filter
            if (!string.IsNullOrWhiteSpace(status))
            {
                var isActive = status == "active";
                query = query.Where(u => u.IsActive == isActive);
            }

            // KPI counts (on full dataset, not filtered)
            var totalUsers = await _context.Users.AsNoTracking().CountAsync();
            var activeUsers = await _context.Users.AsNoTracking().CountAsync(u => u.IsActive);

            // Project into ViewModel via .Select() — never pass raw entities
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new UserRowViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName ?? "N/A",
                    Email = u.Email,
                    AvatarUrl = u.AvatarUrl,
                    Role = u.Role ?? Roles.Student,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    FolderCount = u.Folders.Count,
                    SetCount = u.Sets.Count
                })
                .ToListAsync();

            var viewModel = new UserManagementViewModel
            {
                Users = users,
                SearchQuery = q,
                RoleFilter = role,
                StatusFilter = status,
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                DisabledUsers = totalUsers - activeUsers
            };

            return View(viewModel);
        }

        // GET: /Admin/UserManagement/GetUser/5  (AJAX — for edit modal)
        [HttpGet]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _context.Users.AsNoTracking()
                .Where(u => u.UserId == id)
                .Select(u => new EditUserViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName ?? "",
                    Email = u.Email,
                    Role = u.Role ?? Roles.Student,
                    IsActive = u.IsActive
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng." });

            return Json(user);
        }

        // POST: /Admin/UserManagement/Edit  (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromBody] EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { message = string.Join(", ", errors) });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == model.UserId);
            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng." });

            // Validate role value against allowed roles
            var allowedRoles = new[] { Roles.Admin, Roles.Teacher, Roles.Student };
            if (!allowedRoles.Contains(model.Role))
                return BadRequest(new { message = "Vai trò không hợp lệ." });

            // Check email uniqueness (exclude self)
            var emailTaken = await _context.Users.AsNoTracking()
                .AnyAsync(u => u.Email == model.Email && u.UserId != model.UserId);
            if (emailTaken)
                return BadRequest(new { message = "Email này đã được sử dụng bởi người dùng khác." });

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.Role = model.Role;
            user.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật thành công." });
        }

        // POST: /Admin/UserManagement/ToggleStatus  (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus([FromBody] ToggleStatusRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId);
            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng." });

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                isActive = user.IsActive,
                message = user.IsActive ? "Đã kích hoạt tài khoản." : "Đã vô hiệu hóa tài khoản."
            });
        }

        // POST: /Admin/UserManagement/Delete  (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromBody] DeleteUserRequest request)
        {
            var user = await _context.Users
                .Include(u => u.Sets).ThenInclude(s => s.Cards)
                .Include(u => u.Folders)
                .Include(u => u.ClassMembers)
                .Include(u => u.ClassMessages)
                .Include(u => u.LearningProgresses)
                .Include(u => u.SavedFolders)
                .Include(u => u.UserSpeakingProgresses)
                .FirstOrDefaultAsync(u => u.UserId == request.UserId);

            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng." });

            // Prevent self-deletion
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId == user.UserId.ToString())
                return BadRequest(new { message = "Không thể xóa chính tài khoản đang đăng nhập." });

            // Remove dependent entities first (they are already tracked via Include)
            _context.LearningProgresses.RemoveRange(user.LearningProgresses);
            _context.UserSpeakingProgresses.RemoveRange(user.UserSpeakingProgresses);
            _context.SavedFolders.RemoveRange(user.SavedFolders);
            _context.ClassMessages.RemoveRange(user.ClassMessages);
            _context.ClassMembers.RemoveRange(user.ClassMembers);

            foreach (var set in user.Sets)
            {
                _context.Cards.RemoveRange(set.Cards);
            }
            _context.Sets.RemoveRange(user.Sets);
            _context.Folders.RemoveRange(user.Folders);

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa người dùng." });
        }
    }

    // Request DTOs for AJAX actions
    public class ToggleStatusRequest
    {
        public int UserId { get; set; }
    }

    public class DeleteUserRequest
    {
        public int UserId { get; set; }
    }
}
