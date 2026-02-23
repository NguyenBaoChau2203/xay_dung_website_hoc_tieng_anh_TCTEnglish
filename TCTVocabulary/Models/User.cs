using System;
using System.Collections.Generic;
using TCTVocabulary.Models.TCTVocabulary.Models;

namespace TCTVocabulary.Models;

public partial class User
{
    public int UserId { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    // Bổ sung các property mới khớp với SQL
    public string? FullName { get; set; }
    public string? Role { get; set; }
    public string? AvatarUrl { get; set; } // Added for Google Profile Picture
    public string? ResetPasswordToken { get; set; }
    public DateTime? ResetPasswordTokenExpiry { get; set; }

    public int? Streak { get; set; }

    public int? Goal { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual ICollection<Folder> Folders { get; set; } = new List<Folder>();

    public virtual ICollection<LearningProgress> LearningProgresses { get; set; } = new List<LearningProgress>();

    public virtual ICollection<Set> Sets { get; set; } = new List<Set>();

    public virtual ICollection<Class> ClassesNavigation { get; set; } = new List<Class>();

    public virtual ICollection<SavedFolder> SavedFolders { get; set; } = new List<SavedFolder>();

    public virtual ICollection<ClassMessage> ClassMessages { get; set; } = new List<ClassMessage>();
}
