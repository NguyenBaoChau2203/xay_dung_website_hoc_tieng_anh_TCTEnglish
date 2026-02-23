namespace TCTVocabulary.Models
{
    namespace TCTVocabulary.Models
    {
        public partial class SavedFolder
        {
            public int SavedFolderId { get; set; }

            // Ai là người lưu
            public int UserId { get; set; }

            // Folder được lưu
            public int FolderId { get; set; }

            public DateTime SavedAt { get; set; } = DateTime.Now;

            // Navigation
            public virtual User User { get; set; } = null!;
            public virtual Folder Folder { get; set; } = null!;
        }
    }
}
