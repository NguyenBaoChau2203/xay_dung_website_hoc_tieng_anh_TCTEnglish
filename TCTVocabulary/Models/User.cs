using System;
using System.Collections.Generic;

namespace TCTVocabulary.Models;

public partial class User
{
    public int UserId { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public int? Streak { get; set; }

    public int? Goal { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual ICollection<Folder> Folders { get; set; } = new List<Folder>();

    public virtual ICollection<LearningProgress> LearningProgresses { get; set; } = new List<LearningProgress>();

    public virtual ICollection<Set> Sets { get; set; } = new List<Set>();

    public virtual ICollection<Class> ClassesNavigation { get; set; } = new List<Class>();
﻿namespace TCTVocabulary.Models
{
    public class User
    {
    }
}
