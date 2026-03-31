using System;
using System.ComponentModel.DataAnnotations;

namespace TCTEnglish.ViewModels.AI;

public sealed class SendChatRequest
{
    public Guid? ConversationId { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
}

