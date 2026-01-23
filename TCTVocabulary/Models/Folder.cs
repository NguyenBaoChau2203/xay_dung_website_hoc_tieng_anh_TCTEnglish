using System;
using System.Collections.Generic;

namespace TCTVocabulary.Models;

public partial class Folder
{
    public int FolderId { get; set; }

    public int UserId { get; set; }

    public string FolderName { get; set; } = null!;

    public int? ParentFolderId { get; set; }

    public virtual ICollection<Folder> InverseParentFolder { get; set; } = new List<Folder>();

    public virtual Folder? ParentFolder { get; set; }

    public virtual ICollection<Set> Sets { get; set; } = new List<Set>();

    public virtual User User { get; set; } = null!;
}
