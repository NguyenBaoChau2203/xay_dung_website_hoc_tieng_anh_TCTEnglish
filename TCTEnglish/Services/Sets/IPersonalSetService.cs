using TCTEnglish.ViewModels.Sets;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Sets;

/// <summary>Personal set workspace: list, detail, cards, sharing and cloning. All access rules live here.</summary>
public interface IPersonalSetService
{
    // ----- Access -----
    Task<bool> CanViewSetAsync(int setId, int userId, bool isAdmin, CancellationToken ct = default);
    Task<bool> CanEditSetAsync(int setId, int userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Subset of <paramref name="setIds"/> the user may view (one query; same rule as <see cref="CanViewSetAsync"/>).</summary>
    Task<HashSet<int>> GetViewableSetIdsAsync(IReadOnlyCollection<int> setIds, int userId, bool isAdmin, CancellationToken ct = default);

    // ----- Read side -----
    Task<SetIndexViewModel> GetSetIndexAsync(int userId, bool isAdmin, string? filter, CancellationToken ct = default);
    Task<SetDetailViewModel?> GetSetDetailAsync(int setId, int userId, bool isAdmin, CancellationToken ct = default);
    Task<SetInfoFormViewModel?> GetSetInfoFormAsync(int setId, int userId, bool isAdmin, CancellationToken ct = default);
    Task<SetInfoFormViewModel?> GetNewSetFormAsync(int userId, bool isAdmin, int? folderId, CancellationToken ct = default);
    Task<CardPageDto?> SearchCardsAsync(int setId, CardSearchQuery query, int userId, bool isAdmin, CancellationToken ct = default);
    Task<CardDetailViewModel?> GetCardDetailAsync(int cardId, int userId, bool isAdmin, CancellationToken ct = default);
    Task<CardFormViewModel?> GetCardFormAsync(int cardId, int userId, bool isAdmin, CancellationToken ct = default);
    Task<IReadOnlyList<DuplicateHitDto>> FindDuplicatesAsync(string term, int userId, int? excludeCardId, CancellationToken ct = default);
    Task<SharedSetViewModel?> GetSharedSetAsync(string slug, int? userId, CancellationToken ct = default);
    Task<SetExportFile?> ExportCsvAsync(int setId, int userId, bool isAdmin, CancellationToken ct = default);
    Task<CardImportContextViewModel?> GetImportContextAsync(int setId, int userId, bool isAdmin, CancellationToken ct = default);
    Task<CardComposeViewModel?> GetComposeContextAsync(int setId, int userId, bool isAdmin, CancellationToken ct = default);

    // ----- Write side -----
    Task<OperationResult<int>> CreateSetAsync(SetInfoFormViewModel input, int userId, bool isAdmin, CancellationToken ct = default);
    Task<OperationResult> UpdateSetInfoAsync(int setId, SetInfoFormViewModel input, int userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Deletes progress, cards, subscriptions and the set in one transaction. When <paramref name="confirmName"/> is not null it must match the set name.</summary>
    Task<OperationResult> DeleteSetAsync(int setId, int userId, bool isAdmin, string? confirmName = null, CancellationToken ct = default);

    Task<OperationResult<CardBatchResultDto>> CreateCardsBatchAsync(int setId, IReadOnlyList<CardInputDto> cards, int userId, bool isAdmin, CancellationToken ct = default);
    Task<OperationResult<CardBulkResultDto>> BulkCardsAsync(int setId, CardBulkRequest request, int userId, bool isAdmin, CancellationToken ct = default);
    Task<OperationResult> UpdateCardAsync(int cardId, CardInputDto input, int userId, bool isAdmin, CancellationToken ct = default);
    Task<OperationResult<int>> DeleteCardAsync(int cardId, int userId, bool isAdmin, CancellationToken ct = default);
    Task<OperationResult<bool>> SetSuspendedAsync(int cardId, bool suspended, int userId, bool isAdmin, CancellationToken ct = default);

    Task<OperationResult<string?>> SetShareLinkAsync(int setId, bool enabled, bool regenerate, int userId, bool isAdmin, CancellationToken ct = default);
    Task<OperationResult<int>> SubscribeAsync(int? setId, string? slug, int userId, bool isAdmin, CancellationToken ct = default);
    Task<OperationResult> UnsubscribeAsync(int setId, int userId, CancellationToken ct = default);
    Task<OperationResult<int>> CloneSetAsync(int? setId, string? slug, int userId, bool isAdmin, CancellationToken ct = default);
    Task<OperationResult<SampleSetsResult>> AddSampleSetsAsync(int userId, CancellationToken ct = default);

    // ----- Folders (flat folders of the personal-set workspace) -----

    /// <summary>The user's own folders and the folders they saved, ordered by name, with counts (one grouped query per kind, no per-folder queries).</summary>
    Task<IReadOnlyList<SetFolderTileViewModel>> ListFoldersAsync(int userId, CancellationToken ct = default);

    /// <summary>Null (a 404) unless the viewer owns the folder, saved it, belongs to a class it is shared with, or is an admin.</summary>
    Task<SetFolderPageViewModel?> GetFolderPageAsync(int folderId, int userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Trimmed name, 1 to 100 characters, unique per owner ignoring case. Returns the new folder id.</summary>
    Task<OperationResult<int>> CreateFolderAsync(int userId, string? name, CancellationToken ct = default);

    Task<OperationResult> RenameFolderAsync(int folderId, int userId, bool isAdmin, string? name, CancellationToken ct = default);

    /// <summary>
    /// Keeps the sets (their folder becomes none) and removes the folder with its saves and class links in one transaction.
    /// A folder that still has child folders is refused with the field-error key <c>ChildFolders</c>.
    /// </summary>
    Task<OperationResult> DeleteFolderAsync(int folderId, int userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Moves a set into a folder (null takes it out). The value is the target folder name, or null when the set left its folder.</summary>
    Task<OperationResult<string?>> MoveSetToFolderAsync(int setId, int? folderId, int userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Removes the caller's save of someone else's folder; NotFound when nothing was saved.</summary>
    Task<OperationResult> UnsaveFolderAsync(int folderId, int userId, CancellationToken ct = default);

    // ----- Practice (read-only word selection for /Practice) -----

    Task<PracticeHubViewModel> GetPracticeHubAsync(int? setId, int userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Null when the requested set is not viewable (or the mode is unknown).</summary>
    Task<PracticeSessionViewModel?> GetPracticeAsync(string mode, PracticeQuery query, int userId, bool isAdmin, CancellationToken ct = default);

    Task<HardWordsViewModel> GetHardWordsAsync(int? setId, int userId, bool isAdmin, CancellationToken ct = default);

    // ----- Legacy contract helpers -----

    /// <summary>Legacy POST /Home/EditSet: non-destructive upsert that keeps progress and card details.</summary>
    Task<OperationResult> ApplyLegacyEditAsync(int setId, string name, string? description, IReadOnlyList<LegacyCardRow> rows, int userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Downloads a Pixabay image (https, pixabay hosts only, size/type bounded) to wwwroot/images/cards. Returns null on any failure.</summary>
    Task<string?> ImportExternalImageAsync(string externalUrl, CancellationToken ct = default);
}

public sealed record SampleSetsResult(int Sets, int Cards);

/// <summary>One row of the legacy set editor form (term, definition and optional image changes).</summary>
public sealed class LegacyCardRow
{
    public string Term { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;

    /// <summary>Existing image URL carried by the form, or an external URL to import.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Public URL of a newly uploaded file, already saved by the caller.</summary>
    public string? UploadedImageUrl { get; set; }
}
