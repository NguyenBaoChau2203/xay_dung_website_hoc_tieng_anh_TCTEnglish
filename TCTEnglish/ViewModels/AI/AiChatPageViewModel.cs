using System;
using System.Collections.Generic;

namespace TCTEnglish.ViewModels.AI;

public sealed class AiChatPageViewModel
{
    public Guid? ConversationId { get; init; }
    public IReadOnlyList<AiChatMessageViewModel> Messages { get; init; } = [];
    public IReadOnlyList<AiConversationSummaryViewModel> Conversations { get; init; } = [];
    public string CurrentConversationTitle { get; init; } = string.Empty;
    public string CurrentConversationStatus { get; init; } = string.Empty;
    public bool IsEmbedded { get; init; }
    public bool IsStandardPlan { get; init; }
    public bool HasMoreConversations { get; init; }
    public bool HasMoreMessages { get; init; }
    public DateTime? OldestConversationUpdatedAtUtc { get; init; }
    public Guid? OldestConversationId { get; init; }
    public DateTime? OldestMessageCreatedAtUtc { get; init; }
    public Guid? OldestMessageId { get; init; }
}

