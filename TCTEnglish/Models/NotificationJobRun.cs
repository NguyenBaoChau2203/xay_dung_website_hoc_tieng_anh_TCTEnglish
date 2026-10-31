using System.ComponentModel.DataAnnotations;

namespace TCTEnglish.Models;

/// <summary>
/// Durable checkpoint / lease for NotificationWorker daily jobs.
/// </summary>
public class NotificationJobRun
{
    public long Id { get; set; }

    [MaxLength(64)]
    public string JobName { get; set; } = string.Empty;

    /// <summary>Vietnam business date the job is responsible for.</summary>
    public DateTime BusinessDate { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = "queued"; // queued|running|succeeded|failed

    public DateTime? LeaseUntilUtc { get; set; }

    [MaxLength(64)]
    public string? LeaseOwner { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public int AttemptCount { get; set; }

    [MaxLength(500)]
    public string? LastError { get; set; }

    /// <summary>streak | badge | cleanup | complete. Null on rows created before checkpoint.</summary>
    [MaxLength(32)]
    public string? Phase { get; set; }

    public int? LastUserId { get; set; }

    [MaxLength(64)]
    public string? LeaseToken { get; set; }
}
