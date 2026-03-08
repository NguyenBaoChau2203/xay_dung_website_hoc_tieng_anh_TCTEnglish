using System.Collections.Generic;

namespace TCTVocabulary.Models;

public class SpeakingPlaylist
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }

    public ICollection<SpeakingVideo> SpeakingVideos { get; set; } = new List<SpeakingVideo>();
}
