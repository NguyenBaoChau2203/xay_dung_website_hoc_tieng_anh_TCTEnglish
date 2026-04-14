using System;

namespace TCTVocabulary.Models;

public enum GoalArea
{
    Vocabulary = 1,
    Speaking = 2,
    Writing = 3,
    Reading = 4,
    Listening = 5
}

public class UserGoal
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public GoalArea GoalArea { get; set; }
    public int TargetValue { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
