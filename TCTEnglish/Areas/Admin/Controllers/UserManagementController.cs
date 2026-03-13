using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Areas.Admin.ViewModels;
using TCTVocabulary.Controllers;
using TCTVocabulary.Hubs;
using TCTVocabulary.Models;
using TCTVocabulary.Realtime;
using TCTVocabulary.Services;

namespace TCTVocabulary.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = Roles.Admin)]
    public class UserManagementController : BaseController
    {
        private const int PageSize = 20;

        private readonly DbflashcardContext _context;
        private readonly IAppEmailSender _emailSender;
        private readonly IHubContext<ClassChatHub> _hubContext;

        public UserManagementController(
            DbflashcardContext context,
            IAppEmailSender emailSender,
            IHubContext<ClassChatHub> hubContext)
        {
            _context = context;
            _emailSender = emailSender;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Index(string? q, string? role, string? status, int page = 1)
        {
            var query = BuildFilteredQuery(q, role, status);
            var totalFilteredCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalFilteredCount / (double)PageSize);
            page = Math.Clamp(page, 1, Math.Max(1, totalPages));

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
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

            ApplyLivePresence(users);
            var summary = await BuildSummaryAsync();

            return View(new UserManagementViewModel
            {
                Users = users,
                SearchQuery = q,
                RoleFilter = role,
                StatusFilter = status,
                TotalUsers = summary.TotalUsers,
                OnlineUsers = summary.OnlineUsers,
                OfflineUsers = summary.OfflineUsers,
                BlockedUsers = summary.BlockedUsers,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = PageSize,
                TotalFilteredCount = totalFilteredCount
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _context.Users.AsNoTracking()
                .Where(u => u.UserId == id)
                .Select(u => new EditUserViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName ?? string.Empty,
                    Email = u.Email,
                    Role = u.Role ?? Roles.Standard,
                    Status = u.Status
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

             if (user.Status != UserStatus.Blocked)
            {
                user.Status = ResolveActiveStatus(user.UserId);
            }

            return Json(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromBody] UpdateUserRequest model)
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
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            var allowedRoles = new[] { Roles.Admin, Roles.Premium, Roles.Standard };
            if (!allowedRoles.Contains(model.Role))
            {
                return BadRequest(new { message = "Vai trò không hợp lệ." });
            }

            var emailTaken = await _context.Users.AsNoTracking()
                .AnyAsync(u => u.Email == model.Email && u.UserId != model.UserId);
            if (emailTaken)
            {
                return BadRequest(new { message = "Email này đã được sử dụng bởi người dùng khác." });
            }

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.Role = model.Role;

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Cập nhật thành công.",
                user = CreateRowUpdateViewModel(user)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusRequest request)
        {
            if (!Enum.IsDefined(request.Status))
            {
                return BadRequest(new { message = "Trạng thái không hợp lệ." });
            }

            if (request.Status == UserStatus.Blocked)
            {
                return BadRequest(new { message = "Vui lòng dùng chức năng khóa tài khoản." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            var previousStatus = user.Status;
            user.Status = ResolveActiveStatus(user.UserId);
            if (previousStatus == UserStatus.Blocked)
            {
                user.LockReason = null;
                user.LockExpiry = null;
            }
            await _context.SaveChangesAsync();

            var statusUpdate = AdminUserStatusChangedMessage.Create(user.UserId, previousStatus, user.Status);
            await BroadcastStatusUpdateAsync(previousStatus, user.Status, statusUpdate);

            return Json(new
            {
                success = true,
                message = user.Status == UserStatus.Online
                    ? "Tài khoản đang trực tuyến."
                    : "Tài khoản đã chuyển sang Offline.",
                statusUpdate
            });
        }

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
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            if (GetCurrentUserId() == user.UserId)
            {
                return BadRequest(new { message = "Không thể khóa chính tài khoản đang đăng nhập." });
            }

            var lockExpiry = request.Duration switch
            {
                "30s" => DateTime.UtcNow.AddSeconds(30),
                "1d" => DateTime.UtcNow.AddDays(1),
                "3d" => DateTime.UtcNow.AddDays(3),
                "7d" => DateTime.UtcNow.AddDays(7),
                "permanent" => DateTime.MaxValue,
                _ => (DateTime?)null
            };

            if (!lockExpiry.HasValue)
            {
                return BadRequest(new { message = "Thời hạn khóa không hợp lệ." });
            }

            var previousStatus = user.Status;

            user.Status = UserStatus.Blocked;
            user.LockReason = request.Reason;
            user.LockExpiry = lockExpiry.Value;
            await _context.SaveChangesAsync();

            await _emailSender.SendBlockedNotificationAsync(user.Email, request.Reason, lockExpiry.Value);

            var statusUpdate = AdminUserStatusChangedMessage.Create(user.UserId, previousStatus, user.Status);
            await BroadcastStatusUpdateAsync(previousStatus, user.Status, statusUpdate);

            return Json(new
            {
                success = true,
                message = "Đã khóa tài khoản thành công.",
                statusUpdate
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlockUser([FromBody] UnlockUserRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId);
            if (user == null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            if (user.Status != UserStatus.Blocked)
            {
                return BadRequest(new { message = "Người dùng không ở trạng thái bị khóa." });
            }

            var previousStatus = user.Status;

            user.Status = ResolveActiveStatus(user.UserId);
            user.LockReason = null;
            user.LockExpiry = null;
            await _context.SaveChangesAsync();

            await _emailSender.SendUnlockedNotificationAsync(user.Email, isAutoUnlock: false);

            var statusUpdate = AdminUserStatusChangedMessage.Create(user.UserId, previousStatus, user.Status);
            await BroadcastStatusUpdateAsync(previousStatus, user.Status, statusUpdate);

            return Json(new
            {
                success = true,
                message = "Đã mở khóa tài khoản thành công.",
                statusUpdate
            });
        }

        private IQueryable<User> BuildFilteredQuery(string? q, string? role, string? status)
        {
            var query = _context.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(u =>
                    (u.FullName != null && u.FullName.Contains(q)) ||
                    u.Email.Contains(q));
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => u.Role == role);
            }

            if (string.IsNullOrWhiteSpace(status)
                || !Enum.TryParse<UserStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                return query;
            }

            var onlineUserIds = UserPresenceTracker.GetOnlineUserIds();

            return parsedStatus switch
            {
                UserStatus.Online when onlineUserIds.Count == 0 => query.Where(_ => false),
                UserStatus.Online => query.Where(u => u.Status != UserStatus.Blocked && onlineUserIds.Contains(u.UserId)),
                UserStatus.Offline when onlineUserIds.Count == 0 => query.Where(u => u.Status != UserStatus.Blocked),
                UserStatus.Offline => query.Where(u => u.Status != UserStatus.Blocked && !onlineUserIds.Contains(u.UserId)),
                _ => query.Where(u => u.Status == UserStatus.Blocked)
            };
        }

        private void ApplyLivePresence(IEnumerable<UserRowViewModel> users)
        {
            foreach (var user in users)
            {
                if (user.Status == UserStatus.Blocked)
                {
                    continue;
                }

                user.Status = ResolveActiveStatus(user.UserId);
            }
        }

        private async Task<(int TotalUsers, int OnlineUsers, int OfflineUsers, int BlockedUsers)> BuildSummaryAsync()
        {
            var totalUsers = await _context.Users.AsNoTracking().CountAsync();
            var blockedUsers = await _context.Users.AsNoTracking()
                .CountAsync(u => u.Status == UserStatus.Blocked);

            var onlineUserIds = UserPresenceTracker.GetOnlineUserIds();
            var onlineUsers = onlineUserIds.Count == 0
                ? 0
                : await _context.Users.AsNoTracking()
                    .CountAsync(u => u.Status != UserStatus.Blocked && onlineUserIds.Contains(u.UserId));

            return (
                totalUsers,
                onlineUsers,
                Math.Max(0, totalUsers - onlineUsers - blockedUsers),
                blockedUsers);
        }

        private UserStatus ResolveActiveStatus(int userId)
        {
            return UserPresenceTracker.IsUserOnline(userId)
                ? UserStatus.Online
                : UserStatus.Offline;
        }

        private UserRowUpdateViewModel CreateRowUpdateViewModel(User user)
        {
            return new UserRowUpdateViewModel
            {
                UserId = user.UserId,
                FullName = user.FullName ?? "N/A",
                Email = user.Email,
                Role = user.Role ?? Roles.Standard,
                Status = user.Status == UserStatus.Blocked
                    ? UserStatus.Blocked
                    : ResolveActiveStatus(user.UserId)
            };
        }

        private async Task BroadcastStatusUpdateAsync(
            UserStatus previousStatus,
            UserStatus currentStatus,
            AdminUserStatusChangedMessage statusUpdate)
        {
            if (previousStatus == currentStatus)
            {
                return;
            }

            await _hubContext.Clients.Group(ClassChatHub.AdminPresenceGroupName)
                .SendAsync("UserStatusChanged", statusUpdate);
        }
    }
}
