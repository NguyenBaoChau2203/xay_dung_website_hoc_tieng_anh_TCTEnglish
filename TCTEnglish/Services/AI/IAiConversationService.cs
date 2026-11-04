using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TCTEnglish.Models;

namespace TCTEnglish.Services.AI;

public interface IAiConversationService
{
    Task<AiConversation> CreateConversationAsync(int userId, string? title, CancellationToken ct);
    Task<bool> DeleteConversationAsync(int userId, Guid conversationId, CancellationToken ct);
    Task<IReadOnlyList<AiConversationSummaryDto>> GetConversationsByUserAsync(int userId, CancellationToken ct);
    Task<AiConversationPageDto> GetConversationsPageAsync(
        int userId,
        int pageSize,
        DateTime? beforeUpdatedAtUtc,
        Guid? beforeId,
        CancellationToken ct);
    Task<IReadOnlyList<AiMessage>> GetMessagesByConversationAsync(int userId, Guid conversationId, CancellationToken ct);
    Task<AiMessagePageDto> GetMessagesPageAsync(
        int userId,
        Guid conversationId,
        int pageSize,
        DateTime? beforeCreatedAtUtc,
        Guid? beforeId,
        CancellationToken ct);
}

