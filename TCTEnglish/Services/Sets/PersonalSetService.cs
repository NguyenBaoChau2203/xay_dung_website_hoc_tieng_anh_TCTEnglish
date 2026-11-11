using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TCTEnglish.Models;
using TCTEnglish.ViewModels.Sets;
using TCTVocabulary.Models;
using TCTVocabulary.Services;

namespace TCTEnglish.Services.Sets;

public sealed partial class PersonalSetService : IPersonalSetService
{
    public const int MaxOwnedSets = 200;
    public const int PageSize = 50;
    public const string SystemOwnerEmail = "system@tct.local";

    private readonly DbflashcardContext _db;
    private readonly TimeProvider _time;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PersonalSetService> _logger;

    public PersonalSetService(
        DbflashcardContext db,
        TimeProvider time,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment environment,
        ILogger<PersonalSetService> logger)
    {
        _db = db;
        _time = time;
        _httpClientFactory = httpClientFactory;
        _environment = environment;
        _logger = logger;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    [GeneratedRegex("^[a-z0-9]{10}$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    public static bool IsValidSlug(string? slug) => slug != null && SlugPattern().IsMatch(slug);

    // =====================================================================================
    // Access rules (one implementation, every rule is an IQueryable<Set> filter)
    // =====================================================================================

    /// <summary>Owner or admin.</summary>
    internal IQueryable<Set> ManageableSets(int userId, bool isAdmin)
        => isAdmin ? _db.Sets : _db.Sets.Where(s => s.OwnerId == userId);

    /// <summary>
    /// Manage, public, subscribed, or reachable through a folder the user owns/saved or a class they own/belong to.
    /// The folder/saved/class branch is a superset of LearnerCardAccess.Authorized so chat citations keep opening.
    /// </summary>
    internal IQueryable<Set> ViewableSets(int userId, bool isAdmin)
    {
        if (isAdmin)
        {
            return _db.Sets;
        }

        return _db.Sets.Where(s =>
            s.OwnerId == userId
            || s.Visibility == SetVisibility.Public
            || _db.SetSubscriptions.Any(x => x.SetId == s.SetId && x.UserId == userId)
            || (s.FolderId != null
                && (_db.Folders.Any(f => f.FolderId == s.FolderId && f.UserId == userId)
                    || _db.SavedFolders.Any(saved => saved.UserId == userId && saved.FolderId == s.FolderId)
                    || _db.ClassFolders.Any(link => link.FolderId == s.FolderId
                        && (_db.Classes.Any(c => c.ClassId == link.ClassId && c.OwnerId == userId)
                            || _db.ClassMembers.Any(m => m.ClassId == link.ClassId && m.UserId == userId))))));
    }

    /// <summary>Sets reachable only through a saved folder or a class the user owns/belongs to.</summary>
    private IQueryable<Set> FolderSharedSets(int userId)
        => _db.Sets.Where(s => s.FolderId != null
            && (_db.SavedFolders.Any(saved => saved.UserId == userId && saved.FolderId == s.FolderId)
                || _db.ClassFolders.Any(link => link.FolderId == s.FolderId
                    && (_db.Classes.Any(c => c.ClassId == link.ClassId && c.OwnerId == userId)
                        || _db.ClassMembers.Any(m => m.ClassId == link.ClassId && m.UserId == userId)))));

    public Task<bool> CanViewSetAsync(int setId, int userId, bool isAdmin, CancellationToken ct = default)
        => ViewableSets(userId, isAdmin).AnyAsync(s => s.SetId == setId, ct);

    public async Task<HashSet<int>> GetViewableSetIdsAsync(IReadOnlyCollection<int> setIds, int userId, bool isAdmin, CancellationToken ct = default)
    {
        if (setIds.Count == 0)
        {
            return new HashSet<int>();
        }

        var ids = await ViewableSets(userId, isAdmin).AsNoTracking()
            .Where(s => setIds.Contains(s.SetId))
            .Select(s => s.SetId)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    public Task<bool> CanEditSetAsync(int setId, int userId, bool isAdmin, CancellationToken ct = default)
        => ManageableSets(userId, isAdmin).AnyAsync(s => s.SetId == setId, ct);

    // =====================================================================================
    // Set list
    // =====================================================================================

    public async Task<SetIndexViewModel> GetSetIndexAsync(int userId, bool isAdmin, string? filter, CancellationToken ct = default)
    {
        var normalized = filter is "own" or "subscribed" or "shared" ? filter : null;
        var model = new SetIndexViewModel { Filter = normalized };

        var subscribedIds = _db.SetSubscriptions.Where(x => x.UserId == userId).Select(x => x.SetId);

        var own = normalized is null or "own"
            ? await LoadTilesAsync(_db.Sets.Where(s => s.OwnerId == userId), MaxOwnedSets, "own", ct)
            : new List<SetCardTileViewModel>();

        var subscribed = normalized is null or "subscribed"
            ? await LoadTilesAsync(_db.Sets.Where(s => s.OwnerId != userId && subscribedIds.Contains(s.SetId)), 200, "subscribed", ct)
            : new List<SetCardTileViewModel>();

        var shared = normalized is null or "shared"
            ? await LoadTilesAsync(
                FolderSharedSets(userId).Where(s => s.OwnerId != userId && !subscribedIds.Contains(s.SetId)), 60, "shared", ct)
            : new List<SetCardTileViewModel>();

        var ids = own.Concat(subscribed).Concat(shared).Select(t => t.SetId).Distinct().ToList();
        var stats = await LoadProgressStatsAsync(userId, ids, ct);
        foreach (var tile in own.Concat(subscribed).Concat(shared))
        {
            if (stats.TryGetValue(tile.SetId, out var stat))
            {
                tile.DueCount = stat.Due;
                tile.NewCount = Math.Max(0, tile.WordCount - stat.Started);
            }
            else
            {
                tile.NewCount = tile.WordCount;
            }

            tile.ProgressPercent = tile.WordCount == 0 ? 0 : (int)Math.Round(100.0 * (tile.WordCount - tile.NewCount) / tile.WordCount);
        }

        model.Own = own;
        model.Subscribed = subscribed;
        model.Shared = shared;
        return model;
    }

    private async Task<List<SetCardTileViewModel>> LoadTilesAsync(IQueryable<Set> query, int take, string relation, CancellationToken ct)
    {
        var rows = await query
            .AsNoTracking()
            .OrderByDescending(s => s.SetId)
            .Take(take)
            .Select(s => new
            {
                s.SetId,
                s.SetName,
                s.Visibility,
                HasShareLink = s.ShareSlug != null,
                OwnerName = s.Owner.FullName,
                WordCount = s.Cards.Count,
                SampleTerm = s.Cards.OrderBy(c => c.CardId).Select(c => c.Term).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return rows.Select(r => new SetCardTileViewModel
        {
            SetId = r.SetId,
            SetName = r.SetName,
            OwnerName = relation == "own" ? null : (string.IsNullOrWhiteSpace(r.OwnerName) ? "Người dùng TCT" : r.OwnerName),
            Visibility = r.Visibility,
            HasShareLink = r.HasShareLink,
            CoverHue = SetCoverColor.For(r.SetId),
            SampleTerm = SampleOf(r.SampleTerm, r.SetName),
            WordCount = r.WordCount,
            Relation = relation,
        }).ToList();
    }

    private static string SampleOf(string? term, string setName)
    {
        var source = string.IsNullOrWhiteSpace(term) ? setName : term;
        source = source.Trim();
        if (source.Length == 0)
        {
            return "?";
        }

        return source.Length <= 24 ? source : source[..24];
    }

    private sealed record ProgressStat(int Due, int Started);

    /// <summary>One grouped query over the user's progress for all listed sets (no per-set queries).</summary>
    private async Task<Dictionary<int, ProgressStat>> LoadProgressStatsAsync(int userId, IReadOnlyCollection<int> setIds, CancellationToken ct)
    {
        if (setIds.Count == 0)
        {
            return new();
        }

        var now = Now;
        var rows = await (from p in _db.LearningProgresses.AsNoTracking()
                          where p.UserId == userId
                          join c in _db.Cards.AsNoTracking() on p.CardId equals c.CardId
                          where setIds.Contains(c.SetId)
                          group p by c.SetId into g
                          select new
                          {
                              SetId = g.Key,
                              Due = g.Count(p => !p.IsSuspended && p.NextReviewDate != null && p.NextReviewDate <= now),
                              Started = g.Count(p => p.Status != null),
                          }).ToListAsync(ct);

        return rows.ToDictionary(r => r.SetId, r => new ProgressStat(r.Due, r.Started));
    }

    // =====================================================================================
    // Set detail and forms
    // =====================================================================================

    public async Task<SetDetailViewModel?> GetSetDetailAsync(int setId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var head = await ViewableSets(userId, isAdmin)
            .AsNoTracking()
            .Where(s => s.SetId == setId)
            .Select(s => new
            {
                s.SetId,
                s.SetName,
                s.Description,
                s.Visibility,
                s.OwnerId,
                OwnerName = s.Owner.FullName,
                s.FolderId,
                FolderName = s.Folder != null ? s.Folder.FolderName : null,
                s.ShareSlug,
                WordCount = s.Cards.Count,
                SampleTerm = s.Cards.OrderBy(c => c.CardId).Select(c => c.Term).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);

        if (head == null)
        {
            return null;
        }

        var isOwner = head.OwnerId == userId;
        var canManage = isOwner || isAdmin;
        var isSubscribed = await _db.SetSubscriptions.AnyAsync(x => x.SetId == setId && x.UserId == userId, ct);
        var viaFolder = !isOwner && !isSubscribed
            && await FolderSharedSets(userId).AnyAsync(s => s.SetId == setId, ct);

        var stats = await LoadProgressStatsAsync(userId, new[] { setId }, ct);
        var due = stats.TryGetValue(setId, out var stat) ? stat.Due : 0;
        var mastered = await (from p in _db.LearningProgresses.AsNoTracking()
                              where p.UserId == userId && p.Status == "Mastered"
                              join c in _db.Cards.AsNoTracking() on p.CardId equals c.CardId
                              where c.SetId == setId
                              select p.ProgressId).CountAsync(ct);
        var masteredPercent = head.WordCount == 0 ? 0 : (int)Math.Round(100.0 * mastered / head.WordCount);

        var topics = await _db.Cards.AsNoTracking()
            .Where(c => c.SetId == setId && c.Topic != null && c.Topic != "")
            .Select(c => c.Topic!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(ct);

        var targets = canManage
            ? await ManageableSets(userId, false)
                .AsNoTracking()
                .Where(s => s.SetId != setId)
                .OrderBy(s => s.SetName)
                .Take(MaxOwnedSets)
                .Select(s => new SetMoveTarget { SetId = s.SetId, SetName = s.SetName })
                .ToListAsync(ct)
            : new List<SetMoveTarget>();

        return new SetDetailViewModel
        {
            SetId = head.SetId,
            UserId = userId,
            SetName = head.SetName,
            Description = head.Description,
            Visibility = head.Visibility,
            CoverHue = SetCoverColor.For(head.SetId),
            SampleTerm = SampleOf(head.SampleTerm, head.SetName),
            OwnerName = isOwner ? null : (string.IsNullOrWhiteSpace(head.OwnerName) ? "Người dùng TCT" : head.OwnerName),
            FolderId = head.FolderId,
            FolderName = head.FolderName,
            IsOwner = isOwner,
            CanEdit = canManage,
            CanManage = canManage,
            IsSubscribed = isSubscribed,
            IsStudying = isOwner || isSubscribed || viaFolder,
            HasShareLink = head.ShareSlug != null,
            ShareSlug = canManage ? head.ShareSlug : null,
            WordCount = head.WordCount,
            DueCount = due,
            MasteredPercent = masteredPercent,
            Topics = topics,
            MoveTargets = targets,
            StudyStrip = new StudyStripViewModel { MasteredPercent = masteredPercent, DueCount = due, WordCount = head.WordCount },
        };
    }

    public async Task<SetInfoFormViewModel?> GetSetInfoFormAsync(int setId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var set = await ManageableSets(userId, isAdmin)
            .AsNoTracking()
            .Where(s => s.SetId == setId)
            .Select(s => new SetInfoFormViewModel
            {
                SetId = s.SetId,
                SetName = s.SetName,
                Description = s.Description,
                Visibility = s.Visibility,
                FolderId = s.FolderId,
                WordCount = s.Cards.Count,
                CoverHue = "primary",
            })
            .FirstOrDefaultAsync(ct);

        if (set == null)
        {
            return null;
        }

        set.CoverHue = SetCoverColor.For(setId);
        var ownerId = await _db.Sets.Where(s => s.SetId == setId).Select(s => s.OwnerId).FirstAsync(ct);
        set.AvailableFolders = await LoadFolderOptionsAsync(ownerId, ct);
        return set;
    }

    public async Task<SetInfoFormViewModel?> GetNewSetFormAsync(int userId, bool isAdmin, int? folderId, CancellationToken ct = default)
    {
        if (folderId.HasValue && !await ManageableFolders(userId, isAdmin).AnyAsync(f => f.FolderId == folderId.Value, ct))
        {
            return null;
        }

        return new SetInfoFormViewModel
        {
            Visibility = SetVisibility.Private,
            FolderId = folderId,
            AvailableFolders = await LoadFolderOptionsAsync(userId, ct),
        };
    }

    private IQueryable<Folder> ManageableFolders(int userId, bool isAdmin)
        => isAdmin ? _db.Folders : _db.Folders.Where(f => f.UserId == userId);

    private Task<List<SetFolderOption>> LoadFolderOptionsAsync(int ownerId, CancellationToken ct)
        => _db.Folders.AsNoTracking()
            .Where(f => f.UserId == ownerId)
            .OrderBy(f => f.FolderName)
            .Select(f => new SetFolderOption { FolderId = f.FolderId, FolderName = f.FolderName })
            .ToListAsync(ct);

    // =====================================================================================
    // Word list (search / filter / sort / page)
    // =====================================================================================

    public async Task<CardPageDto?> SearchCardsAsync(int setId, CardSearchQuery query, int userId, bool isAdmin, CancellationToken ct = default)
    {
        if (!await CanViewSetAsync(setId, userId, isAdmin, ct))
        {
            return null;
        }

        var offset = Math.Max(0, query.Offset);
        var progress = _db.LearningProgresses.AsNoTracking().Where(p => p.UserId == userId);
        var rows = from c in _db.Cards.AsNoTracking().Where(c => c.SetId == setId)
                   join p in progress on c.CardId equals p.CardId into joined
                   from p in joined.DefaultIfEmpty()
                   select new { c, p };

        var q = Trim(query.Q, 80);
        if (q != null)
        {
            var pattern = "%" + EscapeLike(q) + "%";
            rows = rows.Where(x => EF.Functions.Like(x.c.Term, pattern, "\\")
                || EF.Functions.Like(x.c.Definition, pattern, "\\")
                || (x.c.Phonetic != null && EF.Functions.Like(x.c.Phonetic, pattern, "\\")));
        }

        var topic = Trim(query.Topic, 60);
        if (topic != null)
        {
            rows = rows.Where(x => x.c.Topic == topic);
        }

        var pos = query.Pos?.Trim().ToLowerInvariant();
        if (CardPartsOfSpeech.IsValid(pos))
        {
            var posPattern = "%," + pos + ",%";
            rows = rows.Where(x => x.c.PartsOfSpeech != null && EF.Functions.Like("," + x.c.PartsOfSpeech + ",", posPattern));
        }

        var level = query.Level?.Trim().ToUpperInvariant();
        if (CefrLevels.IsValid(level))
        {
            rows = rows.Where(x => x.c.Cefr == level);
        }

        const int hard = CardStatus.HardWrongThreshold;
        switch (query.Status)
        {
            case CardStatus.Suspended:
                rows = rows.Where(x => x.p != null && x.p.IsSuspended);
                break;
            case CardStatus.Hard:
                rows = rows.Where(x => x.p != null && !x.p.IsSuspended && (x.p.WrongCount ?? 0) >= hard);
                break;
            case CardStatus.Learned:
                rows = rows.Where(x => x.p != null && !x.p.IsSuspended && (x.p.WrongCount ?? 0) < hard && x.p.Status == "Mastered");
                break;
            case CardStatus.Learning:
                rows = rows.Where(x => x.p != null && !x.p.IsSuspended && (x.p.WrongCount ?? 0) < hard
                    && (x.p.Status == "Learning" || x.p.Status == "Reviewing"));
                break;
            case CardStatus.New:
                rows = rows.Where(x => x.p == null || (!x.p.IsSuspended && (x.p.WrongCount ?? 0) < hard
                    && x.p.Status != "Mastered" && x.p.Status != "Learning" && x.p.Status != "Reviewing"));
                break;
        }

        var total = await rows.CountAsync(ct);

        var ordered = query.Sort switch
        {
            "az" => rows.OrderBy(x => x.c.Term).ThenBy(x => x.c.CardId),
            "hard" => rows.OrderByDescending(x => x.p == null ? 0 : (x.p.WrongCount ?? 0))
                .ThenByDescending(x => x.p == null ? null : x.p.LastReviewedDate)
                .ThenByDescending(x => x.c.CardId),
            _ => rows.OrderBy(x => x.c.CreatedAt == null)
                .ThenByDescending(x => x.c.CreatedAt)
                .ThenByDescending(x => x.c.CardId),
        };

        var page = await ordered
            .Skip(offset)
            .Take(PageSize + 1)
            .Select(x => new
            {
                x.c.CardId,
                x.c.Term,
                x.c.Phonetic,
                x.c.Definition,
                x.c.Cefr,
                x.c.Topic,
                x.c.ImageUrl,
                x.c.Emoji,
                HasProgress = x.p != null,
                IsSuspended = x.p != null && x.p.IsSuspended,
                WrongCount = x.p == null ? null : x.p.WrongCount,
                Status = x.p == null ? null : x.p.Status,
            })
            .ToListAsync(ct);

        var hasMore = page.Count > PageSize;
        var result = new CardPageDto
        {
            Total = total,
            NextOffset = hasMore ? offset + PageSize : null,
            Rows = page.Take(PageSize).Select(r =>
            {
                var key = CardStatus.Classify(r.IsSuspended, r.WrongCount, r.Status, r.HasProgress);
                return new CardRowDto
                {
                    CardId = r.CardId,
                    Term = r.Term,
                    Phonetic = r.Phonetic,
                    Definition = r.Definition,
                    Cefr = r.Cefr,
                    Topic = r.Topic,
                    ImageUrl = r.ImageUrl,
                    Emoji = r.Emoji,
                    IsSuspended = r.IsSuspended,
                    Status = new CardStatusDto { Key = key, Label = CardStatus.LabelOf(key) },
                };
            }).ToList(),
        };

        return result;
    }

    private static string? Trim(string? value, int max)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Length > max ? text[..max] : text;
    }

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    // =====================================================================================
    // Card detail and edit form
    // =====================================================================================

    public async Task<CardDetailViewModel?> GetCardDetailAsync(int cardId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var card = await _db.Cards.AsNoTracking().FirstOrDefaultAsync(c => c.CardId == cardId, ct);
        if (card == null || !await CanViewSetAsync(card.SetId, userId, isAdmin, ct))
        {
            return null;
        }

        var set = await _db.Sets.AsNoTracking()
            .Where(s => s.SetId == card.SetId)
            .Select(s => new { s.SetName, s.OwnerId })
            .FirstAsync(ct);

        var isOwner = set.OwnerId == userId;
        var canEdit = isOwner || isAdmin;

        var details = CardDetailsCodec.Parse(card.DetailsJson, out var invalid);
        if (invalid)
        {
            _logger.LogWarning("Card {CardId} has invalid DetailsJson; showing it as empty.", cardId);
        }

        if (details.Examples.Count == 0 && !string.IsNullOrWhiteSpace(card.Example))
        {
            details.Examples.Add(new CardExample { Text = card.Example, Vi = card.ExampleTranslation });
        }

        var setTerms = await _db.Cards.AsNoTracking()
            .Where(c => c.SetId == card.SetId && c.CardId != cardId)
            .Select(c => new { c.CardId, c.Term })
            .ToListAsync(ct);
        var byTerm = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var other in setTerms)
        {
            byTerm.TryAdd(other.Term.Trim(), other.CardId);
        }

        List<CardRelatedLink> Link(IEnumerable<string> words)
            => words.Select(w => new CardRelatedLink { Text = w, CardId = byTerm.TryGetValue(w.Trim(), out var id) ? id : null }).ToList();

        var progress = await _db.LearningProgresses.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.CardId == cardId, ct);
        var statusKey = CardStatus.Classify(progress);

        return new CardDetailViewModel
        {
            CardId = card.CardId,
            SetId = card.SetId,
            SetName = set.SetName,
            CanEdit = canEdit,
            IsSetOwner = isOwner,
            Term = card.Term,
            Definition = card.Definition,
            Phonetic = card.Phonetic,
            PhoneticUk = card.PhoneticUk,
            MeaningEn = card.MeaningEn,
            PartsOfSpeech = CardPartsOfSpeech.Parse(card.PartsOfSpeech).ToList(),
            Topic = card.Topic,
            Cefr = card.Cefr,
            Emoji = card.Emoji,
            ImageUrl = card.ImageUrl,
            ImageSource = CardDetailsCodec.ParseImageSource(card.ImageSourceJson),
            PersonalNote = isOwner ? card.PersonalNote : null,
            Details = details,
            RelatedSynonyms = Link(details.Synonyms),
            RelatedAntonyms = Link(details.Antonyms),
            Progress = new CardProgressViewModel
            {
                StatusKey = statusKey,
                StatusLabel = CardStatus.LabelOf(statusKey),
                LastReviewedDate = progress?.LastReviewedDate,
                NextReviewDate = progress?.NextReviewDate,
                WrongCount = progress?.WrongCount ?? 0,
                RepetitionCount = progress?.RepetitionCount ?? 0,
                IsSuspended = progress?.IsSuspended ?? false,
                HasReviewed = progress != null && (progress.RepetitionCount > 0 || progress.Status != null),
            },
        };
    }

    public async Task<CardFormViewModel?> GetCardFormAsync(int cardId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var card = await _db.Cards.AsNoTracking().FirstOrDefaultAsync(c => c.CardId == cardId, ct);
        if (card == null || !await CanEditSetAsync(card.SetId, userId, isAdmin, ct))
        {
            return null;
        }

        var setName = await _db.Sets.AsNoTracking().Where(s => s.SetId == card.SetId).Select(s => s.SetName).FirstAsync(ct);
        var details = CardDetailsCodec.Parse(card.DetailsJson, out _);
        if (details.Examples.Count == 0 && !string.IsNullOrWhiteSpace(card.Example))
        {
            details.Examples.Add(new CardExample { Text = card.Example, Vi = card.ExampleTranslation });
        }

        return new CardFormViewModel
        {
            CardId = card.CardId,
            SetId = card.SetId,
            SetName = setName,
            Term = card.Term,
            Definition = card.Definition,
            Phonetic = card.Phonetic,
            PhoneticUk = card.PhoneticUk,
            MeaningEn = card.MeaningEn,
            PartsOfSpeech = CardPartsOfSpeech.Parse(card.PartsOfSpeech).ToList(),
            Topic = card.Topic,
            Emoji = card.Emoji,
            Cefr = card.Cefr,
            PersonalNote = card.PersonalNote,
            ImageUrl = card.ImageUrl,
            ImageSource = CardDetailsCodec.ParseImageSource(card.ImageSourceJson),
            Examples = details.Examples,
            Synonyms = details.Synonyms,
            Antonyms = details.Antonyms,
            Collocations = details.Collocations,
            WordFamily = details.WordFamily,
            VerbForms = details.VerbForms,
            Plural = details.Plural,
            AiGenerated = card.AiGenerated,
        };
    }

    // =====================================================================================
    // Duplicates, shared page, import/compose context
    // =====================================================================================

    public async Task<IReadOnlyList<DuplicateHitDto>> FindDuplicatesAsync(string term, int userId, int? excludeCardId, CancellationToken ct = default)
    {
        var lower = term?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(lower) || lower.Length > CardInputNormalizer.MaxTerm)
        {
            return Array.Empty<DuplicateHitDto>();
        }

        var subscribed = _db.SetSubscriptions.Where(x => x.UserId == userId).Select(x => x.SetId);
        return await (from c in _db.Cards.AsNoTracking()
                      join s in _db.Sets.AsNoTracking() on c.SetId equals s.SetId
                      where (s.OwnerId == userId || subscribed.Contains(s.SetId))
                            && c.Term.ToLower() == lower
                            && (excludeCardId == null || c.CardId != excludeCardId)
                      orderby c.CardId
                      select new DuplicateHitDto { CardId = c.CardId, SetId = s.SetId, SetName = s.SetName, Term = c.Term })
            .Take(5)
            .ToListAsync(ct);
    }

    public async Task<SharedSetViewModel?> GetSharedSetAsync(string slug, int? userId, CancellationToken ct = default)
    {
        if (!IsValidSlug(slug))
        {
            return null;
        }

        var head = await _db.Sets.AsNoTracking()
            .Where(s => s.ShareSlug == slug)
            .Select(s => new
            {
                s.SetId,
                s.SetName,
                s.Description,
                s.OwnerId,
                OwnerName = s.Owner.FullName,
                WordCount = s.Cards.Count,
                LearnerCount = s.Subscriptions.Count,
                SampleTerm = s.Cards.OrderBy(c => c.CardId).Select(c => c.Term).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);

        if (head == null)
        {
            return null;
        }

        var cards = await _db.Cards.AsNoTracking()
            .Where(c => c.SetId == head.SetId)
            .OrderBy(c => c.CardId)
            .Take(PageSize)
            .Select(c => new SharedCardPreview { CardId = c.CardId, Term = c.Term, Phonetic = c.Phonetic, Definition = c.Definition })
            .ToListAsync(ct);

        var isSubscribed = userId.HasValue && await _db.SetSubscriptions.AnyAsync(x => x.SetId == head.SetId && x.UserId == userId.Value, ct);

        return new SharedSetViewModel
        {
            SetId = head.SetId,
            Slug = slug,
            SetName = head.SetName,
            Description = head.Description,
            CoverHue = SetCoverColor.For(head.SetId),
            SampleTerm = SampleOf(head.SampleTerm, head.SetName),
            OwnerName = string.IsNullOrWhiteSpace(head.OwnerName) ? "Người dùng TCT" : head.OwnerName,
            WordCount = head.WordCount,
            LearnerCount = head.LearnerCount,
            IsSignedIn = userId.HasValue,
            IsOwner = userId.HasValue && head.OwnerId == userId.Value,
            IsSubscribed = isSubscribed,
            Cards = cards,
        };
    }

    public async Task<CardImportContextViewModel?> GetImportContextAsync(int setId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var set = await ManageableSets(userId, isAdmin).AsNoTracking()
            .Where(s => s.SetId == setId)
            .Select(s => new { s.SetId, s.SetName })
            .FirstOrDefaultAsync(ct);
        if (set == null)
        {
            return null;
        }

        var terms = await _db.Cards.AsNoTracking()
            .Where(c => c.SetId == setId)
            .Select(c => c.Term)
            .ToListAsync(ct);

        return new CardImportContextViewModel
        {
            SetId = set.SetId,
            SetName = set.SetName,
            ExistingTermsLower = terms.Select(t => t.Trim().ToLowerInvariant()).Distinct().ToList(),
        };
    }

    public async Task<CardComposeViewModel?> GetComposeContextAsync(int setId, int userId, bool isAdmin, CancellationToken ct = default)
    {
        var set = await ManageableSets(userId, isAdmin).AsNoTracking()
            .Where(s => s.SetId == setId)
            .Select(s => new { s.SetId, s.SetName, s.OwnerId })
            .FirstOrDefaultAsync(ct);
        if (set == null)
        {
            return null;
        }

        var targets = await _db.Sets.AsNoTracking()
            .Where(s => s.OwnerId == set.OwnerId)
            .OrderBy(s => s.SetName)
            .Take(MaxOwnedSets)
            .Select(s => new SetMoveTarget { SetId = s.SetId, SetName = s.SetName })
            .ToListAsync(ct);

        var topics = await _db.Cards.AsNoTracking()
            .Where(c => c.SetId == setId && c.Topic != null && c.Topic != "")
            .Select(c => c.Topic!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(ct);

        return new CardComposeViewModel { SetId = set.SetId, SetName = set.SetName, Targets = targets, Topics = topics };
    }
}
