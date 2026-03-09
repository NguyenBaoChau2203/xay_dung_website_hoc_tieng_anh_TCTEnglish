using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Areas.Admin.ViewModels;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTVocabulary.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = Roles.Admin)]
    public class UserManagementController : Controller
    {
        private readonly DbflashcardContext _context;
        private readonly IAppEmailSender _emailSender;

        public UserManagementController(DbflashcardContext context, IAppEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
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
            if (!string.IsNullOrWhiteSpace(status)
                && Enum.TryParse<UserStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                query = query.Where(u => u.Status == parsedStatus);
            }

            // KPI counts (on full dataset, not filtered)
            var stats = await _context.Users.AsNoTracking()
                .GroupBy(x => 1)
                .Select(g => new {
                    TotalUsers = g.Count(),
                    OnlineUsers = g.Count(u => u.Status == UserStatus.Online),
                    BlockedUsers = g.Count(u => u.Status == UserStatus.Blocked)
                })
                .FirstOrDefaultAsync();

            var totalUsers = stats?.TotalUsers ?? 0;
            var onlineUsers = stats?.OnlineUsers ?? 0;
            var blockedUsers = stats?.BlockedUsers ?? 0;

            // Project into ViewModel via .Select() — never pass raw entities
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new UserRowViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName ?? "N/A",
                    Email = u.Email,
                    AvatarUrl = u.AvatarUrl,
                    Role = u.Role ?? Roles.Standard,
                    Status = u.Status,
                    CreatedAt = u.CreatedAt,
                    FolderCount = u.Folders.Count,
                    SetCount = u.Sets.Count,
                    LockReason = u.LockReason,
                    LockExpiry = u.LockExpiry
                })
                .ToListAsync();

            var viewModel = new UserManagementViewModel
            {
                Users = users,
                SearchQuery = q,
                RoleFilter = role,
                StatusFilter = status,
                TotalUsers = totalUsers,
                OnlineUsers = onlineUsers,
                OfflineUsers = totalUsers - onlineUsers - blockedUsers,
                BlockedUsers = blockedUsers
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
                    Role = u.Role ?? Roles.Standard,
                    Status = u.Status
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
            var allowedRoles = new[] { Roles.Admin, Roles.Premium, Roles.Standard };
            if (!allowedRoles.Contains(model.Role))
                return BadRequest(new { message = "Vai trò không hợp lệ." });

            // Validate status value
            if (!Enum.IsDefined(model.Status))
                return BadRequest(new { message = "Trạng thái không hợp lệ." });

            // Check email uniqueness (exclude self)
            var emailTaken = await _context.Users.AsNoTracking()
                .AnyAsync(u => u.Email == model.Email && u.UserId != model.UserId);
            if (emailTaken)
                return BadRequest(new { message = "Email này đã được sử dụng bởi người dùng khác." });

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.Role = model.Role;
            user.Status = model.Status;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật thành công." });
        }

        // POST: /Admin/UserManagement/UpdateStatus  (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusRequest request)
        {
            if (!Enum.IsDefined(request.Status))
                return BadRequest(new { message = "Trạng thái không hợp lệ." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId);
            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng." });

            user.Status = request.Status;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                status = (int)user.Status,
                message = user.Status switch
                {
                    UserStatus.Online => "Đã kích hoạt tài khoản.",
                    UserStatus.Blocked => "Đã chặn tài khoản.",
                    _ => "Tài khoản đã chuyển sang Offline."
                }
            });
        }

        // POST: /Admin/UserManagement/BlockUser  (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BlockUser([FromBody] BlockUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { message = string.Join(", ", errors) });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId);
            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng." });

            // Prevent blocking self
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId == user.UserId.ToString())
                return BadRequest(new { message = "Không thể khóa chính tài khoản đang đăng nhập." });

            // Calculate LockExpiry based on duration
            DateTime? lockExpiry = request.Duration switch
            {
                "30s" => DateTime.UtcNow.AddSeconds(30),
                "1d" => DateTime.UtcNow.AddDays(1),
                "3d" => DateTime.UtcNow.AddDays(3),
                "7d" => DateTime.UtcNow.AddDays(7),
                "permanent" => DateTime.MaxValue,
                _ => null
            };

            if (lockExpiry == null)
                return BadRequest(new { message = "Thời hạn khóa không hợp lệ." });

            user.Status = UserStatus.Blocked;
            user.LockReason = request.Reason;
            user.LockExpiry = lockExpiry.Value;
            await _context.SaveChangesAsync();

            await _emailSender.SendBlockedNotificationAsync(user.Email, request.Reason, lockExpiry.Value);

            return Json(new { success = true, message = "Đã khóa tài khoản thành công." });
        }

        // POST: /Admin/UserManagement/UnlockUser  (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlockUser([FromBody] UnlockUserRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId);
            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng." });

            if (user.Status != UserStatus.Blocked)
                return BadRequest(new { message = "Người dùng không ở trạng thái bị khóa." });

            user.Status = UserStatus.Offline;
            user.LockReason = null;
            user.LockExpiry = null;
            await _context.SaveChangesAsync();

            await _emailSender.SendUnlockedNotificationAsync(user.Email, isAutoUnlock: false);

            return Json(new { success = true, message = "Đã mở khóa tài khoản thành công." });
        }
    }

}
