namespace TCTEnglish.Services.AI.Rag;

/// <summary>
/// Bounded display cache for citation titles and allowlisted routes.
/// Partition key: user, access scope, source key and source version.
/// Access scope includes role, private-media entitlement, class membership and
/// the current corpus snapshot. Personalized answer text is not stored.
/// TTL only limits memory. Callers must pass a fresh authorization result;
/// a failed check removes that user's copies of the source.
/// </summary>
public interface ILearnerSourceDisplayCache
{
    LearnerSourceDisplay? TryReuse(int userId, string accessScope, string sourceKey, string version, bool currentlyAuthorized);

    void Store(int userId, string accessScope, string sourceKey, string version, LearnerSourceDisplay display);

    void Forget(int userId, string sourceKey);

    int HitCount { get; }

    int EntryCount { get; }
}

public sealed record LearnerSourceDisplay(string? Title, string? Route, string SourceType);

public sealed class LearnerSourceDisplayCache : ILearnerSourceDisplayCache
{
    public const int MaxEntries = 128;
    public static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(2);

    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public LearnerSourceDisplayCache(TimeProvider clock)
    {
        _clock = clock;
    }

    public static ILearnerSourceDisplayCache Disabled { get; } = new DisabledCache();

    public int HitCount { get; private set; }

    public int EntryCount
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public LearnerSourceDisplay? TryReuse(int userId, string accessScope, string sourceKey, string version, bool currentlyAuthorized)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            return null;
        }

        if (!currentlyAuthorized)
        {
            Forget(userId, sourceKey);
            return null;
        }

        var key = Key(userId, accessScope, sourceKey, version);
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                return null;
            }

            if (entry.ExpiresAtUtc <= _clock.GetUtcNow())
            {
                _entries.Remove(key);
                return null;
            }

            entry.LastUsedUtc = _clock.GetUtcNow();
            HitCount++;
            return entry.Display;
        }
    }

    public void Store(int userId, string accessScope, string sourceKey, string version, LearnerSourceDisplay display)
    {
        ArgumentNullException.ThrowIfNull(display);
        if (string.IsNullOrWhiteSpace(sourceKey) || string.IsNullOrWhiteSpace(accessScope))
        {
            return;
        }

        var key = Key(userId, accessScope, sourceKey, version);
        var now = _clock.GetUtcNow();
        lock (_gate)
        {
            _entries[key] = new Entry(userId, sourceKey, display, now.Add(EntryTtl), now);
            while (_entries.Count > MaxEntries)
            {
                var oldest = _entries.OrderBy(item => item.Value.LastUsedUtc).First();
                _entries.Remove(oldest.Key);
            }
        }
    }

    public void Forget(int userId, string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            return;
        }

        lock (_gate)
        {
            var stale = _entries.Where(item => item.Value.UserId == userId && item.Value.SourceKey == sourceKey)
                .Select(item => item.Key)
                .ToList();
            foreach (var key in stale)
            {
                _entries.Remove(key);
            }
        }
    }

    private static string Key(int userId, string accessScope, string sourceKey, string version)
        => userId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "\n" + (accessScope ?? string.Empty)
            + "\n" + sourceKey
            + "\n" + (version ?? string.Empty);

    private sealed class Entry(int userId, string sourceKey, LearnerSourceDisplay display, DateTimeOffset expiresAtUtc, DateTimeOffset lastUsedUtc)
    {
        public int UserId { get; } = userId;

        public string SourceKey { get; } = sourceKey;

        public LearnerSourceDisplay Display { get; } = display;

        public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;

        public DateTimeOffset LastUsedUtc { get; set; } = lastUsedUtc;
    }

    private sealed class DisabledCache : ILearnerSourceDisplayCache
    {
        public int HitCount => 0;

        public int EntryCount => 0;

        public LearnerSourceDisplay? TryReuse(int userId, string accessScope, string sourceKey, string version, bool currentlyAuthorized)
            => null;

        public void Store(int userId, string accessScope, string sourceKey, string version, LearnerSourceDisplay display)
        {
        }

        public void Forget(int userId, string sourceKey)
        {
        }
    }
}
