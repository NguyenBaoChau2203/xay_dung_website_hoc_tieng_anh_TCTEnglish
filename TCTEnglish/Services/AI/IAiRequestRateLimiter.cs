namespace TCTEnglish.Services.AI;

public interface IAiRequestRateLimiter
{
    bool TryConsume(int userId, string? ipAddress, out int retryAfterSeconds);
}
