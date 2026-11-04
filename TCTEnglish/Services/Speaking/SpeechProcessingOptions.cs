namespace TCTEnglish.Services.Speaking;

/// <summary>
/// Cloud speech / ASR options. Bound from Learning:Speech. Does not alter AI text model settings.
/// </summary>
public sealed class SpeechProcessingOptions
{
    public const string SectionName = "Learning:Speech";

    /// <summary>When false, speech endpoints stay NotReady without network calls.</summary>
    public bool Enabled { get; set; } = true;

    public OpenAiSpeechOptions OpenAI { get; set; } = new();

    public int InferenceTimeoutSeconds { get; set; } = 15;

    public int MaxAudioSeconds { get; set; } = 15;

    public int MaxPayloadBytes { get; set; } = 2 * 1024 * 1024;

    public int AssessmentTtlMinutes { get; set; } = 10;

    public int MaxConcurrentInferences { get; set; } = 1;

    public int MaxQueuedInferences { get; set; } = 4;

    /// <summary>Process-local assessment guard after access validation. Not a billing quota.</summary>
    public int MaxAssessmentsPerUserPerMinute { get; set; } = 10;
}

public sealed class OpenAiSpeechOptions
{
    public string Model { get; set; } = "gpt-transcribe";

    /// <summary>Official Audio API root. Trailing slash normalized at use. Not AI:BaseUrl.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

    /// <summary>
    /// Optional speech-only key. Omit from appsettings so empty default does not block shared AI key.
    /// Explicit empty/invalid env or User Secrets value does not fall through.
    /// </summary>
    public string? ApiKey { get; set; }

    public int MaxResponseBytes { get; set; } = 262_144;
}
