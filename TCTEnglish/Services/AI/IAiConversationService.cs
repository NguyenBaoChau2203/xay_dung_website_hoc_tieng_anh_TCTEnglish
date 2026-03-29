using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TCTEnglish.Models;

namespace TCTEnglish.Services.AI;

public interface IAiConversationService
{
    Task<AiConversation> CreateConversationAsync(int userId, string? title, CancellationToken ct);
    Task<IReadOnlyList<AiConversationSummaryDto>> GetConversationsByUserAsync(int userId, CancellationToken ct);
    Task<IReadOnlyList<AiMessage>> GetMessagesByConversationAsync(int userId, Guid conversationId, CancellationToken ct);
}

