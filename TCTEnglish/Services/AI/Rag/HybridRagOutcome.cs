namespace TCTEnglish.Services.AI.Rag;

public enum HybridRagOutcome
{
    Answered,
    NeedsClarification,
    InsufficientEvidence,
    OutOfScope,
    AccessDenied,
    Cancelled,
    ProviderUnavailable,
    QuotaExceeded
}
