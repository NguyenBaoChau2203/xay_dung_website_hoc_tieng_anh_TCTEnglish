using System.Security.Cryptography;
using System.Text;

namespace TCTEnglish.Services.AI.Resilience;

public enum AiProviderFamily
{
    Text = 1,
    Embedding = 2,
    Speech = 3
}

public enum AiOutageAdmission
{
    Allowed = 0,
    Probe = 1,
    Rejected = 2
}

/// <summary>
/// Identity of one admission. Settlement and release apply only while this
/// ticket still owns that generation or probe.
/// </summary>
public readonly record struct AiOutageTicket(AiOutageAdmission Admission, long Epoch, long ProbeId)
{
    public bool IsProbe => Admission == AiOutageAdmission.Probe;

    public static AiOutageTicket RejectedTicket { get; } = new(AiOutageAdmission.Rejected, 0, 0);
}

/// <summary>
/// One family/authority circuit. The clock is injected so tests do not sleep.
/// Three completed transient outcomes open the circuit. One probe is admitted
/// after cooldown. Cancellation releases that probe without counting a failure.
/// </summary>
public sealed class AiOutageCircuit
{
    public const int FailureThreshold = 3;
    public static readonly TimeSpan OpenDuration = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaxOpenDuration = TimeSpan.FromSeconds(120);

    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private int _consecutiveTransient;
    private bool _opened;
    private DateTimeOffset _openUntil;
    private bool _probeInFlight;
    private long _epoch = 1;
    private long _probeId;
    private long _nextProbeId;

    public AiOutageCircuit(TimeProvider clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public bool ProbeInFlight
    {
        get
        {
            lock (_gate) return _probeInFlight;
        }
    }

    public AiOutageTicket TryAdmit()
    {
        lock (_gate)
        {
            var now = _clock.GetUtcNow();
            if (_probeInFlight)
            {
                return AiOutageTicket.RejectedTicket;
            }

            if (_opened && now < _openUntil)
            {
                return AiOutageTicket.RejectedTicket;
            }

            if (_opened)
            {
                _probeInFlight = true;
                _probeId = ++_nextProbeId;
                return new AiOutageTicket(AiOutageAdmission.Probe, _epoch, _probeId);
            }

            return new AiOutageTicket(AiOutageAdmission.Allowed, _epoch, 0);
        }
    }

    public void CompleteSuccess(AiOutageTicket ticket)
    {
        lock (_gate)
        {
            if (ticket.IsProbe)
            {
                if (!OwnsProbe(ticket)) return;
                ClearProbe();
                Close();
                return;
            }

            if (!OwnsClosedEpoch(ticket)) return;
            _consecutiveTransient = 0;
        }
    }

    public void CompleteTransient(AiOutageTicket ticket, TimeSpan? retryAfter)
    {
        lock (_gate)
        {
            if (ticket.IsProbe)
            {
                if (!OwnsProbe(ticket)) return;
                ClearProbe();
                Arm(_clock.GetUtcNow(), retryAfter);
                return;
            }

            if (!OwnsClosedEpoch(ticket)) return;
            _consecutiveTransient++;
            if (_consecutiveTransient >= FailureThreshold)
            {
                _consecutiveTransient = 0;
                Arm(_clock.GetUtcNow(), retryAfter);
            }
        }
    }

    public void CompleteNonTransient(AiOutageTicket ticket)
    {
        lock (_gate)
        {
            if (ticket.IsProbe)
            {
                if (!OwnsProbe(ticket)) return;
                ClearProbe();
                Close();
                return;
            }

            if (!OwnsClosedEpoch(ticket)) return;
            _consecutiveTransient = 0;
        }
    }

    public void Release(AiOutageTicket ticket)
    {
        lock (_gate)
        {
            if (!OwnsProbe(ticket)) return;
            ClearProbe();
        }
    }

    private bool OwnsProbe(AiOutageTicket ticket)
        => ticket.IsProbe && _probeInFlight && _probeId == ticket.ProbeId;

    private bool OwnsClosedEpoch(AiOutageTicket ticket)
        => ticket.Admission == AiOutageAdmission.Allowed && ticket.Epoch == _epoch && !_opened && !_probeInFlight;

    private void ClearProbe()
    {
        _probeInFlight = false;
        _probeId = 0;
    }

    private void Close()
    {
        _opened = false;
        _openUntil = default;
        _consecutiveTransient = 0;
        _epoch++;
    }

    private void Arm(DateTimeOffset now, TimeSpan? retryAfter)
    {
        var duration = OpenDuration;
        if (retryAfter is { } extra && extra > duration)
        {
            duration = extra > MaxOpenDuration ? MaxOpenDuration : extra;
        }

        if (duration < TimeSpan.Zero)
        {
            duration = OpenDuration;
        }

        _opened = true;
        _openUntil = now + duration;
        _epoch++;
    }
}

public sealed class AiOutageLease : IDisposable
{
    private readonly AiOutageCircuit? _circuit;
    private readonly AiOutageTicket _ticket;
    private int _completed;

    public AiOutageLease(bool admitted, AiOutageAdmission admission, AiOutageCircuit? circuit, AiOutageTicket ticket)
    {
        Admitted = admitted;
        Admission = admission;
        _circuit = circuit;
        _ticket = ticket;
    }

    public bool Admitted { get; }

    public AiOutageAdmission Admission { get; }

    public void CompleteSuccess() => Finish(circuit => circuit.CompleteSuccess(_ticket));

    public void CompleteTransient(TimeSpan? retryAfter) => Finish(circuit => circuit.CompleteTransient(_ticket, retryAfter));

    public void CompleteNonTransient() => Finish(circuit => circuit.CompleteNonTransient(_ticket));

    public void Release() => Finish(circuit => circuit.Release(_ticket));

    public void Dispose() => Release();

    private void Finish(Action<AiOutageCircuit> action)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0 || _circuit is null)
        {
            return;
        }

        action(_circuit);
    }
}

/// <summary>
/// Process-local circuits for text, embeddings and speech. A raw credential is
/// hashed into the key and is not retained. Restart clears the registry. IIS
/// worker processes do not share it. Tests push an isolated registry.
/// </summary>
public sealed class AiOutageRegistry
{
    public const int MaxCircuits = 64;

    private readonly TimeProvider _clock;
    private readonly bool _passthrough;
    private readonly object _gate = new();
    private readonly Dictionary<string, CircuitEntry> _circuits = new(StringComparer.Ordinal);
    private long _use;

    public static AiOutageRegistry Process { get; } = new(TimeProvider.System, passthrough: false);

    public static AiOutageRegistry Passthrough { get; } = new(TimeProvider.System, passthrough: true);

    public AiOutageRegistry(TimeProvider clock)
        : this(clock, passthrough: false)
    {
    }

    private AiOutageRegistry(TimeProvider clock, bool passthrough)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _passthrough = passthrough;
    }

    public AiOutageLease TryEnter(AiProviderFamily family, string endpoint, string model, string credential)
    {
        if (_passthrough)
        {
            return new AiOutageLease(true, AiOutageAdmission.Allowed, null, default);
        }

        var circuit = GetOrAdd(AiProviderAuthority.Create(family, endpoint, model, credential));
        var ticket = circuit.TryAdmit();
        if (ticket.Admission == AiOutageAdmission.Rejected)
        {
            return new AiOutageLease(false, ticket.Admission, null, ticket);
        }

        return new AiOutageLease(true, ticket.Admission, circuit, ticket);
    }

    public void Reset()
    {
        lock (_gate)
        {
            _circuits.Clear();
        }
    }

    private AiOutageCircuit GetOrAdd(string key)
    {
        lock (_gate)
        {
            if (_circuits.TryGetValue(key, out var existing))
            {
                existing.LastUsed = ++_use;
                return existing.Circuit;
            }

            if (_circuits.Count >= MaxCircuits)
            {
                EvictOne();
            }

            var created = new CircuitEntry(new AiOutageCircuit(_clock), ++_use);
            _circuits[key] = created;
            return created.Circuit;
        }
    }

    private void EvictOne()
    {
        string? oldestKey = null;
        long oldestUse = long.MaxValue;
        string? oldestIdleKey = null;
        long oldestIdleUse = long.MaxValue;
        foreach (var pair in _circuits)
        {
            if (pair.Value.LastUsed < oldestUse)
            {
                oldestUse = pair.Value.LastUsed;
                oldestKey = pair.Key;
            }

            if (!pair.Value.Circuit.ProbeInFlight && pair.Value.LastUsed < oldestIdleUse)
            {
                oldestIdleUse = pair.Value.LastUsed;
                oldestIdleKey = pair.Key;
            }
        }

        var key = oldestIdleKey ?? oldestKey;
        if (key is not null)
        {
            _circuits.Remove(key);
        }
    }

    private sealed class CircuitEntry(AiOutageCircuit circuit, long lastUsed)
    {
        public AiOutageCircuit Circuit { get; } = circuit;

        public long LastUsed { get; set; } = lastUsed;
    }
}

public static class AiProviderAuthority
{
    public static string Create(AiProviderFamily family, string? endpoint, string? model, string? credential)
    {
        var material = string.Join(
            '\n',
            family.ToString(),
            Normalize(endpoint),
            (model ?? string.Empty).Trim(),
            (credential ?? string.Empty).Trim());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static string Normalize(string? endpoint)
    {
        var value = (endpoint ?? string.Empty).Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return value;
        }

        var builder = new UriBuilder(uri)
        {
            Query = string.Empty,
            Fragment = string.Empty,
            UserName = string.Empty,
            Password = string.Empty
        };
        return builder.Uri.AbsoluteUri;
    }
}

public static class AiOutageAmbient
{
    private static readonly AsyncLocal<AiOutageRegistry?> Override = new();

    /// <summary>
    /// Test hosts set this so existing provider tests do not share a process circuit.
    /// The web host leaves it false and uses <see cref="AiOutageRegistry.Process"/>.
    /// </summary>
    public static bool SuppressProcessGuard { get; set; }

    public static AiOutageRegistry Current =>
        Override.Value ?? (SuppressProcessGuard ? AiOutageRegistry.Passthrough : AiOutageRegistry.Process);

    public static IDisposable Push(AiOutageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var previous = Override.Value;
        Override.Value = registry;
        return new Pop(previous);
    }

    private sealed class Pop(AiOutageRegistry? previous) : IDisposable
    {
        public void Dispose() => Override.Value = previous;
    }
}

public static class AiOutagePolicy
{
    public static bool CountsAsOutage(AiProviderException failure)
        => failure.IsTransient && failure.ErrorCode is AiProviderException.ErrorCodeTimeout
            or AiProviderException.ErrorCodeNetwork
            or AiProviderException.ErrorCodeProviderUnavailable
            or AiProviderException.ErrorCodeRateLimited;

    public static void Settle(AiOutageLease? lease, AiProviderException failure, bool sent, TimeSpan? retryAfter)
    {
        if (lease is null || !lease.Admitted)
        {
            return;
        }

        if (!sent)
        {
            lease.Release();
            return;
        }

        if (CountsAsOutage(failure))
        {
            lease.CompleteTransient(retryAfter);
            return;
        }

        lease.CompleteNonTransient();
    }
}
