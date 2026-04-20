using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using TCTEnglish.Models;
using TCTVocabulary.Models;

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
    public virtual DbSet<WritingExercise> WritingExercises { get; set; }
    public virtual DbSet<WritingExerciseSentence> WritingExerciseSentences { get; set; }
    public virtual DbSet<UserWritingAttempt> UserWritingAttempts { get; set; }
    public virtual DbSet<ClassFolder> ClassFolders { get; set; }
    public virtual DbSet<ClassMember> ClassMembers { get; set; }
    public virtual DbSet<Badge> Badges { get; set; }
    public virtual DbSet<UserDailyActivity> UserDailyActivities { get; set; }
    public virtual DbSet<UserBadge> UserBadges { get; set; }
    public virtual DbSet<UserGoal> UserGoals { get; set; }
    public virtual DbSet<UserSpeakingProgress> UserSpeakingProgresses { get; set; }
    public virtual DbSet<AiConversation> AiConversations { get; set; }
    public virtual DbSet<AiMessage> AiMessages { get; set; }
    public virtual DbSet<AiRequestLog> AiRequestLogs { get; set; }
    public virtual DbSet<WritingGenerationLog> WritingGenerationLogs { get; set; }
    public virtual DbSet<UserSpeakingVideoCompletion> UserSpeakingVideoCompletions { get; set; }
    public virtual DbSet<UserWritingExerciseProgress> UserWritingExerciseProgresses { get; set; }
    public virtual DbSet<UserWritingSentenceProgress> UserWritingSentenceProgresses { get; set; }

    // ─── Listening feature ───────────────────────────────────────────────────
    public virtual DbSet<ListeningLesson> ListeningLessons { get; set; }
    public virtual DbSet<ListeningTranscriptLine> ListeningTranscriptLines { get; set; }
    public virtual DbSet<ListeningQuizQuestion> ListeningQuizQuestions { get; set; }
    public virtual DbSet<ListeningVocabItem> ListeningVocabItems { get; set; }
    public virtual DbSet<UserListeningProgress> UserListeningProgresses { get; set; }

    // ─── Notifications ───────────────────────────────────────────────────────
    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<ReadingPassage> ReadingPassages { get; set; }
    public virtual DbSet<ReadingQuestion> ReadingQuestions { get; set; }
    public virtual DbSet<ReadingOption> ReadingOptions { get; set; }
    public virtual DbSet<UserReadingHistory> UserReadingHistories { get; set; }
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

            // ===== OWNER =====
            entity.HasOne(d => d.Owner)
                  .WithMany(p => p.Classes)
                  .HasForeignKey(d => d.OwnerId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK__Classes__OwnerID__412EB0B6");

            // ===== CLASS MEMBERS (QUA ENTITY ClassMember) =====
            entity.HasMany(d => d.ClassMembers)
                  .WithOne(cm => cm.Class)
                  .HasForeignKey(cm => cm.ClassId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ClassMember>(entity =>
        {
            entity.ToTable("ClassMembers");

            entity.HasKey(e => new { e.ClassId, e.UserId });

            entity.Property(e => e.ClassId).HasColumnName("ClassID");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(e => e.Class)
                  .WithMany(c => c.ClassMembers)
                  .HasForeignKey(e => e.ClassId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.ClassMembers)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<UserReadingHistory>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.UserId, e.ReadingPassageId })
                .IsUnique();

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserReadingHistories)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.ReadingPassage)
                .WithMany(p => p.UserReadingHistories)
                .HasForeignKey(d => d.ReadingPassageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        // ReadingQuestion
        modelBuilder.Entity<ReadingQuestion>()
            .HasOne(q => q.Passage)
            .WithMany(p => p.Questions)
            .HasForeignKey(q => q.PassageId)
            .OnDelete(DeleteBehavior.Cascade);

        // ReadingOption
        modelBuilder.Entity<ReadingOption>()
            .HasOne(o => o.Question)
            .WithMany(q => q.Options)
            .HasForeignKey(o => o.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Folder>(entity =>
        {
            entity.HasKey(e => e.FolderId).HasName("PK__Folders__ACD7109F2ECFF2AF");
            entity.Property(e => e.FolderId).HasColumnName("FolderID");
            entity.Property(e => e.FolderName).HasMaxLength(255);
            entity.Property(e => e.ParentFolderId).HasColumnName("ParentFolderID");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.ParentFolder).WithMany(p => p.InverseParentFolder)
                .HasForeignKey(d => d.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict)
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
            entity.Property(e => e.NextReviewDate)
                .HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.WrongCount).HasDefaultValue(0);
            entity.Property(e => e.RepetitionCount).HasDefaultValue(0);

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
            // [Feature: View_Count] - Cột đếm lượt truy cập
            entity.Property(e => e.ViewCount).HasDefaultValue(0);

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
                  .HasForeignKey(e => e.ClassId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.ClassMessages)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(e => e.LongestStreak).HasDefaultValue(0);
            entity.Property(e => e.AvatarUrl)
                .HasColumnType("nvarchar(max)")
                .IsRequired(false);
            entity.Property(e => e.Status)
                .HasDefaultValue(UserStatus.Offline);
            entity.Property(e => e.LockReason)
                .HasMaxLength(1000)
                .IsRequired(false);
            entity.Property(e => e.LockExpiry)
                .HasColumnType("datetime2")
                .IsRequired(false);
        });

        modelBuilder.Entity<UserDailyActivity>(entity =>
        {
            entity.ToTable("UserDailyActivities");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.UserId, e.ActivityDate })
                .IsUnique()
                .HasDatabaseName("IX_UserDailyActivities_UserId_ActivityDate");

            entity.Property(e => e.ActivityDate)
                .HasColumnType("date");

            entity.Property(e => e.UserId)
                .HasColumnName("UserID");

            entity.Property(e => e.XpEarned).HasDefaultValue(0);
            entity.Property(e => e.StreakXpAwarded).HasDefaultValue(0);
            entity.Property(e => e.CardsReviewed).HasDefaultValue(0);
            entity.Property(e => e.NewCardsLearned).HasDefaultValue(0);
            entity.Property(e => e.VocabularyCompletedCount).HasDefaultValue(0);
            entity.Property(e => e.QuizzesCompleted).HasDefaultValue(0);
            entity.Property(e => e.SpeakingCompletedCount).HasDefaultValue(0);
            entity.Property(e => e.WritingCompletedCount).HasDefaultValue(0);
            entity.Property(e => e.ReadingCompletedCount).HasDefaultValue(0);
            entity.Property(e => e.ListeningCompletedCount).HasDefaultValue(0);

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserDailyActivities)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserDailyActivities_Users");
        });

        modelBuilder.Entity<UserGoal>(entity =>
        {
            entity.ToTable("UserGoals");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.UserId, e.GoalArea })
                .IsUnique()
                .HasDatabaseName("IX_UserGoals_UserId_GoalArea");

            entity.Property(e => e.UserId)
                .HasColumnName("UserID");

            entity.Property(e => e.GoalArea)
                .HasConversion<int>();

            entity.Property(e => e.TargetValue)
                .HasDefaultValue(0);

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserGoals)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserGoals_Users");
        });

        modelBuilder.Entity<UserWritingExerciseProgress>(entity =>
        {
            entity.ToTable("UserWritingExerciseProgresses");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.UserId, e.WritingExerciseId })
                .IsUnique()
                .HasDatabaseName("IX_UserWritingExerciseProgresses_UserId_WritingExerciseId");

            entity.Property(e => e.UserId)
                .HasColumnName("UserID");

            entity.Property(e => e.WritingExerciseId)
                .HasColumnName("WritingExerciseID");

            entity.Property(e => e.TotalSentenceCount).HasDefaultValue(0);
            entity.Property(e => e.PassedSentenceCount).HasDefaultValue(0);
            entity.Property(e => e.AttemptCount).HasDefaultValue(0);

            entity.Property(e => e.LastAttemptAt)
                .HasColumnType("datetime2");

            entity.Property(e => e.CompletedAt)
                .HasColumnType("datetime2");

            entity.HasOne(e => e.User)
                .WithMany(u => u.UserWritingExerciseProgresses)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserWritingExerciseProgresses_Users");

            entity.HasOne(e => e.WritingExercise)
                .WithMany(w => w.UserWritingExerciseProgresses)
                .HasForeignKey(e => e.WritingExerciseId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserWritingExerciseProgresses_WritingExercises");
        });

        modelBuilder.Entity<UserWritingSentenceProgress>(entity =>
        {
            entity.ToTable("UserWritingSentenceProgresses");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.UserId, e.SentenceId })
                .IsUnique()
                .HasDatabaseName("IX_UserWritingSentenceProgresses_UserId_SentenceId");

            entity.Property(e => e.UserId)
                .HasColumnName("UserID");

            entity.Property(e => e.WritingExerciseId)
                .HasColumnName("WritingExerciseID");

            entity.Property(e => e.SentenceId)
                .HasColumnName("SentenceID");

            entity.Property(e => e.AttemptCount).HasDefaultValue(0);

            entity.Property(e => e.AcceptedAnswer)
                .HasMaxLength(1000);

            entity.Property(e => e.LastAttemptAt)
                .HasColumnType("datetime2");

            entity.Property(e => e.PassedAt)
                .HasColumnType("datetime2");

            entity.HasOne(e => e.User)
                .WithMany(u => u.UserWritingSentenceProgresses)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserWritingSentenceProgresses_Users");

            entity.HasOne(e => e.WritingExercise)
                .WithMany(w => w.UserWritingSentenceProgresses)
                .HasForeignKey(e => e.WritingExerciseId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UserWritingSentenceProgresses_WritingExercises");

            entity.HasOne(e => e.Sentence)
                .WithMany(s => s.UserWritingSentenceProgresses)
                .HasForeignKey(e => e.SentenceId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserWritingSentenceProgresses_WritingExerciseSentences");
        });

        modelBuilder.Entity<Badge>(entity =>
        {
            entity.ToTable("Badges");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("IX_Badges_Code");

            entity.Property(e => e.Code)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.IconClass)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.MetricType)
                .HasConversion<int>();

            entity.Property(e => e.ThresholdValue);
            entity.Property(e => e.SortOrder);

            entity.HasData(BadgeSeedData.CreateBadges());
        });

        modelBuilder.Entity<UserBadge>(entity =>
        {
            entity.ToTable("UserBadges");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.UserId, e.BadgeId })
                .IsUnique()
                .HasDatabaseName("IX_UserBadges_UserId_BadgeId");

            entity.Property(e => e.UserId)
                .HasColumnName("UserID");

            entity.Property(e => e.BadgeId)
                .HasColumnName("BadgeID");

            entity.Property(e => e.AwardedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserBadges)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserBadges_Users");

            entity.HasOne(d => d.Badge)
                .WithMany(p => p.UserBadges)
                .HasForeignKey(d => d.BadgeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserBadges_Badges");
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
            entity.Property(e => e.Topic).HasMaxLength(100);
            entity.Property(e => e.Duration).HasMaxLength(20);
            entity.Property(e => e.SourceUrl).HasMaxLength(500);
            entity.Property(e => e.SourceType).IsRequired().HasMaxLength(50).HasDefaultValue("admin");
            entity.Property(e => e.TranscriptSource).HasMaxLength(50);
            entity.Property(e => e.ImportStatus).IsRequired().HasMaxLength(50).HasDefaultValue("ready");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(e => new { e.OwnerUserId, e.CreatedAt })
                .HasDatabaseName("IX_SpeakingVideos_OwnerUserId_CreatedAt");

            entity.HasIndex(e => new { e.OwnerUserId, e.YoutubeId })
                .IsUnique()
                .HasDatabaseName("IX_SpeakingVideos_OwnerUserId_YoutubeId")
                .HasFilter("[OwnerUserId] IS NOT NULL");

            entity.HasOne(d => d.SpeakingPlaylist)
                .WithMany(p => p.SpeakingVideos)
                .HasForeignKey(d => d.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SpeakingVideos_SpeakingPlaylists");

            entity.HasOne(d => d.OwnerUser)
                .WithMany(p => p.OwnedSpeakingVideos)
                .HasForeignKey(d => d.OwnerUserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_SpeakingVideos_Users_OwnerUserId");
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
                  .HasDefaultValueSql("getutcdate()");

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
            entity.Property(e => e.VietnameseMeaning).IsRequired().HasDefaultValue(string.Empty);

            entity.HasIndex(e => new { e.VideoId, e.StartTime })
                .HasDatabaseName("IX_SpeakingSentences_VideoId_StartTime");

            entity.HasOne(d => d.SpeakingVideo)
                .WithMany(p => p.SpeakingSentences)
                .HasForeignKey(d => d.VideoId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SpeakingSentences_SpeakingVideos");
        });

        modelBuilder.Entity<WritingExercise>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.UserId, e.IsPublished, e.Level, e.ContentType, e.Topic })
                .HasDatabaseName("IX_WritingExercises_UserId_IsPublished_Level_ContentType_Topic");

            entity.HasIndex(e => new { e.UserId, e.IsPublished, e.CreatedAt })
                .HasDatabaseName("IX_WritingExercises_UserId_IsPublished_CreatedAt");

            entity.Property(e => e.UserId)
                .HasColumnName("UserID");

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Level)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.ContentType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Topic)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.SourceType)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("admin");

            entity.Property(e => e.PreviewText)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.IsPublished)
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(e => e.User)
                .WithMany(u => u.WritingExercises)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_WritingExercises_Users");
        });

        modelBuilder.Entity<WritingExerciseSentence>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.WritingExerciseId, e.SortOrder })
                .IsUnique()
                .HasDatabaseName("IX_WritingExerciseSentences_WritingExerciseId_SortOrder");

            entity.Property(e => e.VietnameseText)
                .IsRequired();

            entity.Property(e => e.EnglishMeaning)
                .IsRequired();

            entity.Property(e => e.BreakAfter)
                .HasDefaultValue(false);

            entity.HasOne(d => d.WritingExercise)
                .WithMany(p => p.WritingExerciseSentences)
                .HasForeignKey(d => d.WritingExerciseId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_WritingExerciseSentences_WritingExercises");
        });

        modelBuilder.Entity<UserWritingAttempt>(entity =>
        {
            entity.ToTable("UserWritingAttempts");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.UserId, e.WritingExerciseId, e.CreatedAtUtc })
                .HasDatabaseName("IX_UserWritingAttempts_UserId_WritingExerciseId_CreatedAtUtc");

            entity.HasIndex(e => new { e.UserId, e.WritingExerciseSentenceId, e.CreatedAtUtc })
                .HasDatabaseName("IX_UserWritingAttempts_UserId_WritingExerciseSentenceId_CreatedAtUtc");

            entity.Property(e => e.UserId)
                .HasColumnName("UserID");

            entity.Property(e => e.SubmittedAnswer)
                .IsRequired();

            entity.Property(e => e.EvaluationSource)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.SummaryTitle)
                .HasMaxLength(200);

            entity.Property(e => e.SummaryText)
                .HasMaxLength(500);

            entity.Property(e => e.ReviewText)
                .HasMaxLength(2000);

            entity.Property(e => e.MeaningFeedback)
                .HasMaxLength(500);

            entity.Property(e => e.GrammarFeedback)
                .HasMaxLength(500);

            entity.Property(e => e.NaturalnessFeedback)
                .HasMaxLength(500);

            entity.Property(e => e.WordChoiceFeedback)
                .HasMaxLength(500);

            entity.Property(e => e.SuggestedRewrite)
                .HasMaxLength(1000);

            entity.Property(e => e.CreatedAtUtc)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserWritingAttempts)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserWritingAttempts_Users");

            entity.HasOne(d => d.WritingExercise)
                .WithMany(p => p.UserWritingAttempts)
                .HasForeignKey(d => d.WritingExerciseId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UserWritingAttempts_WritingExercises");

            entity.HasOne(d => d.WritingExerciseSentence)
                .WithMany(p => p.UserWritingAttempts)
                .HasForeignKey(d => d.WritingExerciseSentenceId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserWritingAttempts_WritingExerciseSentences");
        });

        modelBuilder.Entity<ListeningLesson>(entity =>
        {
            entity.HasIndex(e => new { e.OwnerUserId, e.CreatedAt })
                .HasDatabaseName("IX_ListeningLessons_OwnerUserId_CreatedAt");

            entity.HasOne(d => d.OwnerUser)
                .WithMany()
                .HasForeignKey(d => d.OwnerUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<UserSpeakingProgress>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.PracticedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserSpeakingProgresses)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserSpeakingProgress_Users");

            entity.HasOne(d => d.SpeakingSentence)
                .WithMany(p => p.UserSpeakingProgresses)
                .HasForeignKey(d => d.SentenceId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserSpeakingProgress_SpeakingSentences");
        });

        modelBuilder.Entity<AiConversation>(entity =>
        {
            entity.ToTable("AiConversations");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId)
                .HasColumnName("UserID");

            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.CreatedAtUtc)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(e => e.UpdatedAtUtc)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(e => new { e.UserId, e.UpdatedAtUtc })
                .HasDatabaseName("IX_AiConversations_UserId_UpdatedAtUtc");

            entity.HasOne(e => e.User)
                .WithMany(u => u.AiConversations)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_AiConversations_Users");
        });

        modelBuilder.Entity<AiMessage>(entity =>
        {
            entity.ToTable("AiMessages");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Content)
                .HasMaxLength(8000)
                .IsRequired();

            entity.Property(e => e.ModelName)
                .HasMaxLength(100);

            entity.Property(e => e.Role)
                .HasConversion<int>();

            entity.Property(e => e.CreatedAtUtc)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(e => new { e.ConversationId, e.CreatedAtUtc })
                .HasDatabaseName("IX_AiMessages_ConversationId_CreatedAtUtc");

            entity.HasOne(e => e.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_AiMessages_AiConversations");
        });

        modelBuilder.Entity<AiRequestLog>(entity =>
        {
            entity.ToTable("AiRequestLogs");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId)
                .HasColumnName("UserID");

            entity.Property(e => e.ErrorCode)
                .HasMaxLength(100);

            entity.Property(e => e.ModelName)
                .HasMaxLength(100);

            entity.Property(e => e.RequestedAtUtc)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(e => new { e.UserId, e.RequestedAtUtc })
                .HasDatabaseName("IX_AiRequestLogs_UserId_RequestedAtUtc");

            entity.HasIndex(e => e.RequestedAtUtc)
                .HasDatabaseName("IX_AiRequestLogs_RequestedAtUtc");

            entity.HasOne(e => e.User)
                .WithMany(u => u.AiRequestLogs)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_AiRequestLogs_Users");

            entity.HasOne(e => e.Conversation)
                .WithMany()
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_AiRequestLogs_AiConversations");
        });

        modelBuilder.Entity<WritingGenerationLog>(entity =>
        {
            entity.ToTable("WritingGenerationLogs");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId)
                .HasColumnName("UserID");

            entity.Property(e => e.RequestType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.ErrorCode)
                .HasMaxLength(100);

            entity.Property(e => e.RequestedAtUtc)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(e => new { e.UserId, e.RequestedAtUtc })
                .HasDatabaseName("IX_WritingGenerationLogs_UserId_RequestedAtUtc");

            entity.HasIndex(e => new { e.UserId, e.RequestType, e.RequestedAtUtc })
                .HasDatabaseName("IX_WritingGenerationLogs_UserId_RequestType_RequestedAtUtc");

            entity.HasOne(e => e.User)
                .WithMany(u => u.WritingGenerationLogs)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_WritingGenerationLogs_Users");

        });

        modelBuilder.Entity<UserSpeakingVideoCompletion>(entity =>
        {
            entity.ToTable("UserSpeakingVideoCompletions");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.UserId, e.VideoId })
                .IsUnique()
                .HasDatabaseName("IX_UserSpeakingVideoCompletions_UserId_VideoId");

            entity.Property(e => e.UserId)
                .HasColumnName("UserID");

            entity.Property(e => e.VideoId)
                .HasColumnName("VideoID");

            entity.Property(e => e.CompletedAt)
                .HasColumnType("datetime2");

            entity.Property(e => e.LastEvaluatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserSpeakingVideoCompletions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserSpeakingVideoCompletions_Users");

            entity.HasOne(d => d.SpeakingVideo)
                .WithMany(p => p.UserSpeakingVideoCompletions)
                .HasForeignKey(d => d.VideoId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserSpeakingVideoCompletions_SpeakingVideos");
        });

        // ─── Listening ───────────────────────────────────────────────────────
        modelBuilder.Entity<ListeningLesson>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Level)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.Topic)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.YoutubeId).HasMaxLength(50);
            entity.Property(e => e.AudioUrl).HasMaxLength(500);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
            entity.Property(e => e.Duration).HasMaxLength(20);
            entity.Property(e => e.Speaker1Name).HasMaxLength(150);
            entity.Property(e => e.Speaker2Name).HasMaxLength(150);
            entity.Property(e => e.Speaker1Country).HasMaxLength(100);
            entity.Property(e => e.Speaker2Country).HasMaxLength(100);

            entity.Property(e => e.IsPublished).HasDefaultValue(false);

            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(e => new { e.IsPublished, e.Level, e.Topic })
                .HasDatabaseName("IX_ListeningLessons_Published_Level_Topic");
        });

        modelBuilder.Entity<ListeningTranscriptLine>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Speaker)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Text).IsRequired();

            entity.HasIndex(e => new { e.LessonId, e.OrderIndex })
                .IsUnique()
                .HasDatabaseName("IX_ListeningTranscriptLines_LessonId_OrderIndex");

            entity.HasOne(d => d.Lesson)
                .WithMany(p => p.TranscriptLines)
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ListeningTranscriptLines_ListeningLessons");
        });

        modelBuilder.Entity<ListeningQuizQuestion>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.QuestionText).IsRequired();

            entity.Property(e => e.CorrectAnswer)
                .IsRequired()
                .HasMaxLength(1);

            entity.Property(e => e.OptionA).HasMaxLength(500);
            entity.Property(e => e.OptionB).HasMaxLength(500);
            entity.Property(e => e.OptionC).HasMaxLength(500);
            entity.Property(e => e.OptionD).HasMaxLength(500);

            entity.HasIndex(e => new { e.LessonId, e.OrderIndex })
                .IsUnique()
                .HasDatabaseName("IX_ListeningQuizQuestions_LessonId_OrderIndex");

            entity.HasOne(d => d.Lesson)
                .WithMany(p => p.QuizQuestions)
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ListeningQuizQuestions_ListeningLessons");
        });

        modelBuilder.Entity<ListeningVocabItem>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Word)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Definition).IsRequired();
            entity.Property(e => e.ImageUrl).HasMaxLength(500);

            entity.HasIndex(e => new { e.LessonId, e.OrderIndex })
                .IsUnique()
                .HasDatabaseName("IX_ListeningVocabItems_LessonId_OrderIndex");

            entity.HasOne(d => d.Lesson)
                .WithMany(p => p.VocabItems)
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ListeningVocabItems_ListeningLessons");
        });

        modelBuilder.Entity<UserListeningProgress>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Unique index — one progress record per (user, lesson)
            entity.HasIndex(e => new { e.UserId, e.LessonId })
                .IsUnique()
                .HasDatabaseName("IX_UserListeningProgress_UserId_LessonId");

            entity.Property(e => e.LastAccessedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(e => e.CompletedAt)
                .HasColumnType("datetime2");

            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserListeningProgress_Users");

            entity.HasOne(d => d.Lesson)
                .WithMany(p => p.UserProgresses)
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserListeningProgress_ListeningLessons");
        });

        // ─── Notifications ────────────────────────────────────────────────────
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Message)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(e => e.RelatedUrl)
                .HasMaxLength(500);

            entity.Property(e => e.IconClass)
                .HasMaxLength(100);

            entity.Property(e => e.Type)
                .HasConversion<int>();

            entity.Property(e => e.IsRead)
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            // Composite index: query by user → filter unread → sort by date
            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt })
                .HasDatabaseName("IX_Notifications_UserId_IsRead_CreatedAt");

            entity.HasOne(e => e.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_Notifications_Users");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

