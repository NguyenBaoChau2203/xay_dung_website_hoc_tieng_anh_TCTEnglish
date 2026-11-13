using System.Collections.Generic;
using TCTVocabulary.Models;

namespace TCTVocabulary.ViewModels
{
    public class SpeakingIndexViewModel
    {
        // Existing: flat list of playlists (kept for backward compatibility)
        public List<SpeakingPlaylistViewModel> Playlists { get; set; } = new();

        // NEW: Distinct topic names for the filter UI
        public List<string> Topics { get; set; } = new();

        // NEW: Videos grouped by CEFR level (A1, A2, B1, B2)
        public Dictionary<string, List<SpeakingVideoViewModel>> VideosByLevel { get; set; } = new();

        public List<SpeakingVideoViewModel> MyVideos { get; set; } = new();
        public bool CanImportPrivateVideos { get; set; }
        public bool IsMyVideosLocked { get; set; }

        public int MyCurrentPage { get; set; } = 1;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 24;
        public string? ActiveLevel { get; set; }
        public string? ActiveTopic { get; set; }
        public string? ActiveSearch { get; set; }

        public int PublicTotalCount { get; set; }
        public bool PublicHasMore { get; set; }
        public Dictionary<string, int> PublicTotalByLevel { get; set; } = new();
        public Dictionary<string, bool> PublicHasMoreByLevel { get; set; } = new();
        public Dictionary<string, int> PublicTotalByTopic { get; set; } = new();

        public int MyVideosTotalCount { get; set; }
        public bool MyVideosHasMore { get; set; }
    }

    public class SpeakingPlaylistViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? ThumbnailUrl { get; set; }
        public List<SpeakingVideoViewModel> Videos { get; set; } = new();
        public int TotalVideoCount { get; set; }
        public bool HasMoreVideos => TotalVideoCount > Videos.Count;
    }
}
