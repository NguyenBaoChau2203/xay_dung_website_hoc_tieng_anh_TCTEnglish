using System;

namespace TCTVocabulary.Models;

public class UserBadge
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BadgeId { get; set; }
    public DateTime AwardedAt { get; set; }

    public virtual Badge Badge { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
