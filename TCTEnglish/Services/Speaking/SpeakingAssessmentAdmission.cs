using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Speaking;

public interface ISpeakingAssessmentAdmission
{
    /// <summary>Process-local per-user guard. Call only after ownership/validation.</summary>
    bool TryAdmit(int userId, out int? retryAfterSeconds);
}

/// <summary>Sliding one-minute window of assessment starts per user, with expired-bucket eviction.</summary>
public sealed class SpeakingAssessmentAdmission(
    IOptions<SpeechProcessingOptions> options,
    TimeProvider timeProvider) : ISpeakingAssessmentAdmission
{
    /// <summary>Hard cap on retained user buckets after expiry eviction.</summary>
    public const int MaxTrackedUsers = 16_384;

    private readonly Dictionary<int, List<long>> _windows = new();
    private readonly object _gate = new();

    public bool TryAdmit(int userId, out int? retryAfterSeconds)
    {
        retryAfterSeconds = null;
        var limit = Math.Clamp(options.Value.MaxAssessmentsPerUserPerMinute, 1, 120);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var cutoff = now.AddMinutes(-1).Ticks;

        lock (_gate)
        {
            EvictExpiredUnlocked(cutoff);

            if (!_windows.TryGetValue(userId, out var list))
            {
                if (_windows.Count >= MaxTrackedUsers)
                {
                    // Capacity exhaustion after eviction of expired buckets only.
                    retryAfterSeconds = 5;
                    return false;
                }

                list = new List<long>(limit + 1);
                _windows[userId] = list;
            }
            else
            {
                list.RemoveAll(tick => tick < cutoff);
            }

            if (list.Count >= limit)
            {
                var oldest = list[0];
                var retryAt = new DateTime(oldest, DateTimeKind.Utc).AddMinutes(1);
                var seconds = (int)Math.Ceiling((retryAt - now).TotalSeconds);
                retryAfterSeconds = Math.Clamp(seconds, 1, 60);
                return false;
            }

            list.Add(now.Ticks);
            return true;
        }
    }

    /// <summary>Observability: number of user buckets currently retained.</summary>
    public int TrackedUserCount
    {
        get
        {
            lock (_gate)
            {
                return _windows.Count;
            }
        }
    }

    private void EvictExpiredUnlocked(long cutoff)
    {
        if (_windows.Count == 0)
        {
            return;
        }

        List<int>? remove = null;
        foreach (var (userId, stamps) in _windows)
        {
            stamps.RemoveAll(tick => tick < cutoff);
            if (stamps.Count == 0)
            {
                remove ??= new List<int>();
                remove.Add(userId);
            }
        }

        if (remove == null)
        {
            return;
        }

        foreach (var userId in remove)
        {
            _windows.Remove(userId);
        }
    }
}
