namespace TCTVocabulary.Models;

public class LearningCompletionCredit
{
    public long Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Speaking | Writing</summary>
    public string Feature { get; set; } = string.Empty;

    public string ContentKey { get; set; } = string.Empty;

    public string CreditKey { get; set; } = string.Empty;

    public int XpAwarded { get; set; }

    public DateTime BusinessDate { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual User User { get; set; } = null!;
}
