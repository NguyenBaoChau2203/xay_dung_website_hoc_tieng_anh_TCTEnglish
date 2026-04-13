using Microsoft.EntityFrameworkCore;
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

        var normalizedYoutubeId = YoutubeUrlHelper.NormalizeYoutubeId(youtubeUrl);
        if (normalizedYoutubeId is null)
        {
            return SpeakingImportResult.Invalid("YouTube URL không hợp lệ.");
        }

        var duplicateExists = await _context.SpeakingVideos
            .AsNoTracking()
            .AnyAsync(v => v.OwnerUserId == userId && v.YoutubeId == normalizedYoutubeId, ct);

        if (duplicateExists)
        {
            return SpeakingImportResult.Invalid("Video này đã có trong mục Bài nói của tôi.");
        }

        var transcriptResult = await _youtubeTranscriptService.GetTranscriptForSpeakingImportAsync(normalizedYoutubeId, ct);
        if (!transcriptResult.IsEnglishUsable || transcriptResult.Sentences.Count == 0)
        {
            return SpeakingImportResult.Invalid("Không thể lấy transcript tiếng Anh hợp lệ từ video này.");
        }

        var metadata = await _youtubeTranscriptService.GetVideoMetadataAsync(normalizedYoutubeId);
        if (metadata == null || string.IsNullOrWhiteSpace(metadata.Title))
        {
            return SpeakingImportResult.Invalid("Không thể tải metadata của video YouTube.");
        }

        var title = metadata.Title.Trim();
        if (title.Length > 255)
        {
            title = title[..255];
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        var createdVideoId = 0;

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            var video = new SpeakingVideo
            {
                OwnerUserId = userId,
                PlaylistId = null,
                Title = title,
                YoutubeId = normalizedYoutubeId,
                Level = null,
                Topic = null,
                ThumbnailUrl = metadata.ThumbnailUrl ?? YoutubeUrlHelper.BuildDefaultThumbnailUrl(normalizedYoutubeId),
                Duration = await _youtubeTranscriptService.GetVideoDurationAsync(normalizedYoutubeId),
                SourceUrl = youtubeUrl.Trim(),
                SourceType = PremiumUserSourceType,
                TranscriptSource = transcriptResult.TranscriptSource,
                ImportStatus = "ready",
                CreatedAt = DateTime.UtcNow
            };

            _context.SpeakingVideos.Add(video);
            await _context.SaveChangesAsync(ct);

            foreach (var sentence in transcriptResult.Sentences)
            {
                sentence.VideoId = video.Id;
                sentence.VietnameseMeaning = string.Empty;
            }

            _context.SpeakingSentences.AddRange(transcriptResult.Sentences);
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            createdVideoId = video.Id;
        });

        _logger.LogInformation("User {UserId} imported private speaking video {VideoId} ({YoutubeId}).", userId, createdVideoId, normalizedYoutubeId);
        return SpeakingImportResult.Success(createdVideoId);
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
