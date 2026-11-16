using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.Services.Accounts;
using TCTEnglish.ViewModels;
using TCTVocabulary.Models;

namespace TCTVocabulary.Services;

public partial class WritingService
{
    public Task<DateTime?> RecordExerciseVisitAsync(int userId, int exerciseId, CancellationToken ct = default)
    {
        // Capture before waiting for the account lock; completion order must not
        // turn an older logical visit into a newer one. MariaDB stores microseconds.
        var now = _clock.UtcNow;
        var openedAt = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
        return UserMutationScope.ExecuteAsync<DateTime?>(_context, userId, async () =>
        {
            var access = await RecentWritingAccessAsync(userId, ct);
            if (access == null || !await RecentViewableExercises(access).AnyAsync(e => e.Id == exerciseId, ct)) return null;
            var visits = _context.UserWritingExerciseVisits.Where(v => v.UserId == userId && v.WritingExerciseId == exerciseId);
            var updated = await visits.Where(v => v.LastOpenedAtUtc < openedAt)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.LastOpenedAtUtc, openedAt), ct);
            if (updated == 0)
            {
                if (!await visits.AnyAsync(ct))
                {
                    var visit = new UserWritingExerciseVisit { UserId = userId, WritingExerciseId = exerciseId, LastOpenedAtUtc = openedAt };
                    _context.UserWritingExerciseVisits.Add(visit);
                    try { await _context.SaveChangesAsync(ct); }
                    catch (DbUpdateException ex) when (ex.Entries.Count > 0
                        && ex.Entries.All(entry => ReferenceEquals(entry.Entity, visit)) && IsWritingVisitDuplicate(ex))
                    {
                        _context.Entry(visit).State = EntityState.Detached;
                    }
                }
                // Also covers a competing INSERT between UPDATE and AnyAsync.
                await visits.Where(v => v.LastOpenedAtUtc < openedAt)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.LastOpenedAtUtc, openedAt), ct);
            }
            var stored = await visits.AsNoTracking().Select(v => v.LastOpenedAtUtc).SingleAsync(ct);
            return DateTime.SpecifyKind(stored, DateTimeKind.Utc);
        }, result => result.HasValue, ct);
    }

    private async Task<WritingAccessContext?> RecentWritingAccessAsync(int userId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        if (!await _context.Users.AsNoTracking().AnyAsync(u => u.UserId == userId && u.AnonymizedAt == null
            && (u.Status != UserStatus.Blocked || (u.LockExpiry != null && u.LockExpiry <= now)), ct)) return null;
        return await ResolveWritingAccessContextAsync(userId);
    }

    private IQueryable<WritingExercise> RecentViewableExercises(WritingAccessContext access) =>
        ViewableWritingExercises(access).Where(e => e.WritingExerciseSentences.Any());

    public async Task<IReadOnlyList<RecentWritingExerciseViewModel>> GetRecentExercisesAsync(int userId, int limit = 6, CancellationToken ct = default)
    {
        var access = await RecentWritingAccessAsync(userId, ct);
        if (access == null) return Array.Empty<RecentWritingExerciseViewModel>();
        var rows = await (from v in _context.UserWritingExerciseVisits.AsNoTracking()
                          join e in RecentViewableExercises(access) on v.WritingExerciseId equals e.Id
                          where v.UserId == userId
                          orderby v.LastOpenedAtUtc descending, v.WritingExerciseId descending
                          select new RecentWritingExerciseViewModel
                          {
                              Id = e.Id, Title = e.Title, Level = e.Level, ContentType = e.ContentType, Topic = e.Topic,
                              IsPrivate = e.UserId != null, LastOpenedAtUtc = v.LastOpenedAtUtc,
                              SentenceCount = e.WritingExerciseSentences.Count(),
                              AttemptCount = e.UserWritingAttempts.Count(a => a.UserId == userId),
                              CompletedSentenceCount = e.WritingExerciseSentences.Count(s => s.UserWritingAttempts.Any(a =>
                                  a.UserId == userId && a.Passed && a.WritingExerciseId == e.Id))
                          }).Take(Math.Clamp(limit, 1, 6)).ToListAsync(ct);
        foreach (var row in rows)
        {
            row.LastOpenedAtUtc = DateTime.SpecifyKind(row.LastOpenedAtUtc, DateTimeKind.Utc);
            row.LastOpenedAtDisplay = FormatWritingTimestamp(row.LastOpenedAtUtc);
            row.LevelTitle = BuildWritingIndexViewModel(row.Level).SelectedLevelTitle;
            row.ContentTypeTitle = ResolveWritingContentTypeTitle(row.ContentType);
            row.StatusKey = ResolveWritingProgressStatusKey(row.AttemptCount, row.CompletedSentenceCount, row.SentenceCount);
            row.StatusLabel = ResolveWritingProgressStatusLabel(row.StatusKey);
            row.StartActionLabel = row.AttemptCount == 0 ? "Mở bài" : row.StatusKey == WritingStatusCompleted ? "Luyện lại" : "Tiếp tục";
        }
        return rows;
    }

    private static bool IsWritingVisitDuplicate(DbUpdateException ex)
    {
        for (Exception? inner = ex.InnerException; inner != null; inner = inner.InnerException)
        {
            if (inner is MySqlConnector.MySqlException { Number: 1062 }) return true;
            if (inner.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException"
                && inner.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(inner) is int code && code is 1555 or 2067) return true;
        }
        return false;
    }
}
