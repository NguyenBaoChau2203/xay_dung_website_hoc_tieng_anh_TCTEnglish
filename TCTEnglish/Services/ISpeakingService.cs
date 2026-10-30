using System.Threading;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services;

public interface ISpeakingService
{
    Task<SpeakingIndexViewModel> GetSpeakingIndexViewModelAsync(int? currentUserId);
    Task<SpeakingPracticeViewModel?> GetSpeakingPracticeViewModelAsync(int videoId, int currentUserId);
    Task<SpeakingImportResult> CreateOwnedVideoAsync(int userId, string youtubeUrl, CancellationToken ct = default);
    Task<LearnerImportPreparation> PrepareYoutubeCaptionsAsync(int userId, string youtubeUrl, CancellationToken ct = default);
    Task<LearnerImportPreparation> PrepareSubtitleImportAsync(int userId, string youtubeUrl, string subtitleText, CancellationToken ct = default);
    Task<SpeakingImportResult> PublishPreparedImportAsync(int userId, PreparedLearnerImport prepared, bool reuseMatchingSource, CancellationToken ct = default);
    Task<OperationResult> DeleteOwnedVideoAsync(int userId, int videoId);
}

public sealed class LearnerImportCue
{
    public double StartTime { get; init; }
    public double EndTime { get; init; }
    public string Text { get; init; } = string.Empty;
}

public sealed class PreparedLearnerImport
{
    public required string YoutubeId { get; init; }
    public required string Title { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? Duration { get; init; }
    public required string SourceUrl { get; init; }
    public required string TranscriptSource { get; init; }
    public required string ContentHash { get; init; }
    public required IReadOnlyList<LearnerImportCue> Cues { get; init; }
}

public sealed class LearnerImportPreparation
{
    public PreparedLearnerImport? Prepared { get; init; }
    public SpeakingImportResult? Failure { get; init; }
    public bool IsSuccess => Prepared != null && Failure == null;

    public static LearnerImportPreparation Ready(PreparedLearnerImport prepared) => new() { Prepared = prepared };
    public static LearnerImportPreparation Failed(SpeakingImportResult failure) => new() { Failure = failure };
}

public sealed class SpeakingImportResult
{
    private SpeakingImportResult(bool isSuccess, string? errorMessage = null, int? videoId = null, bool requiresUpgrade = false)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        VideoId = videoId;
        RequiresUpgrade = requiresUpgrade;
    }

    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public int? VideoId { get; }
    public bool RequiresUpgrade { get; }
    public bool IsSourceConflict { get; private set; }

    public static SpeakingImportResult Success(int videoId) => new(true, videoId: videoId);
    public static SpeakingImportResult Invalid(string errorMessage) => new(false, errorMessage);
    public static SpeakingImportResult SourceConflict(string errorMessage) => new(false, errorMessage) { IsSourceConflict = true };
    public static SpeakingImportResult UpgradeRequired(string errorMessage) => new(false, errorMessage, requiresUpgrade: true);
}
