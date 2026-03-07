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

    public virtual ICollection<Card> Cards { get; set; } = new List<Card>();

    public virtual Folder? Folder { get; set; }

    public virtual User Owner { get; set; } = null!;
}
