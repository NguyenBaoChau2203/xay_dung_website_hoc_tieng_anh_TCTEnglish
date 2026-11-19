namespace TCTEnglish.ViewModels.Sets;

/// <summary>One folder in the "Thư mục" row of /Sets (own folders and folders the user saved).</summary>
public sealed class SetFolderTileViewModel
{
    public int FolderId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>False for a folder the user saved from someone else.</summary>
    public bool IsOwn { get; set; } = true;

    /// <summary>Display name of the owner; filled for saved folders only.</summary>
    public string? OwnerName { get; set; }

    public int SetCount { get; set; }
    public int WordCount { get; set; }
    public int DueCount { get; set; }

    /// <summary>One of the kit hues: warning | primary | success | info.</summary>
    public string Hue { get; set; } = "warning";

    /// <summary>The folder "Lộ trình của tôi" the onboarding created, and whether it is less than 7 days old.</summary>
    public bool IsStarterFolder { get; set; }
    public bool IsNew { get; set; }
}

/// <summary>A set the owner could move into the folder shown on the folder page.</summary>
public sealed class SetFolderMovableSetViewModel
{
    public int SetId { get; set; }
    public string SetName { get; set; } = string.Empty;
    public int WordCount { get; set; }

    /// <summary>First word of the set, shown on the small cover; the cover hue is picked from the set id.</summary>
    public string SampleTerm { get; set; } = string.Empty;
    public string CoverHue { get; set; } = "primary";

    /// <summary>Null when the set is not in any folder yet.</summary>
    public string? CurrentFolderName { get; set; }
}

/// <summary>The folder page: header facts, the sets the viewer may see and, for the owner, the sets that can be moved in.</summary>
public sealed class SetFolderPageViewModel
{
    public int FolderId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Owner or admin: rename, delete, create inside, move in and out.</summary>
    public bool CanManage { get; set; }

    /// <summary>The viewer saved this folder (read-only page with "Bỏ lưu").</summary>
    public bool IsSaved { get; set; }

    public string? OwnerName { get; set; }
    public int SetCount { get; set; }
    public int WordCount { get; set; }
    public int DueCount { get; set; }
    public string Hue { get; set; } = "warning";

    /// <summary>Legacy nested folders still exist; such a folder cannot be deleted from this page.</summary>
    public bool HasChildFolders { get; set; }

    public List<SetCardTileViewModel> Sets { get; set; } = new();
    public List<SetFolderMovableSetViewModel> MovableSets { get; set; } = new();
}
