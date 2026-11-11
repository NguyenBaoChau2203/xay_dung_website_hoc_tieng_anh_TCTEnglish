namespace TCTEnglish.Models;

public class UserGoalTargetRevision
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public int GoalArea { get; set; }
    public int TargetValue { get; set; }
    public bool IsActive { get; set; }
    public int RevisionNumber { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime RecordedAtUtc { get; set; }

    public virtual TCTVocabulary.Models.User User { get; set; } = null!;
}
