using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services.Speaking;
using TCTVocabulary.Models;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services;

public sealed class SpeakingService : ISpeakingService
{
    private const string PremiumUserSourceType = "premium-user-youtube";
    private const string UpgradeRequiredMessage = "Vui lòng nâng cấp lên Premium để sử dụng tính năng này.";
    public const int DefaultPublicPageSize = 24;
    public const int MaxPageSize = 100;
    public const int PlaylistPreviewVideoCount = 6;

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

    public async Task<SpeakingIndexViewModel> GetSpeakingIndexViewModelAsync(
        int? currentUserId,
        SpeakingCatalogQuery? query = null)
    {
        query ??= new SpeakingCatalogQuery();
        var pageSize = Math.Clamp(query.PageSize <= 0 ? DefaultPublicPageSize : query.PageSize, 1, MaxPageSize);
        var page = Math.Clamp(query.Page, 1, int.MaxValue / pageSize);
        var skip = (page - 1) * pageSize;

        var activeLevel = NormalizeFilter(query.Level)?.ToUpperInvariant();
        var activeTopic = NormalizeFilter(query.Topic);
        var activeSearch = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        var topics = await _context.SpeakingVideos
            .AsNoTracking()
            .Where(v => v.OwnerUserId == null && !string.IsNullOrEmpty(v.Topic))
            .Select(v => v.Topic)
            .Distinct()
            .OrderBy(t => t)
            .Cast<string>()
            .ToListAsync();

        var playlists = await _context.SpeakingPlaylists
            .AsNoTracking()
            .Select(p => new SpeakingPlaylistViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                ThumbnailUrl = p.ThumbnailUrl,
                TotalVideoCount = p.SpeakingVideos.Count(v => v.OwnerUserId == null),
                Videos = p.SpeakingVideos
                    .Where(v => v.OwnerUserId == null)
                    .OrderBy(v => v.Id)
                    .Take(PlaylistPreviewVideoCount)
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

        var levelOrder = new[] { "A1", "A2", "B1", "B2", "C1", "C2" };
        var videosByLevel = new Dictionary<string, List<SpeakingVideoViewModel>>();
        var publicTotalByLevel = new Dictionary<string, int>();
        var publicHasMoreByLevel = new Dictionary<string, bool>();
        var publicTotalCount = 0;
        var publicHasMore = false;

        if (!query.Mine)
        {
            var publicBase = _context.SpeakingVideos
                .AsNoTracking()
                .Where(v => v.OwnerUserId == null);

            if (activeTopic is not null)
            {
                publicBase = publicBase.Where(v => v.Topic == activeTopic);
            }

            if (activeSearch is not null)
            {
                publicBase = publicBase.Where(v => v.Title.Contains(activeSearch));
            }

            var levelsToLoad = activeLevel is not null
                ? levelOrder.Where(l => l == activeLevel)
                : levelOrder;

            foreach (var level in levelsToLoad)
            {
                var levelQuery = publicBase.Where(v => v.Level == level);
                var total = await levelQuery.CountAsync();
                if (total == 0)
                {
                    continue;
                }

                publicTotalByLevel[level] = total;
                publicTotalCount += total;

                var levelSkip = skip;
                var levelTake = pageSize;
                var hasMore = total > levelSkip + levelTake;
                publicHasMoreByLevel[level] = hasMore;
                publicHasMore = publicHasMore || hasMore;

                var videos = await levelQuery
                    .OrderBy(v => v.Id)
                    .Skip(levelSkip)
                    .Take(levelTake)
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

                videosByLevel[level] = videos;
            }
        }

        var viewModel = new SpeakingIndexViewModel
        {
            Playlists = playlists,
            Topics = topics,
            VideosByLevel = videosByLevel,
            Page = page,
            PageSize = pageSize,
            ActiveLevel = activeLevel,
            ActiveTopic = activeTopic,
            ActiveSearch = activeSearch,
            PublicTotalCount = publicTotalCount,
            PublicHasMore = publicHasMore,
            PublicTotalByLevel = publicTotalByLevel,
            PublicHasMoreByLevel = publicHasMoreByLevel
        };

        if (!currentUserId.HasValue)
        {
            return viewModel;
        }

        var currentRole = await GetNormalizedRoleAsync(currentUserId.Value);
        var isLocked = currentRole == Roles.Standard;
        var myQuery = _context.SpeakingVideos
            .AsNoTracking()
            .Where(v => v.OwnerUserId == currentUserId.Value);

        if (activeSearch is not null)
        {
            myQuery = myQuery.Where(v => v.Title.Contains(activeSearch));
        }

        var myTotal = await myQuery.CountAsync();
        var myVideos = await myQuery
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.Id)
            .Skip(query.Mine ? skip : 0)
            .Take(pageSize)
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
        viewModel.MyCurrentPage = query.Mine ? page : 1;
        viewModel.MyVideosTotalCount = myTotal;
        viewModel.MyVideosHasMore = myTotal > (query.Mine ? skip + pageSize : pageSize);
        viewModel.CanImportPrivateVideos = currentRole is Roles.Admin or Roles.Premium;
        viewModel.IsMyVideosLocked = isLocked;

        return viewModel;
    }

    private static string? NormalizeFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : trimmed;
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
                "Video chưa có phụ đề tiếng Anh với mốc thời gian hợp lệ. Hãy chọn video có phụ đề hoặc tải file SRT/VTT tiếng Anh."));
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
