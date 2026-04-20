using System;
using System.Collections.Concurrent;

namespace TCTVocabulary.Services
{
    public sealed class WritingRequestRateLimiter : IWritingRequestRateLimiter
    {
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

        private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _requestsByKey = new();

        public bool TryConsumeHint(int userId, string? ipAddress, out int retryAfterSeconds)
        {
            return TryConsume("hint", userId, ipAddress, 20, 40, out retryAfterSeconds);
        }

        public bool TryConsumeEvaluation(int userId, string? ipAddress, out int retryAfterSeconds)
        {
            return TryConsume("evaluate", userId, ipAddress, 10, 20, out retryAfterSeconds);
        }

        private bool TryConsume(
            string bucket,
            int userId,
            string? ipAddress,
            int maxRequestsPerUser,
            int maxRequestsPerIp,
            out int retryAfterSeconds)
        {
            var now = DateTime.UtcNow;
            var userAllowed = TryConsumeInternal(
                $"{bucket}:u:{userId}",
                now,
                maxRequestsPerUser,
                out var userRetryAfterSeconds);
            var ipAllowed = TryConsumeInternal(
                $"{bucket}:ip:{ipAddress ?? "unknown"}",
                now,
                maxRequestsPerIp,
                out var ipRetryAfterSeconds);

            retryAfterSeconds = Math.Max(userRetryAfterSeconds, ipRetryAfterSeconds);
            return userAllowed && ipAllowed;
        }

        private bool TryConsumeInternal(string key, DateTime now, int maxRequests, out int retryAfterSeconds)
        {
            var queue = _requestsByKey.GetOrAdd(key, _ => new ConcurrentQueue<DateTime>());

            while (queue.TryPeek(out var head) && now - head > Window)
            {
                queue.TryDequeue(out _);
            }

            if (queue.Count >= maxRequests)
            {
                queue.TryPeek(out var oldest);
                var wait = oldest == default ? TimeSpan.FromSeconds(1) : Window - (now - oldest);
                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
                return false;
            }

            queue.Enqueue(now);
            retryAfterSeconds = 0;
            return true;
        }
    }
}
