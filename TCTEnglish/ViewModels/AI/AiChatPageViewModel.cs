using System;
using System.Collections.Generic;

namespace TCTEnglish.ViewModels.AI;

public sealed class AiChatPageViewModel
{
    public Guid ConversationId { get; init; }
    public IReadOnlyList<AiChatMessageViewModel> Messages { get; init; } = [];
    public bool IsEmbedded { get; init; }
}

