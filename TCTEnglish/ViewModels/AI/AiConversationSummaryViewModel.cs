using System;

namespace TCTEnglish.ViewModels.AI;

public sealed class AiConversationSummaryViewModel
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime UpdatedAtUtc { get; init; }
}
