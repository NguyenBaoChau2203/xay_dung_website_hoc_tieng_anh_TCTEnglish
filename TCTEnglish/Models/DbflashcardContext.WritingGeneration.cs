using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;

namespace TCTVocabulary.Models;

public partial class DbflashcardContext
{
    public DbSet<WritingGenerationOperation> WritingGenerationOperations { get; set; }

    private static void ConfigureWritingGeneration(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WritingGenerationOperation>(e =>
        {
            e.ToTable("WritingGenerationOperations");
            e.HasKey(x => x.Id);
            e.Property(x => x.IdempotencyKey).HasMaxLength(100);
            e.Property(x => x.PayloadHash).HasMaxLength(64);
            e.Property(x => x.ConfirmationHash).HasMaxLength(64);
            e.Property(x => x.InputMode).HasMaxLength(20);
            e.Property(x => x.State).HasMaxLength(30);
            e.Property(x => x.Level).HasMaxLength(20);
            e.Property(x => x.ContentType).HasMaxLength(20);
            e.Property(x => x.Length).HasMaxLength(10);
            e.Property(x => x.Tone).HasMaxLength(10);
            e.Property(x => x.CandidateKeys).HasMaxLength(500);
            e.Property(x => x.ErrorCode).HasMaxLength(100);
            e.Property(x => x.GenerationMethod).HasMaxLength(30);
            e.Property(x => x.TemplateKey).HasMaxLength(50);
            e.Property(x => x.QuotaDayUtc).HasColumnType("date");
            e.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => new { x.UserId, x.Reserved, x.LeaseExpiresAtUtc });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Exercise).WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<WritingExercise>(e =>
        {
            e.Property(x => x.InputMode).HasMaxLength(20);
            e.Property(x => x.GenerationMethod).HasMaxLength(30);
            e.Property(x => x.TemplateKey).HasMaxLength(50);
            e.Property(x => x.FallbackReason).HasMaxLength(100);
        });
    }
}
