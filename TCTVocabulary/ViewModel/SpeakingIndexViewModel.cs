using System.Collections.Generic;
using TCTVocabulary.Models;

namespace TCTVocabulary.ViewModel
{
    public class SpeakingIndexViewModel
    {
        // Existing: flat list of playlists (kept for backward compatibility)
        public List<SpeakingPlaylistViewModel> Playlists { get; set; } = new();

        // NEW: Distinct topic names for the filter UI
        public List<string> Topics { get; set; } = new();

        // NEW: Videos grouped by CEFR level (A1, A2, B1, B2)
        public Dictionary<string, List<SpeakingVideoViewModel>> VideosByLevel { get; set; } = new();
        
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
