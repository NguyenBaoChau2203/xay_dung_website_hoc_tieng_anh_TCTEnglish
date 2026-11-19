using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;

namespace TCTVocabulary.Models;

public partial class DbflashcardContext
{
    public DbSet<AiVoiceCatalogTopic> AiVoiceCatalogTopics => Set<AiVoiceCatalogTopic>();
    public DbSet<AiVoiceCustomTopic> AiVoiceCustomTopics => Set<AiVoiceCustomTopic>();
    public DbSet<AiVoiceSession> AiVoiceSessions => Set<AiVoiceSession>();
    public DbSet<AiVoiceTurn> AiVoiceTurns => Set<AiVoiceTurn>();
    public DbSet<AiVoiceUsageDay> AiVoiceUsageDays => Set<AiVoiceUsageDay>();
    public DbSet<AiVoiceUserLease> AiVoiceUserLeases => Set<AiVoiceUserLease>();
    public DbSet<AiVoiceReceipt> AiVoiceReceipts => Set<AiVoiceReceipt>();

    private static void ConfigureAiVoice(ModelBuilder b)
    {
        b.Entity<AiVoiceCatalogTopic>(e =>
        {
            e.Property(x => x.StableKey).HasMaxLength(120);
            e.HasIndex(x => x.StableKey).IsUnique();
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Scenario).HasMaxLength(2000);
            e.Property(x => x.AiRole).HasMaxLength(120);
            e.Property(x => x.LearnerRole).HasMaxLength(120);
            e.Property(x => x.Level).HasMaxLength(2);
            e.HasIndex(x => new { x.IsPublished, x.SortOrder });
        });
        b.Entity<AiVoiceCustomTopic>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Scenario).HasMaxLength(2000);
            e.Property(x => x.AiRole).HasMaxLength(120);
            e.Property(x => x.LearnerRole).HasMaxLength(120);
            e.Property(x => x.Level).HasMaxLength(2);
            e.HasIndex(x => new { x.UserId, x.IsDeleted });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AiVoiceSession>(e =>
        {
            e.Property(x => x.TopicKind).HasMaxLength(10);
            e.Property(x => x.TopicTitle).HasMaxLength(120);
            e.Property(x => x.TopicSnapshot).HasMaxLength(6000);
            e.Property(x => x.Model).HasMaxLength(80);
            e.Property(x => x.State).HasMaxLength(20);
            e.Property(x => x.AgentState).HasMaxLength(20);
            e.Property(x => x.AccountingState).HasMaxLength(20);
            e.Property(x => x.ProviderCallId).HasMaxLength(200);
            e.Property(x => x.CleanupClaim).HasMaxLength(40);
            e.Property(x => x.PlayingResponseId).HasMaxLength(200);
            e.Property(x => x.CloseReason).HasMaxLength(40);
            e.Property(x => x.QuotaDayUtc).HasColumnType("date");
            e.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            e.HasIndex(x => new { x.State, x.NextCleanupAtUtc });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<AiVoiceTurn>(e =>
        {
            e.Property(x => x.ItemId).HasMaxLength(200);
            e.Property(x => x.ResponseId).HasMaxLength(200);
            e.Property(x => x.Speaker).HasMaxLength(10);
            e.Property(x => x.Text).HasMaxLength(12000);
            e.HasIndex(x => new { x.SessionId, x.ItemId, x.Speaker }).IsUnique();
            e.HasOne<AiVoiceSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AiVoiceUsageDay>(e =>
        {
            e.HasKey(x => new { x.UserId, x.UtcDate });
            e.Property(x => x.UtcDate).HasColumnType("date");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AiVoiceUserLease>(e =>
        {
            e.HasKey(x => x.UserId);
            e.HasIndex(x => x.SessionId).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AiVoiceSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AiVoiceReceipt>(e =>
        {
            e.HasKey(x => new { x.SessionId, x.Kind, x.EventKey });
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.EventKey).HasMaxLength(200);
            e.HasOne<AiVoiceSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AiVoiceCapacity>().ToTable("AiVoiceCapacity");
        b.Entity<AiVoiceCapacity>().HasData(new AiVoiceCapacity { Id = 1 });
    }
}
