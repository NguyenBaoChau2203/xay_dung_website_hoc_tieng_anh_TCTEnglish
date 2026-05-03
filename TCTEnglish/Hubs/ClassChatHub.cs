using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Realtime;
using TCTEnglish.Services.AI;
using TCTVocabulary.Models;
using TCTVocabulary.Realtime;
using TCTVocabulary.Security;
using TCTVocabulary.Services;

namespace TCTEnglish.Hubs
{
    public class ClassChatHub : Hub
    {
        public const string AdminPresenceGroupName = "admin-user-management";

        private readonly DbflashcardContext _context;
        private readonly IClassService _classService;
        private readonly IAiStreamingService _aiStreamingService;
        private readonly ILogger<ClassChatHub> _logger;

        public ClassChatHub(
            DbflashcardContext context,
            IClassService classService,
            IAiStreamingService aiStreamingService,
            ILogger<ClassChatHub> logger)
        {
            _context = context;
            _classService = classService;
            _aiStreamingService = aiStreamingService;
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
            await _aiStreamingService.CancelAllStreamsAsync(Context.ConnectionId);

            var presenceChange = UserPresenceTracker.RemoveConnection(Context.ConnectionId);
            if (presenceChange?.WentOffline == true)
            {
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
                _logger.LogWarning(
                    "Anonymous connection attempted to join class {classId}",
                    classId);

                return;
            }

            var isAdmin = Context.User?.IsInRole(Roles.Admin) == true;

            // kiểm tra quyền truy cập lớp
            if (!await _classService.CanAccessClassAsync(
                    classId,
                    userId.Value,
                    isAdmin))
            {
                _logger.LogWarning(
                    "Access denied when user {userId} tried to join class {classId}",
                    userId.Value,
                    classId);

                throw new HubException("Không có quyền truy cập lớp.");
            }

            // kiểm tra mute -> vẫn được vào room để đọc chat
            // nhưng sẽ không được gửi message (check ở SendMessage)
            var member = await _context.ClassMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ClassId == classId &&
                    x.UserId == userId.Value);

            if (member != null && member.IsMuted)
            {
                await Clients.Caller.SendAsync(
                    "ReceiveSystemMessage",
                    "Bạn đang bị mute và chỉ có thể xem tin nhắn.");
            }

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"class-{classId}");

            _logger.LogInformation(
                "User {userId} joined class group {classId}",
                userId.Value,
                classId);
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

        public async Task StartAiStream(Guid conversationId, string message)
        {
            var userId = TryGetCurrentUserId();
            if (!userId.HasValue)
            {
                throw new HubException("Bạn cần đăng nhập để sử dụng AI Chat.");
            }

            try
            {
                var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
                await using var streamSession = await _aiStreamingService.StartStreamAsync(
                    userId.Value,
                    conversationId,
                    message,
                    Context.ConnectionId,
                    remoteIp,
                    Context.ConnectionAborted);

                await Clients.Caller.SendAsync(
                    "AiStreamStarted",
                    AiStreamStartedMessage.Create(conversationId, streamSession.StreamId));

                var chunkIndex = 0;
                foreach (var chunk in streamSession.Chunks)
                {
                    streamSession.CancellationToken.ThrowIfCancellationRequested();

                    await Clients.Caller.SendAsync(
                        "AiStreamChunk",
                        AiStreamChunkMessage.Create(conversationId, streamSession.StreamId, chunk, chunkIndex++));

                    await Task.Delay(25, streamSession.CancellationToken);
                }

                await Clients.Caller.SendAsync(
                    "AiStreamCompleted",
                    AiStreamCompletedMessage.Create(
                        conversationId,
                        streamSession.StreamId,
                        streamSession.Usage,
                        streamSession.Metadata));
            }
            catch (OperationCanceledException)
            {
                await Clients.Caller.SendAsync(
                    "AiStreamFailed",
                    AiStreamFailedMessage.Create(
                        conversationId,
                        null,
                        "cancelled",
                        "Đã dừng phản hồi AI."));
            }
            catch (KeyNotFoundException)
            {
                await Clients.Caller.SendAsync(
                    "AiStreamFailed",
                    AiStreamFailedMessage.Create(
                        conversationId,
                        null,
                        "not_found",
                        "Không tìm thấy cuộc hội thoại hoặc bạn không có quyền truy cập."));
            }
            catch (AiRateLimitException ex)
            {
                await Clients.Caller.SendAsync(
                    "AiStreamFailed",
                    AiStreamFailedMessage.Create(conversationId, null, ex.ErrorCode, ex.Message));
            }
            catch (AiConcurrentRequestException ex)
            {
                await Clients.Caller.SendAsync(
                    "AiStreamFailed",
                    AiStreamFailedMessage.Create(conversationId, null, ex.ErrorCode, ex.Message));
            }
            catch (ArgumentException ex)
            {
                await Clients.Caller.SendAsync(
                    "AiStreamFailed",
                    AiStreamFailedMessage.Create(conversationId, null, "invalid_request", ex.Message));
            }
            catch (AiProviderException)
            {
                await Clients.Caller.SendAsync(
                    "AiStreamFailed",
                    AiStreamFailedMessage.Create(
                        conversationId,
                        null,
                        "provider_unavailable",
                        "Xin lỗi, hệ thống AI đang bận. Vui lòng thử lại."));
            }
        }

        public Task StopAiStream(string streamId)
        {
            return _aiStreamingService.StopStreamAsync(Context.ConnectionId, streamId).AsTask();
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

