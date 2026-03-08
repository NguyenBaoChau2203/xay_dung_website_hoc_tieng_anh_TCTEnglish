using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using TCTVocabulary.Models;
using Microsoft.EntityFrameworkCore;

namespace TCTVocabulary.Hubs
{
    public class ClassChatHub : Hub
    {
        private readonly DbflashcardContext _context;

        public ClassChatHub(DbflashcardContext context)
        {
            _context = context;
        }

        public async Task JoinClass(int classId)
        {
            var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdStr == null) return;

            int userId = int.Parse(userIdStr);

            bool isMember = await _context.ClassMembers
                .AnyAsync(cm => cm.ClassId == classId && cm.UserId == userId);

            bool isOwner = await _context.Classes
                .AnyAsync(c => c.ClassId == classId && c.OwnerId == userId);

            if (!isMember && !isOwner)
            {
                throw new HubException("Không có quyền truy cập lớp.");
            }

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"class-{classId}"
            );
        }

        public async Task SendMessage(int classId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return;

            // 🔐 Lấy UserId từ Claims
            var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdStr == null) return;

            int userId = int.Parse(userIdStr);

            // 👤 Lấy user
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return;

            var createdAt = DateTime.UtcNow;

            // 💾 Lưu tin nhắn vào DB
            var message = new ClassMessage
            {
                ClassId = classId,
                UserId = userId,
                Content = content,
                CreatedAt = createdAt
            };

            _context.ClassMessages.Add(message);
            await _context.SaveChangesAsync();

            // 📤 Broadcast realtime
            await Clients.Group($"class-{classId}")
                .SendAsync("ReceiveMessage", new
                {
                    userId = userId,
                    fullName = user.FullName,
                    content = content,
                    createdAt = createdAt
                });
        }
        public async Task SendImageMessage(int classId, string imageUrl)
        {
            var userId = int.Parse(Context.User.FindFirstValue(ClaimTypes.NameIdentifier));

            var user = await _context.Users.FindAsync(userId);

            var msg = new ClassMessage
            {
                ClassId = classId,
                UserId = userId,
                Content = imageUrl,
                CreatedAt = DateTime.UtcNow
            };

            _context.ClassMessages.Add(msg);
            await _context.SaveChangesAsync();

            await Clients.Group($"class-{classId}")
                .SendAsync("ReceiveImage", new
                {
                    userId = userId,
                    fullName = user.FullName,
                    imageUrl = imageUrl
                });
        }
    }
}