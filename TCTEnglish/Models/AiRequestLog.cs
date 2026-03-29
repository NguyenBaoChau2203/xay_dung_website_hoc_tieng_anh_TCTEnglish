using System;
using TCTVocabulary.Models;

namespace TCTEnglish.Models;

public class AiRequestLog
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public Guid ConversationId { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorCode { get; set; }
    public int? LatencyMs { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
    public string? ModelName { get; set; }
    public DateTime RequestedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public AiConversation Conversation { get; set; } = null!;
}


