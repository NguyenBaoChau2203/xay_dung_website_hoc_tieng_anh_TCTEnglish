using System.Collections.Concurrent;
using System.Text.Json;

namespace TCTEnglish.Services.AI.Voice;

/// <summary>Only resource handles live in memory. Ownership, quota and cleanup evidence live in DB.</summary>
public sealed class AiVoiceRuntimeRegistry(IServiceScopeFactory scopes) : IDisposable
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> sessions = new();
    public IEnumerable<Guid> ActiveIds => sessions.Keys;
    public void Attach(Guid id, IVoiceSideband sideband)
    {
        var stop = new CancellationTokenSource();
        if (!sessions.TryAdd(id, stop)) { stop.Dispose(); throw new VoiceException("session_conflict", 409); }
        _ = PumpAsync(id, sideband, stop);
    }
    public void Stop(Guid id) { if (sessions.TryRemove(id, out var stop)) stop.Cancel(); }

    private async Task PumpAsync(Guid id, IVoiceSideband sideband, CancellationTokenSource stop)
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var evt = await sideband.ReceiveAsync(stop.Token);
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AiVoiceEventProcessor>().HandleAsync(id, evt, stop.Token);
            }
        }
        catch (Exception) { /* Raw provider/transport errors are never logged or sent to clients. */ }
        finally
        {
            sessions.TryRemove(id, out _);
            await sideband.DisposeAsync();
            try
            {
                using var scope = scopes.CreateScope();
                var quota = scope.ServiceProvider.GetRequiredService<AiVoiceQuotaService>();
                await quota.MutateAsync(id, s => { if (s.State != "closed") { s.State = "closing"; s.CancelRequested = true; s.CloseReason ??= "sideband_lost"; s.NextCleanupAtUtc = DateTime.UtcNow; } }, CancellationToken.None);
            }
            catch { /* Durable expiry/recovery sweep is still responsible for this session. */ }
            stop.Dispose();
        }
    }
    public void Dispose() { foreach (var id in sessions.Keys) Stop(id); }
}
