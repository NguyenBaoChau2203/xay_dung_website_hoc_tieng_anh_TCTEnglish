using TCTVocabulary.Models;
namespace TCTVocabulary.Models
{
    public class ClassMember
    {
        public int UserId { get; set; }
        public int ClassId { get; set; }

        // Thêm các thuộc tính mới
        public ClassRole Role { get; set; } = ClassRole.Member;
        public bool IsMuted { get; set; } = false; // Cấm chat cá nhân
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
        public virtual Class Class { get; set; } = null!;
    }
    public enum ClassRole
    {
        Member = 0,
        Assistant = 1, // Phó nhóm
        Owner = 2      // Trưởng nhóm
    }
}