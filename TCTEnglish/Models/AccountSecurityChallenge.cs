namespace TCTEnglish.Models;

public static class AccountProofPurpose
{
    public const string EmailChange = "EmailChange";
    public const string Reauth = "Reauth";
    public const string SetFirstPassword = "SetFirstPassword";
    public const string Deletion = "Deletion";
    public const string WeeklyUnsubscribe = "WeeklyUnsubscribe";
    public const string VerifyEmail = "VerifyEmail";
}

public class AccountSecurityChallenge
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string Purpose { get; set; } = null!;
    public string TokenDigest { get; set; } = null!;
    public string? NewEmail { get; set; }
    public int SecurityVersion { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public virtual TCTVocabulary.Models.User User { get; set; } = null!;
}
