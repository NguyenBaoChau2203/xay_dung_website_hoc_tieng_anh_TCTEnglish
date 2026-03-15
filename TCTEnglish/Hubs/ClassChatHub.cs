using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TCTVocabulary.Models;
using TCTVocabulary.Realtime;

namespace TCTVocabulary.Hubs
{
    public class ClassChatHub : Hub
    {
        public const string AdminPresenceGroupName = "admin-user-management";

        private readonly DbflashcardContext _context;

        public ClassChatHub(DbflashcardContext context)
        {
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = TryGetCurrentUserId();
            if (userId.HasValue)
            {
                var presenceChange = UserPresenceTracker.AddConnection(userId.Value, Context.ConnectionId);

                if (Context.User?.IsInRole(Roles.Admin) == true)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, AdminPresenceGroupName);
                }

                if (presenceChange.BecameOnline)
                {
                    await UpdatePresenceStatusAsync(userId.Value, UserStatus.Online);
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var presenceChange = UserPresenceTracker.RemoveConnection(Context.ConnectionId);
            if (presenceChange?.WentOffline == true)
            {
                // FIX: absorb brief reload/reconnect gaps before broadcasting the user offline.
                await Task.Delay(UserPresenceTracker.OfflineTransitionDelay);
                if (!UserPresenceTracker.IsUserOnline(presenceChange.UserId))
                {
                    await UpdatePresenceStatusAsync(presenceChange.UserId, UserStatus.Offline);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinClass(int classId)
        {
            var userId = TryGetCurrentUserId();
            if (!userId.HasValue)
            {
                return;
            }

            var isMember = await _context.ClassMembers
                .AnyAsync(cm => cm.ClassId == classId && cm.UserId == userId.Value);

            var isOwner = await _context.Classes
                .AnyAsync(c => c.ClassId == classId && c.OwnerId == userId.Value);

            if (!isMember && !isOwner)
            {
                throw new HubException("Không có quyền truy cập lớp.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"class-{classId}");
        }

        public async Task SendMessage(int classId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            var userId = TryGetCurrentUserId();
            if (!userId.HasValue)
            {
                return;
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                return;
            }

            var createdAt = DateTime.UtcNow;
            var message = new ClassMessage
            {
                ClassId = classId,
                UserId = userId.Value,
                Content = content,
                CreatedAt = createdAt
            };

            _context.ClassMessages.Add(message);
            await _context.SaveChangesAsync();

            await Clients.Group($"class-{classId}")
                .SendAsync("ReceiveMessage", new
                {
                    userId = userId.Value,
                    fullName = user.FullName,
                    content,
                    createdAt
                });
        }

        public async Task SendImageMessage(int classId, string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return;
            }

            var userId = TryGetCurrentUserId();
            if (!userId.HasValue)
            {
                return;
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                return;
            }

            var message = new ClassMessage
            {
                ClassId = classId,
                UserId = userId.Value,
                Content = imageUrl,
                CreatedAt = DateTime.UtcNow
            };

            _context.ClassMessages.Add(message);
            await _context.SaveChangesAsync();

            await Clients.Group($"class-{classId}")
                .SendAsync("ReceiveImage", new
                {
                    userId = userId.Value,
                    fullName = user.FullName,
                    imageUrl
                });
        }

        private int? TryGetCurrentUserId()
        {
            if (int.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return userId;
            }

            return null;
        }

        private async Task UpdatePresenceStatusAsync(int userId, UserStatus requestedStatus)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                return;
            }

            var previousStatus = user.Status;
            var currentStatus = previousStatus == UserStatus.Blocked
                ? UserStatus.Blocked
                : requestedStatus;

            if (previousStatus == currentStatus)
            {
                return;
            }

            user.Status = currentStatus;
            await _context.SaveChangesAsync();

            await Clients.Group(AdminPresenceGroupName)
                .SendAsync("UserStatusChanged",
                    AdminUserStatusChangedMessage.Create(userId, previousStatus, currentStatus));
        }
    }
}
