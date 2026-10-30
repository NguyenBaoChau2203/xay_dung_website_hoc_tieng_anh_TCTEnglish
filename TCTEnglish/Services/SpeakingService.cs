using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services.Speaking;
using TCTVocabulary.Models;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services;

public sealed class SpeakingService : ISpeakingService
{
    private const string PremiumUserSourceType = "premium-user-youtube";
    private const string UpgradeRequiredMessage = "Vui lòng nâng cấp lên Premium để sử dụng tính năng này.";

    private readonly DbflashcardContext _context;
    private readonly IYoutubeTranscriptService _youtubeTranscriptService;
    private readonly ILogger<SpeakingService> _logger;

    public SpeakingService(
        DbflashcardContext context,
        IYoutubeTranscriptService youtubeTranscriptService,
        ILogger<SpeakingService> logger)
    {
        _context = context;
        _youtubeTranscriptService = youtubeTranscriptService;
        _logger = logger;
    }

    public async Task<SpeakingIndexViewModel> GetSpeakingIndexViewModelAsync(int? currentUserId)
    {
        var allVideos = await _context.SpeakingVideos
            .AsNoTracking()
            .Where(v => v.OwnerUserId == null)
            .Select(v => new SpeakingVideoViewModel
            {
                Id = v.Id,
                Title = v.Title,
                YoutubeId = v.YoutubeId,
                Level = v.Level ?? string.Empty,
                Topic = v.Topic ?? string.Empty,
                Duration = v.Duration,
                ThumbnailUrl = v.ThumbnailUrl ?? YoutubeUrlHelper.BuildDefaultThumbnailUrl(v.YoutubeId),
                SentenceCount = v.SpeakingSentences.Count
            })
            .ToListAsync();

        var topics = await _context.SpeakingVideos
            .AsNoTracking()
            .Where(v => v.OwnerUserId == null && !string.IsNullOrEmpty(v.Topic))
            .Select(v => v.Topic)
            .Distinct()
            .OrderBy(t => t)
            .Cast<string>()
            .ToListAsync();

        var levelOrder = new[] { "A1", "A2", "B1", "B2", "C1", "C2" };
        var videosByLevel = new Dictionary<string, List<SpeakingVideoViewModel>>();

        foreach (var level in levelOrder)
        {
            var videosInLevel = allVideos
                .Where(v => v.Level == level)
                .ToList();

            if (videosInLevel.Count > 0)
            {
                videosByLevel[level] = videosInLevel;
            }
        }

        var playlists = await _context.SpeakingPlaylists
            .AsNoTracking()
            .Select(p => new SpeakingPlaylistViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                ThumbnailUrl = p.ThumbnailUrl,
                Videos = p.SpeakingVideos
                    .Where(v => v.OwnerUserId == null)
                    .Select(v => new SpeakingVideoViewModel
                    {
                        Id = v.Id,
                        Title = v.Title,
                        YoutubeId = v.YoutubeId,
                        Level = v.Level ?? string.Empty,
                        Topic = v.Topic ?? string.Empty,
                        Duration = v.Duration,
                        ThumbnailUrl = v.ThumbnailUrl ?? YoutubeUrlHelper.BuildDefaultThumbnailUrl(v.YoutubeId),
                        SentenceCount = v.SpeakingSentences.Count
                    }).ToList()
            })
            .ToListAsync();

        var viewModel = new SpeakingIndexViewModel
        {
            Playlists = playlists,
            Topics = topics,
            VideosByLevel = videosByLevel
        };

        if (!currentUserId.HasValue)
        {
            return viewModel;
        }

        var currentRole = await GetNormalizedRoleAsync(currentUserId.Value);
        var isLocked = currentRole == Roles.Standard;
        var myVideos = await _context.SpeakingVideos
            .AsNoTracking()
            .Where(v => v.OwnerUserId == currentUserId.Value)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new SpeakingVideoViewModel
            {
                Id = v.Id,
                Title = v.Title,
                YoutubeId = v.YoutubeId,
                Level = v.Level ?? string.Empty,
                Topic = v.Topic ?? string.Empty,
                Duration = v.Duration,
                ThumbnailUrl = v.ThumbnailUrl ?? YoutubeUrlHelper.BuildDefaultThumbnailUrl(v.YoutubeId),
                SentenceCount = v.SpeakingSentences.Count,
                IsPrivate = true,
                IsLocked = isLocked,
                ImportStatus = v.ImportStatus,
                CreatedAt = v.CreatedAt
            })
            .ToListAsync();

        viewModel.MyVideos = myVideos;
        viewModel.CanImportPrivateVideos = currentRole is Roles.Admin or Roles.Premium;
        viewModel.IsMyVideosLocked = isLocked;

        return viewModel;
    }

    public async Task<SpeakingPracticeViewModel?> GetSpeakingPracticeViewModelAsync(int videoId, int currentUserId)
    {
        var role = await GetNormalizedRoleAsync(currentUserId);

        var video = await _context.SpeakingVideos
            .AsNoTracking()
            .Include(v => v.SpeakingSentences)
                .ThenInclude(s => s.UserSpeakingProgresses.Where(p => p.UserId == currentUserId))
            .FirstOrDefaultAsync(v => v.Id == videoId && (v.OwnerUserId == null || v.OwnerUserId == currentUserId));

        if (video == null)
        {
            return null;
        }

        if (video.OwnerUserId == currentUserId && role == Roles.Standard)
        {
            return null;
        }

        return new SpeakingPracticeViewModel
        {
            VideoId = video.Id,
            Title = video.Title,
            YoutubeId = video.YoutubeId,
            IsPrivate = video.OwnerUserId.HasValue,
            Sentences = video.SpeakingSentences
                .OrderBy(s => s.StartTime)
                .Select(s =>
                {
                    var latestProgress = s.UserSpeakingProgresses
                        .OrderByDescending(p => p.PracticedAt)
                        .FirstOrDefault();

                    return new SpeakingSentenceViewModel
                    {
                        Id = s.Id,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        Text = s.Text,
                        VietnameseMeaning = s.VietnameseMeaning,
                        TotalScore = latestProgress?.TotalScore,
                        AccuracyScore = latestProgress?.AccuracyScore,
                        FluencyScore = latestProgress?.FluencyScore,
                        CompletenessScore = latestProgress?.CompletenessScore
                    };
                }).ToList()
        };
    }

    public async Task<SpeakingImportResult> CreateOwnedVideoAsync(int userId, string youtubeUrl, CancellationToken ct = default)
    {
        var role = await GetNormalizedRoleAsync(userId);
        if (role is not (Roles.Admin or Roles.Premium))
        {
            return SpeakingImportResult.UpgradeRequired(UpgradeRequiredMessage);
        }

        var preparation = await PrepareYoutubeCaptionsAsync(userId, youtubeUrl, ct);
        if (!preparation.IsSuccess || preparation.Prepared == null)
        {
            return preparation.Failure ?? SpeakingImportResult.Invalid("Không chuẩn bị được import.");
        }

        var duplicateExists = await _context.SpeakingVideos
            .AsNoTracking()
            .AnyAsync(v => v.OwnerUserId == userId && v.YoutubeId == preparation.Prepared.YoutubeId, ct);
        if (duplicateExists)
        {
            return SpeakingImportResult.Invalid("Video này đã có trong mục Bài nói của tôi.");
        }

        return await PublishPreparedImportAsync(userId, preparation.Prepared, reuseMatchingSource: false, ct);
    }

    public async Task<LearnerImportPreparation> PrepareYoutubeCaptionsAsync(int userId, string youtubeUrl, CancellationToken ct = default)
    {
        var normalizedYoutubeId = YoutubeUrlHelper.NormalizeYoutubeId(youtubeUrl);
        if (normalizedYoutubeId is null)
        {
            return LearnerImportPreparation.Failed(SpeakingImportResult.Invalid("YouTube URL không hợp lệ."));
        }

        var transcriptResult = await _youtubeTranscriptService.GetEnglishCaptionsOnlyAsync(normalizedYoutubeId, ct);
        if (!transcriptResult.IsEnglishUsable || transcriptResult.Sentences.Count == 0)
        {
            return LearnerImportPreparation.Failed(SpeakingImportResult.Invalid(
                "Không có caption tiếng Anh hợp lệ (có timestamp). Import Speaking không dùng Gemini fallback."));
        }

        var metadata = await _youtubeTranscriptService.GetVideoMetadataAsync(normalizedYoutubeId);
        if (metadata == null || string.IsNullOrWhiteSpace(metadata.Title))
        {
            return LearnerImportPreparation.Failed(SpeakingImportResult.Invalid("Không thể tải metadata của video YouTube."));
        }

        var duration = await _youtubeTranscriptService.GetVideoDurationAsync(normalizedYoutubeId);
        var cues = transcriptResult.Sentences
            .Select(sentence => new LearnerImportCue
            {
                StartTime = sentence.StartTime,
                EndTime = sentence.EndTime,
                Text = sentence.Text
            })
            .ToList();

        return LearnerImportPreparation.Ready(BuildPrepared(
            normalizedYoutubeId,
            youtubeUrl,
            metadata.Title,
            metadata.ThumbnailUrl,
            duration,
            transcriptResult.TranscriptSource ?? "youtube-captions",
            cues));
    }

    public async Task<LearnerImportPreparation> PrepareSubtitleImportAsync(
        int userId,
        string youtubeUrl,
        string subtitleText,
        CancellationToken ct = default)
    {
        var normalizedYoutubeId = YoutubeUrlHelper.NormalizeYoutubeId(youtubeUrl);
        if (normalizedYoutubeId is null)
        {
            return LearnerImportPreparation.Failed(SpeakingImportResult.Invalid("YouTube URL không hợp lệ."));
        }

        var parsed = SpeakingSubtitleParser.Parse(subtitleText ?? string.Empty);
        if (!parsed.Ok)
        {
            return LearnerImportPreparation.Failed(SpeakingImportResult.Invalid(parsed.Error ?? "Phụ đề không hợp lệ."));
        }

        string title = normalizedYoutubeId;
        string? thumbnail = YoutubeUrlHelper.BuildDefaultThumbnailUrl(normalizedYoutubeId);
        try
        {
            var metadata = await _youtubeTranscriptService.GetVideoMetadataAsync(normalizedYoutubeId);
            if (!string.IsNullOrWhiteSpace(metadata?.Title))
            {
                title = metadata.Title;
                thumbnail = metadata.ThumbnailUrl ?? thumbnail;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Subtitle import continues without YouTube metadata for {YoutubeId}.", normalizedYoutubeId);
        }

        var cues = parsed.Sentences
            .Select(sentence => new LearnerImportCue
            {
                StartTime = sentence.StartTime,
                EndTime = sentence.EndTime,
                Text = sentence.Text
            })
            .ToList();
        var duration = cues.Count == 0 ? null : TimeSpan.FromSeconds(cues[^1].EndTime).ToString(@"mm\:ss", System.Globalization.CultureInfo.InvariantCulture);

        return LearnerImportPreparation.Ready(BuildPrepared(
            normalizedYoutubeId,
            youtubeUrl,
            title,
            thumbnail,
            duration,
            "user-subtitle",
            cues));
    }

    public async Task<SpeakingImportResult> PublishPreparedImportAsync(
        int userId,
        PreparedLearnerImport prepared,
        bool reuseMatchingSource,
        CancellationToken ct = default)
    {
        var role = await GetNormalizedRoleAsync(userId);
        if (role is not (Roles.Admin or Roles.Premium))
        {
            return SpeakingImportResult.UpgradeRequired(UpgradeRequiredMessage);
        }

        if (_context.Database.CurrentTransaction != null)
        {
            return await PublishCoreAsync(userId, prepared, reuseMatchingSource, ct);
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        SpeakingImportResult created = SpeakingImportResult.Invalid("Import failed.");
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            created = await PublishCoreAsync(userId, prepared, reuseMatchingSource, ct);
            if (created.IsSuccess)
            {
                await transaction.CommitAsync(ct);
            }
            else
            {
                await transaction.RollbackAsync(ct);
            }
        });

        return created;
    }

    private async Task<SpeakingImportResult> PublishCoreAsync(
        int userId,
        PreparedLearnerImport prepared,
        bool reuseMatchingSource,
        CancellationToken ct)
    {
        var existing = await _context.SpeakingVideos
            .FirstOrDefaultAsync(v => v.OwnerUserId == userId && v.YoutubeId == prepared.YoutubeId, ct);
        if (existing != null)
        {
            if (!reuseMatchingSource)
            {
                return SpeakingImportResult.Invalid("Video này đã có trong mục Bài nói của tôi.");
            }

            var existingHash = await HashVideoAsync(existing.Id, ct);
            if (string.Equals(existingHash, prepared.ContentHash, StringComparison.Ordinal))
            {
                return SpeakingImportResult.Success(existing.Id);
            }

            return SpeakingImportResult.SourceConflict(
                "Video đã có transcript khác. Không ghi đè nội dung đã lưu.");
        }

        var video = new SpeakingVideo
        {
            OwnerUserId = userId,
            PlaylistId = null,
            Title = prepared.Title,
            YoutubeId = prepared.YoutubeId,
            Level = null,
            Topic = null,
            ThumbnailUrl = prepared.ThumbnailUrl ?? YoutubeUrlHelper.BuildDefaultThumbnailUrl(prepared.YoutubeId),
            Duration = prepared.Duration,
            SourceUrl = prepared.SourceUrl,
            SourceType = PremiumUserSourceType,
            TranscriptSource = prepared.TranscriptSource,
            ImportStatus = "ready",
            CreatedAt = DateTime.UtcNow
        };

        _context.SpeakingVideos.Add(video);
        await _context.SaveChangesAsync(ct);

        var sentences = prepared.Cues.Select(cue => new SpeakingSentence
        {
            VideoId = video.Id,
            StartTime = cue.StartTime,
            EndTime = cue.EndTime,
            Text = cue.Text,
            VietnameseMeaning = string.Empty
        }).ToList();
        _context.SpeakingSentences.AddRange(sentences);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "User {UserId} published private speaking video {VideoId} ({YoutubeId}) from {TranscriptSource}.",
            userId,
            video.Id,
            prepared.YoutubeId,
            prepared.TranscriptSource);
        return SpeakingImportResult.Success(video.Id);
    }

    private static PreparedLearnerImport BuildPrepared(
        string youtubeId,
        string youtubeUrl,
        string title,
        string? thumbnailUrl,
        string? duration,
        string transcriptSource,
        IReadOnlyList<LearnerImportCue> cues)
    {
        var trimmed = title.Trim();
        if (trimmed.Length > 255)
        {
            trimmed = trimmed[..255];
        }

        return new PreparedLearnerImport
        {
            YoutubeId = youtubeId,
            Title = trimmed,
            ThumbnailUrl = thumbnailUrl,
            Duration = duration,
            SourceUrl = youtubeUrl.Trim(),
            TranscriptSource = transcriptSource,
            ContentHash = SpeakingImportFingerprints.ContentHash(cues.Select(cue => cue.Text)),
            Cues = cues
        };
    }

    private async Task<string> HashVideoAsync(int videoId, CancellationToken ct)
    {
        var texts = await _context.SpeakingSentences.AsNoTracking()
            .Where(sentence => sentence.VideoId == videoId)
            .OrderBy(sentence => sentence.StartTime)
            .ThenBy(sentence => sentence.Id)
            .Select(sentence => sentence.Text)
            .ToListAsync(ct);
        return SpeakingImportFingerprints.ContentHash(texts);
    }

    public async Task<OperationResult> DeleteOwnedVideoAsync(int userId, int videoId)
    {
        var video = await _context.SpeakingVideos
            .FirstOrDefaultAsync(v => v.Id == videoId && v.OwnerUserId == userId);

        if (video == null)
        {
            return OperationResult.NotFound();
        }

        var role = await GetNormalizedRoleAsync(userId);
        if (role is not (Roles.Admin or Roles.Premium))
        {
            return OperationResult.Invalid(UpgradeRequiredMessage);
        }

        _context.SpeakingVideos.Remove(video);
        await _context.SaveChangesAsync();
        return OperationResult.Success();
    }

    private async Task<string> GetNormalizedRoleAsync(int userId)
    {
        var role = await _context.Users
            .AsNoTracking()
            .Where(user => user.UserId == userId)
            .Select(user => user.Role)
            .FirstOrDefaultAsync();

        return Roles.Normalize(role);
    }
}
