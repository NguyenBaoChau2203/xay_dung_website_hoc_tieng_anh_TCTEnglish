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
}

