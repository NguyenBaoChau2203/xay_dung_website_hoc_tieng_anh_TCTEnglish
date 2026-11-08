using System.Diagnostics;

namespace TCTEnglish.Services.AI;

/// <summary>
/// Deadline and backoff for one text request. Production uses the system clock.
/// Tests can supply a clock that reaches backoff or the original deadline without
/// waiting on the thread-pool timer.
/// </summary>
public interface IAiTransportClock
{
    IAiTransportBudget Start(CancellationToken caller, TimeSpan timeout);
}

public interface IAiTransportBudget : IDisposable
{
    TimeSpan Elapsed { get; }

    CancellationToken Token { get; }

    Task DelayAsync(TimeSpan delay);
}

public sealed class SystemAiTransportClock : IAiTransportClock
{
    public static SystemAiTransportClock Instance { get; } = new();

    public IAiTransportBudget Start(CancellationToken caller, TimeSpan timeout) => new Budget(caller, timeout);

    private sealed class Budget : IAiTransportBudget
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly CancellationTokenSource _budget;

        public Budget(CancellationToken caller, TimeSpan timeout)
        {
            _budget = CancellationTokenSource.CreateLinkedTokenSource(caller);
            _budget.CancelAfter(timeout);
        }

        public TimeSpan Elapsed => _clock.Elapsed;

        public CancellationToken Token => _budget.Token;

        public Task DelayAsync(TimeSpan delay) => Task.Delay(delay, Token);

        public void Dispose() => _budget.Dispose();
    }
}
