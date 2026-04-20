using System;
using System.Collections.Generic;

namespace TCTVocabulary.Models;

public class SpeakingVideo
{
    public int Id { get; set; }
    public int? PlaylistId { get; set; }
    public int? OwnerUserId { get; set; }
    public string Title { get; set; } = null!;
    public string YoutubeId { get; set; } = null!;
    public string? Level { get; set; }
    // NEW: Topic property for filtering videos by category
    public string? Topic { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Duration { get; set; }
    public string? SourceUrl { get; set; }
    public string SourceType { get; set; } = "admin";
    public string? TranscriptSource { get; set; }
    public string ImportStatus { get; set; } = "ready";
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public SpeakingPlaylist? SpeakingPlaylist { get; set; }
    public User? OwnerUser { get; set; }
    public ICollection<SpeakingSentence> SpeakingSentences { get; set; } = new List<SpeakingSentence>();
    public ICollection<UserSpeakingVideoCompletion> UserSpeakingVideoCompletions { get; set; } = new List<UserSpeakingVideoCompletion>();
}
