using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Voice;

/// <summary>Dedicated scoped context. Serializes short state changes; no network I/O under the DB lock.</summary>
public sealed class AiVoiceQuotaService(DbflashcardContext db, IOptions<AiVoiceOptions> options, TimeProvider clock)
{
    // Storage only: Unicode stays readable/compact. Never render this JSON as HTML.
    private static readonly System.Text.Json.JsonSerializerOptions SnapshotJson = new()
    { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public async Task<T> TransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            // One pre-seeded row serializes capacity + per-user lease + quota across instances.
            var locked = await db.Database.ExecuteSqlRawAsync("UPDATE AiVoiceCapacity SET Revision = Revision WHERE Id = 1", ct);
            _ = locked; // MySQL may report zero changed rows for a successfully locked no-op.
            var result = await work();
            // Stop/expiry can happen after generation but before audio playback finishes.
            foreach (var session in db.ChangeTracker.Entries<AiVoiceSession>().Select(x => x.Entity).Where(x => x.CancelRequested && x.PlayingResponseId != null).ToList())
            {
                var playing = await db.AiVoiceTurns.Where(x => x.SessionId == session.Id && x.ResponseId == session.PlayingResponseId && x.Speaker == "assistant").ToListAsync(ct);
                foreach (var turn in playing) turn.Interrupted = true;
                session.PlayingResponseId = null;
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        });
    }

    public Task<AiVoiceSession> ReserveAsync(int userId, Guid id, VoiceTopicSnapshot topic, bool premium, CancellationToken ct) => TransactionAsync(async () =>
    {
        if (id == Guid.Empty) throw new VoiceException("invalid_session");
        var existing = await db.AiVoiceSessions.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.UserId }).SingleOrDefaultAsync(ct);
        if (existing != null) throw new VoiceException(existing.UserId == userId ? "session_conflict" : "not_found", existing.UserId == userId ? 409 : 404);
        if (await db.AiVoiceUserLeases.AnyAsync(x => x.UserId == userId, ct)) throw new VoiceException("session_conflict", 409);
        if (await db.AiVoiceSessions.CountAsync(x => x.State != "closed", ct) >= Math.Clamp(options.Value.MaxConcurrentSessions, 1, 100))
            throw new VoiceException("capacity_reached", 429);
        var now = clock.GetUtcNow().UtcDateTime;
        var snapshot = System.Text.Json.JsonSerializer.Serialize(topic, SnapshotJson);
        if (snapshot.Length > 6000) throw new VoiceException("invalid_topic");
        if (await db.AiVoiceSessions.CountAsync(x => x.UserId == userId && x.ReservedSeconds > 0 && x.CreatedAtUtc >= now.AddMinutes(-1), ct) >= 5)
            throw new VoiceException("rate_limited", 429);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (user == null || user.AnonymizedAt != null || user.Status == UserStatus.Blocked && (user.LockExpiry == null || user.LockExpiry > now))
            throw new VoiceException("not_found", 404);
        var day = now.Date;
        var usage = await db.AiVoiceUsageDays.SingleOrDefaultAsync(x => x.UserId == userId && x.UtcDate == day, ct);
        if (usage == null) db.AiVoiceUsageDays.Add(usage = new() { UserId = userId, UtcDate = day });
        var dailyLimit = Math.Clamp(premium ? options.Value.PremiumDailySeconds : options.Value.StandardDailySeconds, 1, 3600);
        var perSession = Math.Clamp(premium ? options.Value.PremiumSessionSeconds : options.Value.StandardSessionSeconds, 1, 600);
        var seconds = Math.Min(Math.Min(perSession, dailyLimit - usage.ReservedSeconds - usage.ConsumedSeconds), (int)(day.AddDays(1) - now).TotalSeconds);
        if (seconds < 1) throw new VoiceException("voice_quota_exceeded", 429);
        usage.ReservedSeconds += seconds;
        var session = new AiVoiceSession
        {
            Id = id, UserId = userId, TopicKind = topic.Kind, TopicId = topic.Id,
            TopicTitle = topic.Title, TopicSnapshot = snapshot,
            Model = options.Value.Model, ReservedSeconds = seconds, QuotaDayUtc = day,
            CreatedAtUtc = now, LastActivityAtUtc = now, HeartbeatAtUtc = now,
            ExpiresAtUtc = now.AddSeconds(seconds), NextCleanupAtUtc = now.AddSeconds(options.Value.SetupTimeoutSeconds)
        };
        db.AiVoiceSessions.Add(session);
        db.AiVoiceUserLeases.Add(new() { UserId = userId, SessionId = id });
        return session;
    }, ct);

    public Task<AiVoiceSession> MutateAsync(Guid id, Action<AiVoiceSession> mutation, CancellationToken ct) => TransactionAsync(async () =>
    {
        var row = await db.AiVoiceSessions.SingleAsync(x => x.Id == id, ct);
        mutation(row);
        row.Revision++;
        return row;
    }, ct);

    public Task<AiVoiceSession> CancelAsync(int userId, Guid id, string reason, CancellationToken ct) => TransactionAsync(async () =>
    {
        var row = await db.AiVoiceSessions.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row != null && row.UserId != userId) throw new VoiceException("not_found", 404);
        var now = clock.GetUtcNow().UtcDateTime;
        if (row == null)
        {
            if (id == Guid.Empty) throw new VoiceException("invalid_session");
            if (await db.AiVoiceSessions.CountAsync(x => x.UserId == userId && x.CreatedAtUtc >= now.Date, ct) >= 100)
                throw new VoiceException("rate_limited", 429);
            db.AiVoiceSessions.Add(row = new() { Id = id, UserId = userId, CreatedAtUtc = now,
                State = "closed", AccountingState = "settled", SetupFinished = true, HistoryDeleted = true,
                ClosedAtUtc = now, ExpiresAtUtc = now, QuotaDayUtc = now.Date });
        }
        row.CancelRequested = true;
        row.CloseReason ??= reason;
        if (row.State != "closed") row.State = "closing";
        row.NeedsHangup = row.State != "closed" && row.ProviderCallId != null;
        row.NextCleanupAtUtc = now;
        row.Revision++;
        return row;
    }, ct);

    public Task<bool> SettleClosedAsync(Guid id, bool confirmed, CancellationToken ct) => TransactionAsync(async () =>
    {
        var row = await db.AiVoiceSessions.SingleAsync(x => x.Id == id, ct);
        if (row.AccountingState == "settled")
        {
            row.State = "closed";
            row.NeedsHangup = false;
            return true;
        }
        if (!row.SetupFinished || !confirmed)
        {
            row.AccountingState = "pending";
            return false;
        }
        if (row.UserId is int userId)
        {
            var usage = await db.AiVoiceUsageDays.SingleOrDefaultAsync(x => x.UserId == userId && x.UtcDate == row.QuotaDayUtc, ct);
            if (usage != null)
            {
                usage.ReservedSeconds = Math.Max(0, usage.ReservedSeconds - row.ReservedSeconds);
                // Conservative allocation: every dispatched call consumes its full reserved window.
                // Provider tokens are recorded separately; this is an allowance, not an invoice.
                usage.ConsumedSeconds += row.ProviderDispatched ? row.ReservedSeconds : 0;
            }
            var lease = await db.AiVoiceUserLeases.SingleOrDefaultAsync(x => x.UserId == userId && x.SessionId == id, ct);
            if (lease != null) db.AiVoiceUserLeases.Remove(lease);
        }
        row.State = "closed";
        row.AccountingState = "settled";
        row.NeedsHangup = false;
        row.ClosedAtUtc ??= clock.GetUtcNow().UtcDateTime;
        row.Revision++;
        return true;
    }, ct);
}
