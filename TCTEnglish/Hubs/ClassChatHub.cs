using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using TCTVocabulary.Realtime;
using TCTVocabulary.Security;
using TCTVocabulary.Services;

namespace TCTVocabulary.Hubs
{
    public class ClassChatHub : Hub
    {
        public const string AdminPresenceGroupName = "admin-user-management";

        private readonly DbflashcardContext _context;
        private readonly IClassService _classService;
        private readonly ILogger<ClassChatHub> _logger;

        public ClassChatHub(
            DbflashcardContext context,
            IClassService classService,
            ILogger<ClassChatHub> logger)
        {
            _context = context;
            _classService = classService;
            _logger = logger;
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
                _logger.LogWarning("Anonymous connection attempted to join class {classId}", classId);
                return;
            }

            if (!await _classService.CanAccessClassAsync(classId, userId.Value, Context.User?.IsInRole(Roles.Admin) == true))
            {
                _logger.LogWarning("Access denied when user {userId} tried to join class {classId}", userId.Value, classId);
                throw new HubException("Không có quyền truy cập lớp.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"class-{classId}");
            _logger.LogInformation("User {userId} joined class group {classId}", userId.Value, classId);
        }

        public async Task SendMessage(int classId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogDebug("Ignored empty message for class {classId}", classId);
                return;
            }

            var userId = TryGetCurrentUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Anonymous connection attempted to send message to class {classId}", classId);
                return;
            }

            if (!await _classService.CanAccessClassAsync(classId, userId.Value, Context.User?.IsInRole(Roles.Admin) == true))
            {
                _logger.LogWarning("Access denied when user {userId} tried to send message to class {classId}", userId.Value, classId);
                throw new HubException("Không có quyền truy cập lớp.");
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                _logger.LogWarning("Cannot send message because user {userId} was not found", userId.Value);
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

            _logger.LogInformation(
                "Message created in class {classId} by user {userId}, messageId {messageId}",
                classId,
                userId.Value,
                message.MessageId);

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
                _logger.LogDebug("Ignored empty image message for class {classId}", classId);
                return;
            }

            var userId = TryGetCurrentUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Anonymous connection attempted to send image to class {classId}", classId);
                return;
            }

            if (!await _classService.CanAccessClassAsync(classId, userId.Value, Context.User?.IsInRole(Roles.Admin) == true))
            {
                _logger.LogWarning("Access denied when user {userId} tried to send image to class {classId}", userId.Value, classId);
                throw new HubException("Không có quyền truy cập lớp.");
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                _logger.LogWarning("Cannot send image because user {userId} was not found", userId.Value);
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

            _logger.LogInformation(
                "Image message created in class {classId} by user {userId}, messageId {messageId}",
                classId,
                userId.Value,
                message.MessageId);

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
            if (Context.User.TryGetUserId(out var userId))
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
                _logger.LogWarning("Cannot update presence because user {userId} was not found", userId);
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

            _logger.LogInformation(
                "Presence changed for user {userId}: previousStatus {previousStatus}, currentStatus {currentStatus}",
                userId,
                previousStatus,
                currentStatus);

            await Clients.Group(AdminPresenceGroupName)
                .SendAsync("UserStatusChanged",
                    AdminUserStatusChangedMessage.Create(userId, previousStatus, currentStatus));
        }
    }
}
