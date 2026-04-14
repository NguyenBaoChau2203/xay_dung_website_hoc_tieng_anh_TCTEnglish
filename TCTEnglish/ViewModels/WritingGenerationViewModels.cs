namespace TCTEnglish.ViewModels;

public enum WritingCreateFromAiOutcome
{
    Success,
    Invalid,
    Forbidden,
    QuotaExceeded,
    Failed
}

public sealed class WritingCreateFromAiRequestViewModel
{
    public string SourceText { get; set; } = string.Empty;

    public string? IdempotencyKey { get; set; }
}

public sealed class WritingCreateFromAiResultViewModel
{
    public WritingCreateFromAiOutcome Outcome { get; set; }

    public int ExerciseId { get; set; }

    public bool IsReplay { get; set; }

    public int SentenceCount { get; set; }

    public string Level { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string ErrorCode { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;

    public int RetryAfterSeconds { get; set; }
}
