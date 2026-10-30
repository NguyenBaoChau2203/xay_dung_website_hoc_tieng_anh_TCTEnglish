namespace TCTEnglish.Services.Writing;

/// <summary>
/// Writing sentence evaluation options. Bound from Learning:Writing.
/// Does not alter global AI:* provider settings.
/// </summary>
public sealed class WritingEvaluationOptions
{
    public const string SectionName = "Learning:Writing";

    /// <summary>Local = authoritative local policy; Cloud may only add labeled feedback.</summary>
    public string EvaluationMode { get; set; } = "Local";

    /// <summary>When false (default), Evaluate never calls IAiProviderClient for grading.</summary>
    public bool CloudEnhancementEnabled { get; set; }

    /// <summary>Total budget for optional cloud enhancement on one sentence (seconds).</summary>
    public int CloudEnhancementTimeoutSeconds { get; set; } = 3;

    public bool IsLocalMode =>
        string.Equals(EvaluationMode, "Local", StringComparison.OrdinalIgnoreCase);
}
