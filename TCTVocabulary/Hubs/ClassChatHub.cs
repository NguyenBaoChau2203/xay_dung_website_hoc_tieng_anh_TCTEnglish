using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using TCTVocabulary.Models;

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
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"class-{classId}"
            );
        }

        public async Task SendMessage(int classId, string content)
        {
            // 🔐 Lấy UserId từ Claims
            var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdStr == null) return;

            int userId = int.Parse(userIdStr);

            // 👤 Lấy FullName từ DB
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return;

            // 📤 Broadcast message
            await Clients.Group($"class-{classId}")
                .SendAsync("ReceiveMessage", new
                {
                    userId = userId,
                    fullName = user.FullName, // ✅ hiện FullName
                    content = content,
                    createdAt = DateTime.UtcNow
                });
        }
    }
}

