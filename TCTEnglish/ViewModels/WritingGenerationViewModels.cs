namespace TCTEnglish.ViewModels;

public sealed record WritingGenerationQuotaViewModel(int OwnerUserId, int? DailyLimit, int Succeeded, int Reserved, DateTime ResetAtUtc)
{
    public bool IsUnlimited => DailyLimit == null;
    public int? Remaining => DailyLimit.HasValue ? Math.Max(0, DailyLimit.Value - Succeeded - Reserved) : null;
}

public enum WritingCreateFromAiOutcome
{
    Success,
    Invalid,
    Forbidden,
    QuotaExceeded,
    Failed,
    Pending,
    TemplateOffer,
    Conflict,
    NotFound
}

public sealed class WritingCreateFromAiRequestViewModel
{
    public string SourceText { get; set; } = string.Empty;

    public string? IdempotencyKey { get; set; }
    public string? Mode { get; set; }
    public string? Brief { get; set; }
    public string? Level { get; set; }
    public string? ContentType { get; set; }
    public string? Length { get; set; }
    public string? Tone { get; set; }
}

public sealed class WritingTemplateConfirmation
{
    public string TemplateKey { get; set; } = "";
    public int TemplateVersion { get; set; }
    public bool AcceptLimitedCoverage { get; set; }
    public Dictionary<string, string> Slots { get; set; } = new();
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
    public Guid? OperationId { get; set; }
    public string Status { get; set; } = "failed";
    public string? InputMode { get; set; }
    public string? GenerationMethod { get; set; }
    public string? TemplateKey { get; set; }
    public int? TemplateVersion { get; set; }
    public string? FallbackReason { get; set; }
    public string Coverage { get; set; } = "Unsupported";
    public string[] CoveredConstraints { get; set; } = [];
    public string[] MissingConstraints { get; set; } = [];
    public IReadOnlyList<TCTEnglish.Services.Writing.Generation.WritingTemplateCapability> Templates { get; set; } = [];
    public int KnownProviderTokens { get; set; }
    public int UncertainProviderTokenUpper { get; set; }
    public bool ProviderOutcomeUncertain { get; set; }
    public DateTime? OfferExpiresAtUtc { get; set; }
}
