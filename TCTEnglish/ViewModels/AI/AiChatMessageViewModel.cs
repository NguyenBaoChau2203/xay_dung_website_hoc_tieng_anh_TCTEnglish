using System;

namespace TCTEnglish.ViewModels.AI;

public sealed class AiChatMessageViewModel
{
    public Guid Id { get; init; }
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}

