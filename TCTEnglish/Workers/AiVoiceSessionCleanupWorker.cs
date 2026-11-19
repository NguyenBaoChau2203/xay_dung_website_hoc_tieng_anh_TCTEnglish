using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TCTEnglish.Services.AI.Voice;
using TCTVocabulary.Models;

namespace TCTEnglish.Workers;

public sealed class AiVoiceSessionCleanupWorker(IServiceScopeFactory scopes, AiVoiceRuntimeRegistry registry,
    IOptions<AiVoiceOptions> options, TimeProvider clock, ILogger<AiVoiceSessionCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await SweepAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception) { logger.LogWarning("Voice cleanup sweep failed; durable recovery remains pending."); }
        }
    }

    public async Task SweepAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var now = clock.GetUtcNow().UtcDateTime;
        var ids = await db.AiVoiceSessions.AsNoTracking().Where(x => x.State != "closed").OrderBy(x => x.CreatedAtUtc).Select(x => x.Id).Take(100).ToListAsync(ct);
        foreach (var id in ids)
        {
            using var itemScope = scopes.CreateScope();
            var itemDb = itemScope.ServiceProvider.GetRequiredService<DbflashcardContext>();
            var quota = itemScope.ServiceProvider.GetRequiredService<AiVoiceQuotaService>();
            var row = await itemDb.AiVoiceSessions.AsNoTracking().SingleAsync(x => x.Id == id, ct);
            var local = registry.ActiveIds.Contains(id);
            var reason = row.UserId == null ? "account_deleted" : !options.Value.Enabled ? "disabled"
                : now >= row.ExpiresAtUtc ? "quota" : now - row.LastActivityAtUtc > TimeSpan.FromSeconds(Math.Clamp(options.Value.IdleTimeoutSeconds, 15, 300)) ? "idle"
                : !local && now - row.HeartbeatAtUtc > TimeSpan.FromSeconds(35) ? "process_lost" : null;
            if (reason == null && row.UserId is int userId)
            {
                try
                {
                    var premium = await itemScope.ServiceProvider.GetRequiredService<AiVoiceEntitlementService>().IsPremiumAsync(userId, ct);
                    if (row.TopicKind == "custom" && !premium) reason = "premium_expired";
                }
                catch (VoiceException) { reason = "access_revoked"; }
            }
            if (reason != null)
                row = await quota.MutateAsync(id, s => { if (s.State != "closing") s.NextCleanupAtUtc = now;
                    s.State = "closing"; s.CancelRequested = true; s.CloseReason ??= reason;
                    if (!local && now - s.CreatedAtUtc > TimeSpan.FromSeconds(40)) s.SetupFinished = true; }, ct);
            else if (local)
                row = await quota.MutateAsync(id, s => s.HeartbeatAtUtc = now, ct);
            if (row.State == "closing" && row.NextCleanupAtUtc <= now && row.HangupAttempts < Math.Clamp(options.Value.CleanupMaxAttempts, 1, 10))
                await itemScope.ServiceProvider.GetRequiredService<AiVoiceSessionService>().CleanupAsync(id, ct);
        }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        foreach (var id in registry.ActiveIds.ToArray())
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AiVoiceQuotaService>().MutateAsync(id, s => { s.State = "closing"; s.CancelRequested = true; s.CloseReason ??= "shutdown"; }, ct);
                await scope.ServiceProvider.GetRequiredService<AiVoiceSessionService>().CleanupAsync(id, ct);
            }
            catch { /* Restart recovery has the persisted call ID and reservation. */ }
        }
        await base.StopAsync(ct);
    }
}
