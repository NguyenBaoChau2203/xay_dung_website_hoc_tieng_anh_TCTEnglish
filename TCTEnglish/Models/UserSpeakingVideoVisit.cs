namespace TCTEnglish.Models;

public class UserSpeakingVideoVisit
{
    public int UserId { get; set; }
    public int VideoId { get; set; }
    public DateTime LastOpenedAtUtc { get; set; }

    public virtual TCTVocabulary.Models.User User { get; set; } = null!;
    public virtual TCTVocabulary.Models.SpeakingVideo SpeakingVideo { get; set; } = null!;
}
