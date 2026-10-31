namespace TCTVocabulary.Models;

public class SpeakingAssessment
{
    public long Id { get; set; }

    /// <summary>Hard-to-guess public receipt id returned to the client.</summary>
    public Guid PublicId { get; set; }

    public int UserId { get; set; }

    public int SentenceId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public string EngineName { get; set; } = string.Empty;

    public string ModelIdentity { get; set; } = string.Empty;

    public string TranscriptText { get; set; } = string.Empty;

    /// <summary>Ordered text-match score 0–100. Not pronunciation fluency.</summary>
    public double TextMatchScore { get; set; }

    public bool SpeechDetected { get; set; }

    /// <summary>Completed | NoSpeech | EngineUnavailable | InvalidAudio</summary>
    public string Status { get; set; } = string.Empty;

    public bool Consumed { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual SpeakingSentence SpeakingSentence { get; set; } = null!;
}
