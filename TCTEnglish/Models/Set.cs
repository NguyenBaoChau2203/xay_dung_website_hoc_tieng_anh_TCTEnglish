using System;
using System.Collections.Generic;

namespace TCTVocabulary.Models;

public partial class Set
{
    public int SetId { get; set; }

    public string SetName { get; set; } = null!;

    public int OwnerId { get; set; }

    public int? FolderId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public string? Description { get; set; }

    // [Feature: View_Count] - Đếm số lượt truy cập
    public int ViewCount { get; set; }

    // Personal sets: "Private" | "Public". Public default keeps seeders, Admin and shared-data imports unchanged.
    public string Visibility { get; set; } = "Public";

    // Secret capability used by /s/{slug}; never exported in shared packages.
    public string? ShareSlug { get; set; }

    public int? ClonedFromSetId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<TCTEnglish.Models.SetSubscription> Subscriptions { get; set; } = new List<TCTEnglish.Models.SetSubscription>();

    public virtual ICollection<Card> Cards { get; set; } = new List<Card>();

    public virtual Folder? Folder { get; set; }

    public virtual User Owner { get; set; } = null!;
}
