using System;
using System.Collections.Concurrent;

namespace TCTEnglish.Services.AI;

public sealed class AiRequestRateLimiter : IAiRequestRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _requestsByKey = new();

    public bool TryConsume(int userId, string? ipAddress, out int retryAfterSeconds)
    {
        var now = DateTime.UtcNow;
        var userAllowed = TryConsumeInternal($"u:{userId}", now, 20, out var userRetryAfterSeconds);
        var ipAllowed = TryConsumeInternal($"ip:{ipAddress ?? "unknown"}", now, 40, out var ipRetryAfterSeconds);

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
