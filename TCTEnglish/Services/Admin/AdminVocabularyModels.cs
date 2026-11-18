namespace TCTEnglish.Services.Admin;

public static class AdminVocabularyMessages
{
    public const string LearnerContentReadOnly = "Không sửa nội dung bộ thẻ của học viên.";
    public const string SystemRenameBlocked = "Không đổi tên bộ từ vựng hệ thống: tệp dữ liệu đồng bộ theo tên. Hãy tạo bộ mới nếu cần.";
    public const string FolderNotOwned = "Thư mục không thuộc chủ sở hữu của bộ thẻ.";
    public const string SystemAlwaysPublic = "Bộ hệ thống luôn công khai.";
    public const string ConfirmNameMismatch = "Tên xác nhận không khớp.";
    public const string SystemFolderChangeWarning = "Đã đổi thư mục. Tệp system-vocabulary.json đồng bộ theo cả tên bộ và thư mục: hãy sửa tệp tương ứng để không bị tạo bản sao.";
}

/// <summary>Filters of the set list. Source is "system", "user" or anything else for all sets.</summary>
public sealed record AdminSetQuery(
    string? Source = null,
    int? FolderId = null,
    string? Visibility = null,
    string? Q = null,
    string? Sort = null,
    int Page = 1,
    int PageSize = 20);

public sealed class AdminSetRow
{
    public int SetId { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public int OwnerId { get; init; }
    public string OwnerName { get; init; } = "";
    public string OwnerEmail { get; init; } = "";
    public bool IsSystem { get; init; }
    public int? FolderId { get; init; }
    public string? FolderName { get; init; }
    /// <summary>"Public", "Link" (private with a share link) or "Private".</summary>
    public string Exposure { get; init; } = "Private";
    public int CardCount { get; init; }
    public int TopicCount { get; init; }
    public int ViewCount { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public sealed record AdminFolderOption(int FolderId, string Name);

public sealed class AdminSetListResult
{
    public IReadOnlyList<AdminSetRow> Rows { get; init; } = Array.Empty<AdminSetRow>();
    public int TotalItems { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int TotalPages { get; init; }
    public IReadOnlyList<AdminFolderOption> SystemFolders { get; init; } = Array.Empty<AdminFolderOption>();
}

public sealed record AdminSetFigures(int SystemSets, int SystemCards, int UserSets, int SystemCardsMissingExampleOrPhonetic);

public sealed class AdminSetInput
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? FolderId { get; set; }
}

public sealed class AdminSetForm
{
    public int? SetId { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public int? FolderId { get; init; }
    public bool IsSystem { get; init; } = true;
    public string OwnerName { get; init; } = "";
    public string Exposure { get; init; } = "Public";
    public IReadOnlyList<AdminFolderOption> Folders { get; init; } = Array.Empty<AdminFolderOption>();
}

/// <summary>Filters of the cards page. Quality is "", "complete", "missing-example", "missing-phonetic" or "missing-picture".</summary>
public sealed record AdminCardQuery(string? Q = null, string? Topic = null, string? Quality = null, int Page = 1, int PageSize = 50);

public sealed class AdminCardRow
{
    public int CardId { get; init; }
    public string Term { get; init; } = "";
    public string Definition { get; init; } = "";
    public string? Phonetic { get; init; }
    public string? Topic { get; init; }
    public string? Example { get; init; }
    public string? Emoji { get; init; }
    public string? ImageUrl { get; init; }
    public bool MissingExample { get; init; }
    public bool MissingPhonetic { get; init; }
    public bool MissingPicture { get; init; }
    public bool IsComplete => !MissingExample && !MissingPhonetic && !MissingPicture;
}

public sealed class AdminCardListResult
{
    public int SetId { get; init; }
    public string SetName { get; init; } = "";
    public string? Description { get; init; }
    public bool IsSystem { get; init; }
    public int OwnerId { get; init; }
    public string OwnerName { get; init; } = "";
    public string? FolderName { get; init; }
    public string Exposure { get; init; } = "Private";
    public IReadOnlyList<AdminCardRow> Rows { get; init; } = Array.Empty<AdminCardRow>();
    public int TotalItems { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public int TotalPages { get; init; }
    public IReadOnlyList<string> Topics { get; init; } = Array.Empty<string>();
    /// <summary>Cards of the whole set (not just the filtered page).</summary>
    public int TotalCards { get; init; }
    public int LessonCount { get; init; }
    public int MissingExampleOrPhonetic { get; init; }
    /// <summary>Distinct learners who have progress on at least one card of the set.</summary>
    public int LearnerCount { get; init; }
}

public sealed class AdminCardInput
{
    public string? Term { get; set; }
    public string? Definition { get; set; }
    public string? Phonetic { get; set; }
    public string? PhoneticUk { get; set; }
    public string? PartsOfSpeech { get; set; }
    public string? Cefr { get; set; }
    public string? Emoji { get; set; }
    public string? ImageUrl { get; set; }
    public string? Topic { get; set; }
    public string? Example { get; set; }
    public string? ExampleTranslation { get; set; }
    public string? MeaningEn { get; set; }
}

public sealed class AdminCardForm
{
    public int? CardId { get; init; }
    public int SetId { get; init; }
    public string SetName { get; init; } = "";
    public bool IsSystem { get; init; }
    public AdminCardInput Input { get; init; } = new();
}

public sealed class SystemVocabularyExportResult
{
    public string FileName { get; init; } = "system-vocabulary.json";
    public string Json { get; init; } = "[]";
}

public interface IAdminVocabularyService
{
    Task<AdminSetListResult> ListSetsAsync(AdminSetQuery query, CancellationToken ct = default);
    Task<AdminSetFigures> GetFiguresAsync(CancellationToken ct = default);
    Task<AdminSetForm?> GetSetFormAsync(int? setId, CancellationToken ct = default);
    Task<AdminResult<int>> CreateSystemSetAsync(AdminSetInput input, int adminId, CancellationToken ct = default);
    Task<AdminResult> UpdateSetAsync(int setId, AdminSetInput input, int adminId, CancellationToken ct = default);
    /// <summary>Learner sets can only be made private (which also turns the share link off); system sets are always public.</summary>
    Task<AdminResult> SetVisibilityAsync(int setId, string visibility, int adminId, CancellationToken ct = default);
    Task<AdminResult> DeleteSetAsync(int setId, string? confirmName, int adminId, CancellationToken ct = default);
    Task<AdminCardListResult?> ListCardsAsync(int setId, AdminCardQuery query, CancellationToken ct = default);
    Task<AdminCardForm?> GetCardFormAsync(int? cardId, int? setId, CancellationToken ct = default);
    Task<AdminResult<int>> AddCardAsync(int setId, AdminCardInput input, int adminId, CancellationToken ct = default);
    /// <summary>Returns the id of the card's real set, so the caller can redirect to it.</summary>
    Task<AdminResult<int>> UpdateCardAsync(int cardId, AdminCardInput input, int adminId, CancellationToken ct = default);
    /// <summary>Returns the id of the card's real set.</summary>
    Task<AdminResult<int>> DeleteCardAsync(int cardId, int adminId, CancellationToken ct = default);
    Task<SystemVocabularyExportResult?> ExportSystemSetAsync(int setId, CancellationToken ct = default);
}
