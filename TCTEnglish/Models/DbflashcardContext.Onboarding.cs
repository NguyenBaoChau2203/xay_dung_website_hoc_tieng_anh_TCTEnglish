using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;

namespace TCTVocabulary.Models;

public partial class DbflashcardContext
{
    public DbSet<UserOnboarding> UserOnboardings => Set<UserOnboarding>();

    private static void ConfigureOnboarding(ModelBuilder b)
    {
        b.Entity<UserOnboarding>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).ValueGeneratedNever();
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.GoalsJson).HasMaxLength(400);
            e.Property(x => x.TopicKeysJson).HasMaxLength(1000);
            e.Property(x => x.Level).HasMaxLength(2);
            e.Property(x => x.KnownTermsJson).HasColumnType("mediumtext");
            e.Property(x => x.DecksJson).HasColumnType("mediumtext");
            e.Property(x => x.LastRequestId).HasMaxLength(36);
            e.Property(x => x.LastRequestHash).HasMaxLength(64);
            e.Property(x => x.LastResultJson).HasMaxLength(4000);
            e.HasOne<User>().WithOne().HasForeignKey<UserOnboarding>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
