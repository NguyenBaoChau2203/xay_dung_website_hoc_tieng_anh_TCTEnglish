using TCTVocabulary.Models;
namespace TCTVocabulary.Models
{
    public class ClassBlacklist
    {
        public int ClassId { get; set; }
        public int UserId { get; set; }
        public DateTime BannedAt { get; set; } = DateTime.UtcNow;

        public virtual Class Class { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}