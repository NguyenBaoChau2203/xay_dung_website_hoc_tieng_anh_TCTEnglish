using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Speaking;

public interface ISpeakingImportJobService
{
    Task<SpeakingImportJobEnqueueResult> EnqueueYoutubeImportAsync(int userId, string youtubeUrl, CancellationToken ct);
    Task<SpeakingImportJobEnqueueResult> EnqueueSubtitleImportAsync(int userId, string youtubeUrl, string subtitleText, CancellationToken ct);
    Task<SpeakingImportJob?> GetOwnedJobAsync(int userId, Guid publicId, CancellationToken ct);
}

public sealed class SpeakingImportJobEnqueueResult
{
    public bool RequiresUpgrade { get; init; }
    public bool IsInvalid { get; init; }
    public bool IsConflict { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? JobPublicId { get; init; }
    public string? StatusUrl { get; init; }

    public static SpeakingImportJobEnqueueResult Upgrade(string message) => new()
    {
        RequiresUpgrade = true,
        ErrorMessage = message
    };

    public static SpeakingImportJobEnqueueResult Invalid(string message) => new()
    {
        IsInvalid = true,
        ErrorMessage = message
    };

    public static SpeakingImportJobEnqueueResult Conflict(string message) => new()
    {
        IsConflict = true,
        ErrorMessage = message
    };

    public static SpeakingImportJobEnqueueResult Accepted(Guid publicId) => new()
    {
        JobPublicId = publicId,
        StatusUrl = $"/api/speaking/imports/{publicId}"
    };
}

public sealed class SpeakingImportJobService : ISpeakingImportJobService
{
    private readonly DbflashcardContext _context;

    public SpeakingImportJobService(DbflashcardContext context)
    {
        _context = context;
    }

    public Task<SpeakingImportJobEnqueueResult> EnqueueYoutubeImportAsync(int userId, string youtubeUrl, CancellationToken ct)
        => EnqueueAsync(userId, youtubeUrl, "youtube", subtitlePayload: null, contentHash: null, ct);

    public Task<SpeakingImportJobEnqueueResult> EnqueueSubtitleImportAsync(
        int userId,
        string youtubeUrl,
        string subtitleText,
        CancellationToken ct)
    {
        var text = subtitleText ?? string.Empty;
        var parsed = SpeakingSubtitleParser.Parse(text);
        if (!parsed.Ok)
        {
            return Task.FromResult(SpeakingImportJobEnqueueResult.Invalid(parsed.Error ?? "Phụ đề không hợp lệ."));
        }

        if (text.Length > 1_000_000)
        {
            return Task.FromResult(SpeakingImportJobEnqueueResult.Invalid("Subtitle vượt quá 1 MiB."));
        }

        var hash = SpeakingImportFingerprints.ContentHash(parsed.Sentences.Select(sentence => sentence.Text));
        return EnqueueAsync(userId, youtubeUrl, "subtitle", text, hash, ct);
    }

    public Task<SpeakingImportJob?> GetOwnedJobAsync(int userId, Guid publicId, CancellationToken ct)
    {
        return _context.SpeakingImportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.PublicId == publicId && j.OwnerUserId == userId, ct);
    }

    private async Task<SpeakingImportJobEnqueueResult> EnqueueAsync(
        int userId,
        string youtubeUrl,
        string sourceKind,
        string? subtitlePayload,
        string? contentHash,
        CancellationToken ct)
    {
        var role = await _context.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);
        if (Roles.Normalize(role) is not (Roles.Admin or Roles.Premium))
        {
            return SpeakingImportJobEnqueueResult.Upgrade("Vui lòng nâng cấp lên Premium để sử dụng tính năng này.");
        }

        var youtubeId = YoutubeUrlHelper.NormalizeYoutubeId(youtubeUrl);
        if (youtubeId is null)
        {
            return SpeakingImportJobEnqueueResult.Invalid("YouTube URL không hợp lệ.");
        }

        var fingerprint = SpeakingImportFingerprints.ForOwnerVideo(userId, youtubeId);
        var existing = await _context.SpeakingImportJobs
            .AsNoTracking()
            .Where(j => j.OwnerUserId == userId && j.RequestFingerprint == fingerprint)
            .OrderByDescending(j => j.Id)
            .FirstOrDefaultAsync(ct);

        if (existing != null)
        {
            if (existing.Status is "queued" or "running")
            {
                return SpeakingImportJobEnqueueResult.Accepted(existing.PublicId);
            }

            if (existing.Status == "succeeded")
            {
                var videoId = await ResolveOwnedVideoIdAsync(userId, youtubeId, existing.ResultVideoId, ct);
                if (videoId is int ownedVideoId)
                {
                    if (sourceKind == "subtitle" && contentHash != null)
                    {
                        var videoHash = await HashVideoAsync(ownedVideoId, ct);
                        if (!string.Equals(videoHash, contentHash, StringComparison.Ordinal))
                        {
                            return SpeakingImportJobEnqueueResult.Conflict(
                                "Video đã có transcript khác. Xóa bài cũ trước khi nhập phụ đề khác. Không ghi đè.");
                        }
                    }

                    return SpeakingImportJobEnqueueResult.Accepted(existing.PublicId);
                }

                var requeuedDeleted = await RequeueAsync(existing.Id, userId, youtubeId, youtubeUrl, sourceKind, subtitlePayload, contentHash, ct);
                return requeuedDeleted
                    ? SpeakingImportJobEnqueueResult.Accepted(existing.PublicId)
                    : await ReloadAcceptedAsync(userId, fingerprint, ct);
            }

            if (existing.Status is "failed" or "cancelled")
            {
                var requeued = await RequeueAsync(existing.Id, userId, youtubeId, youtubeUrl, sourceKind, subtitlePayload, contentHash, ct);
                return requeued
                    ? SpeakingImportJobEnqueueResult.Accepted(existing.PublicId)
                    : await ReloadAcceptedAsync(userId, fingerprint, ct);
            }
        }

        var job = new SpeakingImportJob
        {
            PublicId = Guid.NewGuid(),
            OwnerUserId = userId,
            YoutubeId = youtubeId,
            SourceUrl = youtubeUrl.Trim(),
            RequestFingerprint = fingerprint,
            Status = "queued",
            AttemptCount = 0,
            MaxAttempts = 3,
            CreatedAtUtc = DateTime.UtcNow,
            SourceKind = sourceKind,
            SubtitlePayload = subtitlePayload,
            SourceContentHash = contentHash
        };
        _context.SpeakingImportJobs.Add(job);
        try
        {
            await _context.SaveChangesAsync(ct);
            return SpeakingImportJobEnqueueResult.Accepted(job.PublicId);
        }
        catch (DbUpdateException ex) when (RelationalUniqueConflict.IsUnique(ex))
        {
            _context.Entry(job).State = EntityState.Detached;
            return await ReloadAcceptedAsync(userId, fingerprint, ct);
        }
    }

    private async Task<SpeakingImportJobEnqueueResult> ReloadAcceptedAsync(int userId, string fingerprint, CancellationToken ct)
    {
        var winner = await _context.SpeakingImportJobs
            .AsNoTracking()
            .Where(j => j.OwnerUserId == userId && j.RequestFingerprint == fingerprint)
            .OrderByDescending(j => j.Id)
            .FirstOrDefaultAsync(ct);
        return winner == null
            ? SpeakingImportJobEnqueueResult.Invalid("Không đọc lại được job import.")
            : SpeakingImportJobEnqueueResult.Accepted(winner.PublicId);
    }

    private async Task<bool> RequeueAsync(
        long jobId,
        int userId,
        string youtubeId,
        string youtubeUrl,
        string sourceKind,
        string? subtitlePayload,
        string? contentHash,
        CancellationToken ct)
    {
        var rows = await _context.SpeakingImportJobs
            .Where(j => j.Id == jobId
                && j.OwnerUserId == userId
                && (j.Status == "failed" || j.Status == "cancelled" || j.Status == "succeeded"))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, "queued")
                .SetProperty(j => j.YoutubeId, youtubeId)
                .SetProperty(j => j.SourceUrl, youtubeUrl.Trim())
                .SetProperty(j => j.SourceKind, sourceKind)
                .SetProperty(j => j.SubtitlePayload, subtitlePayload)
                .SetProperty(j => j.SourceContentHash, contentHash)
                .SetProperty(j => j.AttemptCount, 0)
                .SetProperty(j => j.ResultVideoId, (int?)null)
                .SetProperty(j => j.LastError, (string?)null)
                .SetProperty(j => j.CompletedAtUtc, (DateTime?)null)
                .SetProperty(j => j.StartedAtUtc, (DateTime?)null)
                .SetProperty(j => j.LeaseUntilUtc, (DateTime?)null)
                .SetProperty(j => j.LeaseOwner, (string?)null)
                .SetProperty(j => j.LeaseToken, (string?)null)
                .SetProperty(j => j.NextAttemptAtUtc, (DateTime?)null), ct);
        return rows == 1;
    }

    private async Task<int?> ResolveOwnedVideoIdAsync(int userId, string youtubeId, int? resultVideoId, CancellationToken ct)
    {
        if (resultVideoId is int id)
        {
            var owned = await _context.SpeakingVideos.AsNoTracking()
                .AnyAsync(v => v.Id == id && v.OwnerUserId == userId, ct);
            if (owned)
            {
                return id;
            }
        }

        return await _context.SpeakingVideos.AsNoTracking()
            .Where(v => v.OwnerUserId == userId && v.YoutubeId == youtubeId)
            .Select(v => (int?)v.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<string> HashVideoAsync(int videoId, CancellationToken ct)
    {
        var texts = await _context.SpeakingSentences.AsNoTracking()
            .Where(s => s.VideoId == videoId)
            .OrderBy(s => s.StartTime)
            .ThenBy(s => s.Id)
            .Select(s => s.Text)
            .ToListAsync(ct);
        return SpeakingImportFingerprints.ContentHash(texts);
    }
}
