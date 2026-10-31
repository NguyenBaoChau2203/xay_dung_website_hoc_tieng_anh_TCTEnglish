namespace TCTEnglish.Services.Speaking;

/// <summary>
/// Local speech / ASR options. Bound from Learning:Speech. Does not alter global AI:*.
/// </summary>
public sealed class SpeechProcessingOptions
{
    public const string SectionName = "Learning:Speech";

    /// <summary>Internal whisper.cpp HTTP base URL (no trailing slash). Empty = not ready.</summary>
    public string WhisperHttpBaseUrl { get; set; } = string.Empty;

    public string WhisperInferencePath { get; set; } = "/inference";

    public string ModelIdentity { get; set; } = "whisper.cpp-unconfigured";

    public int InferenceTimeoutSeconds { get; set; } = 15;

    public int MaxAudioSeconds { get; set; } = 15;

    public int MaxPayloadBytes { get; set; } = 2 * 1024 * 1024;

    public int AssessmentTtlMinutes { get; set; } = 10;

    public int MaxConcurrentInferences { get; set; } = 1;

    public int MaxQueuedInferences { get; set; } = 4;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(WhisperHttpBaseUrl);
}
