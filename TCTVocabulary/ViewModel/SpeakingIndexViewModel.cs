using System.Collections.Generic;

namespace TCTVocabulary.ViewModel
{
    public class SpeakingIndexViewModel
    {
        public List<SpeakingPlaylistViewModel> Playlists { get; set; } = new();
    }

    public class SpeakingPlaylistViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? ThumbnailUrl { get; set; }
        public List<SpeakingVideoViewModel> Videos { get; set; } = new();
    }
}
