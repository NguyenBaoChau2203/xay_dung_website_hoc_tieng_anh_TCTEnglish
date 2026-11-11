using TCTVocabulary.Models;

namespace TCTEnglish.Models;

/// <summary>A user following (studying) a set they do not own.</summary>
public class SetSubscription
{
    public int SetSubscriptionId { get; set; }

    public int UserId { get; set; }

    public int SetId { get; set; }

    public DateTime SubscribedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Set Set { get; set; } = null!;
}
