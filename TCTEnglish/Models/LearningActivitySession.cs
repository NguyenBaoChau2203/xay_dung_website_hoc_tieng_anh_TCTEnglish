namespace TCTEnglish.Models;

public class LearningActivitySession
{
    public Guid SessionId { get; set; }
    public int UserId { get; set; }
    public string Skill { get; set; } = null!;
    public string Mode { get; set; } = null!;
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastAcceptedAtUtc { get; set; }
    public int LastSequence { get; set; }
    public int LastAcceptedCumulativeSeconds { get; set; }
    public string LeaseToken { get; set; } = null!;
    public DateTime LeaseExpiresAtUtc { get; set; }

    public virtual TCTVocabulary.Models.User User { get; set; } = null!;
    public virtual ICollection<LearningActivitySegment> Segments { get; set; } = new List<LearningActivitySegment>();
}

public class LearningActivitySegment
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public int UserId { get; set; }
    public string Skill { get; set; } = null!;
    public int Sequence { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime EndedAtUtc { get; set; }
    public int AcceptedSeconds { get; set; }

    public virtual LearningActivitySession Session { get; set; } = null!;
}
