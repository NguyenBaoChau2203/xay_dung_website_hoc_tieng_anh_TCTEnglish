namespace TCTVocabulary.Models;

public class SpeakingSentence
{
    public int Id { get; set; }
    public int VideoId { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public string Text { get; set; } = null!;
    public string VietnameseMeaning { get; set; } = null!;

    public SpeakingVideo SpeakingVideo { get; set; } = null!;
}
