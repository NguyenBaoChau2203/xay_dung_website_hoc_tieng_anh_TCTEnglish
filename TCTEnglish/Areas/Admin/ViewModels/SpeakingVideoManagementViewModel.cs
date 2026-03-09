using System.ComponentModel.DataAnnotations;

namespace TCTVocabulary.Areas.Admin.ViewModels;

public sealed class SpeakingVideoListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string YoutubeId { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? Duration { get; set; }
    public int SentenceCount { get; set; }
}

public sealed class SpeakingPlaylistOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class SpeakingVideoCreateViewModel
{
    [Required(ErrorMessage = "YouTube Video ID is required.")]
    [Display(Name = "YouTube Video ID")]
    [StringLength(200)]
    public string YoutubeId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Title is required.")]
    [Display(Name = "Title")]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Level is required.")]
    [Display(Name = "Level")]
    [StringLength(50)]
    public string Level { get; set; } = string.Empty;

    [Display(Name = "Topic")]
    [StringLength(100)]
    public string Topic { get; set; } = "General";

    [Display(Name = "Playlist")]
    public int PlaylistId { get; set; }

    public List<SpeakingPlaylistOptionViewModel> Playlists { get; set; } = new();
}
