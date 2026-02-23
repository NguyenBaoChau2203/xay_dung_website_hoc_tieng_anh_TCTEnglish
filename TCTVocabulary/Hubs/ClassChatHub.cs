using Microsoft.AspNetCore.SignalR;

namespace TCTVocabulary.Hubs;

public class ClassChatHub : Hub
{
    public async Task JoinClass(string classId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"class-{classId}");
    }

    public async Task SendMessage(string classId, string userName, string message)
    {
        await Clients.Group($"class-{classId}")
            .SendAsync("ReceiveMessage", userName, message);
    }
}
