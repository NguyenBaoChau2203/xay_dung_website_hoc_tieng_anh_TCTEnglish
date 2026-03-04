using TCTVocabulary.Models;

public class ClassMember
{
    public int UserId { get; set; }
    public int ClassId { get; set; }

    public User User { get; set; } = null!;
    public Class Class { get; set; } = null!;

}
