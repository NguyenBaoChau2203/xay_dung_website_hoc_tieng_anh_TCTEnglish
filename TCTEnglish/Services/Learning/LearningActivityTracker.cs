using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;
using TCTEnglish.Services.Accounts;

namespace TCTEnglish.Services.Learning;

public sealed class ActivityCheckpointRequest
{
    public Guid SessionId { get; init; }
    public int Sequence { get; init; }
    public int CumulativeActiveSeconds { get; init; }
    public string LeaseToken { get; init; } = string.Empty;
    public bool Visible { get; init; }
    public bool EndSession { get; init; }
}

public sealed class ActivityCheckpointResult
{
    public bool Accepted { get; init; }
    public int AcceptedSeconds { get; init; }
    public string? Error { get; init; }
    public bool Duplicate { get; init; }
    public bool NotFound { get; init; }
}

public interface ILearningActivityTracker
{
    Task<(Guid SessionId, string LeaseToken)?> OpenSessionAsync(int userId, string feature, string? mode, CancellationToken cancellationToken = default);

    Task<ActivityCheckpointResult> AcceptCheckpointAsync(int userId, ActivityCheckpointRequest request, CancellationToken cancellationToken = default);
}

public sealed class LearningActivityTracker : ILearningActivityTracker
{
    public const int MaxCheckpointSeconds = 45;
    public const int MaxCumulativeSeconds = 8 * 60 * 60;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(40);
    private readonly DbflashcardContext _context;
    private readonly TimeProvider _timeProvider;

    public LearningActivityTracker(DbflashcardContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public Task<(Guid SessionId, string LeaseToken)?> OpenSessionAsync(int userId, string feature, string? mode, CancellationToken cancellationToken = default)
        => UserMutationScope.ExecuteAsync(_context, userId,
            () => OpenSessionCoreAsync(userId, feature, mode, cancellationToken), result => result.HasValue, cancellationToken);

    private async Task<(Guid SessionId, string LeaseToken)?> OpenSessionCoreAsync(int userId, string feature, string? mode, CancellationToken cancellationToken)
    {
        if (!LearningActivityCatalog.TryMap(feature, mode, out var skill, out var storedMode))
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (!await _context.Users.AnyAsync(row => row.UserId == userId && row.AnonymizedAt == null
                && (row.Status != UserStatus.Blocked || (row.LockExpiry != null && row.LockExpiry <= now)), cancellationToken)
            || await _context.LearningActivitySessions.CountAsync(row => row.UserId == userId
                && row.StartedAtUtc > now.AddMinutes(-5), cancellationToken) >= 30)
            return null;
        var session = new LearningActivitySession
        {
            SessionId = Guid.NewGuid(),
            UserId = userId,
            Skill = skill,
            Mode = storedMode,
            StartedAtUtc = now,
            LastAcceptedAtUtc = now,
            LeaseToken = Guid.NewGuid().ToString("N"),
            LeaseExpiresAtUtc = now
        };
        _context.LearningActivitySessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);
        return (session.SessionId, session.LeaseToken);
    }

    public Task<ActivityCheckpointResult> AcceptCheckpointAsync(int userId, ActivityCheckpointRequest request, CancellationToken cancellationToken = default)
        => UserMutationScope.ExecuteAsync(_context, userId,
            () => AcceptCheckpointCoreAsync(userId, request, cancellationToken), result => result.Accepted, cancellationToken);

    private async Task<ActivityCheckpointResult> AcceptCheckpointCoreAsync(int userId, ActivityCheckpointRequest request, CancellationToken cancellationToken)
    {
        if (request.Sequence <= 0 || request.CumulativeActiveSeconds < 0 || request.CumulativeActiveSeconds > MaxCumulativeSeconds)
        {
            return new ActivityCheckpointResult { Error = "Dữ liệu thời gian không hợp lệ." };
        }

        if (string.IsNullOrWhiteSpace(request.LeaseToken))
        {
            return new ActivityCheckpointResult { Error = "Phiên theo dõi không hợp lệ." };
        }

        var session = await _context.LearningActivitySessions.FirstOrDefaultAsync(
            row => row.SessionId == request.SessionId && row.UserId == userId,
            cancellationToken);
        if (session == null || session.UserId != userId || !string.Equals(session.LeaseToken, request.LeaseToken, StringComparison.Ordinal))
        {
            return new ActivityCheckpointResult { Error = "Không tìm thấy phiên theo dõi.", NotFound = true };
        }

        await _context.Entry(session).ReloadAsync(cancellationToken);

        var existing = await _context.LearningActivitySegments.AsNoTracking().FirstOrDefaultAsync(
            row => row.SessionId == session.SessionId && row.Sequence == request.Sequence,
            cancellationToken);
        if (existing != null || request.Sequence <= session.LastSequence)
        {
            return new ActivityCheckpointResult
            {
                Accepted = true,
                Duplicate = true,
                AcceptedSeconds = existing?.AcceptedSeconds ?? 0
            };
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (session.StartedAtUtc < now.AddHours(-8))
            return new ActivityCheckpointResult { Error = "Phiên theo dõi đã hết hạn." };
        var holdsLease = await _context.LearningActivitySessions.AnyAsync(
            row => row.UserId == userId
                   && row.SessionId != session.SessionId
                   && row.LeaseExpiresAtUtc > now,
            cancellationToken);

        var rawDelta = request.CumulativeActiveSeconds - session.LastAcceptedCumulativeSeconds;
        var wallSeconds = Math.Max(0, (int)Math.Floor((now - session.LastAcceptedAtUtc).TotalSeconds));
        var accepted = request.Visible && !holdsLease
            ? Math.Clamp(rawDelta, 0, Math.Min(wallSeconds, MaxCheckpointSeconds))
            : 0;
        if (rawDelta < 0)
        {
            return new ActivityCheckpointResult { Error = "Dữ liệu thời gian không hợp lệ." };
        }

        var proposedEnd = now;
        var proposedStart = proposedEnd.AddSeconds(-accepted);
        if (accepted > 0)
        {
            var lastCoveredEnd = await _context.LearningActivitySegments.AsNoTracking()
                .Where(row => row.UserId == userId && row.AcceptedSeconds > 0
                    && row.StartedAtUtc < proposedEnd && row.EndedAtUtc > proposedStart)
                .Select(row => (DateTime?)row.EndedAtUtc).MaxAsync(cancellationToken);
            if (lastCoveredEnd > proposedStart) proposedStart = lastCoveredEnd.Value;
            accepted = Math.Max(0, (int)Math.Floor((proposedEnd - proposedStart).TotalSeconds));
            proposedStart = proposedEnd.AddSeconds(-accepted);
        }

        _context.LearningActivitySegments.Add(new LearningActivitySegment
        {
            SessionId = session.SessionId,
            UserId = userId,
            Skill = session.Skill,
            Sequence = request.Sequence,
            StartedAtUtc = proposedStart,
            EndedAtUtc = proposedEnd,
            AcceptedSeconds = accepted
        });
        session.LastSequence = request.Sequence;
        session.LastAcceptedCumulativeSeconds = request.CumulativeActiveSeconds;
        session.LastAcceptedAtUtc = now;
        if (request.EndSession || !request.Visible)
        {
            session.LeaseExpiresAtUtc = now;
        }
        else if (!holdsLease && accepted > 0)
        {
            session.LeaseExpiresAtUtc = now.Add(LeaseDuration);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new ActivityCheckpointResult { Accepted = true, AcceptedSeconds = accepted };
    }

}
