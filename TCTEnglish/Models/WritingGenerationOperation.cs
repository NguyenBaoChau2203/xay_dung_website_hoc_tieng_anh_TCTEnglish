using TCTVocabulary.Models;

namespace TCTEnglish.Models;

/// <summary>Private durable receipt. No raw source/brief or slot PII is retained here.</summary>
public sealed class WritingGenerationOperation
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public string InputMode { get; set; } = "";
    public string? Level { get; set; }
    public string? ContentType { get; set; }
    public string? Length { get; set; }
    public string? Tone { get; set; }
    public string State { get; set; } = "running";
    public Guid Fence { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LeaseExpiresAtUtc { get; set; }
    public DateTime? OfferExpiresAtUtc { get; set; }
    public DateTime QuotaDayUtc { get; set; }
    public bool Reserved { get; set; }
    public bool QuotaSettled { get; set; }
    public string CandidateKeys { get; set; } = "";
    public int CatalogVersion { get; set; } = 1;
    public string? ErrorCode { get; set; }
    public string? ConfirmationHash { get; set; }
    public string? GenerationMethod { get; set; }
    public string? TemplateKey { get; set; }
    public int? TemplateVersion { get; set; }
    public int? ExerciseId { get; set; }
    public int SentenceCount { get; set; }
    public int KnownProviderTokens { get; set; }
    public int UncertainProviderTokenUpper { get; set; }
    public bool ProviderOutcomeUncertain { get; set; }
    public User User { get; set; } = null!;
    public WritingExercise? Exercise { get; set; }
}
