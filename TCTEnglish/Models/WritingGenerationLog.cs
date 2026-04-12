using System;
using TCTVocabulary.Models;

namespace TCTEnglish.Models;

public class WritingGenerationLog
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public string RequestType { get; set; } = null!;
    public bool IsSuccess { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime RequestedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
