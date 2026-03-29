using System;
using System.Collections.Generic;
using TCTVocabulary.Models;

namespace TCTEnglish.Models;

public class AiConversation
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "New chat";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public ICollection<AiMessage> Messages { get; set; } = new List<AiMessage>();
}


