using System;
using System.Threading;
using System.Threading.Tasks;
using TCTEnglish.ViewModels.AI;

namespace TCTEnglish.Services.AI;

public interface IAiChatService
{
    Task<ChatReplyDto> SendAsync(int userId, Guid? conversationId, string message, CancellationToken ct);
}

