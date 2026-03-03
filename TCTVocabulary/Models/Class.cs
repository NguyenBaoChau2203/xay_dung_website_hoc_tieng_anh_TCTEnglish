namespace TCTVocabulary.Models
{
    public partial class Class
    {
        public int ClassId { get; set; }

        public string ClassName { get; set; } = null!;

        public int OwnerId { get; set; }

        // Mật khẩu lớp (hash)
        public string? PasswordHash { get; set; }

        public bool HasPassword { get; set; }

        // Avatar lớp
        public string? ImageUrl { get; set; }

        // Mô tả lớp
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; }

        // ===== Navigation =====
        public virtual User Owner { get; set; } = null!;

        public virtual ICollection<User> Users { get; set; } = new List<User>();

        public virtual ICollection<ClassMessage> ClassMessages { get; set; } = new List<ClassMessage>();
        public virtual ICollection<ClassFolder> ClassFolders { get; set; }
            = new List<ClassFolder>();
    }
}