using System;
using System.Collections.Generic;

namespace TCTVocabulary.Models;

public partial class Class
{
    public int ClassId { get; set; }

    public string ClassName { get; set; } = null!;

    public int OwnerId { get; set; }

    public virtual User Owner { get; set; } = null!;

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
