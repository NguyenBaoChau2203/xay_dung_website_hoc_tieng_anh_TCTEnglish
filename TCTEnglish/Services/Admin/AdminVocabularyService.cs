using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Services.Sets;
using TCTVocabulary.Models;
using TCTVocabulary.Services.Vocabulary;

namespace TCTEnglish.Services.Admin;

/// <summary>
/// Admin rules for vocabulary sets and cards. Controllers stay thin and call this service.
/// System sets (owner system@tct.local) are public and editable but cannot be renamed, because the
/// seeder matches them by name; learner sets can only be viewed, made private and deleted.
/// </summary>
public sealed class AdminVocabularyService : IAdminVocabularyService
{
    public const int MaxSetNameLength = 255;
    public const int MaxSetDescriptionLength = 1000;
    public const int MaxTermLength = 255;
    public const int MaxDefinitionLength = 1000;
    public const int MaxPhoneticLength = 100;
    public const int MaxEmojiLength = 16;
    public const int MaxTopicLength = 100;
    public const int MaxExampleLength = 1000;
    public const int MaxMeaningEnLength = 400;
    public const int MaxImageUrlLength = 500;
    private const string SystemEmail = PersonalSetService.SystemOwnerEmail;
    private const string FallbackExportFolder = "Từ vựng hệ thống";

    private static readonly JsonSerializerOptions ExportOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly DbflashcardContext _db;
    private readonly IPersonalSetService _personalSets;
    private readonly ILogger<AdminVocabularyService> _logger;

    public AdminVocabularyService(DbflashcardContext db, IPersonalSetService personalSets, ILogger<AdminVocabularyService> logger)
    {
        _db = db;
        _personalSets = personalSets;
        _logger = logger;
    }

    // =====================================================================================
    // Set list and figures
    // =====================================================================================

    public async Task<AdminSetListResult> ListSetsAsync(AdminSetQuery query, CancellationToken ct = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var sets = _db.Sets.AsNoTracking().AsQueryable();

        sets = query.Source switch
        {
            "system" => sets.Where(s => s.Owner.Email == SystemEmail),
            "user" => sets.Where(s => s.Owner.Email != SystemEmail),
            _ => sets,
        };

        if (query.FolderId.HasValue)
        {
            var folderId = query.FolderId.Value;
            sets = sets.Where(s => s.FolderId == folderId);
        }

        sets = query.Visibility switch
        {
            "Public" => sets.Where(s => s.Visibility == SetVisibility.Public),
            "Link" => sets.Where(s => s.Visibility != SetVisibility.Public && s.ShareSlug != null),
            "Private" => sets.Where(s => s.Visibility != SetVisibility.Public && s.ShareSlug == null),
            _ => sets,
        };

        var text = AdminInputRules.NormalizeText(query.Q, 200).Value;
        if (text.Length > 0)
        {
            sets = sets.Where(s => s.SetName.Contains(text) || (s.Description != null && s.Description.Contains(text)));
        }

        sets = query.Sort switch
        {
            "name" => sets.OrderBy(s => s.SetName).ThenBy(s => s.SetId),
            "cards" => sets.OrderByDescending(s => s.Cards.Count).ThenBy(s => s.SetName).ThenBy(s => s.SetId),
            "views" => sets.OrderByDescending(s => s.ViewCount).ThenBy(s => s.SetName).ThenBy(s => s.SetId),
            _ => sets.OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.SetId),
        };

        var total = await sets.CountAsync(ct);
        var totalPages = AdminInputRules.TotalPages(total, pageSize);
        var page = AdminInputRules.ClampPage(query.Page, totalPages);

        var raw = await sets
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.SetId,
                s.SetName,
                s.Description,
                s.OwnerId,
                OwnerName = s.Owner.FullName,
                OwnerEmail = s.Owner.Email,
                s.FolderId,
                FolderName = s.Folder != null ? s.Folder.FolderName : null,
                s.Visibility,
                HasLink = s.ShareSlug != null,
                CardCount = s.Cards.Count,
                s.ViewCount,
                s.CreatedAt,
            })
            .ToListAsync(ct);

        // Distinct topics per set in a separate grouped query: MariaDB cannot resolve the outer set column
        // inside the derived table that a correlated DISTINCT-COUNT subquery produces.
        var pageIds = raw.Select(r => r.SetId).ToList();
        var topicCounts = pageIds.Count == 0
            ? new Dictionary<int, int>()
            : await _db.Cards.AsNoTracking()
                .Where(c => pageIds.Contains(c.SetId) && c.Topic != null && c.Topic != "")
                .GroupBy(c => c.SetId)
                .Select(g => new { SetId = g.Key, Topics = g.Select(c => c.Topic).Distinct().Count() })
                .ToDictionaryAsync(x => x.SetId, x => x.Topics, ct);

        var folders = await SystemFoldersAsync(ct);

        return new AdminSetListResult
        {
            Rows = raw.Select(r => new AdminSetRow
            {
                SetId = r.SetId,
                Name = r.SetName,
                Description = r.Description,
                OwnerId = r.OwnerId,
                OwnerName = string.IsNullOrWhiteSpace(r.OwnerName) ? r.OwnerEmail : r.OwnerName,
                OwnerEmail = r.OwnerEmail,
                IsSystem = string.Equals(r.OwnerEmail, SystemEmail, StringComparison.OrdinalIgnoreCase),
                FolderId = r.FolderId,
                FolderName = r.FolderName,
                Exposure = ExposureOf(r.Visibility, r.HasLink),
                CardCount = r.CardCount,
                TopicCount = topicCounts.GetValueOrDefault(r.SetId),
                ViewCount = r.ViewCount,
                CreatedAt = r.CreatedAt,
            }).ToList(),
            TotalItems = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            SystemFolders = folders,
        };
    }

    public async Task<AdminSetFigures> GetFiguresAsync(CancellationToken ct = default)
    {
        var systemSets = await _db.Sets.AsNoTracking().CountAsync(s => s.Owner.Email == SystemEmail, ct);
        var userSets = await _db.Sets.AsNoTracking().CountAsync(s => s.Owner.Email != SystemEmail, ct);
        var systemCards = await _db.Cards.AsNoTracking().CountAsync(c => c.Set.Owner.Email == SystemEmail, ct);
        var incomplete = await _db.Cards.AsNoTracking().CountAsync(c => c.Set.Owner.Email == SystemEmail
            && (c.Example == null || c.Example.Trim() == "" || c.Phonetic == null || c.Phonetic.Trim() == ""), ct);

        return new AdminSetFigures(systemSets, systemCards, userSets, incomplete);
    }

    // =====================================================================================
    // Set forms and writes
    // =====================================================================================

    public async Task<AdminSetForm?> GetSetFormAsync(int? setId, CancellationToken ct = default)
    {
        if (setId == null)
        {
            return new AdminSetForm
            {
                IsSystem = true,
                OwnerName = "TCT English (hệ thống)",
                Exposure = "Public",
                Folders = await SystemFoldersAsync(ct),
            };
        }

        var set = await _db.Sets.AsNoTracking()
            .Where(s => s.SetId == setId.Value)
            .Select(s => new
            {
                s.SetId,
                s.SetName,
                s.Description,
                s.FolderId,
                s.OwnerId,
                OwnerName = s.Owner.FullName,
                OwnerEmail = s.Owner.Email,
                s.Visibility,
                HasLink = s.ShareSlug != null,
            })
            .FirstOrDefaultAsync(ct);
        if (set == null)
        {
            return null;
        }

        var isSystem = IsSystemEmail(set.OwnerEmail);
        var folders = await _db.Folders.AsNoTracking()
            .Where(f => f.UserId == set.OwnerId)
            .OrderBy(f => f.FolderName)
            .Select(f => new AdminFolderOption(f.FolderId, f.FolderName))
            .ToListAsync(ct);

        return new AdminSetForm
        {
            SetId = set.SetId,
            Name = set.SetName,
            Description = set.Description,
            FolderId = set.FolderId,
            IsSystem = isSystem,
            OwnerName = isSystem ? "TCT English (hệ thống)" : (string.IsNullOrWhiteSpace(set.OwnerName) ? set.OwnerEmail : set.OwnerName),
            Exposure = ExposureOf(set.Visibility, set.HasLink),
            Folders = folders,
        };
    }

    public async Task<AdminResult<int>> CreateSystemSetAsync(AdminSetInput input, int adminId, CancellationToken ct = default)
    {
        var systemUserId = await GetSystemUserIdAsync(ct);
        if (systemUserId == null)
        {
            return AdminResult<int>.Invalid($"Chưa có tài khoản hệ thống ({SystemEmail}) để làm chủ bộ từ vựng.");
        }

        var errors = new Dictionary<string, string>();
        var name = CheckSetName(input.Name, errors);
        var description = CheckSetDescription(input.Description, errors);
        await CheckFolderAsync(input.FolderId, systemUserId.Value, errors, ct);

        if (!errors.ContainsKey("Name") && name.Length > 0)
        {
            var lowered = name.ToLower();
            var exists = await _db.Sets.AsNoTracking()
                .AnyAsync(s => s.OwnerId == systemUserId.Value && s.SetName.ToLower() == lowered, ct);
            if (exists)
            {
                errors["Name"] = "Đã có bộ từ vựng hệ thống trùng tên này.";
            }
        }

        if (errors.Count > 0)
        {
            return AdminResult<int>.Invalid("Hãy kiểm tra lại các trường được đánh dấu.", errors);
        }

        var now = DateTime.UtcNow;
        var set = new Set
        {
            SetName = name,
            Description = description,
            FolderId = input.FolderId,
            OwnerId = systemUserId.Value,
            Visibility = SetVisibility.Public,
            CreatedAt = now,
            UpdatedAt = now,
            ViewCount = 0,
        };
        _db.Sets.Add(set);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin {AdminId} created system set {SetId} ({Name})", adminId, set.SetId, name);
        return AdminResult<int>.Success(set.SetId);
    }

    public async Task<AdminResult> UpdateSetAsync(int setId, AdminSetInput input, int adminId, CancellationToken ct = default)
    {
        var set = await _db.Sets.Include(s => s.Owner).FirstOrDefaultAsync(s => s.SetId == setId, ct);
        if (set == null)
        {
            return AdminResult.NotFound();
        }

        if (!IsSystemEmail(set.Owner.Email))
        {
            return AdminResult.Forbidden(AdminVocabularyMessages.LearnerContentReadOnly);
        }

        var errors = new Dictionary<string, string>();
        var name = CheckSetName(input.Name, errors);
        var description = CheckSetDescription(input.Description, errors);
        await CheckFolderAsync(input.FolderId, set.OwnerId, errors, ct);

        if (!errors.ContainsKey("Name")
            && !string.Equals(name, AdminInputRules.NormalizeText(set.SetName, MaxSetNameLength).Value, StringComparison.Ordinal))
        {
            errors["Name"] = AdminVocabularyMessages.SystemRenameBlocked;
        }

        if (errors.Count > 0)
        {
            return AdminResult.Invalid("Hãy kiểm tra lại các trường được đánh dấu.", errors);
        }

        var folderChanged = set.FolderId != input.FolderId;
        set.Description = description;
        set.FolderId = input.FolderId;
        set.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin {AdminId} updated system set {SetId}", adminId, setId);
        return folderChanged
            ? AdminResult.Success(new[] { AdminVocabularyMessages.SystemFolderChangeWarning })
            : AdminResult.Success();
    }

    public async Task<AdminResult> SetVisibilityAsync(int setId, string visibility, int adminId, CancellationToken ct = default)
    {
        if (visibility != SetVisibility.Private && visibility != SetVisibility.Public)
        {
            return AdminResult.Invalid("Chế độ hiển thị không hợp lệ.");
        }

        var set = await _db.Sets.Include(s => s.Owner).FirstOrDefaultAsync(s => s.SetId == setId, ct);
        if (set == null)
        {
            return AdminResult.NotFound();
        }

        if (IsSystemEmail(set.Owner.Email))
        {
            return visibility == SetVisibility.Public
                ? AdminResult.Success()
                : AdminResult.Invalid(AdminVocabularyMessages.SystemAlwaysPublic);
        }

        if (visibility == SetVisibility.Public)
        {
            return AdminResult.Forbidden("Admin chỉ có thể đặt bộ thẻ của học viên về riêng tư.");
        }

        if (set.Visibility == SetVisibility.Private && set.ShareSlug == null)
        {
            return AdminResult.Success();
        }

        // The share link grants access whatever the visibility is, so private also means "link off".
        set.Visibility = SetVisibility.Private;
        set.ShareSlug = null;
        set.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin {AdminId} made learner set {SetId} of user {OwnerId} private", adminId, setId, set.OwnerId);
        return AdminResult.Success();
    }

    public async Task<AdminResult> DeleteSetAsync(int setId, string? confirmName, int adminId, CancellationToken ct = default)
    {
        var set = await _db.Sets.AsNoTracking()
            .Where(s => s.SetId == setId)
            .Select(s => new { s.SetId, s.SetName, s.OwnerId, CardCount = s.Cards.Count })
            .FirstOrDefaultAsync(ct);
        if (set == null)
        {
            return AdminResult.NotFound();
        }

        if (confirmName == null || !string.Equals(confirmName.Trim(), set.SetName.Trim(), StringComparison.Ordinal))
        {
            return AdminResult.Invalid(AdminVocabularyMessages.ConfirmNameMismatch);
        }

        var result = await _personalSets.DeleteSetAsync(setId, adminId, isAdmin: true, confirmName, ct);
        if (result.IsSuccess)
        {
            _logger.LogInformation("Admin {AdminId} deleted set {SetId} of owner {OwnerId} with {CardCount} cards", adminId, setId, set.OwnerId, set.CardCount);
            return AdminResult.Success();
        }

        return result.Status == TCTVocabulary.Services.OperationStatus.NotFound
            ? AdminResult.NotFound(result.ErrorMessage)
            : AdminResult.Invalid(result.ErrorMessage);
    }

    // =====================================================================================
    // Cards
    // =====================================================================================

    public async Task<AdminCardListResult?> ListCardsAsync(int setId, AdminCardQuery query, CancellationToken ct = default)
    {
        var head = await _db.Sets.AsNoTracking()
            .Where(s => s.SetId == setId)
            .Select(s => new
            {
                s.SetId,
                s.SetName,
                s.Description,
                s.OwnerId,
                OwnerName = s.Owner.FullName,
                OwnerEmail = s.Owner.Email,
                FolderName = s.Folder != null ? s.Folder.FolderName : null,
                s.Visibility,
                HasLink = s.ShareSlug != null,
            })
            .FirstOrDefaultAsync(ct);
        if (head == null)
        {
            return null;
        }

        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var cards = _db.Cards.AsNoTracking().Where(c => c.SetId == setId);

        var topic = AdminInputRules.NormalizeText(query.Topic, MaxTopicLength).Value;
        if (topic.Length > 0)
        {
            cards = cards.Where(c => c.Topic == topic);
        }

        var text = AdminInputRules.NormalizeText(query.Q, 200).Value;
        if (text.Length > 0)
        {
            cards = cards.Where(c => c.Term.Contains(text) || c.Definition.Contains(text));
        }

        cards = query.Quality switch
        {
            "missing-example" => cards.Where(c => c.Example == null || c.Example.Trim() == ""),
            "missing-phonetic" => cards.Where(c => c.Phonetic == null || c.Phonetic.Trim() == ""),
            "missing-picture" => cards.Where(c => (c.Emoji == null || c.Emoji.Trim() == "") && (c.ImageUrl == null || c.ImageUrl.Trim() == "")),
            "complete" => cards.Where(c => c.Example != null && c.Example.Trim() != ""
                && c.Phonetic != null && c.Phonetic.Trim() != ""
                && ((c.Emoji != null && c.Emoji.Trim() != "") || (c.ImageUrl != null && c.ImageUrl.Trim() != ""))),
            _ => cards,
        };

        var total = await cards.CountAsync(ct);
        var totalPages = AdminInputRules.TotalPages(total, pageSize);
        var page = AdminInputRules.ClampPage(query.Page, totalPages);

        var rows = await cards
            .OrderBy(c => c.CardId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new AdminCardRow
            {
                CardId = c.CardId,
                Term = c.Term,
                Definition = c.Definition,
                Phonetic = c.Phonetic,
                Topic = c.Topic,
                Example = c.Example,
                Emoji = c.Emoji,
                ImageUrl = c.ImageUrl,
                MissingExample = c.Example == null || c.Example.Trim() == "",
                MissingPhonetic = c.Phonetic == null || c.Phonetic.Trim() == "",
                MissingPicture = (c.Emoji == null || c.Emoji.Trim() == "") && (c.ImageUrl == null || c.ImageUrl.Trim() == ""),
            })
            .ToListAsync(ct);

        var all = _db.Cards.AsNoTracking().Where(c => c.SetId == setId);
        var lessonCards = await all.OrderBy(c => c.CardId).Select(c => new LessonCard(c.CardId, c.Topic)).ToListAsync(ct);
        var topics = await all.Where(c => c.Topic != null && c.Topic != "").Select(c => c.Topic!).Distinct().ToListAsync(ct);
        var incomplete = await all.CountAsync(c => c.Example == null || c.Example.Trim() == "" || c.Phonetic == null || c.Phonetic.Trim() == "", ct);
        var learners = await _db.LearningProgresses.AsNoTracking().Where(p => p.Card.SetId == setId).Select(p => p.UserId).Distinct().CountAsync(ct);

        var isSystem = IsSystemEmail(head.OwnerEmail);
        return new AdminCardListResult
        {
            SetId = head.SetId,
            SetName = head.SetName,
            Description = head.Description,
            IsSystem = isSystem,
            OwnerId = head.OwnerId,
            OwnerName = isSystem ? "TCT English (hệ thống)" : (string.IsNullOrWhiteSpace(head.OwnerName) ? head.OwnerEmail : head.OwnerName),
            FolderName = head.FolderName,
            Exposure = ExposureOf(head.Visibility, head.HasLink),
            Rows = rows,
            TotalItems = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Topics = topics.OrderBy(t => t, StringComparer.Ordinal).ToList(),
            TotalCards = lessonCards.Count,
            LessonCount = VocabularyLessonBuilder.Build(lessonCards).Count,
            MissingExampleOrPhonetic = incomplete,
            LearnerCount = learners,
        };
    }

    public async Task<AdminCardForm?> GetCardFormAsync(int? cardId, int? setId, CancellationToken ct = default)
    {
        if (cardId.HasValue)
        {
            var card = await _db.Cards.AsNoTracking()
                .Where(c => c.CardId == cardId.Value)
                .Select(c => new { Card = c, SetName = c.Set.SetName, OwnerEmail = c.Set.Owner.Email })
                .FirstOrDefaultAsync(ct);
            if (card == null)
            {
                return null;
            }

            return new AdminCardForm
            {
                CardId = card.Card.CardId,
                SetId = card.Card.SetId,
                SetName = card.SetName,
                IsSystem = IsSystemEmail(card.OwnerEmail),
                Input = new AdminCardInput
                {
                    Term = card.Card.Term,
                    Definition = card.Card.Definition,
                    Phonetic = card.Card.Phonetic,
                    PhoneticUk = card.Card.PhoneticUk,
                    PartsOfSpeech = card.Card.PartsOfSpeech,
                    Cefr = card.Card.Cefr,
                    Emoji = card.Card.Emoji,
                    ImageUrl = card.Card.ImageUrl,
                    Topic = card.Card.Topic,
                    Example = card.Card.Example,
                    ExampleTranslation = card.Card.ExampleTranslation,
                    MeaningEn = card.Card.MeaningEn,
                },
            };
        }

        if (!setId.HasValue)
        {
            return null;
        }

        var set = await _db.Sets.AsNoTracking()
            .Where(s => s.SetId == setId.Value)
            .Select(s => new { s.SetId, s.SetName, OwnerEmail = s.Owner.Email })
            .FirstOrDefaultAsync(ct);
        return set == null
            ? null
            : new AdminCardForm { SetId = set.SetId, SetName = set.SetName, IsSystem = IsSystemEmail(set.OwnerEmail) };
    }

    public async Task<AdminResult<int>> AddCardAsync(int setId, AdminCardInput input, int adminId, CancellationToken ct = default)
    {
        var set = await _db.Sets.AsNoTracking()
            .Where(s => s.SetId == setId)
            .Select(s => new { s.SetId, OwnerEmail = s.Owner.Email })
            .FirstOrDefaultAsync(ct);
        if (set == null)
        {
            return AdminResult<int>.NotFound();
        }

        if (!IsSystemEmail(set.OwnerEmail))
        {
            return AdminResult<int>.Forbidden(AdminVocabularyMessages.LearnerContentReadOnly);
        }

        var errors = new Dictionary<string, string>();
        var clean = CheckCard(input, errors);
        if (errors.Count > 0)
        {
            return AdminResult<int>.Invalid("Hãy kiểm tra lại các trường được đánh dấu.", errors);
        }

        var now = DateTime.UtcNow;
        var card = new Card { SetId = setId, CreatedAt = now, UpdatedAt = now };
        Apply(card, clean);
        _db.Cards.Add(card);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin {AdminId} added card {CardId} to system set {SetId}", adminId, card.CardId, setId);
        return AdminResult<int>.Success(card.CardId);
    }

    public async Task<AdminResult<int>> UpdateCardAsync(int cardId, AdminCardInput input, int adminId, CancellationToken ct = default)
    {
        var card = await _db.Cards.Include(c => c.Set).ThenInclude(s => s.Owner).FirstOrDefaultAsync(c => c.CardId == cardId, ct);
        if (card == null)
        {
            return AdminResult<int>.NotFound();
        }

        if (!IsSystemEmail(card.Set.Owner.Email))
        {
            return AdminResult<int>.Forbidden(AdminVocabularyMessages.LearnerContentReadOnly);
        }

        var errors = new Dictionary<string, string>();
        var clean = CheckCard(input, errors);
        if (errors.Count > 0)
        {
            return AdminResult<int>.Invalid("Hãy kiểm tra lại các trường được đánh dấu.", errors);
        }

        Apply(card, clean);
        card.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Admin {AdminId} updated card {CardId} of system set {SetId}", adminId, cardId, card.SetId);
        return AdminResult<int>.Success(card.SetId);
    }

    public async Task<AdminResult<int>> DeleteCardAsync(int cardId, int adminId, CancellationToken ct = default)
    {
        var card = await _db.Cards.AsNoTracking()
            .Where(c => c.CardId == cardId)
            .Select(c => new { c.CardId, c.SetId, OwnerEmail = c.Set.Owner.Email })
            .FirstOrDefaultAsync(ct);
        if (card == null)
        {
            return AdminResult<int>.NotFound();
        }

        if (!IsSystemEmail(card.OwnerEmail))
        {
            return AdminResult<int>.Forbidden(AdminVocabularyMessages.LearnerContentReadOnly);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.LearningProgresses.Where(p => p.CardId == cardId).ExecuteDeleteAsync(ct);
        await _db.Cards.Where(c => c.CardId == cardId).ExecuteDeleteAsync(ct);
        await _db.Sets.Where(s => s.SetId == card.SetId).ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation("Admin {AdminId} deleted card {CardId} of system set {SetId}", adminId, cardId, card.SetId);
        return AdminResult<int>.Success(card.SetId);
    }

    // =====================================================================================
    // Export for system-vocabulary.json
    // =====================================================================================

    public async Task<SystemVocabularyExportResult?> ExportSystemSetAsync(int setId, CancellationToken ct = default)
    {
        var set = await _db.Sets.AsNoTracking()
            .Where(s => s.SetId == setId && s.Owner.Email == SystemEmail)
            .Select(s => new
            {
                s.SetName,
                s.Description,
                FolderName = s.Folder != null ? s.Folder.FolderName : null,
            })
            .FirstOrDefaultAsync(ct);
        if (set == null)
        {
            return null;
        }

        var cards = await _db.Cards.AsNoTracking()
            .Where(c => c.SetId == setId)
            .OrderBy(c => c.CardId)
            .Select(c => new ExportCard
            {
                Term = c.Term,
                Definition = c.Definition,
                Topic = c.Topic,
                Phonetic = c.Phonetic,
                Example = c.Example,
                ExampleTranslation = c.ExampleTranslation,
                ImageUrl = c.ImageUrl,
            })
            .ToListAsync(ct);

        var folders = new[]
        {
            new ExportFolder
            {
                FolderName = string.IsNullOrWhiteSpace(set.FolderName) ? FallbackExportFolder : set.FolderName,
                Sets = new[] { new ExportSet { SetName = set.SetName, Description = set.Description, Cards = cards } },
            },
        };

        return new SystemVocabularyExportResult
        {
            FileName = $"system-vocabulary-{Slugify(set.SetName)}.json",
            Json = JsonSerializer.Serialize(folders, ExportOptions),
        };
    }

    // Property order mirrors wwwroot/data/system-vocabulary.json exactly.
    private sealed class ExportFolder
    {
        public string FolderName { get; set; } = "";
        public IReadOnlyList<ExportSet> Sets { get; set; } = Array.Empty<ExportSet>();
    }

    private sealed class ExportSet
    {
        public string SetName { get; set; } = "";
        public string? Description { get; set; }
        public IReadOnlyList<ExportCard> Cards { get; set; } = Array.Empty<ExportCard>();
    }

    private sealed class ExportCard
    {
        public string Term { get; set; } = "";
        public string Definition { get; set; } = "";
        public string? Topic { get; set; }
        public string? Phonetic { get; set; }
        public string? Example { get; set; }
        public string? ExampleTranslation { get; set; }
        public string? ImageUrl { get; set; }
    }

    // =====================================================================================
    // Validation helpers
    // =====================================================================================

    private sealed record CleanCard(
        string Term, string Definition, string? Phonetic, string? PhoneticUk, string? PartsOfSpeech, string? Cefr,
        string? Emoji, string? ImageUrl, string? Topic, string? Example, string? ExampleTranslation, string? MeaningEn);

    private static CleanCard CheckCard(AdminCardInput input, Dictionary<string, string> errors)
    {
        string Required(string? raw, string field, int max, string label, bool singleLine = true)
        {
            var check = AdminInputRules.NormalizeText(raw, max, singleLine);
            if (check.IsEmpty)
            {
                errors[field] = $"Hãy nhập {label}.";
            }
            else if (check.TooLong)
            {
                errors[field] = $"{Capitalize(label)} tối đa {max} ký tự.";
            }

            return check.Value;
        }

        string? Optional(string? raw, string field, int max, string label, bool singleLine = true)
        {
            var check = AdminInputRules.NormalizeText(raw, max, singleLine);
            if (check.TooLong)
            {
                errors[field] = $"{Capitalize(label)} tối đa {max} ký tự.";
            }

            return check.IsEmpty ? null : check.Value;
        }

        var term = Required(input.Term, "Term", MaxTermLength, "từ");
        var definition = Required(input.Definition, "Definition", MaxDefinitionLength, "nghĩa tiếng Việt");
        var phonetic = Optional(input.Phonetic, "Phonetic", MaxPhoneticLength, "phiên âm (US)");
        var phoneticUk = Optional(input.PhoneticUk, "PhoneticUk", MaxPhoneticLength, "phiên âm (UK)");
        var emoji = Optional(input.Emoji, "Emoji", MaxEmojiLength, "emoji");
        var topic = Optional(input.Topic, "Topic", MaxTopicLength, "chủ đề");
        var example = Optional(input.Example, "Example", MaxExampleLength, "ví dụ", singleLine: false);
        var exampleTranslation = Optional(input.ExampleTranslation, "ExampleTranslation", MaxExampleLength, "bản dịch ví dụ", singleLine: false);
        var meaningEn = Optional(input.MeaningEn, "MeaningEn", MaxMeaningEnLength, "nghĩa tiếng Anh");

        string? cefr = null;
        var cefrCheck = AdminInputRules.NormalizeText(input.Cefr, 10).Value.ToUpperInvariant();
        if (cefrCheck.Length > 0)
        {
            if (AdminInputRules.IsCefrLevel(cefrCheck))
            {
                cefr = cefrCheck;
            }
            else
            {
                errors["Cefr"] = "Cấp độ CEFR phải là A1, A2, B1, B2, C1 hoặc C2.";
            }
        }

        string? parts = null;
        var rawParts = AdminInputRules.NormalizeText(input.PartsOfSpeech, 200).Value;
        if (rawParts.Length > 0)
        {
            var tokens = rawParts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToLowerInvariant()).ToList();
            if (tokens.Any(t => !CardPartsOfSpeech.IsValid(t)))
            {
                errors["PartsOfSpeech"] = "Từ loại không hợp lệ. Dùng noun, verb, adj, adv, pron, prep, conj…";
            }
            else
            {
                parts = CardPartsOfSpeech.Join(tokens);
            }
        }

        string? image = null;
        var rawImage = input.ImageUrl?.Trim() ?? "";
        if (rawImage.Length > 0)
        {
            if (rawImage.Length > MaxImageUrlLength)
            {
                errors["ImageUrl"] = $"Liên kết ảnh tối đa {MaxImageUrlLength} ký tự.";
            }
            else if (!AdminInputRules.IsSafeImageUrl(rawImage))
            {
                errors["ImageUrl"] = "Ảnh phải là liên kết https://… hoặc đường dẫn nội bộ bắt đầu bằng /.";
            }
            else
            {
                image = rawImage;
            }
        }

        return new CleanCard(term, definition, phonetic, phoneticUk, parts, cefr, emoji, image, topic, example, exampleTranslation, meaningEn);
    }

    private static void Apply(Card card, CleanCard clean)
    {
        card.Term = clean.Term;
        card.Definition = clean.Definition;
        card.Phonetic = clean.Phonetic;
        card.PhoneticUk = clean.PhoneticUk;
        card.PartsOfSpeech = clean.PartsOfSpeech;
        card.Cefr = clean.Cefr;
        card.Emoji = clean.Emoji;
        card.ImageUrl = clean.ImageUrl;
        card.Topic = clean.Topic;
        card.Example = clean.Example;
        card.ExampleTranslation = clean.ExampleTranslation;
        card.MeaningEn = clean.MeaningEn;
    }

    private static string CheckSetName(string? raw, Dictionary<string, string> errors)
    {
        var check = AdminInputRules.NormalizeText(raw, MaxSetNameLength);
        if (check.IsEmpty)
        {
            errors["Name"] = "Hãy nhập tên bộ từ vựng.";
        }
        else if (check.TooLong)
        {
            errors["Name"] = $"Tên bộ từ vựng tối đa {MaxSetNameLength} ký tự.";
        }

        return check.Value;
    }

    private static string? CheckSetDescription(string? raw, Dictionary<string, string> errors)
    {
        var check = AdminInputRules.NormalizeText(raw, MaxSetDescriptionLength);
        if (check.TooLong)
        {
            errors["Description"] = $"Mô tả tối đa {MaxSetDescriptionLength} ký tự.";
        }

        return check.IsEmpty ? null : check.Value;
    }

    /// <summary>S1: a set may only sit in a folder owned by the set's owner, because folder sharing grants access to everything inside.</summary>
    private async Task CheckFolderAsync(int? folderId, int ownerId, Dictionary<string, string> errors, CancellationToken ct)
    {
        if (!folderId.HasValue)
        {
            return;
        }

        var folderOwner = await _db.Folders.AsNoTracking()
            .Where(f => f.FolderId == folderId.Value)
            .Select(f => (int?)f.UserId)
            .FirstOrDefaultAsync(ct);
        if (folderOwner == null)
        {
            errors["FolderId"] = "Không tìm thấy thư mục.";
        }
        else if (folderOwner.Value != ownerId)
        {
            errors["FolderId"] = AdminVocabularyMessages.FolderNotOwned;
        }
    }

    // =====================================================================================
    // Small helpers
    // =====================================================================================

    private static bool IsSystemEmail(string? email) => string.Equals(email, SystemEmail, StringComparison.OrdinalIgnoreCase);

    private static string ExposureOf(string? visibility, bool hasLink)
        => visibility == SetVisibility.Public ? "Public" : (hasLink ? "Link" : "Private");

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private Task<int?> GetSystemUserIdAsync(CancellationToken ct)
        => _db.Users.AsNoTracking().Where(u => u.Email == SystemEmail).Select(u => (int?)u.UserId).FirstOrDefaultAsync(ct);

    private async Task<IReadOnlyList<AdminFolderOption>> SystemFoldersAsync(CancellationToken ct)
        => await _db.Folders.AsNoTracking()
            .Where(f => f.User.Email == SystemEmail)
            .OrderBy(f => f.FolderName)
            .Select(f => new AdminFolderOption(f.FolderId, f.FolderName))
            .ToListAsync(ct);

    /// <summary>"Màu sắc &amp; Hình dạng" becomes "mau-sac-hinh-dang" for the download name.</summary>
    private static string Slugify(string text)
    {
        var decomposed = text.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        var lastDash = true;
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (ch < 128 && char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastDash = false;
            }
            else if (!lastDash)
            {
                sb.Append('-');
                lastDash = true;
            }
        }

        var slug = sb.ToString().Trim('-');
        return slug.Length == 0 ? "bo-tu-vung" : slug;
    }
}
