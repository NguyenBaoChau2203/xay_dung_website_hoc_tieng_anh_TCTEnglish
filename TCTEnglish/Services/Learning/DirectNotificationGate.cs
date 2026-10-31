namespace TCTEnglish.Services.Learning;

/// <summary>
/// Scoped flag so Speaking/Writing credit paths enqueue outbox intents
/// instead of inserting notifications directly. Other features stay on the direct path.
/// </summary>
public sealed class DirectNotificationGate
{
    public bool SuppressDirect { get; set; }
}
