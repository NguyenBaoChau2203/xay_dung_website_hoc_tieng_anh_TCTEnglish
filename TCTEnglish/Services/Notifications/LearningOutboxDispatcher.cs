using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTVocabulary.Models;

namespace TCTEnglish.Services.Notifications;

public interface ILearningOutboxDispatcher
{
    Task<int> DrainAsync(int maxEvents, CancellationToken cancellationToken);
}

public sealed class LearningOutboxDispatcher : ILearningOutboxDispatcher
{
    private const int MaxAttempts = 10;
    private readonly DbflashcardContext _context;
    private readonly LearningBusinessClock _clock;
    private readonly ILogger<LearningOutboxDispatcher> _logger;

    public LearningOutboxDispatcher(
        DbflashcardContext context,
        LearningBusinessClock clock,
        ILogger<LearningOutboxDispatcher> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> DrainAsync(int maxEvents, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var due = await _context.LearningNotificationOutboxEvents
            .Where(e => e.ProcessedAtUtc == null
                && e.AttemptCount < MaxAttempts
                && (e.NextAttemptAtUtc == null || e.NextAttemptAtUtc <= now))
            .OrderBy(e => e.Id)
            .Take(Math.Clamp(maxEvents, 1, 100))
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var item in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!TryReadIntent(item, out var intent, out var failure))
                {
                    item.AttemptCount = MaxAttempts;
                    item.LastError = failure;
                    item.NextAttemptAtUtc = null;
                    await _context.SaveChangesAsync(cancellationToken);
                    continue;
                }

                await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
                var exists = await _context.Notifications.AsNoTracking()
                    .AnyAsync(n => n.UserId == item.UserId && n.EventKey == intent.EventKey, cancellationToken);
                if (!exists)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = item.UserId,
                        Title = intent.Title,
                        Message = intent.Message,
                        Type = intent.Type,
                        IconClass = intent.IconClass,
                        RelatedUrl = intent.RelatedUrl,
                        IsRead = false,
                        CreatedAt = intent.OccurredAtUtc,
                        EventKey = intent.EventKey
                    });
                }

                item.ProcessedAtUtc = _clock.UtcNow;
                item.LastError = null;
                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                processed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (_context.Database.CurrentTransaction != null)
                {
                    await _context.Database.RollbackTransactionAsync(cancellationToken);
                }

                foreach (var entry in _context.ChangeTracker.Entries<Notification>().Where(e => e.State == EntityState.Added).ToList())
                {
                    entry.State = EntityState.Detached;
                }

                var tracked = await _context.LearningNotificationOutboxEvents
                    .FirstOrDefaultAsync(e => e.Id == item.Id, cancellationToken);
                if (tracked == null)
                {
                    continue;
                }

                tracked.AttemptCount += 1;
                tracked.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                tracked.NextAttemptAtUtc = _clock.UtcNow.AddSeconds(Math.Min(300, 30 * tracked.AttemptCount));
                tracked.ProcessedAtUtc = null;
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(ex, "Outbox event {EventId} failed attempt {Attempt}", item.EventId, tracked.AttemptCount);
            }
        }

        return processed;
    }

    private static bool TryReadIntent(LearningNotificationOutbox item, out OutboxIntent intent, out string failure)
    {
        intent = default;
        failure = string.Empty;
        if (item.PayloadVersion < 2 || string.IsNullOrWhiteSpace(item.PayloadJson))
        {
            failure = "needs-review: legacy payload is not a typed notification intent";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(item.PayloadJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("notificationType", out var typeProp)
                || !Enum.TryParse<NotificationType>(typeProp.GetString(), out var type)
                || !root.TryGetProperty("eventKey", out var keyProp)
                || !root.TryGetProperty("title", out var titleProp)
                || !root.TryGetProperty("message", out var messageProp))
            {
                failure = "needs-review: payload v2 missing notification fields";
                return false;
            }

            var occurred = item.OccurredAtUtc ?? item.CreatedAtUtc;
            if (root.TryGetProperty("occurredAtUtc", out var occurredProp)
                && occurredProp.TryGetDateTime(out var parsed))
            {
                occurred = parsed.Kind == DateTimeKind.Utc ? parsed : DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            intent = new OutboxIntent(
                type,
                keyProp.GetString() ?? string.Empty,
                titleProp.GetString() ?? string.Empty,
                messageProp.GetString() ?? string.Empty,
                root.TryGetProperty("iconClass", out var icon) ? icon.GetString() ?? "fas fa-bell" : "fas fa-bell",
                root.TryGetProperty("relatedUrl", out var url) ? url.GetString() : "/Goals",
                occurred);
            if (string.IsNullOrWhiteSpace(intent.EventKey))
            {
                failure = "needs-review: empty event key";
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            failure = "needs-review: malformed payload";
            return false;
        }
    }

    private readonly record struct OutboxIntent(
        NotificationType Type,
        string EventKey,
        string Title,
        string Message,
        string IconClass,
        string? RelatedUrl,
        DateTime OccurredAtUtc);
}
