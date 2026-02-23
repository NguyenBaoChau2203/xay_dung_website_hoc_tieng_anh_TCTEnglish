namespace TCTVocabulary.Models
{
    public partial class ClassMessage
    {
        public int MessageId { get; set; }

        public int ClassId { get; set; }
        public int UserId { get; set; }

        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public virtual Class Class { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
