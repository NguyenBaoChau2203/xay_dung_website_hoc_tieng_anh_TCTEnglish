namespace TCTEnglish.Services.Speaking;

public enum SpeechTranscriptionStatus
{
    Ok = 1,
    NoSpeech = 2,
    NotReady = 3,
    Timeout = 4,
    Unavailable = 5,
    InvalidAudio = 6
}

public sealed class SpeechTranscriptionResult
{
    public required SpeechTranscriptionStatus Status { get; init; }
    public string Transcript { get; init; } = string.Empty;
    public IReadOnlyList<SpeechSegment> Segments { get; init; } = Array.Empty<SpeechSegment>();
    public string EngineName { get; init; } = string.Empty;
    public string ModelIdentity { get; init; } = string.Empty;
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
}

public sealed class SpeechSegment
{
    public double StartSeconds { get; init; }
    public double EndSeconds { get; init; }
    public string Text { get; init; } = string.Empty;
}

public interface ISpeechTranscriber
{
    Task<SpeechTranscriptionResult> TranscribeAsync(Stream wavPcm16Mono16kHz, CancellationToken cancellationToken);
}
