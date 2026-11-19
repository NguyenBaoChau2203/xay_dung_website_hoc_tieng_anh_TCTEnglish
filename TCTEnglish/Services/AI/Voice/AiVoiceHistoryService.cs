using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.ViewModels.AI.Voice;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.AI.Voice;

public sealed class AiVoiceHistoryService(DbflashcardContext db, AiVoiceSessionService sessions, AiVoiceQuotaService quota)
{
    public async Task<IReadOnlyList<VoiceHistoryVm>> ListAsync(int userId, int page, CancellationToken ct) =>
        await db.AiVoiceSessions.AsNoTracking().Where(x => x.UserId == userId && !x.HistoryDeleted && x.ReservedSeconds > 0)
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Skip((Math.Clamp(page, 1, 10000) - 1) * 20).Take(20)
            .Select(x => new VoiceHistoryVm(x.Id, x.TopicTitle, x.CreatedAtUtc, x.State, x.CloseReason, x.AccountingState)).ToListAsync(ct);

    public async Task<VoiceHistoryDetailVm> DetailAsync(int userId, Guid id, CancellationToken ct)
    {
        var row = await OwnedAsync(userId, id, ct);
        var turns = await db.AiVoiceTurns.AsNoTracking().Where(x => x.SessionId == id).OrderBy(x => x.Id)
            .Select(x => new VoiceTurnVm(x.Id, x.Speaker, x.Text, x.Interrupted)).ToListAsync(ct);
        return new(new(row.Id, row.TopicTitle, row.CreatedAtUtc, row.State, row.CloseReason, row.AccountingState), turns);
    }
    public async Task<AiVoiceSession> OwnedAsync(int userId, Guid id, CancellationToken ct) =>
        await db.AiVoiceSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId && !x.HistoryDeleted, ct)
        ?? throw new VoiceException("not_found", 404);

    public async Task DeleteAsync(int userId, Guid id, CancellationToken ct)
    {
        await OwnedAsync(userId, id, ct);
        await sessions.CloseAsync(userId, id, "history_deleted", ct);
        await quota.TransactionAsync(async () =>
        {
            var row = await db.AiVoiceSessions.SingleAsync(x => x.Id == id && x.UserId == userId, ct);
            row.HistoryDeleted = true; row.TopicTitle = ""; row.TopicSnapshot = ""; row.PlayingResponseId = null;
            db.AiVoiceTurns.RemoveRange(await db.AiVoiceTurns.Where(x => x.SessionId == id).ToListAsync(ct));
            return true;
        }, ct);
    }

    // Called inside the existing account-deletion transaction after capacity/user locking.
    public static async Task ScrubAccountAsync(DbflashcardContext context, int userId, CancellationToken ct)
    {
        var rows = await context.AiVoiceSessions.Where(x => x.UserId == userId).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        await context.AiVoiceTurns.Where(x => ids.Contains(x.SessionId)).ExecuteDeleteAsync(ct);
        await context.AiVoiceReceipts.Where(x => ids.Contains(x.SessionId)).ExecuteDeleteAsync(ct);
        await context.AiVoiceUserLeases.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await context.AiVoiceCustomTopics.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await context.AiVoiceUsageDays.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        foreach (var row in rows)
        {
            row.UserId = null; row.TopicTitle = ""; row.TopicSnapshot = ""; row.TopicId = 0;
            row.HistoryDeleted = true; row.CancelRequested = true; row.PlayingResponseId = null;
            if (row.State != "closed") { row.State = "closing"; row.CloseReason = "account_deleted"; row.NextCleanupAtUtc = DateTime.UtcNow; }
        }
        await context.SaveChangesAsync(ct);
    }
}
