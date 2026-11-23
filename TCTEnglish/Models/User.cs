using System;
using System.Collections.Generic;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTVocabulary.Models;

public enum UserStatus
{
    Offline = 0,
    Online = 1,
    Blocked = 2
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Teacher = "Teacher";
    public const string Premium = "Premium";
    public const string Standard = "Standard";
    public const string LegacyStudent = "Student";

    /// <summary>Teacher or Admin may create and own virtual classes.</summary>
    public static bool CanCreateClass(string? role)
    {
        var normalized = Normalize(role);
        return normalized is Teacher or Admin;
    }

    /// <summary>Standard/Premium (and legacy Student) are learners for class nav.</summary>
    public static bool IsLearner(string? role)
    {
        var normalized = Normalize(role);
        return normalized is Standard or Premium;
    }

    /// <summary>Billing must not overwrite Admin or Teacher when syncing Premium.</summary>
    public static bool IsProtectedFromBillingRoleSync(string? role)
    {
        var normalized = Normalize(role);
        return normalized is Admin or Teacher;
    }

    public static string Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return Standard;
        }

        var trimmedRole = role.Trim();

        if (trimmedRole.Equals(Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Admin;
        }

        if (trimmedRole.Equals(Teacher, StringComparison.OrdinalIgnoreCase))
        {
            return Teacher;
        }

        if (trimmedRole.Equals(Premium, StringComparison.OrdinalIgnoreCase))
        {
            return Premium;
        }

        if (trimmedRole.Equals(Standard, StringComparison.OrdinalIgnoreCase)
            || trimmedRole.Equals(LegacyStudent, StringComparison.OrdinalIgnoreCase))
        {
            return Standard;
        }

        return Standard;
    }
}

public partial class User
{
    public int UserId { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    // Bổ sung các property mới khớp với SQL
    public string? FullName { get; set; }
    public string? Role { get; set; }

    /// <summary>CEFR reading access level A1–C2. Defaults to A1.</summary>
    public string ReadingLevel { get; set; } = "A1";

    public UserStatus Status { get; set; } = UserStatus.Offline;
    public string? AvatarUrl { get; set; } // Added for Google Profile Picture
    public string? ResetPasswordToken { get; set; }
    public DateTime? ResetPasswordTokenExpiry { get; set; }

    public string? LockReason { get; set; }
    public DateTime? LockExpiry { get; set; }

    public int? Streak { get; set; }

    public int? LongestStreak { get; set; }

    public DateTime? LastStudyDate { get; set; }

    public int? Goal { get; set; }

    public DateTime? CreatedAt { get; set; }

    /// <summary>Server session generation. Missing or older cookie claims are rejected.</summary>
    public int SecurityVersion { get; set; }

    /// <summary>Set only after the address is confirmed. Null is not treated as verified.</summary>
    public DateTime? EmailVerifiedAt { get; set; }

    /// <summary>Set when the account is anonymized. The row is no longer a login identity.</summary>
    public DateTime? AnonymizedAt { get; set; }


    public virtual ICollection<Folder> Folders { get; set; } = new List<Folder>();

    public virtual ICollection<LearningProgress> LearningProgresses { get; set; } = new List<LearningProgress>();
    public virtual ICollection<UserDailyActivity> UserDailyActivities { get; set; } = new List<UserDailyActivity>();
    public virtual ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
    public virtual ICollection<AiConversation> AiConversations { get; set; } = new List<AiConversation>();
    public virtual ICollection<AiRequestLog> AiRequestLogs { get; set; } = new List<AiRequestLog>();
    public virtual ICollection<WritingGenerationLog> WritingGenerationLogs { get; set; } = new List<WritingGenerationLog>();
    public virtual ICollection<UserGoal> UserGoals { get; set; } = new List<UserGoal>();

    public virtual ICollection<Set> Sets { get; set; } = new List<Set>();


    public virtual ICollection<SavedFolder> SavedFolders { get; set; } = new List<SavedFolder>();

    public virtual ICollection<ClassMessage> ClassMessages { get; set; } = new List<ClassMessage>();

    public virtual ICollection<ClassMember> ClassMembers { get; set; }
    = new List<ClassMember>();
    public virtual ICollection<Class> Classes { get; set; }
    = new List<Class>();

    public virtual ICollection<UserSpeakingProgress> UserSpeakingProgresses { get; set; } = new List<UserSpeakingProgress>();
    public virtual ICollection<SpeakingVideo> OwnedSpeakingVideos { get; set; } = new List<SpeakingVideo>();
    public virtual ICollection<UserWritingAttempt> UserWritingAttempts { get; set; } = new List<UserWritingAttempt>();
    public virtual ICollection<WritingAcceptedAnswer> WritingAcceptedAnswersApproved { get; set; } = new List<WritingAcceptedAnswer>();
    public virtual ICollection<WritingExercise> WritingExercises { get; set; } = new List<WritingExercise>();
    public virtual ICollection<UserSpeakingVideoCompletion> UserSpeakingVideoCompletions { get; set; } = new List<UserSpeakingVideoCompletion>();
    public virtual ICollection<TCTEnglish.Models.UserSpeakingVideoVisit> UserSpeakingVideoVisits { get; set; } = new List<TCTEnglish.Models.UserSpeakingVideoVisit>();
    public virtual ICollection<UserWritingExerciseProgress> UserWritingExerciseProgresses { get; set; } = new List<UserWritingExerciseProgress>();
    public virtual ICollection<UserWritingSentenceProgress> UserWritingSentenceProgresses { get; set; } = new List<UserWritingSentenceProgress>();
    public virtual ICollection<UserReadingHistory> UserReadingHistories { get; set; } = new List<UserReadingHistory>();
    public virtual ICollection<ReadingAttempt> ReadingAttempts { get; set; } = new List<ReadingAttempt>();
    public virtual ICollection<ReadingAnnotation> ReadingAnnotations { get; set; } = new List<ReadingAnnotation>();
    public virtual ICollection<StudentVocabulary> StudentVocabularies { get; set; } = new List<StudentVocabulary>();

    // ─── Notifications ────────────────────────────────────────────────────────
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

