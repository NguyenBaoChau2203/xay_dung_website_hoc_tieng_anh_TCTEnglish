using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services;
using TCTEnglish.Services.Speaking;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTVocabulary.Workers;

/// <summary>
/// Processes queued Speaking import jobs. Network and parse stay outside the publish transaction.
/// Video, sentences and job success commit together. One process-local worker; not a cluster lease.
/// </summary>
public sealed class SpeakingImportWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PrepareTimeout = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SpeakingImportWorker> _logger;
    private readonly TimeProvider _time;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    public SpeakingImportWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<SpeakingImportWorker> logger,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _time = timeProvider;
    }

    /// <summary>Test seam. Production leaves this null. Invoked after prepare and before publish.</summary>
    public Func<CancellationToken, Task>? BeforePublishAsync { get; set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "SpeakingImportWorker cycle failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    public async Task ProcessOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DbflashcardContext>();
        var speakingService = new SpeakingService(
            context,
            scope.ServiceProvider.GetRequiredService<IYoutubeTranscriptService>(),
            scope.ServiceProvider.GetRequiredService<ILogger<SpeakingService>>());

        var now = UtcNow();
        var candidate = await context.SpeakingImportJobs.AsNoTracking()
            .Where(j =>
                (j.Status == "queued"
                    && j.AttemptCount < j.MaxAttempts
                    && (j.NextAttemptAtUtc == null || j.NextAttemptAtUtc <= now))
                || (j.Status == "running" && j.LeaseUntilUtc != null && j.LeaseUntilUtc < now))
            .OrderBy(j => j.Id)
            .FirstOrDefaultAsync(ct);
        if (candidate == null)
        {
            return;
        }

        var token = Guid.NewGuid().ToString("N");
        var claimed = await context.SpeakingImportJobs
            .Where(j => j.Id == candidate.Id && (
                (j.Status == "queued"
                    && j.AttemptCount < j.MaxAttempts
                    && (j.NextAttemptAtUtc == null || j.NextAttemptAtUtc <= now))
                || (j.Status == "running" && j.LeaseUntilUtc != null && j.LeaseUntilUtc < now)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, "running")
                .SetProperty(j => j.LeaseToken, token)
                .SetProperty(j => j.LeaseOwner, _instanceId)
                .SetProperty(j => j.LeaseUntilUtc, now.Add(LeaseDuration))
                .SetProperty(j => j.AttemptCount, j => j.AttemptCount + 1)
                .SetProperty(j => j.StartedAtUtc, j => j.StartedAtUtc ?? now)
                .SetProperty(j => j.LastError, (string?)null), ct);
        if (claimed != 1)
        {
            return;
        }

        var job = await context.SpeakingImportJobs.AsNoTracking()
            .FirstAsync(j => j.Id == candidate.Id, ct);
        try
        {
            if (!await StillPremiumAsync(context, job.OwnerUserId, ct))
            {
                await FailPermanentAsync(context, job.Id, token, "Premium required at publish time.", ct);
                return;
            }

            using var prepareTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            prepareTimeout.CancelAfter(PrepareTimeout);
            var preparation = await PrepareAsync(speakingService, job, prepareTimeout.Token);
            if (!preparation.IsSuccess || preparation.Prepared == null)
            {
                await ScheduleRetryAsync(context, job, token, preparation.Failure?.ErrorMessage ?? "Import failed.", ct);
                return;
            }

            if (BeforePublishAsync != null)
            {
                await BeforePublishAsync(ct);
            }

            if (!await StillPremiumAsync(context, job.OwnerUserId, ct))
            {
                await FailPermanentAsync(context, job.Id, token, "Premium required at publish time.", ct);
                return;
            }

            var renewed = await context.SpeakingImportJobs
                .Where(j => j.Id == job.Id && j.LeaseToken == token && j.Status == "running")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.LeaseUntilUtc, UtcNow().Add(LeaseDuration)), ct);
            if (renewed != 1)
            {
                return;
            }

            await using var transaction = await context.Database.BeginTransactionAsync(ct);
            var owned = await context.SpeakingImportJobs.AsNoTracking()
                .AnyAsync(j => j.Id == job.Id && j.LeaseToken == token && j.Status == "running", ct);
            if (!owned)
            {
                await transaction.RollbackAsync(ct);
                return;
            }

            var published = await speakingService.PublishPreparedImportAsync(
                job.OwnerUserId,
                preparation.Prepared,
                reuseMatchingSource: true,
                ct);
            if (published.IsSourceConflict)
            {
                await transaction.RollbackAsync(ct);
                await FailPermanentAsync(context, job.Id, token, published.ErrorMessage ?? "Source conflict.", ct);
                return;
            }

            if (!published.IsSuccess || published.VideoId is not int videoId)
            {
                await transaction.RollbackAsync(ct);
                await ScheduleRetryAsync(context, job, token, published.ErrorMessage ?? "Import failed.", ct);
                return;
            }

            var finished = await context.SpeakingImportJobs
                .Where(j => j.Id == job.Id && j.LeaseToken == token && j.Status == "running")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, "succeeded")
                    .SetProperty(j => j.ResultVideoId, videoId)
                    .SetProperty(j => j.CompletedAtUtc, UtcNow())
                    .SetProperty(j => j.LastError, (string?)null)
                    .SetProperty(j => j.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(j => j.LeaseOwner, (string?)null)
                    .SetProperty(j => j.SourceContentHash, preparation.Prepared.ContentHash)
                    .SetProperty(j => j.SourceKind, job.SourceKind ?? "youtube"), ct);
            if (finished != 1)
            {
                await transaction.RollbackAsync(ct);
                return;
            }

            await transaction.CommitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await context.SpeakingImportJobs
                .Where(j => j.Id == job.Id && j.LeaseToken == token)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, "queued")
                    .SetProperty(j => j.AttemptCount, j => j.AttemptCount > 0 ? j.AttemptCount - 1 : 0)
                    .SetProperty(j => j.LeaseToken, (string?)null)
                    .SetProperty(j => j.LeaseOwner, (string?)null)
                    .SetProperty(j => j.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(j => j.NextAttemptAtUtc, (DateTime?)null), CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Speaking import job {JobId} failed.", job.PublicId);
            await ScheduleRetryAsync(context, job, token, "Import exception.", CancellationToken.None);
        }
    }

    private static async Task<LearnerImportPreparation> PrepareAsync(
        SpeakingService speakingService,
        SpeakingImportJob job,
        CancellationToken ct)
    {
        var legacyYoutube = string.IsNullOrWhiteSpace(job.SourceKind) || job.SourceKind == "youtube";
        if (!legacyYoutube && !string.IsNullOrWhiteSpace(job.SubtitlePayload))
        {
            return await speakingService.PrepareSubtitleImportAsync(job.OwnerUserId, job.SourceUrl, job.SubtitlePayload, ct);
        }

        return await speakingService.PrepareYoutubeCaptionsAsync(job.OwnerUserId, job.SourceUrl, ct);
    }

    private async Task ScheduleRetryAsync(
        DbflashcardContext context,
        SpeakingImportJob job,
        string token,
        string error,
        CancellationToken ct)
    {
        var latest = await context.SpeakingImportJobs.AsNoTracking()
            .Where(j => j.Id == job.Id && j.LeaseToken == token)
            .Select(j => new { j.AttemptCount, j.MaxAttempts, j.Id })
            .FirstOrDefaultAsync(ct);
        if (latest == null)
        {
            return;
        }

        var failed = latest.AttemptCount >= latest.MaxAttempts;
        var delaySeconds = Math.Min(120, 15 * Math.Max(1, latest.AttemptCount)) + (int)(latest.Id % 7);
        var message = Truncate(error);
        await context.SpeakingImportJobs
            .Where(j => j.Id == job.Id && j.LeaseToken == token)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, failed ? "failed" : "queued")
                .SetProperty(j => j.LastError, message)
                .SetProperty(j => j.CompletedAtUtc, failed ? UtcNow() : (DateTime?)null)
                .SetProperty(j => j.NextAttemptAtUtc, failed ? (DateTime?)null : UtcNow().AddSeconds(delaySeconds))
                .SetProperty(j => j.LeaseUntilUtc, (DateTime?)null)
                .SetProperty(j => j.LeaseOwner, (string?)null)
                .SetProperty(j => j.LeaseToken, (string?)null), ct);
    }

    private async Task FailPermanentAsync(
        DbflashcardContext context,
        long jobId,
        string token,
        string error,
        CancellationToken ct)
    {
        await context.SpeakingImportJobs
            .Where(j => j.Id == jobId && j.LeaseToken == token)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, "failed")
                .SetProperty(j => j.LastError, Truncate(error))
                .SetProperty(j => j.CompletedAtUtc, UtcNow())
                .SetProperty(j => j.LeaseUntilUtc, (DateTime?)null)
                .SetProperty(j => j.LeaseOwner, (string?)null)
                .SetProperty(j => j.NextAttemptAtUtc, (DateTime?)null), ct);
    }

    private static async Task<bool> StillPremiumAsync(DbflashcardContext context, int userId, CancellationToken ct)
    {
        var role = await context.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);
        return Roles.Normalize(role) is Roles.Admin or Roles.Premium;
    }

    private DateTime UtcNow() => _time.GetUtcNow().UtcDateTime;

    private static string Truncate(string? message)
    {
        var text = string.IsNullOrWhiteSpace(message) ? "Import failed." : message.Trim();
        return text.Length > 500 ? text[..500] : text;
    }
}
