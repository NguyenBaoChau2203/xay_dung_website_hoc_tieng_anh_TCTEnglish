namespace TCTVocabulary.Models
{
    public class ClassFolder
    {
        public int ClassFolderId { get; set; }

        public int ClassId { get; set; }
        public int FolderId { get; set; }

        public int AddedByUserId { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.Now;

        // ===== Navigation =====
        public virtual Class Class { get; set; } = null!;
        public virtual Folder Folder { get; set; } = null!;
        public virtual User AddedByUser { get; set; } = null!;
    }
}