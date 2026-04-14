namespace TCTVocabulary.Services
{
    public interface IWritingRequestRateLimiter
    {
        bool TryConsumeHint(int userId, string? ipAddress, out int retryAfterSeconds);
        bool TryConsumeEvaluation(int userId, string? ipAddress, out int retryAfterSeconds);
    }
}
