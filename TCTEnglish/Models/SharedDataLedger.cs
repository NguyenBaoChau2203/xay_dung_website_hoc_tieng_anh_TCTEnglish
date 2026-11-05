using Microsoft.EntityFrameworkCore;

namespace TCTEnglish.Models
{
    public sealed class SharedDataApplication
    {
        public string PackageId { get; set; } = "";
        public int Version { get; set; }
        public string Digest { get; set; } = "";
        public DateTime AppliedAtUtc { get; set; }
    }

    public sealed class SharedDataBinding
    {
        public string Key { get; set; } = "";
        public string Kind { get; set; } = "";
        public int EntityId { get; set; }
        public string Digest { get; set; } = "";
        public string PackageId { get; set; } = "";
    }
}

namespace TCTVocabulary.Models
{
    public partial class DbflashcardContext
    {
        public DbSet<TCTEnglish.Models.SharedDataApplication> SharedDataApplications => Set<TCTEnglish.Models.SharedDataApplication>();
        public DbSet<TCTEnglish.Models.SharedDataBinding> SharedDataBindings => Set<TCTEnglish.Models.SharedDataBinding>();

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TCTEnglish.Models.SharedDataApplication>(e =>
            {
                e.HasKey(x => new { x.PackageId, x.Version });
                e.Property(x => x.PackageId).HasMaxLength(120).IsUnicode(false);
                e.Property(x => x.Digest).HasMaxLength(64).IsUnicode(false);
            });
            modelBuilder.Entity<TCTEnglish.Models.SharedDataBinding>(e =>
            {
                e.HasKey(x => x.Key);
                e.Property(x => x.Key).HasMaxLength(160).IsUnicode(false);
                e.Property(x => x.Kind).HasMaxLength(64).IsUnicode(false);
                e.Property(x => x.PackageId).HasMaxLength(120).IsUnicode(false);
                e.Property(x => x.Digest).HasMaxLength(64).IsUnicode(false);
                e.HasIndex(x => new { x.Kind, x.EntityId }).IsUnique();
            });
        }
    }
}
