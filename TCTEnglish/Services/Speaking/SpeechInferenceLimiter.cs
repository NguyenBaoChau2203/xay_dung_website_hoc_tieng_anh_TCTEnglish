using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Speaking;

public interface ISpeechInferenceLimiter
{
    Task<IAsyncDisposable?> AcquireAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Process-wide admission control for whisper HTTP. Not a cluster limiter.
/// </summary>
public sealed class SpeechInferenceLimiter : ISpeechInferenceLimiter
{
    private readonly SemaphoreSlim _slots;
    private readonly int _maxAdmission;
    private int _admitted;

    public SpeechInferenceLimiter(IOptions<SpeechProcessingOptions> options)
    {
        var running = Math.Max(1, options.Value.MaxConcurrentInferences);
        var queued = Math.Max(0, options.Value.MaxQueuedInferences);
        _maxAdmission = running + queued;
        _slots = new SemaphoreSlim(running, running);
    }

    public async Task<IAsyncDisposable?> AcquireAsync(CancellationToken cancellationToken)
    {
        var admitted = Interlocked.Increment(ref _admitted);
        if (admitted > _maxAdmission)
        {
            Interlocked.Decrement(ref _admitted);
            return null;
        }

        try
        {
            await _slots.WaitAsync(cancellationToken);
            return new Lease(_slots, () => Interlocked.Decrement(ref _admitted));
        }
        catch
        {
            Interlocked.Decrement(ref _admitted);
            throw;
        }
    }

    private sealed class Lease : IAsyncDisposable
    {
        private readonly SemaphoreSlim _slots;
        private readonly Action _releaseAdmission;
        private int _disposed;

        public Lease(SemaphoreSlim slots, Action releaseAdmission)
        {
            _slots = slots;
            _releaseAdmission = releaseAdmission;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _slots.Release();
                _releaseAdmission();
            }

            return ValueTask.CompletedTask;
        }
    }
}
