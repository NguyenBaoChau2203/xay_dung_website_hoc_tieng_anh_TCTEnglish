namespace TCTEnglish.Models;

public static class AccountFileCleanupStatuses
{
    public const string Pending = "Pending";
    public const string Done = "Done";
    public const string Failed = "Failed";
}

public class AccountFileCleanupJob
{
    public long Id { get; set; }
    public int FormerUserId { get; set; }
    public string PublicUrl { get; set; } = null!;
    public string Status { get; set; } = AccountFileCleanupStatuses.Pending;
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
}
