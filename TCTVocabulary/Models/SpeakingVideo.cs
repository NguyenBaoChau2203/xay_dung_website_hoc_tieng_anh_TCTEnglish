using System.Collections.Generic;

namespace TCTVocabulary.Models;

public class SpeakingVideo
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string YoutubeId { get; set; } = null!;
    public string Level { get; set; } = null!;
    public string ThumbnailUrl { get; set; } = null!;

    public ICollection<SpeakingSentence> SpeakingSentences { get; set; } = new List<SpeakingSentence>();
}
