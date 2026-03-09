using System;
using System.Collections.Generic;
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
    public const string Premium = "Premium";
    public const string Standard = "Standard";
}

public partial class User
{
    public int UserId { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    // Bổ sung các property mới khớp với SQL
    public string? FullName { get; set; }
    public string? Role { get; set; }
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


    public virtual ICollection<Folder> Folders { get; set; } = new List<Folder>();

    public virtual ICollection<LearningProgress> LearningProgresses { get; set; } = new List<LearningProgress>();

    public virtual ICollection<Set> Sets { get; set; } = new List<Set>();


    public virtual ICollection<SavedFolder> SavedFolders { get; set; } = new List<SavedFolder>();

    public virtual ICollection<ClassMessage> ClassMessages { get; set; } = new List<ClassMessage>();

    public virtual ICollection<ClassMember> ClassMembers { get; set; }
    = new List<ClassMember>();
    public virtual ICollection<Class> Classes { get; set; }
    = new List<Class>();

    public virtual ICollection<UserSpeakingProgress> UserSpeakingProgresses { get; set; } = new List<UserSpeakingProgress>();
}
