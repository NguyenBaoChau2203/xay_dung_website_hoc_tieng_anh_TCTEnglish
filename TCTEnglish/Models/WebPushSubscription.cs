namespace TCTEnglish.Models;

public class WebPushSubscription
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string EndpointHash { get; set; } = null!;
    public string Endpoint { get; set; } = null!;
    public string P256dh { get; set; } = null!;
    public string Auth { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }

    public virtual TCTVocabulary.Models.User User { get; set; } = null!;
}
