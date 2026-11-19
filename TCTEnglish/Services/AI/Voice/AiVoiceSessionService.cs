using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.ViewModels.AI.Voice;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Voice;

public sealed class AiVoiceSessionService(DbflashcardContext db, AiVoiceQuotaService quota, AiVoiceTopicService topics,
    AiVoiceEntitlementService entitlement, IRealtimeVoiceClient provider, AiVoiceRuntimeRegistry registry, TimeProvider clock)
{
    public async Task<VoiceStartVm> StartAsync(int userId, VoiceStartInput input, CancellationToken ct)
    {
        VoiceSdpPolicy.Validate(input.OfferSdp);
        var topic = await topics.ResolveAsync(userId, input.TopicKind, input.TopicId, ct);
        var premium = await entitlement.IsPremiumAsync(userId, ct);
        if (!provider.IsConfigured) throw new VoiceException("voice_unavailable", 503);
        var row = await quota.ReserveAsync(userId, input.SessionId, topic, premium, ct);
        // Reservation has committed. All post-reservation failures converge on durable cleanup.
        try
        {
            ct.ThrowIfCancellationRequested();
            if (topic.Kind == "custom") await topics.ResolveAsync(userId, topic.Kind, topic.Id, ct);
            await quota.MutateAsync(row.Id, s =>
            {
                if (s.CancelRequested) throw new VoiceException("session_cancelled", 409);
                s.ProviderDispatched = true;
            }, CancellationToken.None);
            var call = await provider.CreateCallAsync(input.OfferSdp, topic.Instructions,
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("tct-voice:" + userId))),
                id => quota.MutateAsync(row.Id, s => { s.ProviderCallId = id; s.NeedsHangup = true; }, CancellationToken.None), CancellationToken.None);
            row = await db.AiVoiceSessions.AsNoTracking().SingleAsync(x => x.Id == row.Id, CancellationToken.None);
            if (row.CancelRequested || ct.IsCancellationRequested) throw new VoiceException("session_cancelled", 409);
            using var setup = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var sideband = await provider.AttachAsync(row.ProviderCallId!, topic.Instructions, setup.Token);
            try
            {
                row = await quota.MutateAsync(row.Id, s =>
                {
                    if (s.CancelRequested || ct.IsCancellationRequested || s.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
                        throw new VoiceException("session_cancelled", 409);
                    s.State = "active";
                    s.SetupFinished = true;
                    s.HeartbeatAtUtc = clock.GetUtcNow().UtcDateTime;
                }, CancellationToken.None);
                registry.Attach(row.Id, sideband);
                // Close can win between the committed activation and attaching the handle.
                var current = await db.AiVoiceSessions.AsNoTracking().SingleAsync(x => x.Id == row.Id, CancellationToken.None);
                if (current.CancelRequested || current.State != "active") throw new VoiceException("session_cancelled", 409);
                return new(row.Id, call.AnswerSdp, row.ExpiresAtUtc);
            }
            catch { await sideband.DisposeAsync(); throw; }
        }
        catch (Exception error)
        {
            await quota.MutateAsync(row.Id, s => { s.SetupFinished = true; s.CancelRequested = true;
                if (s.State != "closed") s.State = "closing"; s.CloseReason ??= "setup_failed";
                if (error is VoiceProviderRejectedException) s.ProviderDispatched = false; }, CancellationToken.None);
            await CleanupAsync(row.Id, CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> CloseAsync(int userId, Guid id, string reason, CancellationToken ct)
    {
        await quota.CancelAsync(userId, id, reason, ct);
        return await CleanupAsync(id, CancellationToken.None);
    }

    // Trusted lifecycle operation. Public callers must first check ownership via CloseAsync/history.
    public async Task<bool> CleanupAsync(Guid id, CancellationToken ct)
    {
        registry.Stop(id);
        var claim = Guid.NewGuid().ToString("N");
        var row = await quota.MutateAsync(id, s =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            if (s.State == "closed" || !s.SetupFinished || s.CleanupClaimUntilUtc > now) return;
            s.CleanupClaim = claim;
            s.CleanupClaimUntilUtc = now.AddSeconds(20);
            s.HangupAttempts++;
            s.NextCleanupAtUtc = now.AddSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Min(6, s.HangupAttempts))));
        }, ct);
        if (row.State == "closed") return true;
        if (row.CleanupClaim != claim) return false;
        var confirmed = !row.ProviderDispatched;
        if (row.ProviderCallId != null)
        {
            try { await provider.HangupAsync(row.ProviderCallId, ct); confirmed = true; }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or VoiceException) { confirmed = false; }
        }
        var closed = await quota.SettleClosedAsync(id, confirmed, ct);
        await quota.MutateAsync(id, s => { if (s.CleanupClaim == claim) { s.CleanupClaim = null; s.CleanupClaimUntilUtc = null; } }, ct);
        return closed;
    }
}
