using System;

namespace TCTEnglish.Models;

public enum AiMessageRole
{
    System = 0,
    User = 1,
    Assistant = 2
}

public class AiMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }

    public AiConversation Conversation { get; set; } = null!;

    public AiMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;

    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public string? ModelName { get; set; }

    /// <summary>
    /// Nullable versioned grounding envelope. Legacy rows stay null.
    /// Routes and source text are not stored here.
    /// </summary>
    public string? GroundingMetadata { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public AiMessageGrounding? Grounding { get; set; }
}

