using System.Diagnostics;

namespace TCTEnglish.Tests;

/// <summary>
/// One acceptance run's POST, token and duration gate. It sits in front of fake
/// HTTP and does not change the production 12-request / 40,000-token job cap.
/// Unknown usage stays reserved until the run ends.
/// </summary>
public sealed class AiAcceptanceRunGuard : IDisposable
{
    private readonly object _gate = new();
    private readonly int _maxPosts;
    private readonly int _maxTokenAccount;
    private readonly TimeSpan _maxDuration;
    private readonly Func<TimeSpan> _elapsed;
    private readonly CancellationTokenSource _deadline;
    private int _pending;

    public AiAcceptanceRunGuard(int maxPosts, int maxTokenAccount, TimeSpan maxDuration, Func<TimeSpan>? elapsed = null)
    {
        if (maxPosts < 1) throw new ArgumentOutOfRangeException(nameof(maxPosts));
        if (maxTokenAccount < 1) throw new ArgumentOutOfRangeException(nameof(maxTokenAccount));
        if (maxDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maxDuration));
        _maxPosts = maxPosts;
        _maxTokenAccount = maxTokenAccount;
        _maxDuration = maxDuration;
        var clock = Stopwatch.StartNew();
        _elapsed = elapsed ?? (() => clock.Elapsed);
        _deadline = new CancellationTokenSource(maxDuration);
    }

    public int Posts { get; private set; }

    public long KnownTokens { get; private set; }

    public long UnknownTokens { get; private set; }

    public CancellationToken Token => _deadline.Token;

    internal SemaphoreSlim HttpGate { get; } = new(1, 1);

    public string Refusal { get; private set; } = string.Empty;

    public bool Promoted { get; private set; }

    public bool TryAdmit(int tokenUpper, int jobRemaining)
    {
        lock (_gate)
        {
            if (_pending > 0)
            {
                RecordUnknown();
            }

            if (tokenUpper < 1 || jobRemaining < 0)
            {
                Refusal = "invalid";
                return false;
            }

            if (Expired())
            {
                Refusal = "duration";
                return false;
            }

            if (Posts >= _maxPosts)
            {
                Refusal = "post-cap";
                return false;
            }

            if (tokenUpper > jobRemaining)
            {
                Refusal = "job-remaining";
                return false;
            }

            if ((long)KnownTokens + UnknownTokens + tokenUpper > _maxTokenAccount)
            {
                Refusal = "run-token";
                return false;
            }

            Posts++;
            _pending = tokenUpper;
            Refusal = string.Empty;
            return true;
        }
    }

    public void RecordKnown(int tokens)
    {
        lock (_gate)
        {
            if (_pending <= 0) throw new InvalidOperationException("No admitted request owns this receipt.");
            if (tokens <= 0)
            {
                RecordUnknown();
                Refusal = "invalid-usage";
                return;
            }

            var admittedUpper = _pending;
            _pending = 0;
            KnownTokens += tokens;
            if (tokens > admittedUpper || KnownTokens + UnknownTokens > _maxTokenAccount)
            {
                Refusal = "usage-exceeded";
                _deadline.Cancel();
            }
        }
    }

    public void RecordUnknown()
    {
        lock (_gate)
        {
            if (_pending <= 0)
            {
                return;
            }

            UnknownTokens += _pending;
            _pending = 0;
        }
    }

    public bool TryPromote(int cachedChunks, int requiredChunks)
    {
        lock (_gate)
        {
            if (Expired())
            {
                Refusal = "duration";
                return false;
            }
            if (UnknownTokens > 0 || _pending > 0 || Refusal.Length > 0 || cachedChunks != requiredChunks || requiredChunks < 1)
            {
                return false;
            }

            Promoted = true;
            return true;
        }
    }

    private bool Expired()
    {
        if (_elapsed() < _maxDuration && !_deadline.IsCancellationRequested) return false;
        _deadline.Cancel();
        return true;
    }

    public void Dispose()
    {
        _deadline.Dispose();
        HttpGate.Dispose();
    }
}
