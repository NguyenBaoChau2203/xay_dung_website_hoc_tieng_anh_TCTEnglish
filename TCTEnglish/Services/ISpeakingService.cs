using System.Threading;
using TCTVocabulary.ViewModels;

namespace TCTVocabulary.Services;

public interface ISpeakingService
{
    Task<SpeakingIndexViewModel> GetSpeakingIndexViewModelAsync(int? currentUserId);
    Task<SpeakingPracticeViewModel?> GetSpeakingPracticeViewModelAsync(int videoId, int currentUserId);
    Task<SpeakingImportResult> CreateOwnedVideoAsync(int userId, string youtubeUrl, CancellationToken ct = default);
    Task<OperationResult> DeleteOwnedVideoAsync(int userId, int videoId);
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

    public static SpeakingImportResult Success(int videoId) => new(true, videoId: videoId);
    public static SpeakingImportResult Invalid(string errorMessage) => new(false, errorMessage);
    public static SpeakingImportResult UpgradeRequired(string errorMessage) => new(false, errorMessage, requiresUpgrade: true);
}
