using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using TCTVocabulary.Models.TCTVocabulary.Models;

namespace TCTVocabulary.Models;

public partial class DbflashcardContext : DbContext
{
    // Constructor mặc định
    public DbflashcardContext()
    {
    }

    // Constructor nhận options từ Program.cs (Quan trọng để DI hoạt động)
    public DbflashcardContext(DbContextOptions<DbflashcardContext> options)
        : base(options)
    {
    }

    // Khai báo các bảng trong Database
    public virtual DbSet<Card> Cards { get; set; }
    public virtual DbSet<Class> Classes { get; set; }
    public virtual DbSet<Folder> Folders { get; set; }
    public virtual DbSet<LearningProgress> LearningProgresses { get; set; }
    public virtual DbSet<Set> Sets { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<SavedFolder> SavedFolders { get; set; }
    public virtual DbSet<ClassMessage> ClassMessages { get; set; }
    public virtual DbSet<SpeakingPlaylist> SpeakingPlaylists { get; set; }
    public virtual DbSet<SpeakingVideo> SpeakingVideos { get; set; }
    public virtual DbSet<SpeakingSentence> SpeakingSentences { get; set; }
    public virtual DbSet<ClassFolder> ClassFolders { get; set; }
   

    // SỬA LỖI: Để trống hàm này để tránh xung đột với chuỗi kết nối trong Program.cs
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Không code gì ở đây cả
    }

    // Cấu hình chi tiết các bảng (Mapping)
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Card>(entity =>
        {
            entity.HasKey(e => e.CardId).HasName("PK__Cards__55FECD8E2103FA66");
            entity.Property(e => e.CardId).HasColumnName("CardID");
            entity.Property(e => e.SetId).HasColumnName("SetID");
            entity.Property(e => e.Term).HasMaxLength(255);

            entity.HasOne(d => d.Set).WithMany(p => p.Cards)
                .HasForeignKey(d => d.SetId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Cards__SetID__4CA06362");
        });

        modelBuilder.Entity<Class>(entity =>
        {
            entity.HasKey(e => e.ClassId)
                  .HasName("PK__Classes__CB1927A0B52422EC");

            entity.Property(e => e.ClassId)
                  .HasColumnName("ClassID");

            entity.Property(e => e.ClassName)
                  .HasMaxLength(255)
                  .IsRequired();

            entity.Property(e => e.OwnerId)
                  .HasColumnName("OwnerID");

            entity.Property(e => e.PasswordHash)
                  .HasMaxLength(255)
                  .IsUnicode(false);

            entity.Property(e => e.HasPassword)
                  .HasDefaultValue(false);

            entity.Property(e => e.ImageUrl)
                  .HasMaxLength(500);

            entity.Property(e => e.Description)
                  .HasMaxLength(1000);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("(getdate())")
                  .HasColumnType("datetime");

            entity.HasOne(d => d.Owner)
                  .WithMany(p => p.Classes)
                  .HasForeignKey(d => d.OwnerId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK__Classes__OwnerID__412EB0B6");

            entity.HasMany(d => d.Users)
                  .WithMany(p => p.ClassesNavigation)
                  .UsingEntity<Dictionary<string, object>>(
                      "ClassMember",
                      r => r.HasOne<User>()
                            .WithMany()
                            .HasForeignKey("UserId")
                            .OnDelete(DeleteBehavior.ClientSetNull),
                      l => l.HasOne<Class>()
                            .WithMany()
                            .HasForeignKey("ClassId")
                            .OnDelete(DeleteBehavior.ClientSetNull),
                      j =>
                      {
                          j.HasKey("ClassId", "UserId");
                          j.ToTable("ClassMembers");
                          j.IndexerProperty<int>("ClassId").HasColumnName("ClassID");
                          j.IndexerProperty<int>("UserId").HasColumnName("UserID");
                      });
        });

        modelBuilder.Entity<Folder>(entity =>
        {
            entity.HasKey(e => e.FolderId).HasName("PK__Folders__ACD7109F2ECFF2AF");
            entity.Property(e => e.FolderId).HasColumnName("FolderID");
            entity.Property(e => e.FolderName).HasMaxLength(255);
            entity.Property(e => e.ParentFolderId).HasColumnName("ParentFolderID");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.ParentFolder).WithMany(p => p.InverseParentFolder)
                .HasForeignKey(d => d.ParentFolderId)
                .HasConstraintName("FK__Folders__ParentF__3E52440B");

            entity.HasOne(d => d.User).WithMany(p => p.Folders)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Folders__UserID__3D5E1FD2");
        });

        modelBuilder.Entity<LearningProgress>(entity =>
        {
            entity.HasKey(e => e.ProgressId).HasName("PK__Learning__BAE29C8531677C23");
            entity.ToTable("LearningProgress");
            entity.HasIndex(e => new { e.UserId, e.CardId }, "UQ__Learning__E2D72075740E222A").IsUnique();
            entity.Property(e => e.ProgressId).HasColumnName("ProgressID");
            entity.Property(e => e.CardId).HasColumnName("CardID");
            entity.Property(e => e.LastReviewedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.WrongCount).HasDefaultValue(0);

            entity.HasOne(d => d.Card).WithMany(p => p.LearningProgresses)
                .HasForeignKey(d => d.CardId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LearningP__CardI__5441852A");

            entity.HasOne(d => d.User).WithMany(p => p.LearningProgresses)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LearningP__UserI__534D60F1");
        });

        modelBuilder.Entity<Set>(entity =>
        {
            entity.HasKey(e => e.SetId).HasName("PK__Sets__7E08473D47BDA11E");
            entity.Property(e => e.SetId).HasColumnName("SetID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.FolderId).HasColumnName("FolderID");
            entity.Property(e => e.OwnerId).HasColumnName("OwnerID");
            entity.Property(e => e.SetName).HasMaxLength(255);

            entity.HasOne(d => d.Folder).WithMany(p => p.Sets)
                .HasForeignKey(d => d.FolderId)
                .HasConstraintName("FK__Sets__FolderID__49C3F6B7");

            entity.HasOne(d => d.Owner).WithMany(p => p.Sets)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Sets__OwnerID__48CFD27E");
        });
        modelBuilder.Entity<SavedFolder>(entity =>
        {
            entity.HasKey(e => e.SavedFolderId);

            entity.HasIndex(e => new { e.UserId, e.FolderId })
                  .IsUnique(); // tránh save trùng

            entity.HasOne(e => e.User)
                  .WithMany(u => u.SavedFolders)
                  .HasForeignKey(e => e.UserId);

            entity.HasOne(e => e.Folder)
                  .WithMany(f => f.SavedFolders)
                  .HasForeignKey(e => e.FolderId);
        });
        modelBuilder.Entity<ClassMessage>(entity =>
 {
     entity.HasKey(e => e.MessageId);

     entity.Property(e => e.Content)
           .IsRequired();

     entity.HasOne(e => e.Class)
           .WithMany(c => c.ClassMessages)
           .HasForeignKey(e => e.ClassId);

     entity.HasOne(e => e.User)
           .WithMany(u => u.ClassMessages)
           .HasForeignKey(e => e.UserId);
 });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CCAC46CDCFCA");
            entity.HasIndex(e => e.Email, "UQ__Users__A9D10534362A84D8").IsUnique();
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Goal).HasDefaultValue(0);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Streak).HasDefaultValue(0);
            entity.Property(e => e.AvatarUrl)
                .HasColumnType("nvarchar(max)")
                .IsRequired(false);
        });

        modelBuilder.Entity<SpeakingPlaylist>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<SpeakingVideo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
            entity.Property(e => e.YoutubeId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
            entity.Property(e => e.Level).HasMaxLength(50);
            entity.Property(e => e.Duration).HasMaxLength(20);

            entity.HasOne(d => d.SpeakingPlaylist)
                .WithMany(p => p.SpeakingVideos)
                .HasForeignKey(d => d.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SpeakingVideos_SpeakingPlaylists");
        });
        modelBuilder.Entity<ClassFolder>(entity =>
        {
            entity.ToTable("ClassFolders");

            entity.HasKey(e => e.ClassFolderId);

            entity.Property(e => e.ClassFolderId)
                  .HasColumnName("ClassFolderID");

            entity.Property(e => e.ClassId)
                  .HasColumnName("ClassID")
                  .IsRequired();

            entity.Property(e => e.FolderId)
                  .HasColumnName("FolderID")
                  .IsRequired();

            entity.Property(e => e.AddedByUserId)
                  .HasColumnName("AddedByUserID")
                  .IsRequired();

            entity.Property(e => e.AddedAt)
                  .HasDefaultValueSql("GETDATE()");

            // ===== RELATIONS =====

            entity.HasOne(e => e.Class)
                  .WithMany(c => c.ClassFolders) // ⭐ BẮT BUỘC
                  .HasForeignKey(e => e.ClassId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Folder)
                  .WithMany()
                  .HasForeignKey(e => e.FolderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AddedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.AddedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            // ===== UNIQUE =====
            entity.HasIndex(e => new { e.ClassId, e.FolderId })
                  .IsUnique();
        });
        modelBuilder.Entity<SpeakingSentence>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired();
            entity.Property(e => e.VietnameseMeaning).IsRequired();

            entity.HasOne(d => d.SpeakingVideo)
                .WithMany(p => p.SpeakingSentences)
                .HasForeignKey(d => d.VideoId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SpeakingSentences_SpeakingVideos");
        });

        modelBuilder.Entity<User>().HasData(
            new User
        {
                UserId = 1, 
                Email = "baochau1512v@gmail.com",
                PasswordHash = "$2a$11$.ryXFi1l5E2Bomp0jygOMuPyhcqbpwpMuOHB79oBcAe9idsouoL6u",
                FullName = "chau2203",
                Role = "User", 
                Streak = 0,
                Goal = 0,
                CreatedAt = new DateTime(2026, 2, 1, 16, 20, 51, 117)
            }
);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}