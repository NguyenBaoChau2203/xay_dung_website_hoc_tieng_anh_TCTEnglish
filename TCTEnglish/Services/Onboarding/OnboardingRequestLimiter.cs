using System.Collections.Concurrent;

namespace TCTEnglish.Services.Onboarding;

/// <summary>Limits the writing calls of the onboarding API (complete, refill) per user.</summary>
public interface IOnboardingRequestLimiter
{
    /// <summary>True when the call may go on; false when the user has used up the window.</summary>
    bool TryAcquire(int userId);
}

/// <summary>
/// 10 calls per 10 minutes per user, in memory. A small singleton on purpose: the shared rate-limiter setup lives in
/// Program.cs, which this feature only extends with registration lines.
/// </summary>
public sealed class OnboardingRequestLimiter : IOnboardingRequestLimiter
{
    public const int Permits = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<int, Queue<DateTimeOffset>> _calls = new();

    public OnboardingRequestLimiter(TimeProvider time) => _time = time;

    public bool TryAcquire(int userId)
    {
        var now = _time.GetUtcNow();
        var queue = _calls.GetOrAdd(userId, _ => new Queue<DateTimeOffset>());
        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() >= Window)
            {
                queue.Dequeue();
            }

            if (queue.Count >= Permits)
            {
                return false;
            }

            queue.Enqueue(now);
            return true;
        }
    }
}
